using System.Text.Json;
using HGame01Server.Models;
using HGame01Server.Models.GameData;
using HGame01Server.Repository;

namespace HGame01Server.Services;

/// 우편함 비즈니스 로직. 발송 / 조회 / 단건 수령 / 모두 받기 / 만료 lazy 청소.
/// 만료/수령 모두 string 비교 — 다른 테이블과 동일 yyyy-MM-dd HH:mm:ss UTC 컨벤션.
public class MailService
{
    private readonly GameDbContext _context;
    private readonly IGameDB _gameDB;
    private readonly CurrencyService _currencyService;
    private readonly GameDataManager _gameDataManager;

    public MailService(GameDbContext context, IGameDB gameDB, CurrencyService currencyService, GameDataManager gameDataManager)
    {
        _context = context;
        _gameDB = gameDB;
        _currencyService = currencyService;
        _gameDataManager = gameDataManager;
    }

    /// 우편함 목록 조회 — 만료 메일 lazy 청소 후 미수령(claimedAt == "") + 미만료 메일만 반환.
    /// 수령된 메일은 DB 에 보관 (CS/이력 추적) 하되 클라 응답에선 제외 → 사용자 화면에서 자동 사라짐.
    public async Task<List<PkMailEntry>> GetMailsAsync(long uid)
    {
        var now = NowStr();
        await _gameDB.DeleteExpiredMailsAsync(uid, now);
        var mails = await _gameDB.GetClaimableMailsAsync(uid, now);
        return mails.Select(ToPacket).ToList();
    }

    /// 단건 수령 — 트랜잭션 안에서 claim + 보상 지급. 중간 실패 시 전체 롤백.
    public async Task<PkMailClaimResponse> ClaimAsync(long uid, string mailId)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var mail = await _gameDB.GetMailAsync(uid, mailId);
            if (mail == null)
            {
                return new PkMailClaimResponse { Result = ErrorCode.MailNotFound, MailId = mailId };
            }
            if (!string.IsNullOrEmpty(mail.claimedAt))
            {
                return new PkMailClaimResponse { Result = ErrorCode.MailAlreadyClaimed, MailId = mailId };
            }
            if (IsExpired(mail))
            {
                return new PkMailClaimResponse { Result = ErrorCode.MailExpired, MailId = mailId };
            }

            var rewardEntries = ParseRewards(mail.rewardsJson);
            var rewards = await GrantRewardsAsync(uid, rewardEntries);

            // B안: row 보관 + claimedAt 채움. 클라 List 응답은 GetClaimableMailsAsync 로 미수령만 반환하므로 사용자 화면에서 자동 사라짐.
            mail.claimedAt = NowStr();
            await _gameDB.UpdateMailClaimedAsync(mail);

            await tx.CommitAsync();

            var response = new PkMailClaimResponse
            {
                Result = ErrorCode.None,
                MailId = mailId,
                Rewards = rewards,
            };
            await _currencyService.PopulateCurrenciesAsync(response, uid);
            return response;
        }
        catch (Exception ex)
        {
            _ = ex;
            await tx.RollbackAsync();
            return new PkMailClaimResponse { Result = ErrorCode.MailClaimFailed, MailId = mailId };
        }
    }

    /// 모두 받기 — 단일 트랜잭션, 1건이라도 실패하면 전체 롤백 (Q3 A안).
    public async Task<PkMailClaimAllResponse> ClaimAllAsync(long uid)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var mails = await _gameDB.GetClaimableMailsAsync(uid, NowStr());
            if (mails.Count == 0)
            {
                return new PkMailClaimAllResponse { Result = ErrorCode.MailNoClaimable };
            }

            var ids = new List<string>(mails.Count);
            var allRewards = new List<PkRewardResult>();
            var now = NowStr();

            foreach (var mail in mails)
            {
                var rewardEntries = ParseRewards(mail.rewardsJson);
                var results = await GrantRewardsAsync(uid, rewardEntries);
                allRewards.AddRange(results);

                mail.claimedAt = now;
                ids.Add(mail.mailId);
            }

            // B안: batch claimedAt 갱신 (row 보관).
            await _gameDB.UpdateMailsClaimedBatchAsync(mails);
            await tx.CommitAsync();

            var response = new PkMailClaimAllResponse
            {
                Result = ErrorCode.None,
                ClaimedMailIds = ids,
                Rewards = allRewards,
            };
            await _currencyService.PopulateCurrenciesAsync(response, uid);
            return response;
        }
        catch (Exception ex)
        {
            _ = ex;
            await tx.RollbackAsync();
            return new PkMailClaimAllResponse { Result = ErrorCode.MailClaimFailed };
        }
    }

    /// 외부 (ShopService 등) 가 호출하는 메일 발송 진입점. expireAt 자동 계산.
    public async Task<string> SendAsync(long uid, string titleKey,
                                        List<MailRewardEntry> rewards,
                                        string iconAtlas, string iconKey,
                                        int expireMinutes,
                                        string senderType = "System",
                                        string mailKind = "Generic")
    {
        var mail = new GameUserMail
        {
            mailId = Guid.NewGuid().ToString("N"),
            uid = uid,
            titleKey = titleKey ?? "",
            mailKind = string.IsNullOrEmpty(mailKind) ? "Generic" : mailKind,
            rewardsJson = JsonSerializer.Serialize(rewards ?? new()),
            iconAtlas = iconAtlas ?? "",
            iconKey = iconKey ?? "",
            senderType = senderType ?? "System",
            sentAt = NowStr(),
            expireAt = FormatTime(DateTime.UtcNow.AddMinutes(expireMinutes)),
            claimedAt = "",
        };

        await _gameDB.AddMailAsync(mail);
        return mail.mailId;
    }


    // ===== 헬퍼 =====

    /// 보상 N개 지급. RewardResolver로 tag → RewardType/ItemKind 판별.
    /// 이번 prototype 은 Item & Currency 만 실제 지급 — Equipment/Pet 등 인스턴스형은 후속 RewardGrantService 통합에서 처리.
    ///
    /// ⚠ 분기 통합 검토 시점:
    ///   - 같은 RewardType=="Item"&&ItemKind=="Currency" 시퀀스가 ShopService.GrantRewardAsync 와 중복.
    ///   - 새 RewardType (Pet/Treasure) 또는 새 지급 트리거 (Quest/Login 보너스) 추가 시점에는
    ///     아래 분기를 RewardGrantService.GrantAsync(uid, PkRewardResult) 로 추출해 한 곳에 모을 것.
    ///   - Plan 영역 4 (RewardGrantService 통합) 참조. 인터페이스(PkRewardResult)는 이미 결정되어
    ///     있으니 통합 비용은 분기 이전만큼.
    private async Task<List<PkRewardResult>> GrantRewardsAsync(long uid, List<MailRewardEntry> rewards)
    {
        var results = new List<PkRewardResult>(rewards.Count);

        foreach (var reward in rewards)
        {
            var result = RewardResolver.Resolve(_gameDataManager, reward.rewardTag, reward.count);

            if (result.RewardType == "Item" && result.ItemKind == "Currency")
            {
                string currencyName = RewardResolver.ResolveCurrencyType(_gameDataManager, result.RewardTag);
                int currencyType = ParseCurrencyType(currencyName);
                await _currencyService.AddAsync(uid, currencyType, reward.count);
            }
            // Equipment / Pet 등 인스턴스형 지급은 prototype 미지원 — 후속 RewardGrantService 통합 시 분기 추가.

            results.Add(result);
        }

        return results;
    }

    private static List<MailRewardEntry> ParseRewards(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new();
        }

        try
        {
            return JsonSerializer.Deserialize<List<MailRewardEntry>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private static int ParseCurrencyType(string s)
    {
        return s?.ToLowerInvariant() switch
        {
            "gold" => CurrencyType.Gold,
            _ => CurrencyType.Diamond,
        };
    }

    private static PkMailEntry ToPacket(GameUserMail m)
    {
        var rewards = ParseRewards(m.rewardsJson)
            .Select(r => new PkMailReward { RewardTag = r.rewardTag, Count = r.count })
            .ToList();

        return new PkMailEntry
        {
            MailId = m.mailId,
            TitleKey = m.titleKey,
            MailKind = m.mailKind,
            Rewards = rewards,
            IconAtlas = m.iconAtlas,
            IconKey = m.iconKey,
            SentAt = m.sentAt,
            ExpireAt = m.expireAt,
            ClaimedAt = m.claimedAt,
            SenderType = m.senderType,
        };
    }

    private static bool IsExpired(GameUserMail mail)
    {
        return string.Compare(mail.expireAt, NowStr()) < 0;
    }

    private static string NowStr()
    {
        return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private static string FormatTime(DateTime t)
    {
        return t.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
