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

    public EquipmentStorageController(ILogger<EquipmentStorageController> logger, EquipmentStorageService storageService)
    {
        _logger = logger;
        _storageService = storageService;
    }

    /// 장비 보관함 칸 수 확장. 다이아 100당 +5칸.
    [HttpPost("ExpandCapacity")]
    public async Task<PkExpandCapacityResponse> ExpandCapacity([FromHeader] HeaderDTO header, [FromBody] PkExpandCapacityRequest request)
    {
        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;

        _logger.ZLogInformation($"[EquipmentStorage/ExpandCapacity] Uid:{uid}");

        return await _storageService.ExpandCapacityAsync(uid);
    }
}
