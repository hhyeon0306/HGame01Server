using HGame01Server.Models;
using HGame01Server.Repository;

namespace HGame01Server.Services;

/// 클라 → 서버 이벤트 멱등 dedup의 housekeeping. (uid, eventClientId) unique 가드는 DB index 책임.
/// 이벤트 적용 + dedup 기록은 QuestProgressService.ApplyBatchAsync가 단일 트랜잭션으로 처리.
/// 본 서비스는 7일 이전 dedup entry GC만 담당 — QuestSeasonScheduler가 1시간 주기 호출.
public class QuestEventDeduplicator
{
    private readonly IGameDB _gameDB;
    private readonly IClock _clock;

    public QuestEventDeduplicator(IGameDB gameDB, IClock clock)
    {
        _gameDB = gameDB;
        _clock = clock;
    }

    /// 7일 이전 dedup entry GC. QuestSeasonScheduler가 1일 1회 호출.
    public async Task<int> GcAsync()
    {
        var since = _clock.UtcNow.AddDays(-7);
        return await _gameDB.GcQuestEventAppliedAsync(FormatUtc(since));
    }

    private static string FormatUtc(DateTime utc) => utc.ToString("yyyy-MM-dd HH:mm:ss");
}
