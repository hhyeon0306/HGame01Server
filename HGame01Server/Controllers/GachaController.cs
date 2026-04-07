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
    private readonly CurrencyService _currencyService;

    public GachaController(ILogger<GachaController> logger, GachaService gachaService, CurrencyService currencyService)
    {
        _logger = logger;
        _gachaService = gachaService;
        _currencyService = currencyService;
    }

    /// 뽑기 실행.
    [HttpPost("Pull")]
    public async Task<PkGachaPullResponse> Pull([FromHeader] HeaderDTO header, [FromBody] PkGachaPullRequest request)
    {
        var response = new PkGachaPullResponse();

        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;

        _logger.ZLogInformation($"[Gacha/Pull] Uid:{uid}, PullCount:{request.PullCount}, UseTicket:{request.UseTicket}");

        var (error, items) = await _gachaService.PullAsync(uid, request.PullCount, request.UseTicket);
        if (error != ErrorCode.None)
        {
            response.Result = error;
            return response;
        }

        response.Items = items;
        response.Currencies = await _currencyService.GetAllAsync(uid);
        response.Result = ErrorCode.None;
        return response;
    }
}
