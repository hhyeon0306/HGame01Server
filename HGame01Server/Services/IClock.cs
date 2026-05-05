using System;

namespace HGame01Server.Services;

/// 시각 의존성 추상화. 단위 테스트에서 임의 UTC 주입 가능.
/// DateTime.UtcNow를 코드 곳곳에 직접 호출 X — 본 인터페이스 경유.
public interface IClock
{
    DateTime UtcNow { get; }
    double SecondsUntil(DateTime target);
}
