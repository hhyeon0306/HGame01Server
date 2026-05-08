using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using HGame01Server.Models;
using HGame01Server.Models.GameData;
using HGame01Server.Repository;

namespace HGame01Server.Services;

/// 퀘스트 진행 누적 서비스 (Phase 8). 이벤트 → 인스턴스 progress/status 권위 갱신.
/// 멱등 보장 + 인스턴스 갱신은 단일 트랜잭션으로 묶어 atomic commit/rollback.
public class QuestProgressService
{
    private readonly IGameDB _gameDB;
    private readonly IClock _clock;

    public QuestProgressService(IGameDB gameDB, IClock clock)
    {
        _gameDB = gameDB;
        _clock = clock;
    }

    /// 클라 이벤트 배치 적용. dedup INSERT + 인스턴스 UPDATE를 단일 SaveChanges로 묶어 atomic 보장.
    /// 부분 실패 시 dedup 키만 소비되는 사고 차단.
    /// 반환: (적용 카운트, 중복/skip 카운트, 갱신된 인스턴스 목록).
    public async Task<(int applied, int duplicate, List<GameUserQuestInstance> updated)>
        ApplyBatchAsync(long uid, List<PkQuestEventEntry> events)
    {
        if (events == null || events.Count == 0)
        {
            return (0, 0, new List<GameUserQuestInstance>());
        }

        var nowStr = FormatUtc(_clock.UtcNow);

        // 만료 인스턴스 lazy 정리 — Active 응답이 stale 안 보이도록.
        await _gameDB.ExpireQuestInstancesAsync(uid, nowStr);

        var instances = await _gameDB.GetQuestInstancesByUidAsync(uid);
        var byInstanceId = instances.ToDictionary(i => i.instanceId, i => i);

        // 사전 dedup batch 조회 — 한 round-trip으로 모든 eventClientId의 적용 여부 확인.
        var validEventIds = events
            .Where(e => e != null && !string.IsNullOrEmpty(e.EventClientId))
            .Select(e => e.EventClientId)
            .ToList();
        var alreadyApplied = await _gameDB.GetAppliedEventClientIdsAsync(uid, validEventIds);

        var newApplieds = new List<GameUserQuestEventApplied>();
        var seenEventIds = new HashSet<string>();
        var updatedSet = new HashSet<string>();
        int applied = 0;
        int duplicate = 0;

        foreach (var evt in events)
        {
            if (evt == null || string.IsNullOrEmpty(evt.EventClientId))
            {
                duplicate++;
                continue;
            }

            // 같은 배치 내 동일 eventClientId 중복 — 첫 건만 적용, 이후는 duplicate.
            if (!seenEventIds.Add(evt.EventClientId))
            {
                duplicate++;
                continue;
            }

            // 사전 dedup hit — 이미 적용됐던 이벤트.
            if (alreadyApplied.Contains(evt.EventClientId))
            {
                duplicate++;
                continue;
            }

            // 인스턴스 매칭 — 만료/Claimed/Expired면 skip.
            if (!byInstanceId.TryGetValue(evt.QuestInstanceId, out var inst))
            {
                duplicate++;
                continue;
            }
            if (inst.status != "InProgress")
            {
                duplicate++;
                continue;
            }
            if (evt.Delta <= 0)
            {
                duplicate++;
                continue;
            }

            // progress 누적 + 완료 판정 — child=0 단일 가정 (Composite는 클라 측 분기, 서버는 합산만).
            ApplyDelta(inst, evt.Delta);
            inst.lastUpdatedUtc = nowStr;
            updatedSet.Add(inst.instanceId);

            newApplieds.Add(new GameUserQuestEventApplied
            {
                uid = uid,
                eventClientId = evt.EventClientId,
                appliedAtUtc = nowStr,
            });
            applied++;
        }

        var updatedList = updatedSet.Select(id => byInstanceId[id]).ToList();

        // 단일 트랜잭션으로 dedup INSERT + 인스턴스 UPDATE를 묶음.
        // unique 가드 위반(race) 시 DbUpdateException — 전체 rollback. 호출자(Controller)는 ErrorCode 반환.
        if (newApplieds.Count > 0 || updatedList.Count > 0)
        {
            await _gameDB.ApplyQuestEventBatchAsync(newApplieds, updatedList);
        }

        return (applied, duplicate, updatedList);
    }

    /// progress JSON 갱신 + 완료 판정. child=0 슬롯에 delta 누적.
    /// Composite의 child별 진행은 본 서비스가 모름 — 향후 EventTypeName 별 라우팅으로 확장.
    private static void ApplyDelta(GameUserQuestInstance inst, int delta)
    {
        var entries = ParseSubProgress(inst.subProgressJson);
        var slot = entries.FirstOrDefault(e => e.ChildIndex == 0);
        if (slot == null)
        {
            slot = new PkSubProgressEntry { ChildIndex = 0, Progress = 0, Required = 1 };
            entries.Add(slot);
        }
        slot.Progress += delta;

        // 완료 판정 — 모든 child가 Required 도달.
        bool allDone = entries.All(e => e.Progress >= e.Required);
        if (allDone && inst.status == "InProgress")
        {
            inst.status = "Completed";
        }

        inst.subProgressJson = JsonSerializer.Serialize(entries);
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

    private static string FormatUtc(DateTime utc) => utc.ToString("yyyy-MM-dd HH:mm:ss");
}
