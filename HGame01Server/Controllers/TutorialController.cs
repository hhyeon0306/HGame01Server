using HGame01Server.Models;
using HGame01Server.Repository;
using HGame01Server.Services;
using Microsoft.AspNetCore.Mvc;
using ZLogger;

namespace HGame01Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TutorialController : ControllerBase
{
    private readonly ILogger<TutorialController> _logger;
    private readonly TutorialService _tutorialService;

    public TutorialController(ILogger<TutorialController> logger, TutorialService tutorialService)
    {
        _logger = logger;
        _tutorialService = tutorialService;
    }

    /// 튜토리얼 그룹 완료 push — 클라가 player.Play 성공 + cancellation 통과 후에만 호출.
    /// idempotent — 동일 태그 중복 호출 무시.
    [HttpPost("Complete")]
    public async Task<PkCompleteTutorialResponse> Complete([FromHeader] HeaderDTO header, [FromBody] PkCompleteTutorialRequest request)
    {
        var response = new PkCompleteTutorialResponse();
        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;

        response.Result = await _tutorialService.CompleteAsync(uid, request.TutorialTagName);
        return response;
    }

    /// 치트 전용 — 완료 튜토리얼 전체 클리어. 다음 trigger 시 재발동.
    /// 클라 cheat 명령 `tutreset`에서 호출.
    [HttpPost("CheatResetAll")]
    public async Task<PkCheatResetTutorialResponse> CheatResetAll([FromHeader] HeaderDTO header, [FromBody] PkCheatResetTutorialRequest request)
    {
        var response = new PkCheatResetTutorialResponse();
        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;

        response.Result = await _tutorialService.CheatResetAllAsync(uid);
        return response;
    }
}
