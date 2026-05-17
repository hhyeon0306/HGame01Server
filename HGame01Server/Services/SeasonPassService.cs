using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HGame01Server.Models;
using HGame01Server.Models.GameData;
using HGame01Server.Repository;

namespace HGame01Server.Services;

/// 서버 측 SeasonPass 도메인 서비스.
/// GetState / ApplyStageResult(멱등) / PurchasePremium / ClaimAvailable(level_rewards 매핑 → currency 누적).
public class SeasonPassService
{
    private readonly IGameDB _gameDB;
    private readonly IClock _clock;
    private readonly GameDataManager _gameDataManager;
    private readonly CurrencyService _currencyService;
    private readonly MailService _mailService;

    /// 현재 단일 활성 시즌 식별자. 추후 GdbSeasonPassData에서 startUtc/endUtc 범위로 lookup 예정.
    private const string CurrentSeasonId = "Tag.Pass.Season1";

    /// 시즌 만료 정산 우편 제목 Localization 키.
    private const string SeasonEndedMailTitleKey = "seasonpass.mail.season_ended.title";

    public SeasonPassService(IGameDB gameDB, IClock clock, GameDataManager gameDataManager, CurrencyService currencyService, MailService mailService)
    {
        _gameDB = gameDB;
        _clock = clock;
        _gameDataManager = gameDataManager;
        _currencyService = currencyService;
        _mailService = mailService;
    }

    /// 현재 시즌 상태 조회. Row 없으면 신규 발급 후 반환.
    public async Task<PkSeasonPassStateResponse> GetStateAsync(long uid)
    {
        var row = await ResolveOrCreateUserSeasonPassAsync(uid);
        await SettleIfExpiredAsync(row);
        return new PkSeasonPassStateResponse
        {
            result = ErrorCode.None,
            state = BuildStateDto(row),
        };
    }

    /// 스테이지 결과 적립. (uid, stageRunId) unique로 멱등 보장.
    /// 같은 stageRunId 재요청 시 누적 X — 첫 적립 시점의 expGained 그대로 반환.
    public async Task<PkSeasonPassApplyStageResultResponse> ApplyStageResultAsync(long uid, string stageRunId, string stageResult)
    {
        if (string.IsNullOrEmpty(stageRunId))
        {
            return new PkSeasonPassApplyStageResultResponse { result = ErrorCode.InValidRequestHttpBody };
        }

        // 멱등 dedup — 이미 처리된 stageRunId면 누적 없이 멱등 응답.
        var existing = await _gameDB.GetSeasonPassStageRunAsync(uid, stageRunId);
        if (existing != null)
        {
            var existingRow = await ResolveOrCreateUserSeasonPassAsync(uid);
            return new PkSeasonPassApplyStageResultResponse
            {
                result = ErrorCode.None,
                expGained = existing.expGained,
                state = BuildStateDto(existingRow),
            };
        }

        int expGained = stageResult == "Victory"
            ? _gameDataManager.GetConstInt(GdbConst.SeasonPass.Category, GdbConst.SeasonPass.StageVictoryExp, defaultValue: 100)
            : _gameDataManager.GetConstInt(GdbConst.SeasonPass.Category, GdbConst.SeasonPass.StageDefeatExp, defaultValue: 30);

        string nowStr = NowStr();
        var row = await ResolveOrCreateUserSeasonPassAsync(uid);
        row.currentExp += expGained;
        row.updatedAtUtc = nowStr;

        var runRow = new GameUserSeasonPassStageRun
        {
            uid = uid,
            stageRunId = stageRunId,
            seasonId = row.seasonId,
            stageResult = stageResult,
            expGained = expGained,
            appliedAtUtc = nowStr,
        };

        await _gameDB.SaveSeasonPassAndStageRunAsync(row, runRow);

        return new PkSeasonPassApplyStageResultResponse
        {
            result = ErrorCode.None,
            expGained = expGained,
            state = BuildStateDto(row),
        };
    }

    /// 프리미엄 패스 활성화. 포폴 단계 영수증 검증 생략 — productId / receipt는 시그니처만 보존.
    public async Task<PkSeasonPassPurchasePremiumResponse> PurchasePremiumAsync(long uid, string productId, string receipt)
    {
        var row = await ResolveOrCreateUserSeasonPassAsync(uid);
        if (!row.isPremium)
        {
            row.isPremium = true;
            row.updatedAtUtc = NowStr();
            await _gameDB.UpsertUserSeasonPassAsync(row);
        }

        return new PkSeasonPassPurchasePremiumResponse
        {
            result = ErrorCode.None,
            state = BuildStateDto(row),
        };
    }

    /// 수령 가능 보상 일괄 지급. GdbSeasonPassData.level_rewards 매핑 → currency 누적 → claimed 레벨 갱신.
    public async Task<PkSeasonPassClaimResponse> ClaimAvailableAsync(long uid)
    {
        var row = await ResolveOrCreateUserSeasonPassAsync(uid);

        int expPerLevel = _gameDataManager.GetConstInt(GdbConst.SeasonPass.Category, GdbConst.SeasonPass.ExpPerLevel, defaultValue: 100);
        var seasonData = _gameDataManager.Get<GdbSeasonPassData>(s => s.tag == row.seasonId);

        // 최대 레벨은 levelRewards 마지막 entry로 도출 — 단일 출처 원칙(SeasonPassConstantsData에 별도 maxPassLevel 필드 없음).
        int maxLevel = seasonData?.level_rewards?.LastOrDefault()?.level ?? 0;
        int currentLevel = ComputeCurrentLevel(row.currentExp, expPerLevel, maxLevel);
        var alreadyBasic = ParseLevelSet(row.claimedBasicJson);
        var alreadyPremium = ParseLevelSet(row.claimedPremiumJson);

        var grantedBasic = new List<int>();
        var grantedPremium = new List<int>();
        var currencyAccum = new Dictionary<int, long>();
        // currencyType → 대표 rewardTag. CoinFly 연출용 대표보상을 서버가 데이터 lookup으로 확정 (클라 tag 추측 제거).
        var currencyTagByType = new Dictionary<int, string>();

        for (int level = 1; level <= currentLevel; level++)
        {
            var entry = seasonData?.level_rewards?.FirstOrDefault(e => e.level == level);
            if (entry == null)
            {
                continue;
            }

            if (!alreadyBasic.Contains(level))
            {
                grantedBasic.Add(level);
                AccumulateReward(currencyAccum, currencyTagByType, entry.basic_reward, entry.basic_count);
            }

            if (row.isPremium && !alreadyPremium.Contains(level))
            {
                grantedPremium.Add(level);
                AccumulateReward(currencyAccum, currencyTagByType, entry.premium_reward, entry.premium_count);
            }
        }

        // currency 누적 적용 — 한 reward tag에 여러 레벨 보상이 묶이는 경우 합산 후 1회 호출.
        foreach (var kv in currencyAccum)
        {
            await _currencyService.AddAsync(uid, kv.Key, kv.Value);
        }

        foreach (var lv in grantedBasic)
        {
            alreadyBasic.Add(lv);
        }

        foreach (var lv in grantedPremium)
        {
            alreadyPremium.Add(lv);
        }

        row.claimedBasicJson = SerializeLevelSet(alreadyBasic);
        row.claimedPremiumJson = SerializeLevelSet(alreadyPremium);
        row.updatedAtUtc = NowStr();
        await _gameDB.UpsertUserSeasonPassAsync(row);

        var response = new PkSeasonPassClaimResponse
        {
            result = ErrorCode.None,
            grantedBasicLevels = grantedBasic,
            grantedPremiumLevels = grantedPremium,
            // CoinFly 연출용 대표보상 — 서버가 데이터 lookup으로 정확한 rewardTag 확정 (Quest 패턴 정합).
            reward = BuildRepresentativeReward(currencyAccum, currencyTagByType),
            state = BuildStateDto(row),
        };
        // 재화는 유저 전체 절대 스냅샷으로만 채운다 (currency-contract — 부분 목록 시 클라에서 미포함 통화 0 소실).
        await _currencyService.PopulateCurrenciesAsync(response, uid);

        return response;
    }

    /// 누적 currency 중 대표(amount 최대) 1종을 RewardResolver로 PkRewardResult 합성 — CoinFly 연출용.
    /// Quest dailyBundle topCurrency 패턴 정합. 클라가 tag를 추측하지 않도록 서버가 데이터 lookup으로 확정.
    private PkRewardResult? BuildRepresentativeReward(Dictionary<int, long> accum, Dictionary<int, string> tagByType)
    {
        if (accum.Count == 0)
        {
            return null;
        }

        var top = accum.OrderByDescending(kv => kv.Value).First();
        if (!tagByType.TryGetValue(top.Key, out var tag) || string.IsNullOrEmpty(tag))
        {
            return null;
        }

        return RewardResolver.Resolve(_gameDataManager, tag, (int)top.Value);
    }


    /// 디버그 전용 — 패스 상태 강제 변이. op="levelup"(+1레벨) / "max"(만렙) / "reset"(전체 와이프).
    /// maxLevel·expPerLevel 도출은 ClaimAvailableAsync와 동일 출처(level_rewards / GdbConst) — 단일 진실원.
    public async Task<PkSeasonPassCheatResponse> CheatAsync(long uid, string op)
    {
        var row = await ResolveOrCreateUserSeasonPassAsync(uid);

        int expPerLevel = _gameDataManager.GetConstInt(GdbConst.SeasonPass.Category, GdbConst.SeasonPass.ExpPerLevel, defaultValue: 100);
        var seasonData = _gameDataManager.Get<GdbSeasonPassData>(s => s.tag == row.seasonId);
        int maxLevel = seasonData?.level_rewards?.LastOrDefault()?.level ?? 0;
        // 1-base 정의상 만렙 exp = (maxLevel-1)*expPerLevel. 클라 RefreshSlider의 totalExp와 동일 기준.
        int maxExp = maxLevel > 0 ? (maxLevel - 1) * expPerLevel : 0;

        switch (op)
        {
            case "levelup":
                row.currentExp += expPerLevel;
                if (maxLevel > 0 && row.currentExp > maxExp)
                {
                    row.currentExp = maxExp;
                }
                break;

            case "max":
                if (maxLevel > 0)
                {
                    row.currentExp = maxExp;
                }
                break;

            case "reset":
                row.currentExp = 0;
                row.isPremium = false;
                row.claimedBasicJson = "[]";
                row.claimedPremiumJson = "[]";
                row.isSettled = false;        // 새 시즌 시작 — 만료 정산 재무장
                row.cheatForceEnded = false;  // 강제 만료 해제 → 다시 active
                break;

            case "end":
                // 유저 row만 강제 만료 (게임데이터 종료시각은 전역이라 안 건드림).
                row.cheatForceEnded = true;
                break;

            default:
                return new PkSeasonPassCheatResponse { result = ErrorCode.InValidRequestHttpBody };
        }

        row.updatedAtUtc = NowStr();
        await _gameDB.UpsertUserSeasonPassAsync(row);

        // end op는 즉시 정산 트리거 — 응답이 바로 ended 상태/우편 발송 반영(클라 GetState 재호출 불필요).
        await SettleIfExpiredAsync(row);

        return new PkSeasonPassCheatResponse
        {
            result = ErrorCode.None,
            state = BuildStateDto(row),
        };
    }


    // ===== private =====

    /// 경험치 → 현재 레벨 (1-base). 시작(exp 0) 1, expPerLevel 단위 누적 시 +1, maxLevel 캡.
    /// ⚠ 클라 SeasonPassRuntimeState.ComputeCurrentLevel과 반드시 동일 공식 — 표시 레벨 = 보상 지급 레벨 일치 보장.
    private static int ComputeCurrentLevel(int currentExp, int expPerLevel, int maxLevel)
    {
        if (expPerLevel <= 0)
        {
            return 1;
        }

        int level = currentExp / expPerLevel + 1;
        if (maxLevel > 0 && level > maxLevel)
        {
            level = maxLevel;
        }

        return level;
    }

    /// 시즌 상태 — endUtc 경과면 "ended"(만료), 아니면 "active". 클라 표시·진입 분기용.
    private string ComputeSeasonStatus(GameUserSeasonPass row)
    {
        return IsSeasonExpired(row) ? "ended" : "active";
    }

    /// 만료 판정 — cheat 강제 만료(유저별) 또는 게임데이터 종료시각(GdbSeasonPassData.end_utc, 전역) 경과.
    private bool IsSeasonExpired(GameUserSeasonPass row)
    {
        if (row.cheatForceEnded)
        {
            return true;
        }

        string endStr = ResolveSeasonEndUtc(row.seasonId);
        if (string.IsNullOrEmpty(endStr))
        {
            return false;
        }

        if (!DateTime.TryParse(endStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var endUtc))
        {
            return false;
        }

        return _clock.UtcNow >= endUtc;
    }

    /// Lazy 만료 정산 — endUtc 경과 && 미정산이면 미수령 보상을 우편 발송 후 isSettled 마킹(멱등).
    /// GetState 진입 시 호출. 보상 없어도 정산 완료 마킹(중복 발송/체크 방지).
    /// 정산 우편 만료 기간 (분) — 7일. SendAsync는 expireMinutes를 그대로 적용하므로 0이면 즉시 만료됨에 주의.
    private const int SettleMailExpireMinutes = 60 * 24 * 7;

    private async Task SettleIfExpiredAsync(GameUserSeasonPass row)
    {
        if (row.isSettled || !IsSeasonExpired(row))
        {
            return;
        }

        var unclaimed = CollectUnclaimedRewards(row);
        if (unclaimed.Count > 0)
        {
            // 우편 cell은 단일 reward만 표시·아이콘 매핑하므로, 같은 통화(rewardTag)는 합산해 tag당 1통 발송.
            var grouped = unclaimed
                .GroupBy(r => r.rewardTag)
                .Select(g => new MailRewardEntry { rewardTag = g.Key, count = g.Sum(x => x.count) })
                .ToList();

            foreach (var entry in grouped)
            {
                var single = new List<MailRewardEntry> { entry };
                string iconKey = ResolveItemIconName(entry.rewardTag);
                await _mailService.SendAsync(
                    row.uid,
                    SeasonEndedMailTitleKey,
                    single,
                    iconAtlas: string.IsNullOrEmpty(iconKey) ? "" : "ItemAtlas",
                    iconKey: iconKey,
                    expireMinutes: SettleMailExpireMinutes);
            }
        }

        row.isSettled = true;
        row.updatedAtUtc = NowStr();
        await _gameDB.UpsertUserSeasonPassAsync(row);
    }

    /// rewardTag(GameplayTag) → GdbItemData.icon_name 룩업. 우편함 cell 아이콘용 (QuestService.ResolveItemIconName과 동일 패턴).
    private string ResolveItemIconName(string rewardTag)
    {
        if (string.IsNullOrEmpty(rewardTag))
        {
            return "";
        }

        var items = _gameDataManager.GetList<GdbItemData>();
        var item = items?.FirstOrDefault(i => i.tag == rewardTag);
        return item?.icon_name ?? "";
    }

    /// 도달 레벨까지의 미수령 basic/premium 보상 → 우편 reward 목록. ClaimAvailableAsync의 레벨 순회와 동일 기준.
    private List<MailRewardEntry> CollectUnclaimedRewards(GameUserSeasonPass row)
    {
        var result = new List<MailRewardEntry>();

        int expPerLevel = _gameDataManager.GetConstInt(GdbConst.SeasonPass.Category, GdbConst.SeasonPass.ExpPerLevel, defaultValue: 100);
        var seasonData = _gameDataManager.Get<GdbSeasonPassData>(s => s.tag == row.seasonId);
        int maxLevel = seasonData?.level_rewards?.LastOrDefault()?.level ?? 0;
        int currentLevel = ComputeCurrentLevel(row.currentExp, expPerLevel, maxLevel);

        var claimedBasic = ParseLevelSet(row.claimedBasicJson);
        var claimedPremium = ParseLevelSet(row.claimedPremiumJson);

        for (int level = 1; level <= currentLevel; level++)
        {
            var entry = seasonData?.level_rewards?.FirstOrDefault(e => e.level == level);
            if (entry == null)
            {
                continue;
            }

            if (!claimedBasic.Contains(level) && !string.IsNullOrEmpty(entry.basic_reward) && entry.basic_count > 0)
            {
                result.Add(new MailRewardEntry { rewardTag = entry.basic_reward, count = entry.basic_count });
            }

            if (row.isPremium && !claimedPremium.Contains(level) && !string.IsNullOrEmpty(entry.premium_reward) && entry.premium_count > 0)
            {
                result.Add(new MailRewardEntry { rewardTag = entry.premium_reward, count = entry.premium_count });
            }
        }

        return result;
    }

    private async Task<GameUserSeasonPass> ResolveOrCreateUserSeasonPassAsync(long uid)
    {
        var row = await _gameDB.GetUserSeasonPassAsync(uid, CurrentSeasonId);
        if (row != null)
        {
            // 종료시각은 게임데이터 단일 출처라 row 보강 불필요 (seasonEndUtc 컬럼 제거됨).
            return row;
        }

        string nowStr = NowStr();
        row = new GameUserSeasonPass
        {
            uid = uid,
            seasonId = CurrentSeasonId,
            currentExp = 0,
            isPremium = false,
            claimedBasicJson = "[]",
            claimedPremiumJson = "[]",
            updatedAtUtc = nowStr,
        };
        await _gameDB.UpsertUserSeasonPassAsync(row);
        return row;
    }

    /// GdbSeasonPassData.end_utc lookup — Lazy 만료 정산 비교용. 미업로드/미설정이면 빈 문자열.
    private string ResolveSeasonEndUtc(string seasonId)
    {
        var data = _gameDataManager.Get<GdbSeasonPassData>(s => s.tag == seasonId);
        return data?.end_utc ?? "";
    }

    /// reward tag → ItemData lookup → currencyType 매핑 → accum에 누적. MailService 패턴 차용.
    /// tagByType에는 currencyType별 첫 rewardTag를 기록 — 대표보상(CoinFly) 합성용. SeasonPass는 통화당 동일 tag라 첫 값으로 충분.
    private void AccumulateReward(Dictionary<int, long> accum, Dictionary<int, string> tagByType, string rewardTag, int count)
    {
        if (string.IsNullOrEmpty(rewardTag) || count <= 0)
        {
            return;
        }

        string currencyName = RewardResolver.ResolveCurrencyType(_gameDataManager, rewardTag);
        int currencyType = ParseCurrencyType(currencyName);

        if (!accum.ContainsKey(currencyType))
        {
            accum[currencyType] = 0;
        }

        accum[currencyType] += count;

        if (!tagByType.ContainsKey(currencyType))
        {
            tagByType[currencyType] = rewardTag;
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

    private PkSeasonPassState BuildStateDto(GameUserSeasonPass row)
    {
        int expPerLevel = _gameDataManager.GetConstInt(GdbConst.SeasonPass.Category, GdbConst.SeasonPass.ExpPerLevel, defaultValue: 100);
        var seasonData = _gameDataManager.Get<GdbSeasonPassData>(s => s.tag == row.seasonId);
        int maxLevel = seasonData?.level_rewards?.LastOrDefault()?.level ?? 0;

        int currentLevel = ComputeCurrentLevel(row.currentExp, expPerLevel, maxLevel);
        int nextLevel = maxLevel > 0 ? Math.Min(currentLevel + 1, maxLevel) : currentLevel + 1;
        // 만렙이면 현재 레벨 칸이 가득(=100%). 그 외엔 레벨 내 누적 경험치.
        bool isMax = maxLevel > 0 && currentLevel >= maxLevel;
        int expInCurrentLevel = expPerLevel <= 0 ? 0 : (isMax ? expPerLevel : row.currentExp % expPerLevel);

        return new PkSeasonPassState
        {
            seasonId = row.seasonId,
            currentExp = row.currentExp,
            isPremium = row.isPremium,
            claimedBasic = ParseLevelList(row.claimedBasicJson),
            claimedPremium = ParseLevelList(row.claimedPremiumJson),
            // 클라 헤더 남은시간 표시용 — 게임데이터 종료시각(전역 단일 출처)을 응답에 채워 전송.
            seasonEndUtc = ResolveSeasonEndUtc(row.seasonId),
            currentLevel = currentLevel,
            nextLevel = nextLevel,
            expInCurrentLevel = expInCurrentLevel,
            expPerLevel = expPerLevel,
            seasonStatus = ComputeSeasonStatus(row),
        };
    }

    private static HashSet<int> ParseLevelSet(string json)
    {
        var list = ParseLevelList(json);
        return new HashSet<int>(list);
    }

    private static List<int> ParseLevelList(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new List<int>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<int>>(json) ?? new List<int>();
        }
        catch
        {
            return new List<int>();
        }
    }

    private static string SerializeLevelSet(HashSet<int> set)
    {
        var sorted = new List<int>(set);
        sorted.Sort();
        return JsonSerializer.Serialize(sorted);
    }

    private string NowStr()
    {
        return _clock.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }
}
