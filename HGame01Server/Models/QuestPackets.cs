using System.Collections.Generic;

namespace HGame01Server.Models;

// ============================================================
// Quest 시스템 패킷 (Phase 8)
// 클라 Architecture.md §10 서버 프로토콜 준수.
// ============================================================

// POST api/Quest/Active
public class PkQuestActiveRequest { }

public class PkQuestActiveResponse
{
    public ErrorCode Result { get; set; }
    public List<PkQuestInstanceDto> Instances { get; set; } = new();
}

// POST api/Quest/EventsBatch
public class PkQuestEventBatchRequest
{
    public List<PkQuestEventEntry> Events { get; set; } = new();
}

public class PkQuestEventBatchResponse
{
    public ErrorCode Result { get; set; }

    /// 멱등 적용 결과 — 신규 적용된 entry 수.
    public int Applied { get; set; }

    /// 중복(이미 처리된 eventClientId) entry 수.
    public int Duplicate { get; set; }

    /// 갱신된 인스턴스(progress/status 변동) 권위값.
    public List<PkQuestInstanceDto> UpdatedInstances { get; set; } = new();
}

// POST api/Quest/Claim
public class PkQuestClaimRequest
{
    public string InstanceId { get; set; } = "";
}

public class PkQuestClaimResponse
{
    public ErrorCode Result { get; set; }
    public string InstanceId { get; set; } = "";

    /// 보상 결과 — 클라가 RewardContext.Reward로 전달해 RewardSequenceRunner.RunAsync.
    public PkRewardResult? Reward { get; set; }

    /// 갱신된 currencies absolute. CurrencyApplyStep이 UserCurrencyStore.UpdateFromServer에 적용.
    public List<PkCurrency> Currencies { get; set; } = new();
}

// POST api/Quest/RefreshDaily
public class PkQuestRefreshRequest
{
    /// 클라 EGameplayTag.QuestContainer_Daily의 stableId. 서버는 이 컨테이너의 기존 슬롯을 만료 후 신규 발급.
    /// 클라 권위 — 자기 컨테이너 enum stableId만 보내는 거라 치팅 영향 없음 (서버는 questTag prefix로 풀 자동 필터).
    public int ContainerStableId { get; set; }
}

public class PkQuestRefreshResponse
{
    public ErrorCode Result { get; set; }
    public List<PkQuestInstanceDto> Instances { get; set; } = new();
}

// ============================================================
// 보조 DTO
// ============================================================

/// 서버 → 클라 인스턴스 권위 모델. Architecture §10.2 Phase 8 full.
/// quest_tag stableId는 GdbQuestData 자동 동기화에 미포함이라 DTO에서 제거 (Phase 9 condition 다형 직렬화 도입 시 재검토).
public class PkQuestInstanceDto
{
    public string InstanceId { get; set; } = "";
    public int QuestDataId { get; set; }

    public int ContainerStableId { get; set; }

    public List<PkSubProgressEntry> SubProgress { get; set; } = new();

    /// "InProgress" / "Completed" / "Claimed" / "Expired" — 클라 EQuestStatus enum 문자열.
    public string Status { get; set; } = "InProgress";

    public string IssuedAtUtc { get; set; } = "";
    public string ExpiresAtUtc { get; set; } = "";
}

public class PkSubProgressEntry
{
    public int ChildIndex { get; set; }
    public int Progress { get; set; }
    public int Required { get; set; }
}

/// 클라 → 서버 1 이벤트 entry. (uid, eventClientId) unique 가드.
public class PkQuestEventEntry
{
    /// GUID v4 — QuestEventBuffer가 (Quest, Producer event) 쌍당 1개 부여 ([결정 #17]).
    public string EventClientId { get; set; } = "";

    public string QuestInstanceId { get; set; } = "";

    /// 클라 typeof(TEvent).Name — 서버 화이트리스트 검증.
    public string EventTypeName { get; set; } = "";

    /// 클라 Condition.Delta 결과. 서버는 자체 검증으로 어뷰징 방지.
    public int Delta { get; set; }

    public string OccurredAtUtc { get; set; } = "";

    /// 이벤트 메타데이터 (예: monsterTagStableId, isBoss). 서버 dedup/검증용.
    public Dictionary<string, string> Metadata { get; set; } = new();
}
