using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using HGame01Server.Models;
using HGame01Server.Models.GameData;
using HGame01Server.Repository;

namespace HGame01Server.Services;

/// 서버 측 SeasonPass 도메인 서비스.
/// Phase B 골격 — exp 누적 + 멱등 dedup + premium 토글까지. Claim 보상 매핑은 Phase D/E에서 확장.
///
/// 시즌 식별은 현재 단일 활성 시즌(Tag.Pass.Season1) 고정 — 시즌 스케줄러는 Live 운영 시점에 도입.
public class SeasonPassService
{
    private readonly IGameDB _gameDB;
    private readonly IClock _clock;
    private readonly GameDataManager _gameDataManager;

    /// 현재 단일 활성 시즌 식별자. 추후 GdbSeasonPassData에서 startUtc/endUtc 범위로 lookup 예정.
    private const string CurrentSeasonId = "Tag.Pass.Season1";

    public SeasonPassService(IGameDB gameDB, IClock clock, GameDataManager gameDataManager)
    {
        _gameDB = gameDB;
        _clock = clock;
        _gameDataManager = gameDataManager;
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

        // exp 계산 — GdbConst.Pass.stageVictoryExp / stageDefeatExp. 미업로드 단계 대비 default.
        int expGained = stageResult == "Victory"
            ? _gameDataManager.GetConstInt(GdbConst.Pass.Category, "stageVictoryExp", defaultValue: 100)
            : _gameDataManager.GetConstInt(GdbConst.Pass.Category, "stageDefeatExp", defaultValue: 30);

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

    /// 수령 가능 보상 일괄 지급. Phase B 골격 — 현재 도달 레벨 / 미수령 레벨 계산만.
    /// 실제 보상(currency/item) 지급은 GdbSeasonPassData.levelRewards 매핑 작업(Phase D/E)에서 확장.
    public async Task<PkSeasonPassClaimResponse> ClaimAvailableAsync(long uid)
    {
        var row = await ResolveOrCreateUserSeasonPassAsync(uid);

        int expPerLevel = _gameDataManager.GetConstInt(GdbConst.Pass.Category, "expPerLevel", defaultValue: 1000);
        int maxLevel = _gameDataManager.GetConstInt(GdbConst.Pass.Category, GdbConst.Pass.MaxPassLevel, defaultValue: 30);
        int reachedLevel = expPerLevel > 0 ? row.currentExp / expPerLevel : 0;
        if (reachedLevel > maxLevel)
        {
            reachedLevel = maxLevel;
        }

        var alreadyBasic = ParseLevelSet(row.claimedBasicJson);
        var alreadyPremium = ParseLevelSet(row.claimedPremiumJson);

        var grantedBasic = new List<int>();
        var grantedPremium = new List<int>();
        for (int level = 1; level <= reachedLevel; level++)
        {
            if (!alreadyBasic.Contains(level))
            {
                grantedBasic.Add(level);
            }

            if (row.isPremium && !alreadyPremium.Contains(level))
            {
                grantedPremium.Add(level);
            }
        }

        // 실제 보상 적용은 Phase D/E에서 GdbSeasonPassData.levelRewards 매핑 후 currency/item 지급으로 확장.
        // 현재는 claimed 레벨 누적만 — 클라가 멱등 응답으로 인지.
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

        return new PkSeasonPassClaimResponse
        {
            result = ErrorCode.None,
            grantedBasicLevels = grantedBasic,
            grantedPremiumLevels = grantedPremium,
            currencies = new List<PkCurrency>(),
            state = BuildStateDto(row),
        };
    }


    // ===== private =====

    /// 현재 시즌 row를 조회하거나 없으면 신규 발급. 시즌 종료 시각은 GdbSeasonPassData에서 lookup (없으면 빈 문자열).
    private async Task<GameUserSeasonPass> ResolveOrCreateUserSeasonPassAsync(long uid)
    {
        var row = await _gameDB.GetUserSeasonPassAsync(uid, CurrentSeasonId);
        if (row != null)
        {
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

    /// GdbSeasonPassData.endUtc lookup — 클라 업로드 안 됐으면 빈 문자열 (Lazy 만료 정산 단계에서 처리).
    /// 현재는 SeasonPassData가 List 필드 복합이라 GameDataUploader.TrySerializeValue에서 스킵될 가능성 — 추후 보강.
    private string ResolveSeasonEndUtc(string seasonId)
    {
        return string.Empty;
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
