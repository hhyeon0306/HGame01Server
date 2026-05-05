using HGame01Server.Models;
using HGame01Server.Repository;
using HGame01Server.Services;
using Microsoft.AspNetCore.Mvc;
using ZLogger;

namespace HGame01Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class QuestController : ControllerBase
{
    private readonly ILogger<QuestController> _logger;
    private readonly QuestService _questService;
    private readonly QuestProgressService _questProgress;

    public QuestController(ILogger<QuestController> logger, QuestService questService, QuestProgressService questProgress)
    {
        _logger = logger;
        _questService = questService;
        _questProgress = questProgress;
    }

    /// 활성 quest 인스턴스 조회 — Hydrate / Reconcile.
    [HttpPost("Active")]
    public async Task<PkQuestActiveResponse> Active([FromHeader] HeaderDTO header)
    {
        var response = new PkQuestActiveResponse();
        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;
        _logger.ZLogInformation($"[Quest/Active] Uid:{uid}");

        response.Instances = await _questService.GetActiveAsync(uid);
        response.Result = ErrorCode.None;
        return response;
    }

    /// 클라 이벤트 배치 적용 — 멱등 dedup + progress 누적.
    [HttpPost("EventsBatch")]
    public async Task<PkQuestEventBatchResponse> EventsBatch([FromHeader] HeaderDTO header, [FromBody] PkQuestEventBatchRequest request)
    {
        var response = new PkQuestEventBatchResponse();
        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;
        _logger.ZLogInformation($"[Quest/EventsBatch] Uid:{uid} Events:{request.Events?.Count ?? 0}");

        var (applied, duplicate, updated) = await _questProgress.ApplyBatchAsync(uid, request.Events ?? new());
        response.Applied = applied;
        response.Duplicate = duplicate;
        response.UpdatedInstances = updated.Select(QuestService.ToDto).ToList();
        response.Result = ErrorCode.None;
        return response;
    }

    /// 보상 수령 — Completed → Claimed + currencies 응답.
    [HttpPost("Claim")]
    public async Task<PkQuestClaimResponse> Claim([FromHeader] HeaderDTO header, [FromBody] PkQuestClaimRequest request)
    {
        var response = new PkQuestClaimResponse { InstanceId = request.InstanceId };
        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;
        _logger.ZLogInformation($"[Quest/Claim] Uid:{uid} InstanceId:{request.InstanceId}");

        var (error, reward, currencies) = await _questService.ClaimAsync(uid, request.InstanceId);
        response.Result = error;
        response.Reward = reward;
        response.Currencies = currencies;
        return response;
    }

    /// 일일 슬롯 강제 재발급 — 자정 통과 시 클라가 호출.
    /// Daily 풀 questDataId / slotCount는 Phase 8 후속에서 GdbQuestPool 또는 GdbConst로 외부화.
    /// 본 라운드는 클라 요청 metadata로 임시 전달 또는 server-side fixed (TODO).
    [HttpPost("RefreshDaily")]
    public async Task<PkQuestRefreshResponse> RefreshDaily([FromHeader] HeaderDTO header, [FromBody] PkQuestRefreshRequest request)
    {
        var response = new PkQuestRefreshResponse();
        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;
        _logger.ZLogInformation($"[Quest/RefreshDaily] Uid:{uid}");

        // TODO: Daily 풀 GdbQuestPool 자동 동기화 후 본 endpoint에서 직접 조회.
        // 본 라운드는 빈 응답 — 클라가 Active로 fallback.
        response.Instances = new();
        response.Result = ErrorCode.None;
        return response;
    }
}
