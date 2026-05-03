using HGame01Server.Models;
using HGame01Server.Repository;
using HGame01Server.Services;
using Microsoft.AspNetCore.Mvc;
using ZLogger;

namespace HGame01Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class EquipmentStorageController : ControllerBase
{
    private readonly ILogger<EquipmentStorageController> _logger;
    private readonly EquipmentStorageService _storageService;
    private readonly CurrencyService _currencyService;

    public EquipmentStorageController(ILogger<EquipmentStorageController> logger, EquipmentStorageService storageService, CurrencyService currencyService)
    {
        _logger = logger;
        _storageService = storageService;
        _currencyService = currencyService;
    }

    /// 장비 보관함 칸 수 확장. 다이아 100당 +5칸.
    [HttpPost("ExpandCapacity")]
    public async Task<PkExpandCapacityResponse> ExpandCapacity([FromHeader] HeaderDTO header, [FromBody] PkExpandCapacityRequest request)
    {
        var response = new PkExpandCapacityResponse();

        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;

        _logger.ZLogInformation($"[EquipmentStorage/ExpandCapacity] Uid:{uid}");

        var (error, newCapacity) = await _storageService.ExpandCapacityAsync(uid);
        response.Result = error;
        response.NewCapacity = newCapacity;

        // 클라 store가 다이아 차감을 즉시 반영할 수 있도록 currencies 동봉.
        if (error == ErrorCode.None)
        {
            response.Currencies = await _currencyService.GetAllAsync(uid);
        }

        return response;
    }
}
