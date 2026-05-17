using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HGame01Server.Models;

// ============================================================
// SeasonPass 시스템 DB 모델
// 유저당 시즌마다 row 1개 (복합 PK: uid + seasonId).
// 멱등 키 stageRunId는 별도 dedup 테이블로 보관 — Quest eventClientId 패턴 차용.
// ============================================================

/// 유저 × 시즌 단위 상태. 시즌 롤오버 시 새 row 발급 (과거 시즌 row 보존 — 정산/통계 용도).
[Table("user_season_pass")]
public class GameUserSeasonPass
{
    public long uid { get; set; }

    /// GameplayTag.name — 예: "Tag.Pass.Season1".
    [MaxLength(64)]
    public string seasonId { get; set; } = "";

    public int currentExp { get; set; }

    public bool isPremium { get; set; }

    /// JSON int[] — 수령한 Basic 보상 레벨.
    public string claimedBasicJson { get; set; } = "[]";

    /// JSON int[] — 수령한 Premium 보상 레벨.
    public string claimedPremiumJson { get; set; } = "[]";

    /// 만료 정산 완료 여부 — 미수령 보상 우편 발송을 1회만(멱등). passreset 시 false로 리셋.
    public bool isSettled { get; set; }

    /// 디버그 전용 — cheat(passend)로 이 유저만 강제 만료. 시즌 종료시각은 게임데이터(GdbSeasonPassData.end_utc) 단일 출처라
    /// 유저별 시각 컬럼을 두지 않고, 강제 만료 의도만 bool로 표현. passreset 시 false.
    public bool cheatForceEnded { get; set; }

    public string updatedAtUtc { get; set; } = "";
}


/// 스테이지 결과 보고 멱등 dedup. (uid, stageRunId) unique.
/// Quest eventClientId 패턴 동일 — race / 재시도 / 중복 보고 차단.
[Table("user_season_pass_stage_runs")]
public class GameUserSeasonPassStageRun
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long id { get; set; }

    public long uid { get; set; }

    /// 클라 GUID v4 — (uid, stageRunId) unique 인덱스로 멱등 보장.
    [MaxLength(64)]
    public string stageRunId { get; set; } = "";

    /// 적용된 시즌 — 시즌 롤오버 직전 보고된 stage가 어느 시즌으로 적립됐는지 추적.
    [MaxLength(64)]
    public string seasonId { get; set; } = "";

    /// "Victory" / "Defeat" — 감사 / 통계용.
    [MaxLength(16)]
    public string stageResult { get; set; } = "";

    public int expGained { get; set; }

    public string appliedAtUtc { get; set; } = "";
}
