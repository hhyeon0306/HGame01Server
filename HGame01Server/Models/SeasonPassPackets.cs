using System.Collections.Generic;

namespace HGame01Server.Models;

// ============================================================
// SeasonPass 패킷 (서버 — 클라 SeasonPassPackets와 1:1 대응)
// 클라 응답 필드명은 camelCase (Unity 측 [Serializable] 직렬화 호환).
// ASP.NET Core 기본 PropertyNamingPolicy.CamelCase 또는 [JsonPropertyName] 둘 다 동작 — 본 패킷은 PascalCase 필드명 그대로 직렬화 후
// 클라 Newtonsoft DefaultContractResolver가 ignore case로 매핑하므로 호환.
// ============================================================

public class PkSeasonPassState
{
    public string seasonId { get; set; } = "";
    public int currentExp { get; set; }
    public bool isPremium { get; set; }
    public List<int> claimedBasic { get; set; } = new();
    public List<int> claimedPremium { get; set; } = new();
    public string seasonEndUtc { get; set; } = "";
}


// POST api/SeasonPass/State
public class PkSeasonPassStateRequest { }

public class PkSeasonPassStateResponse
{
    public ErrorCode result { get; set; }
    public PkSeasonPassState? state { get; set; }
}


// POST api/SeasonPass/ApplyStageResult
public class PkSeasonPassApplyStageResultRequest
{
    public string stageRunId { get; set; } = "";
    public string stageResult { get; set; } = "";  // "Victory" / "Defeat"
}

public class PkSeasonPassApplyStageResultResponse
{
    public ErrorCode result { get; set; }
    public int expGained { get; set; }
    public PkSeasonPassState? state { get; set; }
}


// POST api/SeasonPass/PurchasePremium
public class PkSeasonPassPurchasePremiumRequest
{
    public string productId { get; set; } = "";
    public string receipt { get; set; } = "";
}

public class PkSeasonPassPurchasePremiumResponse
{
    public ErrorCode result { get; set; }
    public PkSeasonPassState? state { get; set; }
}


// POST api/SeasonPass/Claim
public class PkSeasonPassClaimRequest { }

public class PkSeasonPassClaimResponse
{
    public ErrorCode result { get; set; }
    public List<int> grantedBasicLevels { get; set; } = new();
    public List<int> grantedPremiumLevels { get; set; } = new();
    public List<PkCurrency> currencies { get; set; } = new();
    public PkSeasonPassState? state { get; set; }
}
