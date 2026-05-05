using System;

namespace HGame01Server.Services;

/// 주기적 리셋 사이클 추상화. 도메인(Shop/Quest/Pass) 무관 시각 계산만 담당.
public interface IResetSchedule
{
    /// 현재 사이클 시작 시각 (UTC).
    DateTime Current { get; }

    /// 다음 리셋 시각 (UTC).
    DateTime Next { get; }

    /// t가 현재 사이클 이전(과거)인가.
    bool HasPassed(DateTime t);

    /// 현재로부터 다음 리셋까지 남은 초.
    double SecondsUntilNext { get; }
}
