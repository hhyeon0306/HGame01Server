using HGame01Server.Models;
using HGame01Server.Repository;
using HGame01Server.Services;
using Microsoft.AspNetCore.Mvc;
using ZLogger;

namespace HGame01Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MailController : ControllerBase
{
    private readonly ILogger<MailController> _logger;
    private readonly MailService _mailService;

    public MailController(ILogger<MailController> logger, MailService mailService)
    {
        _logger = logger;
        _mailService = mailService;
    }

    /// 우편함 목록 조회. 만료 메일은 응답에서 제외 + DB lazy 삭제.
    [HttpPost("List")]
    public async Task<PkMailListResponse> List([FromHeader] HeaderDTO header)
    {
        var response = new PkMailListResponse();

        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;

        _logger.ZLogInformation($"[Mail/List] Uid:{uid}");

        response.Mails = await _mailService.GetMailsAsync(uid);
        response.Result = ErrorCode.None;
        return response;
    }

    /// 단건 수령.
    [HttpPost("Claim")]
    public async Task<PkMailClaimResponse> Claim([FromHeader] HeaderDTO header, [FromBody] PkMailClaimRequest request)
    {
        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        _logger.ZLogInformation($"[Mail/Claim] Uid:{userInfo.UId}, MailId:{request.MailId}");
        return await _mailService.ClaimAsync(userInfo.UId, request.MailId);
    }

    /// 모두 받기. 1건이라도 실패하면 전체 롤백 (단일 트랜잭션).
    [HttpPost("ClaimAll")]
    public async Task<PkMailClaimAllResponse> ClaimAll([FromHeader] HeaderDTO header)
    {
        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        _logger.ZLogInformation($"[Mail/ClaimAll] Uid:{userInfo.UId}");
        return await _mailService.ClaimAllAsync(userInfo.UId);
    }

#if DEBUG
    /// 치트 전용. 자기 자신에게 메일 발송 — 우편함 UI 동작 검증용.
    [HttpPost("CheatSendMail")]
    public async Task<PkMailCheatSendResponse> CheatSendMail([FromHeader] HeaderDTO header, [FromBody] PkMailCheatSendRequest request)
    {
        var response = new PkMailCheatSendResponse();

        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;

        _logger.ZLogInformation($"[Mail/CheatSendMail] Uid:{uid}");

        var rewards = request.Rewards.Select(r => new MailRewardEntry
        {
            rewardTag = r.RewardTag,
            count = r.Count,
        }).ToList();

        // 0 또는 음수면 7일 기본값. 음수 (e.g., -1) 으로 만료 시뮬도 가능 — 그 경우는 expireAt 이 과거가 되므로 lazy eviction 즉시 대상.
        int expireMinutes = request.ExpireMinutes != 0 ? request.ExpireMinutes : (60 * 24 * 7);

        var mailId = await _mailService.SendAsync(
            uid: uid,
            titleKey: request.TitleKey,
            rewards: rewards,
            iconAtlas: request.IconAtlas,
            iconKey: request.IconKey,
            expireMinutes: expireMinutes,
            senderType: "System",
            mailKind: request.MailKind);

        response.Result = ErrorCode.None;
        response.MailId = mailId;
        return response;
    }
#endif
}
