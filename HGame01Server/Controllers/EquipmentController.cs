using HGame01Server.Models;
using HGame01Server.Repository;
using HGame01Server.Services;
using Microsoft.AspNetCore.Mvc;
using ZLogger;

namespace HGame01Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class EquipmentController : ControllerBase
{
    private readonly ILogger<EquipmentController> _logger;
    private readonly EquipmentService _equipmentService;

    public EquipmentController(ILogger<EquipmentController> logger, EquipmentService equipmentService)
    {
        _logger = logger;
        _equipmentService = equipmentService;
    }

    /// 장비 장착.
    [HttpPost("Equip")]
    public async Task<PkEquipResponse> Equip([FromHeader] HeaderDTO header, [FromBody] PkEquipRequest request)
    {
        var response = new PkEquipResponse();

        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;

        _logger.ZLogInformation($"[Equipment/Equip] Uid:{uid}, EquipmentDbId:{request.EquipmentDbId}, CharacterId:{request.CharacterId}");

        var (error, equipments) = await _equipmentService.EquipAsync(uid, request.EquipmentDbId, request.CharacterId);
        response.Result = error;
        response.Equipments = equipments;
        return response;
    }

    /// 장비 해제.
    [HttpPost("Unequip")]
    public async Task<PkUnequipResponse> Unequip([FromHeader] HeaderDTO header, [FromBody] PkUnequipRequest request)
    {
        var response = new PkUnequipResponse();

        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;

        _logger.ZLogInformation($"[Equipment/Unequip] Uid:{uid}, EquipmentDbId:{request.EquipmentDbId}");

        var (error, equipments) = await _equipmentService.UnequipAsync(uid, request.EquipmentDbId);
        response.Result = error;
        response.Equipments = equipments;
        return response;
    }
}
