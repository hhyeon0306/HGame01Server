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

    public QuestService(IGameDB gameDB, IClock clock, GameDataManager gameDataManager, CurrencyService currencyService)
    {
        _gameDB = gameDB;
        _clock = clock;
        _gameDataManager = gameDataManager;
        _currencyService = currencyService;

        // Daily 리셋 시간은 ShopConstants와 공유 — 모든 도메인이 같은 자정.
        int resetHour = _gameDataManager.GetConstInt(GdbConst.Shop.Category, GdbConst.Shop.DailyResetHourUtc, 20);
        _dailyReset = new DailyResetSchedule(clock, resetHour);
    }

    /// 활성 인스턴스 조회 — 만료 lazy 정리 + DTO 변환.
    public async Task<List<PkQuestInstanceDto>> GetActiveAsync(long uid)
    {
        var nowStr = FormatUtc(_clock.UtcNow);
        await _gameDB.ExpireQuestInstancesAsync(uid, nowStr);
        var instances = await _gameDB.GetQuestInstancesByUidAsync(uid);
        return instances.Select(ToDto).ToList();
    }

    /// 일일 슬롯 발급 — 자정 통과 시 호출. 기존 Daily 슬롯 만료 + 신규 발급.
    public async Task<List<PkQuestInstanceDto>> RefreshDailyAsync(long uid, int dailyContainerStableId, int slotCount, IReadOnlyList<int> dailyQuestDataIds)
    {
        var nowStr = FormatUtc(_clock.UtcNow);

        // 기존 Daily 슬롯 만료 — 같은 컨테이너 + InProgress/Completed 상태인 인스턴스.
        var existing = await _gameDB.GetQuestInstancesByUidAsync(uid);
        var dailyExisting = existing
            .Where(q => q.containerStableId == dailyContainerStableId
                && (q.status == "InProgress" || q.status == "Completed"))
            .ToList();
        foreach (var q in dailyExisting)
        {
            q.status = "Expired";
            q.lastUpdatedUtc = nowStr;
        }
        if (dailyExisting.Count > 0)
        {
            await _gameDB.UpdateQuestInstancesBatchAsync(dailyExisting);
        }

        // 신규 발급 — pool에서 slotCount만큼.
        if (dailyQuestDataIds == null || dailyQuestDataIds.Count == 0)
        {
            return new List<PkQuestInstanceDto>();
        }
        var nextResetStr = FormatUtc(_dailyReset.Next);
        int limit = System.Math.Min(slotCount, dailyQuestDataIds.Count);
        var newInstances = new List<GameUserQuestInstance>(limit);
        for (int i = 0; i < limit; i++)
        {
            int qdId = dailyQuestDataIds[i];
            newInstances.Add(new GameUserQuestInstance
            {
                instanceId = System.Guid.NewGuid().ToString(),
                uid = uid,
                questDataId = qdId,
                containerStableId = dailyContainerStableId,
                subProgressJson = "[]",
                status = "InProgress",
                issuedAtUtc = nowStr,
                expiresAtUtc = nextResetStr,
                lastUpdatedUtc = nowStr,
            });
        }
        await _gameDB.AddQuestInstancesBatchAsync(newInstances);
        return newInstances.Select(ToDto).ToList();
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
        PkRewardResult? reward = null;
        if (!string.IsNullOrEmpty(quest.reward_item) && quest.reward_count > 0)
        {
            reward = RewardResolver.Resolve(_gameDataManager, quest.reward_item, quest.reward_count);

            // Currency 보상은 즉시 누적. 비-Currency(Equipment/Item)는 응답에 정보만 — Phase 9 RewardGrantService 통합 시 실제 지급.
            string currencyName = RewardResolver.ResolveCurrencyType(_gameDataManager, reward.RewardTag);
            if (!string.IsNullOrEmpty(currencyName))
            {
                int currencyTypeId = ParseCurrencyType(currencyName);
                await _gameDB.UpsertCurrencyAsync(uid, currencyTypeId, quest.reward_count);
            }
        }

        // 인스턴스 Claimed 전환.
        inst.status = "Claimed";
        inst.lastUpdatedUtc = FormatUtc(_clock.UtcNow);
        await _gameDB.UpdateQuestInstanceAsync(inst);

        var currencies = await _currencyService.GetAllAsync(uid);
        return (ErrorCode.None, reward, currencies);
    }

    /// DB row → DTO 변환.
    public static PkQuestInstanceDto ToDto(GameUserQuestInstance inst)
    {
        return new PkQuestInstanceDto
        {
            InstanceId = inst.instanceId,
            QuestDataId = inst.questDataId,
            QuestTagStableId = 0, // GdbQuestData lookup은 호출자가 필요시 채움 (현재 미사용)
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
            "gold" => CurrencyType.Gold,
            _ => CurrencyType.Diamond,
        };
    }

    private static string FormatUtc(DateTime utc) => utc.ToString("yyyy-MM-dd HH:mm:ss");
}
