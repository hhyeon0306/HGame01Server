using HGame01Server.Models;
using HGame01Server.Repository;
using HGame01Server.Services;
using Microsoft.AspNetCore.Mvc;
using ZLogger;

namespace HGame01Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class GachaController : ControllerBase
{
    private readonly ILogger<GachaController> _logger;
    private readonly GachaService _gachaService;

    public GachaController(ILogger<GachaController> logger, GachaService gachaService)
    {
        _logger = logger;
        _gachaService = gachaService;
    }

    /// 뽑기 실행.
    [HttpPost("Pull")]
    public async Task<PkGachaPullResponse> Pull([FromHeader] HeaderDTO header, [FromBody] PkGachaPullRequest request)
    {
        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;

        _logger.ZLogInformation($"[Gacha/Pull] Uid:{uid}, PullCount:{request.PullCount}");

        return await _gachaService.PullAsync(uid, request.PullCount);
    }
}
