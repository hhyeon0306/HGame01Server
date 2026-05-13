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

    /// 현재 단일 활성 시즌 식별자. 추후 GdbSeasonPassData에서 startUtc/endUtc 범위로 lookup 예정.
    private const string CurrentSeasonId = "Tag.Pass.Season1";

    public SeasonPassService(IGameDB gameDB, IClock clock, GameDataManager gameDataManager, CurrencyService currencyService)
    {
        _gameDB = gameDB;
        _clock = clock;
        _gameDataManager = gameDataManager;
        _currencyService = currencyService;
    }

    /// 현재 시즌 상태 조회. Row 없으면 신규 발급 후 반환.
    public async Task<PkSeasonPassStateResponse> GetStateAsync(long uid)
    {
        var row = await ResolveOrCreateUserSeasonPassAsync(uid);
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
        int reachedLevel = expPerLevel > 0 ? row.currentExp / expPerLevel : 0;
        if (maxLevel > 0 && reachedLevel > maxLevel)
        {
            reachedLevel = maxLevel;
        }
        var alreadyBasic = ParseLevelSet(row.claimedBasicJson);
        var alreadyPremium = ParseLevelSet(row.claimedPremiumJson);

        var grantedBasic = new List<int>();
        var grantedPremium = new List<int>();
        var currencyAccum = new Dictionary<int, long>();

        for (int level = 1; level <= reachedLevel; level++)
        {
            var entry = seasonData?.level_rewards?.FirstOrDefault(e => e.level == level);
            if (entry == null)
            {
                continue;
            }

            if (!alreadyBasic.Contains(level))
            {
                grantedBasic.Add(level);
                AccumulateReward(currencyAccum, entry.basic_reward, entry.basic_count);
            }

            if (row.isPremium && !alreadyPremium.Contains(level))
            {
                grantedPremium.Add(level);
                AccumulateReward(currencyAccum, entry.premium_reward, entry.premium_count);
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

        // currency absolute 응답 — 클라 UI absolute 적용 (Quest 응답 패턴 정합).
        var currencies = new List<PkCurrency>();
        foreach (var kv in currencyAccum)
        {
            var cur = await _gameDB.GetCurrencyAsync(uid, kv.Key);
            currencies.Add(new PkCurrency { CurrencyType = kv.Key, Amount = cur?.amount ?? 0 });
        }

        return new PkSeasonPassClaimResponse
        {
            result = ErrorCode.None,
            grantedBasicLevels = grantedBasic,
            grantedPremiumLevels = grantedPremium,
            currencies = currencies,
            state = BuildStateDto(row),
        };
    }


    // ===== private =====

    private async Task<GameUserSeasonPass> ResolveOrCreateUserSeasonPassAsync(long uid)
    {
        var row = await _gameDB.GetUserSeasonPassAsync(uid, CurrentSeasonId);
        if (row != null)
        {
            // 기존 row의 seasonEndUtc 비어있으면 자동 보강 — GdbSeasonPassData 업로드 이전에 생성된 row 보정.
            if (string.IsNullOrEmpty(row.seasonEndUtc))
            {
                string resolved = ResolveSeasonEndUtc(CurrentSeasonId);
                if (!string.IsNullOrEmpty(resolved))
                {
                    row.seasonEndUtc = resolved;
                    row.updatedAtUtc = NowStr();
                    await _gameDB.UpsertUserSeasonPassAsync(row);
                }
            }
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
            seasonEndUtc = ResolveSeasonEndUtc(CurrentSeasonId),
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
    private void AccumulateReward(Dictionary<int, long> accum, string rewardTag, int count)
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
        return new PkSeasonPassState
        {
            seasonId = row.seasonId,
            currentExp = row.currentExp,
            isPremium = row.isPremium,
            claimedBasic = ParseLevelList(row.claimedBasicJson),
            claimedPremium = ParseLevelList(row.claimedPremiumJson),
            seasonEndUtc = row.seasonEndUtc ?? "",
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
