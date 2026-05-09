using HGame01Server.Models;
using HGame01Server.Models.GameData;
using HGame01Server.Repository;

namespace HGame01Server.Services;

/// 상점 비즈니스 로직.
/// 클라 ShopData(GameplayTag 기반) ↔ 서버 GdbShopData(Admin/UploadGameData 자동 export). shopItemId는 GameplayTag string.
public class ShopService
{
    private readonly GameDbContext _context;
    private readonly IGameDB _gameDB;
    private readonly CurrencyService _currencyService;
    private readonly GameDataManager _gameDataManager;
    private readonly MailService _mailService;
    private readonly IResetSchedule _dailyReset;

    public ShopService(GameDbContext context, IGameDB gameDB, CurrencyService currencyService, GameDataManager gameDataManager, MailService mailService, IResetSchedule dailyReset)
    {
        _context = context;
        _gameDB = gameDB;
        _currencyService = currencyService;
        _gameDataManager = gameDataManager;
        _mailService = mailService;
        _dailyReset = dailyReset;
    }

    /// 일일 상점 아이템 목록 + 구매 여부 조회.
    public async Task<List<PkShopItemState>> GetDailyItemsAsync(long uid)
    {
        return await GetItemsByTabAsync(uid, "Daily", _dailyReset.Current);
    }

    /// 주간 상점 — Weekly 탭은 폐기됐지만 endpoint 호환을 위해 빈 리스트 반환.
    public Task<List<PkShopItemState>> GetWeeklyItemsAsync(long uid)
    {
        _ = uid;
        return Task.FromResult(new List<PkShopItemState>());
    }

    private async Task<List<PkShopItemState>> GetItemsByTabAsync(long uid, string tabType, DateTime resetTime)
    {
        var shopItems = _gameDataManager.GetList<GdbShopData>();
        if (shopItems == null)
        {
            return new();
        }

        var tabItems = shopItems.Where(s => s.tab_type == tabType).ToList();
        var resetStr = FormatResetTime(resetTime);
        var purchases = await _gameDB.GetPurchasesSinceAsync(uid, resetStr);
        var countByTag = purchases.GroupBy(p => p.shopItemId).ToDictionary(g => g.Key, g => g.Count());

        return tabItems.Select(item => new PkShopItemState
        {
            ShopItemId = item.tag,
            PurchasedCount = countByTag.TryGetValue(item.tag, out var c) ? c : 0
        }).ToList();
    }

    /// 상점 아이템 구매 처리. Currency 결제만 — Cash 결제는 BuyDiamondAsync 사용.
    public async Task<(ErrorCode error, PkRewardResult reward)> BuyItemAsync(long uid, string shopItemTag)
    {
        if (string.IsNullOrEmpty(shopItemTag))
        {
            return (ErrorCode.ShopItemNotFound, new());
        }

        var shopItems = _gameDataManager.GetList<GdbShopData>();
        var shopItem = shopItems?.FirstOrDefault(s => s.tag == shopItemTag);
        if (shopItem == null)
        {
            return (ErrorCode.ShopItemNotFound, new());
        }

        if (shopItem.payment_method != "Currency")
        {
            // Cash 결제는 별도 엔드포인트
            return (ErrorCode.ShopInvalidAmount, new());
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 일일 구매 횟수 검증 (Daily만; CashShop/Gacha는 무제한)
            if (shopItem.tab_type == "Daily")
            {
                var resetStr = FormatResetTime(_dailyReset.Current);
                var purchases = await _gameDB.GetPurchasesSinceAsync(uid, resetStr);
                int todayCount = purchases.Count(p => p.shopItemId == shopItemTag);
                int limit = shopItem.daily_limit > 0 ? shopItem.daily_limit : 1;
                if (todayCount >= limit)
                {
                    return (ErrorCode.ShopItemAlreadyPurchased, new());
                }
            }

            // 재화 차감
            var costType = ParseCurrencyType(shopItem.price_currency);
            var deductError = await _currencyService.DeductAsync(uid, costType, shopItem.price_amount);
            if (deductError != ErrorCode.None)
            {
                return (ErrorCode.ShopInsufficientCurrency, new());
            }

            // 보상 지급
            var reward = await GrantRewardAsync(uid, shopItem);

            // 구매 기록
            var purchase = new GameUserShopPurchase
            {
                uid = uid,
                shopItemId = shopItemTag,
                purchasedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            };
            await _gameDB.AddPurchaseAsync(purchase);

            await transaction.CommitAsync();
            return (ErrorCode.None, reward);
        }
        catch (Exception ex)
        {
            _ = ex;
            await transaction.RollbackAsync();
            return (ErrorCode.ShopBuyFailed, new());
        }
    }

    /// Cash 결제. 즉시 지급 X — 우편함으로 발송. 클라는 "메일 도착" 흐름으로 처리.
    /// shopItemTag (예: "Tag.Shop.Product.Diamond_6") 식별. 보상 종류/수량은 서버 측 GdbShopData / GdbItemData 조회로 결정 (가격 변조 방지).
    public async Task<ErrorCode> BuyDiamondAsync(long uid, string shopItemTag)
    {
        if (string.IsNullOrEmpty(shopItemTag))
        {
            return ErrorCode.ShopItemNotFound;
        }

        var shopItems = _gameDataManager.GetList<GdbShopData>();
        var shopItem = shopItems?.FirstOrDefault(s => s.tag == shopItemTag);
        if (shopItem == null)
        {
            return ErrorCode.ShopItemNotFound;
        }

        if (shopItem.payment_method != "Cash")
        {
            return ErrorCode.ShopInvalidAmount;
        }

        // TODO: 실제 영수증 검증 (포폴 단계 X)

        // 우편함 발송 — Cash 패키지 1건당 1보상 가정 (현재 GdbShopData 스키마)
        var rewards = new List<MailRewardEntry>
        {
            new()
            {
                rewardTag = shopItem.reward_item,
                count = shopItem.reward_count,
            },
        };

        // iconKey 는 GdbShopData.icon_name (예: "Shop_Product_Icon_Diamond_6") — ShopProductAtlas sprite 명명 규약.
        await _mailService.SendAsync(
            uid: uid,
            titleKey: shopItem.name_key ?? "",
            rewards: rewards,
            iconAtlas: "ShopProductAtlas",
            iconKey: shopItem.icon_name ?? "",
            expireMinutes: 60 * 24 * 30,
            senderType: "Compensation",
            mailKind: "CashPurchase");

        return ErrorCode.None;
    }

    /// 보상 지급 — RewardResolver로 tag → RewardType/ItemKind 판별. 이번 prototype은 Currency만 실제 지급, Equipment 는 응답에 정보만 담음.
    ///
    /// ⚠ 분기 통합 검토 시점:
    ///   - 같은 RewardType=="Item"&&ItemKind=="Currency" 시퀀스가 MailService.GrantRewardsAsync 와 중복.
    ///   - 새 RewardType (Pet/Treasure) 또는 새 지급 트리거 (Quest/Login 보너스) 추가 시점에는
    ///     아래 분기를 RewardGrantService.GrantAsync(uid, PkRewardResult) 로 추출해 한 곳에 모을 것.
    ///   - Plan 영역 4 (RewardGrantService 통합) 참조. 인터페이스(PkRewardResult)는 이미 결정되어
    ///     있으니 통합 비용은 분기 이전만큼.
    private async Task<PkRewardResult> GrantRewardAsync(long uid, GdbShopData shopItem)
    {
        var reward = RewardResolver.Resolve(_gameDataManager, shopItem.reward_item, shopItem.reward_count);

        if (reward.RewardType == "Item" && reward.ItemKind == "Currency")
        {
            string currencyName = RewardResolver.ResolveCurrencyType(_gameDataManager, reward.RewardTag);
            int currencyType = ParseCurrencyType(currencyName);
            await _currencyService.AddAsync(uid, currencyType, shopItem.reward_count);
        }
        // Equipment 지급은 별건 (이번 prototype 미지원 — 추후 RewardGrantService 통합)

        return reward;
    }

    /// "Diamond" / "Gold" → CurrencyType 상수 값.
    private static int ParseCurrencyType(string s)
    {
        return s?.ToLowerInvariant() switch
        {
            "gold" => CurrencyType.Gold,
            _ => CurrencyType.Diamond,
        };
    }

    /// 치트 전용. 오늘 자 사용자 구매 기록을 모두 삭제 → 일일 카운터 0 복구.
    public async Task<int> CheatResetDailyAsync(long uid)
    {
        var resetStr = FormatResetTime(GetDailyResetTime());
        return await _gameDB.DeletePurchasesSinceAsync(uid, resetStr);
    }

    /// 일일 초기화 시점: ShopConstants.dailyResetHourUtc 기준 가장 최근 리셋 시각.
    /// 예: dailyResetHourUtc=20일 때 현재 UTC 22시면 오늘 20시가 리셋, 현재 UTC 18시면 어제 20시가 리셋.
    private DateTime GetDailyResetTime()
    {
        int resetHour = _gameDataManager.GetConstInt(GdbConst.Shop.Category, GdbConst.Shop.DailyResetHourUtc, 20);
        var now = DateTime.UtcNow;
        var resetToday = now.Date.AddHours(resetHour);
        return now >= resetToday ? resetToday : resetToday.AddDays(-1);
    }

    private static string FormatResetTime(DateTime resetTime)
    {
        return resetTime.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
