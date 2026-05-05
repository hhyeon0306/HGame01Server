using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using HGame01Server.Models;
using HGame01Server.Models.GameData;
using HGame01Server.Repository;

namespace HGame01Server.Services;

/// 퀘스트 진행 누적 서비스 (Phase 8). 이벤트 → 인스턴스 progress/status 권위 갱신.
/// 멱등은 QuestEventDeduplicator 책임. 본 서비스는 적용/완료 판정.
public class QuestProgressService
{
    private readonly IGameDB _gameDB;
    private readonly IClock _clock;
    private readonly QuestEventDeduplicator _dedup;

    public QuestProgressService(IGameDB gameDB, IClock clock, QuestEventDeduplicator dedup)
    {
        _gameDB = gameDB;
        _clock = clock;
        _dedup = dedup;
    }

    /// 클라 이벤트 배치 적용. 적용/중복 카운트 + 갱신된 인스턴스 목록 반환.
    public async Task<(int applied, int duplicate, List<GameUserQuestInstance> updated)>
        ApplyBatchAsync(long uid, List<PkQuestEventEntry> events)
    {
        if (events == null || events.Count == 0)
        {
            return (0, 0, new List<GameUserQuestInstance>());
        }

        // 만료 인스턴스 lazy 정리 — Active 응답이 사용자에게 stale 안 보이도록.
        var nowStr = FormatUtc(_clock.UtcNow);
        await _gameDB.ExpireQuestInstancesAsync(uid, nowStr);

        var instances = await _gameDB.GetQuestInstancesByUidAsync(uid);
        var byInstanceId = instances.ToDictionary(i => i.instanceId, i => i);
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

            // dedup — 이미 적용됐으면 skip (멱등).
            if (await _dedup.IsAlreadyAppliedAsync(uid, evt.EventClientId))
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

            // 적용 기록 시도 — 동시성 가드. 실패면 다른 요청이 먼저 적용한 중복.
            var recorded = await _dedup.RecordAppliedAsync(uid, evt.EventClientId);
            if (!recorded)
            {
                duplicate++;
                continue;
            }

            // progress 누적 — child=0 단일 가정 (Composite는 클라 측 분기, 서버는 합산만).
            ApplyDelta(inst, evt.Delta);
            inst.lastUpdatedUtc = nowStr;
            updatedSet.Add(inst.instanceId);
            applied++;
        }

        // 갱신된 인스턴스만 batch update.
        var updatedList = updatedSet
            .Select(id => byInstanceId[id])
            .ToList();
        if (updatedList.Count > 0)
        {
            await _gameDB.UpdateQuestInstancesBatchAsync(updatedList);
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
