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
public class PkQuestRefreshRequest { }

public class PkQuestRefreshResponse
{
    public ErrorCode Result { get; set; }
    public List<PkQuestInstanceDto> Instances { get; set; } = new();
}

// ============================================================
// 보조 DTO
// ============================================================

/// 서버 → 클라 인스턴스 권위 모델. Architecture §10.2 Phase 8 full.
public class PkQuestInstanceDto
{
    public string InstanceId { get; set; } = "";
    public int QuestDataId { get; set; }

    /// 클라 GameplayTag.stableId — Tag 인덱스 매칭용.
    public int QuestTagStableId { get; set; }
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
