namespace HGame01Server.Models.GameData;

/// Quest 도메인 서버 상수 — GdbConst.cs 자동 덮어쓰기 회피용 별도 파일.
/// 클라 EGameplayTag enum 값과 동기 필요. 클라 enum 변경 시 본 파일도 갱신.
public static class QuestServerConstants
{
    /// 클라 EGameplayTag.QuestContainer_Daily의 stableId.
    public const int QuestContainerDailyStableId = 1017135686;

    /// 일일 슬롯 수 — RefreshDaily 호출 시 풀에서 발급할 인스턴스 개수.
    public const int DailySlotCount = 4;
}
