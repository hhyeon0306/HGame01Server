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

        _logger.ZLogInformation($"[Equipment/Equip] Uid:{uid}, EquipmentDbId:{request.EquipmentDbId}, CharacterTag:{request.CharacterTag}");

        var (error, equipments) = await _equipmentService.EquipAsync(uid, request.EquipmentDbId, request.CharacterTag);
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

    /// 자동 장착(다건 일괄). 클라가 슬롯별 최강 1개씩 결정해 dbIds로 전달.
    /// 슬롯 중복 거절 + 트랜잭션 원자성. 응답에 equipments 전체 + equippedCount.
    [HttpPost("EquipBatch")]
    public async Task<PkAutoEquipResponse> EquipBatch([FromHeader] HeaderDTO header, [FromBody] PkAutoEquipRequest request)
    {
        var response = new PkAutoEquipResponse();

        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;

        int requestCount = request.EquipmentDbIds?.Count ?? 0;
        _logger.ZLogInformation($"[Equipment/EquipBatch] Uid:{uid}, Count:{requestCount}, CharacterTag:{request.CharacterTag}");

        var (error, equipments, equippedCount) = await _equipmentService.AutoEquipAsync(uid, request.EquipmentDbIds ?? new(), request.CharacterTag);
        response.Result = error;
        response.Equipments = equipments;
        response.EquippedCount = equippedCount;
        return response;
    }

    /// 장비 다건 판매. 응답에 equipments + currencies + soldGold + soldCount.
    [HttpPost("Sell")]
    public async Task<PkSellResponse> Sell([FromHeader] HeaderDTO header, [FromBody] PkSellRequest request)
    {
        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;

        int requestCount = request.EquipmentDbIds?.Count ?? 0;
        _logger.ZLogInformation($"[Equipment/Sell] Uid:{uid}, Count:{requestCount}");

        return await _equipmentService.SellAsync(uid, request.EquipmentDbIds ?? new());
    }
}
