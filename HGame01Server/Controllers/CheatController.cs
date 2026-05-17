using HGame01Server.Models;
using HGame01Server.Repository;
using HGame01Server.Services;
using Microsoft.AspNetCore.Mvc;
using ZLogger;

namespace HGame01Server.Controllers;

/// 디버그 전용 — 도메인 비귀속 치트(재화 등) 진입점.
/// 도메인별 치트는 각 도메인 컨트롤러(Quest/SeasonPass/Mail)에 둔다.
[Route("api/[controller]")]
[ApiController]
public class CheatController : ControllerBase
{
    private readonly ILogger<CheatController> _logger;
    private readonly CurrencyService _currencyService;

    public CheatController(ILogger<CheatController> logger, CurrencyService currencyService)
    {
        _logger = logger;
        _currencyService = currencyService;
    }

    /// 단일 통화를 절대값으로 설정. 응답은 전체 재화 스냅샷(currency-contract).
    [HttpPost("SetCurrency")]
    public async Task<PkCheatSetCurrencyResponse> SetCurrency([FromHeader] HeaderDTO header, [FromBody] PkCheatSetCurrencyRequest request)
    {
        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;
        _logger.ZLogInformation($"[Cheat/SetCurrency] Uid:{uid} Type:{request.CurrencyType} Amount:{request.Amount}");

        var response = new PkCheatSetCurrencyResponse
        {
            Result = await _currencyService.SetAsync(uid, request.CurrencyType, request.Amount),
        };

        // 재화는 유저 전체 절대 스냅샷으로만 채운다 (currency-contract — 부분 목록 시 클라 미포함 통화 0 소실).
        await _currencyService.PopulateCurrenciesAsync(response, uid);
        return response;
    }
}
