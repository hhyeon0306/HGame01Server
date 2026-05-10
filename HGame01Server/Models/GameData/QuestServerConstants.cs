namespace HGame01Server.Models.GameData;

/// Quest 도메인 서버 상수 — GdbConst.cs 자동 덮어쓰기 회피용 별도 파일.
/// 클라 EGameplayTag enum 값과 동기 필요. 클라 enum 변경 시 본 파일도 갱신.
public static class QuestServerConstants
{
    /// 클라 EGameplayTag.QuestContainer_Daily의 stableId.
    public const int QuestContainerDailyStableId = 1017135686;

    /// 일일 슬롯 수 — RefreshDaily 호출 시 풀에서 발급할 인스턴스 개수.
    public const int DailySlotCount = 4;

    /// 일일 종합 보상 활성 임계 — 활성 Daily 슬롯 중 Claimed 카운트 ≥ 본 값일 때 종합 보상 수령 가능.
    /// 향후 GdbConst.Quest.RequiredCompletedCount 데이터 기반 전환 시 본 상수 제거.
    public const int DailyBundleRequiredClaimedCount = 4;

    /// 일일 종합 보상 — Diamond 누적량 (라운드 D 1차: hardcoded).
    /// 향후 GdbConst의 dailyMissionRewards 풀 List 직렬화 지원 시 데이터 기반 전환.
    public const long DailyBundleRewardDiamondAmount = 450;
}
