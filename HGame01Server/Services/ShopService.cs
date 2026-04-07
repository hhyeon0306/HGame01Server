using HGame01Server.Models;
using HGame01Server.Models.GameData;
using HGame01Server.Repository;

namespace HGame01Server.Services;

/// 상점 비즈니스 로직.
public class ShopService
{
    private readonly GameDbContext _context;
    private readonly IGameDB _gameDB;
    private readonly CurrencyService _currencyService;
    private readonly GameDataManager _gameDataManager;

    private const int MAX_DIAMOND_PURCHASE = 100000;

    public ShopService(GameDbContext context, IGameDB gameDB, CurrencyService currencyService, GameDataManager gameDataManager)
    {
        _context = context;
        _gameDB = gameDB;
        _currencyService = currencyService;
        _gameDataManager = gameDataManager;
    }

    /// 일일 상점 아이템 목록 + 구매 여부 조회.
    public async Task<List<PkShopItemState>> GetDailyItemsAsync(long uid)
    {
        var shopItems = _gameDataManager.GetList<GdbShopItemData>();
        if (shopItems == null)
        {
            return new();
        }

        var dailyItems = shopItems.Where(s => s.shopType == "daily").ToList();
        var resetStr = FormatResetTime(GetDailyResetTime());
        var purchases = await _gameDB.GetPurchasesSinceAsync(uid, resetStr);
        var purchasedIds = purchases.Select(p => p.shopItemId).ToHashSet();

        return dailyItems.Select(item => new PkShopItemState
        {
            ShopItemId = item.id,
            Purchased = purchasedIds.Contains(item.id)
        }).ToList();
    }

    /// 주간 상점 아이템 목록 + 구매 여부 조회.
    public async Task<List<PkShopItemState>> GetWeeklyItemsAsync(long uid)
    {
        var shopItems = _gameDataManager.GetList<GdbShopItemData>();
        if (shopItems == null)
        {
            return new();
        }

        var weeklyItems = shopItems.Where(s => s.shopType == "weekly").ToList();
        var resetStr = FormatResetTime(GetWeeklyResetTime());
        var purchases = await _gameDB.GetPurchasesSinceAsync(uid, resetStr);
        var purchasedIds = purchases.Select(p => p.shopItemId).ToHashSet();

        return weeklyItems.Select(item => new PkShopItemState
        {
            ShopItemId = item.id,
            Purchased = purchasedIds.Contains(item.id)
        }).ToList();
    }

    /// 상점 아이템 구매 처리.
    public async Task<(ErrorCode error, PkRewardResult reward)> BuyItemAsync(long uid, int shopItemId)
    {
        // 1. 상품 존재 확인
        var shopItems = _gameDataManager.GetList<GdbShopItemData>();
        var shopItem = shopItems?.FirstOrDefault(s => s.id == shopItemId);
        if (shopItem == null)
        {
            return (ErrorCode.ShopItemNotFound, new());
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 2. 초기화 시점 이후 이미 구매했는지 확인 (트랜잭션 내부에서 TOCTOU 방지)
            var resetTime = shopItem.shopType == "daily" ? GetDailyResetTime() : GetWeeklyResetTime();
            var resetStr = FormatResetTime(resetTime);
            var purchases = await _gameDB.GetPurchasesSinceAsync(uid, resetStr);
            if (purchases.Any(p => p.shopItemId == shopItemId))
            {
                return (ErrorCode.ShopItemAlreadyPurchased, new());
            }

            // 3. 재화 차감
            var deductError = await _currencyService.DeductAsync(uid, shopItem.costCurrencyType, shopItem.costAmount);
            if (deductError != ErrorCode.None)
            {
                return (ErrorCode.ShopInsufficientCurrency, new());
            }

            // 4. 보상 지급
            var reward = new PkRewardResult
            {
                RewardType = shopItem.rewardType,
                Amount = shopItem.rewardAmount,
                EquipmentId = shopItem.rewardEquipmentId
            };

            if (shopItem.rewardType == 0)
            {
                // 재화 보상
                await _currencyService.AddAsync(uid, shopItem.rewardCurrencyType, shopItem.rewardAmount);
            }
            else if (shopItem.rewardType == 1)
            {
                // 장비 보상 — GdbEquipmentData에서 slot 조회
                int slot = GetEquipmentSlot(shopItem.rewardEquipmentId);
                string now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                var equipment = new GameUserEquipment
                {
                    uid = uid,
                    equipmentId = shopItem.rewardEquipmentId,
                    slot = slot,
                    isEquipped = false,
                    equippedCharacterId = 0,
                    acquiredAt = now
                };
                await _gameDB.AddEquipmentAsync(equipment);
            }

            // 5. 구매 기록 저장
            var purchase = new GameUserShopPurchase
            {
                uid = uid,
                shopItemId = shopItemId,
                purchasedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            };
            await _gameDB.AddPurchaseAsync(purchase);

            await transaction.CommitAsync();
            return (ErrorCode.None, reward);
        }
        catch (Exception ex)
        {
            // TODO: ILogger 주입 후 로깅 추가
            _ = ex;
            await transaction.RollbackAsync();
            return (ErrorCode.ShopBuyFailed, new());
        }
    }

    /// 다이아몬드 구매 (인앱 결제 후 서버 검증 대역).
    public async Task<(ErrorCode error, long diamondAmount)> BuyDiamondAsync(long uid, string productId, int amount)
    {
        if (amount <= 0 || amount > MAX_DIAMOND_PURCHASE)
        {
            return (ErrorCode.ShopInvalidAmount, 0);
        }

        // TODO: 실제 영수증 검증 로직 추가 필요
        var addError = await _currencyService.AddAsync(uid, GdbConst.CurrencyType.Diamond, amount);
        if (addError != ErrorCode.None)
        {
            return (addError, 0);
        }

        long currentAmount = await _currencyService.GetAmountAsync(uid, GdbConst.CurrencyType.Diamond);
        return (ErrorCode.None, currentAmount);
    }

    /// GdbEquipmentData에서 장비 slot 값 조회. 데이터 없으면 0 반환.
    private int GetEquipmentSlot(int equipmentId)
    {
        var equipments = _gameDataManager.GetList<GdbEquipmentData>();
        var data = equipments?.FirstOrDefault(e => e.id == equipmentId);
        return data?.slot ?? 0;
    }

    /// 일일 초기화 시점: 오늘 자정 (UTC).
    private static DateTime GetDailyResetTime()
    {
        return DateTime.UtcNow.Date;
    }

    /// 주간 초기화 시점: 이번 주 월요일 자정 (UTC).
    private static DateTime GetWeeklyResetTime()
    {
        var now = DateTime.UtcNow;
        int diff = ((int)now.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return now.Date.AddDays(-diff);
    }

    private static string FormatResetTime(DateTime resetTime)
    {
        return resetTime.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
