using HGame01Server.Models;
using HGame01Server.Repository;

namespace HGame01Server.Services;

/// 클라 → 서버 이벤트 멱등 처리. (uid, eventClientId) unique 가드 (Architecture §10.3).
/// 7일 이내 dedup 보장. 7일 이전 entry는 GC. 클라 재전송/장애 차단.
public class QuestEventDeduplicator
{
    private readonly IGameDB _gameDB;
    private readonly IClock _clock;

    public QuestEventDeduplicator(IGameDB gameDB, IClock clock)
    {
        _gameDB = gameDB;
        _clock = clock;
    }

    /// 이미 적용된 이벤트인가. true면 호출자가 duplicate 카운트 + dispatch skip.
    public async Task<bool> IsAlreadyAppliedAsync(long uid, string eventClientId)
    {
        if (string.IsNullOrEmpty(eventClientId))
        {
            return false;
        }
        var applied = await _gameDB.GetQuestEventAppliedAsync(uid, eventClientId);
        return applied != null;
    }

    /// 이벤트 적용 기록. unique 가드 위반 시 false (다른 동시 요청이 먼저 처리).
    public async Task<bool> RecordAppliedAsync(long uid, string eventClientId)
    {
        try
        {
            await _gameDB.RecordQuestEventAppliedAsync(new GameUserQuestEventApplied
            {
                uid = uid,
                eventClientId = eventClientId,
                appliedAtUtc = FormatUtc(_clock.UtcNow),
            });
            return true;
        }
        catch
        {
            // unique 가드 위반 — 다른 동시 요청이 먼저 적용. duplicate로 처리.
            return false;
        }
    }

    /// 7일 이전 dedup entry GC. QuestSeasonScheduler가 1일 1회 호출.
    public async Task<int> GcAsync()
    {
        var since = _clock.UtcNow.AddDays(-7);
        return await _gameDB.GcQuestEventAppliedAsync(FormatUtc(since));
    }

    private static string FormatUtc(DateTime utc) => utc.ToString("yyyy-MM-dd HH:mm:ss");
}
