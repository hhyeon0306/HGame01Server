using System;

namespace HGame01Server.Services;

/// 일일 리셋 — UTC 기준 hourUtc시. lazy 계산.
/// "현재 시각 기준 가장 최근 hourUtc 시점"이 Current.
public sealed class DailyResetSchedule : IResetSchedule
{
    private readonly IClock _clock;
    private readonly int _hourUtc;

    public DailyResetSchedule(IClock clock, int hourUtc)
    {
        _clock = clock;
        _hourUtc = NormalizeHour(hourUtc);
    }

    public DateTime Current
    {
        get
        {
            var now = _clock.UtcNow;
            var resetToday = now.Date.AddHours(_hourUtc);
            return now >= resetToday ? resetToday : resetToday.AddDays(-1);
        }
    }

    public DateTime Next => Current.AddDays(1);

    public bool HasPassed(DateTime t) => t < Current;

    public double SecondsUntilNext => _clock.SecondsUntil(Next);

    private static int NormalizeHour(int hour)
    {
        if (hour < 0 || hour > 23)
        {
            throw new ArgumentOutOfRangeException(nameof(hour), $"hour={hour} 유효 범위 0~23");
        }
        return hour;
    }
}
