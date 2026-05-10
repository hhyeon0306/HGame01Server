using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using HGame01Server.Models;
using HGame01Server.Models.GameData;
using HGame01Server.Repository;

namespace HGame01Server.Services;

/// 서버 측 Quest 도메인 서비스. Active/Claim/RefreshDaily 흐름 + GdbQuestData 조회.
/// 진행 누적은 QuestProgressService 책임. 본 서비스는 인스턴스 발급/조회/수령.
public class QuestService
{
    private readonly IGameDB _gameDB;
    private readonly IClock _clock;
    private readonly IResetSchedule _dailyReset;
    private readonly GameDataManager _gameDataManager;
    private readonly CurrencyService _currencyService;

    public QuestService(IGameDB gameDB, IClock clock, IResetSchedule dailyReset, GameDataManager gameDataManager, CurrencyService currencyService)
    {
        _gameDB = gameDB;
        _clock = clock;
        _dailyReset = dailyReset;
        _gameDataManager = gameDataManager;
        _currencyService = currencyService;
    }

    /// 활성(InProgress/Completed) 인스턴스 조회 — 만료 lazy 정리 + DTO 변환.
    /// Expired/Claimed는 응답에서 제외 — 클라가 stale row를 dedup으로 채택하는 사고 차단.
    public async Task<List<PkQuestInstanceDto>> GetActiveAsync(long uid)
    {
        var nowStr = FormatUtc(_clock.UtcNow);
        await _gameDB.ExpireQuestInstancesAsync(uid, nowStr);
        var instances = await _gameDB.GetActiveQuestInstancesByUidAsync(uid);
        return instances.Select(ToDto).ToList();
    }

    /// 일일 슬롯 발급 — 자정 통과 시 호출. 기존 InProgress 만료 + 신규 InProgress 발급을 단일 트랜잭션으로 atomic 보장.
    /// Completed(미수령)는 만료 대상에서 제외 — 사용자 보상 보호.
    /// 신규 인스턴스의 subProgressJson은 GdbQuestData required count로 채움 — 서버 권위 progress 판정의 토대.
    /// idempotency 가드 — 만료 시각 전 fresh InProgress가 존재하면 skip 후 기존 슬롯 반환.
    /// 자정 통과 후엔 ExpireQuestInstancesAsync(GetActiveAsync 진입점)가 expiresAtUtc 비교로 자동 Expired 처리하므로 정상 발급 흐름 진입.
    public async Task<List<PkQuestInstanceDto>> RefreshDailyAsync(long uid, int dailyContainerStableId, int slotCount, IReadOnlyList<int> dailyQuestDataIds, bool force = false)
    {
        var nowStr = FormatUtc(_clock.UtcNow);

        var existing = await _gameDB.GetActiveQuestInstancesByUidAsync(uid);
        var dailyInProgress = existing
            .Where(q => q.containerStableId == dailyContainerStableId
                && q.status == "InProgress")
            .ToList();

        // idempotency — 같은 reset window 내 재호출은 신규 발급 skip.
        // string.Compare는 yyyy-MM-dd HH:mm:ss 형식이라 lexical = chronological 일치.
        // 응답은 신규 발급 여부와 무관하게 자기 컨테이너의 모든 활성(InProgress/Completed/Claimed) 인스턴스 — 클라 Hydrate가 dedup 정렬.
        // Claimed 인스턴스 누락 시 클라가 destroy → UX 사고 차단.
        // force=true면 idempotency 가드 우회 — cheat resetdaily 강제 재발급 흐름.
        bool anyFresh = !force && dailyInProgress.Any(q =>
            !string.IsNullOrEmpty(q.expiresAtUtc) && string.Compare(q.expiresAtUtc, nowStr) > 0);
        if (anyFresh)
        {
            return existing
                .Where(q => q.containerStableId == dailyContainerStableId)
                .Select(ToDto)
                .ToList();
        }

        // 만료 진행 — InProgress + Claimed 모두 Expired로. Completed(미수령)는 유지하여 사용자 보상 보호.
        // Claimed를 함께 만료해야 자정 회전마다 history row 누적되는 사고 차단 (어제 받은 보상은 다음 window에서는 의미 없음).
        var dailyExisting = existing
            .Where(q => q.containerStableId == dailyContainerStableId
                && (q.status == "InProgress" || q.status == "Claimed"))
            .ToList();
        foreach (var q in dailyExisting)
        {
            q.status = "Expired";
            q.lastUpdatedUtc = nowStr;
        }

        // 신규 발급 — pool에서 slotCount만큼.
        var newInstances = new List<GameUserQuestInstance>();
        if (dailyQuestDataIds != null && dailyQuestDataIds.Count > 0)
        {
            var nextResetStr = FormatUtc(_dailyReset.Next);
            int limit = System.Math.Min(slotCount, dailyQuestDataIds.Count);
            for (int i = 0; i < limit; i++)
            {
                int qdId = dailyQuestDataIds[i];
                newInstances.Add(new GameUserQuestInstance
                {
                    instanceId = System.Guid.NewGuid().ToString(),
                    uid = uid,
                    questDataId = qdId,
                    containerStableId = dailyContainerStableId,
                    subProgressJson = BuildInitialSubProgressJson(qdId),
                    status = "InProgress",
                    issuedAtUtc = nowStr,
                    expiresAtUtc = nextResetStr,
                    lastUpdatedUtc = nowStr,
                });
            }
        }

        await _gameDB.RefreshDailyQuestsTransactionAsync(dailyExisting, newInstances);

        // 응답 — 신규 InProgress + 기존 Completed/Claimed (Expired 제외).
        // 신규 발급 흐름에서도 기존 Claimed가 응답에 살아있어야 클라가 destroy 사고 X.
        var refreshed = await _gameDB.GetActiveQuestInstancesByUidAsync(uid);
        return refreshed
            .Where(q => q.containerStableId == dailyContainerStableId)
            .Select(ToDto)
            .ToList();
    }

    /// 신규 인스턴스의 초기 subProgress JSON. GdbQuestData.required_count로 채움 — ApplyDelta가 Required 정합 판정 가능.
    /// 본 라운드는 단일 condition 가정. Composite는 Phase 9 도입 시 condition 다형 직렬화로 확장.
    /// quest 미발견 또는 required_count<=0이면 데이터 결함 — fail-fast throw로 즉시 노출 (Active 응답 자체가 실패).
    private string BuildInitialSubProgressJson(int questDataId)
    {
        var quests = _gameDataManager.GetList<GdbQuestData>();
        var quest = quests?.FirstOrDefault(q => q.id == questDataId);
        if (quest == null)
        {
            throw new InvalidOperationException($"[Quest] BuildInitialSubProgressJson: GdbQuestData id={questDataId} 미발견 — 데이터 시드 결함.");
        }
        if (quest.required_count <= 0)
        {
            throw new InvalidOperationException($"[Quest] BuildInitialSubProgressJson: GdbQuestData id={questDataId} (tag={quest.quest_tag}) required_count={quest.required_count} 무효 — 디자이너 데이터 결함.");
        }
        var entries = new List<PkSubProgressEntry>
        {
            new() { ChildIndex = 0, Progress = 0, Required = quest.required_count }
        };
        return JsonSerializer.Serialize(entries);
    }

    /// 보상 수령 — Completed에서만 허용. RewardResolver로 보상 결정 + Currency 즉시 누적.
    /// Equipment/Item 등 비-Currency 보상은 응답에 정보만 담고 실제 지급은 Phase 9 RewardGrantService 통합에서 처리.
    public async Task<(ErrorCode error, PkRewardResult? reward, List<PkCurrency> currencies)>
        ClaimAsync(long uid, string instanceId)
    {
        var inst = await _gameDB.GetQuestInstanceAsync(uid, instanceId);
        if (inst == null)
        {
            return (ErrorCode.QuestInstanceNotFound, null, new());
        }
        if (inst.status != "Completed")
        {
            return (ErrorCode.QuestNotClaimable, null, new());
        }

        // GdbQuestData 조회 — id 기반. reward_item / reward_count 자동 동기화 필드 사용.
        var quests = _gameDataManager.GetList<GdbQuestData>();
        var quest = quests?.FirstOrDefault(q => q.id == inst.questDataId);
        if (quest == null)
        {
            return (ErrorCode.QuestDataNotFound, null, new());
        }

        // 보상 결정 — Shop과 동일 패턴. reward_item 미설정이면 reward null (status 전환만).
        // Currency 보상은 즉시 누적. 비-Currency(Equipment/Item)는 응답에 정보만 — Phase 9 RewardGrantService 통합 시 실제 지급.
        // currencyTypeId=-1은 통화 없음 sentinel — Diamond=0이 valid 값이라 0을 sentinel로 절대 사용 금지.
        PkRewardResult? reward = null;
        int currencyTypeId = -1;
        long currencyDelta = 0;
        if (!string.IsNullOrEmpty(quest.reward_item) && quest.reward_count > 0)
        {
            reward = RewardResolver.Resolve(_gameDataManager, quest.reward_item, quest.reward_count);
            string currencyName = RewardResolver.ResolveCurrencyType(_gameDataManager, reward.RewardTag);
            if (!string.IsNullOrEmpty(currencyName))
            {
                currencyTypeId = ParseCurrencyType(currencyName);
                currencyDelta = quest.reward_count;
            }
        }

        // 인스턴스 Claimed 전환 + Currency 누적을 단일 트랜잭션으로 묶음 — 부분 실패 시 중복 지급 사고 차단.
        inst.status = "Claimed";
        inst.lastUpdatedUtc = FormatUtc(_clock.UtcNow);
        await _gameDB.ClaimQuestTransactionAsync(inst, uid, currencyTypeId, currencyDelta);

        var currencies = await _currencyService.GetAllAsync(uid);
        return (ErrorCode.None, reward, currencies);
    }

    /// 일일 종합 보상 수령 — 활성 Daily 슬롯 중 Claimed 카운트가 임계 이상이면 Currency 누적.
    /// 일일 1회 제한: users.lastDailyBundleClaimedDateUtc 컬럼이 현 reset window 일자와 일치하면 AlreadyClaimed.
    /// users 컬럼 갱신 + Currency 누적은 ClaimDailyBundleTransactionAsync로 atomic 보장.
    public async Task<(ErrorCode error, PkRewardResult? reward, List<PkCurrency> currencies)>
        ClaimDailyBundleAsync(long uid)
    {
        // ① 일일 1회 제한 — IResetSchedule.Current 기준 일자(yyyy-MM-dd) 비교.
        // 자정(또는 dailyResetHourUtc) 통과 시 reset window가 새 일자로 회전 → 컬럼 값과 달라지므로 자동으로 다시 수령 가능.
        var currentDateUtc = _dailyReset.Current.ToString("yyyy-MM-dd");
        var lastClaimedDate = await _gameDB.GetLastDailyBundleClaimedDateAsync(uid);
        if (lastClaimedDate == currentDateUtc)
        {
            return (ErrorCode.QuestDailyBundleAlreadyClaimed, null, new());
        }

        // ② Claimed 카운트 검증 — 활성 Daily 인스턴스 중 Claimed가 임계 이상이어야 함.
        var instances = await _gameDB.GetActiveQuestInstancesByUidAsync(uid);
        int claimedDailyCount = instances.Count(i =>
            i.containerStableId == QuestServerConstants.QuestContainerDailyStableId
            && i.status == "Claimed");

        if (claimedDailyCount < QuestServerConstants.DailyBundleRequiredClaimedCount)
        {
            return (ErrorCode.QuestDailyBundleNotReady, null, new());
        }

        // ③ Currency 누적 + 컬럼 갱신을 atomic 트랜잭션으로 묶음 — 부분 실패 시 컬럼 미갱신으로 재호출 중복 지급 사고 차단.
        // Diamond hardcoded (라운드 D 2차에 데이터 기반 전환 예정).
        long delta = QuestServerConstants.DailyBundleRewardDiamondAmount;
        await _gameDB.ClaimDailyBundleTransactionAsync(uid, currentDateUtc, CurrencyType.Diamond, delta);

        var currencies = await _currencyService.GetAllAsync(uid);
        var reward = new PkRewardResult
        {
            RewardTag = "DailyBundle",
            Count = (int)delta,
            RewardType = "Currency",
            ItemKind = "Currency",
        };
        return (ErrorCode.None, reward, currencies);
    }

    /// DB row → DTO 변환.
    public static PkQuestInstanceDto ToDto(GameUserQuestInstance inst)
    {
        return new PkQuestInstanceDto
        {
            InstanceId = inst.instanceId,
            QuestDataId = inst.questDataId,
            ContainerStableId = inst.containerStableId,
            SubProgress = ParseSubProgress(inst.subProgressJson),
            Status = inst.status,
            IssuedAtUtc = inst.issuedAtUtc,
            ExpiresAtUtc = inst.expiresAtUtc,
        };
    }

    private static List<PkSubProgressEntry> ParseSubProgress(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new List<PkSubProgressEntry>();
        }
        try
        {
            return JsonSerializer.Deserialize<List<PkSubProgressEntry>>(json) ?? new List<PkSubProgressEntry>();
        }
        catch
        {
            return new List<PkSubProgressEntry>();
        }
    }

    private static int ParseCurrencyType(string s)
    {
        return s?.ToLowerInvariant() switch
        {
            "diamond" => CurrencyType.Diamond,
            "gold" => CurrencyType.Gold,
            _ => -1,
        };
    }

    private static string FormatUtc(DateTime utc) => utc.ToString("yyyy-MM-dd HH:mm:ss");
}
