using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HGame01Server.Models;

// ============================================================
// Quest 시스템 DB 모델 (Phase 8)
// 인스턴스는 row 1개 = 1 슬롯. 멱등 dedup 테이블 별도.
// ============================================================

[Table("user_quest_instances")]
public class GameUserQuestInstance
{
    /// 클라 GUID v4 — Hydrate/Reconcile 시 클라가 사용. PK.
    [Key]
    public string instanceId { get; set; } = "";

    public long uid { get; set; }

    /// GdbQuestData.id 매칭 — DataTable.GetById<QuestData>(questDataId).
    public int questDataId { get; set; }

    /// 컨테이너 식별 — EGameplayTag.stableId (예: QuestContainer_Daily).
    public int containerStableId { get; set; }

    /// JSON [{"childIndex":0,"progress":7,"required":10}, ...]. SubProgress의 직렬화.
    public string subProgressJson { get; set; } = "[]";

    /// "InProgress" / "Completed" / "Claimed" / "Expired"
    public string status { get; set; } = "InProgress";

    /// 모두 yyyy-MM-dd HH:mm:ss UTC.
    public string issuedAtUtc { get; set; } = "";
    public string expiresAtUtc { get; set; } = "";
    public string lastUpdatedUtc { get; set; } = "";
}

/// 멱등 dedup. (uid, questInstanceId, eventClientId) unique. 7일 GC.
/// 설계 의도(Architecture §10.3 "Unique 키: (userId, questInstanceId, eventClientId)")에 정합.
[Table("user_quest_events_applied")]
public class GameUserQuestEventApplied
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long id { get; set; }

    public long uid { get; set; }

    /// GameUserQuestInstance.instanceId 매칭 — dedup 키 일부.
    [MaxLength(64)]
    public string questInstanceId { get; set; } = "";

    /// GUID v4. (uid, questInstanceId, eventClientId) unique 인덱스 — Migration에 명시.
    [MaxLength(64)]
    public string eventClientId { get; set; } = "";

    /// 클라 typeof(TEvent).Name — 감사/추적 + 향후 라우팅 확장용.
    [MaxLength(64)]
    public string eventTypeName { get; set; } = "";

    public string appliedAtUtc { get; set; } = "";
}
