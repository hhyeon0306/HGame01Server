using HGame01Server.Models;
using HGame01Server.Repository;
using HGame01Server.Services;
using Microsoft.AspNetCore.Mvc;
using ZLogger;

namespace HGame01Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SeasonPassController : ControllerBase
{
    private readonly ILogger<SeasonPassController> _logger;
    private readonly SeasonPassService _seasonPassService;

    public SeasonPassController(ILogger<SeasonPassController> logger, SeasonPassService seasonPassService)
    {
        _logger = logger;
        _seasonPassService = seasonPassService;
    }

    /// 현재 시즌 상태 조회 (부팅·팝업 진입 시).
    [HttpPost("State")]
    public async Task<PkSeasonPassStateResponse> State([FromHeader] HeaderDTO header)
    {
        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;
        _logger.ZLogInformation($"[SeasonPass/State] Uid:{uid}");

        return await _seasonPassService.GetStateAsync(uid);
    }

    /// 스테이지 결과 적립. stageRunId(GUID v4) 멱등 키 — 재시도 시 누적 X.
    [HttpPost("ApplyStageResult")]
    public async Task<PkSeasonPassApplyStageResultResponse> ApplyStageResult([FromHeader] HeaderDTO header, [FromBody] PkSeasonPassApplyStageResultRequest request)
    {
        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;
        _logger.ZLogInformation($"[SeasonPass/ApplyStageResult] Uid:{uid} stageRunId:{request.stageRunId} result:{request.stageResult}");

        return await _seasonPassService.ApplyStageResultAsync(uid, request.stageRunId ?? "", request.stageResult ?? "");
    }

    /// 프리미엄 패스 결제 완료 통보. 포폴 단계 영수증 검증 생략.
    [HttpPost("PurchasePremium")]
    public async Task<PkSeasonPassPurchasePremiumResponse> PurchasePremium([FromHeader] HeaderDTO header, [FromBody] PkSeasonPassPurchasePremiumRequest request)
    {
        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;
        _logger.ZLogInformation($"[SeasonPass/PurchasePremium] Uid:{uid} productId:{request.productId}");

        return await _seasonPassService.PurchasePremiumAsync(uid, request.productId ?? "", request.receipt ?? "");
    }

    /// 수령 가능 보상 일괄 지급. 단건 API 없음.
    [HttpPost("Claim")]
    public async Task<PkSeasonPassClaimResponse> Claim([FromHeader] HeaderDTO header)
    {
        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;
        _logger.ZLogInformation($"[SeasonPass/Claim] Uid:{uid}");

        return await _seasonPassService.ClaimAvailableAsync(uid);
    }

    /// 디버그 전용 — 패스 상태 강제 변이. op="levelup"|"max"|"reset".
    [HttpPost("Cheat")]
    public async Task<PkSeasonPassCheatResponse> Cheat([FromHeader] HeaderDTO header, [FromBody] PkSeasonPassCheatRequest request)
    {
        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;
        _logger.ZLogInformation($"[SeasonPass/Cheat] Uid:{uid} op:{request.op}");

        return await _seasonPassService.CheatAsync(uid, request.op ?? "");
    }
}
