using System;

namespace HGame01Server.Services;

/// IClock 실 구현. DateTime.UtcNow 호출 격리.
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;

    public double SecondsUntil(DateTime target) => (target - UtcNow).TotalSeconds;
}
