using HGame01Server.Models;
using HGame01Server.Repository;
using HGame01Server.Services;
using Microsoft.AspNetCore.Mvc;
using ZLogger;

namespace HGame01Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ShopController : ControllerBase
{
    private readonly ILogger<ShopController> _logger;
    private readonly ShopService _shopService;
    private readonly CurrencyService _currencyService;

    public ShopController(ILogger<ShopController> logger, ShopService shopService, CurrencyService currencyService)
    {
        _logger = logger;
        _shopService = shopService;
        _currencyService = currencyService;
    }

    /// 일일 상점 아이템 목록 조회.
    [HttpPost("DailyList")]
    public async Task<PkShopListResponse> DailyList([FromHeader] HeaderDTO header)
    {
        var response = new PkShopListResponse();

        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;

        _logger.ZLogInformation($"[Shop/DailyList] Uid:{uid}");

        response.Items = await _shopService.GetDailyItemsAsync(uid);
        response.Result = ErrorCode.None;
        return response;
    }

    /// 주간 상점 아이템 목록 조회.
    [HttpPost("WeeklyList")]
    public async Task<PkShopListResponse> WeeklyList([FromHeader] HeaderDTO header)
    {
        var response = new PkShopListResponse();

        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;

        _logger.ZLogInformation($"[Shop/WeeklyList] Uid:{uid}");

        response.Items = await _shopService.GetWeeklyItemsAsync(uid);
        response.Result = ErrorCode.None;
        return response;
    }

    /// 상점 아이템 구매.
    [HttpPost("Buy")]
    public async Task<PkShopBuyResponse> Buy([FromHeader] HeaderDTO header, [FromBody] PkShopBuyRequest request)
    {
        var response = new PkShopBuyResponse();

        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;

        _logger.ZLogInformation($"[Shop/Buy] Uid:{uid}, ShopItemId:{request.ShopItemId}");

        var (error, reward) = await _shopService.BuyItemAsync(uid, request.ShopItemId);
        if (error != ErrorCode.None)
        {
            response.Result = error;
            return response;
        }

        response.Reward = reward;
        response.Currencies = await _currencyService.GetAllAsync(uid);
        response.Result = ErrorCode.None;
        return response;
    }

#if DEBUG
    /// 치트 전용. 오늘 자 일일 구매 기록 삭제 — DEBUG 빌드에서만 노출.
    [HttpPost("CheatResetDaily")]
    public async Task<PkCheatResetDailyResponse> CheatResetDaily([FromHeader] HeaderDTO header, [FromBody] PkCheatResetDailyRequest request)
    {
        var response = new PkCheatResetDailyResponse();

        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;

        _logger.ZLogInformation($"[Shop/CheatResetDaily] Uid:{uid}");

        int deleted = await _shopService.CheatResetDailyAsync(uid);
        response.Result = ErrorCode.None;
        response.DeletedCount = deleted;
        return response;
    }
#endif

    /// 다이아몬드 구매 (인앱 결제).
    [HttpPost("BuyDiamond")]
    public async Task<PkBuyDiamondResponse> BuyDiamond([FromHeader] HeaderDTO header, [FromBody] PkBuyDiamondRequest request)
    {
        var response = new PkBuyDiamondResponse();

        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;

        _logger.ZLogInformation($"[Shop/BuyDiamond] Uid:{uid}, ShopItemTag:{request.ShopItemTag}");

        var (error, diamondAmount) = await _shopService.BuyDiamondAsync(uid, request.ShopItemTag);
        response.Result = error;
        response.DiamondAmount = diamondAmount;
        return response;
    }
}
