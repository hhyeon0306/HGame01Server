using Microsoft.Extensions.Hosting;
using ZLogger;

namespace HGame01Server.Services;

/// 백그라운드 cron — 1시간 주기 quest 시스템 housekeeping.
/// (1) 시즌 종료 통과 인스턴스 만료 처리 + 미수령 보상 우편 발송 (P9 PassQuestContainer 도입 시 활성).
/// (2) 7일 이전 dedup entry GC.
/// 본 라운드는 dedup GC만 활성. 시즌 우편 발송은 P9 진입 시 추가.
public class QuestSeasonScheduler : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromHours(1);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QuestSeasonScheduler> _logger;

    public QuestSeasonScheduler(IServiceProvider serviceProvider, ILogger<QuestSeasonScheduler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.ZLogInformation($"[QuestSeasonScheduler] 시작");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception e)
            {
                _logger.ZLogError(e, $"[QuestSeasonScheduler] tick 실패: {e.Message}");
            }
            try
            {
                await Task.Delay(TickInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
        _logger.ZLogInformation($"[QuestSeasonScheduler] 종료");
    }

    private async Task TickAsync(CancellationToken ct)
    {
        // BackgroundService는 Singleton — Scoped 서비스(QuestEventDeduplicator)는 scope 안에서 resolve.
        using var scope = _serviceProvider.CreateScope();
        var dedup = scope.ServiceProvider.GetRequiredService<QuestEventDeduplicator>();
        int gced = await dedup.GcAsync();
        if (gced > 0)
        {
            _logger.ZLogInformation($"[QuestSeasonScheduler] dedup GC: {gced}건 삭제");
        }

        // P9 시즌 종료 우편 발송은 PassQuestContainer 도입 후 추가 — Architecture §10.6.
    }
}
