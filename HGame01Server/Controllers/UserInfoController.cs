using HGame01Server.Models;
using HGame01Server.Repository;
using HGame01Server.Services;
using Microsoft.AspNetCore.Mvc;
using ZLogger;

namespace HGame01Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UserInfoController : ControllerBase
{
    private readonly ILogger<UserInfoController> _logger;
    private readonly CharacterService _characterService;
    private readonly CurrencyService _currencyService;
    private readonly EquipmentService _equipmentService;
    private readonly EquipmentStorageService _storageService;

    public UserInfoController(ILogger<UserInfoController> logger, CharacterService characterService, CurrencyService currencyService, EquipmentService equipmentService, EquipmentStorageService storageService)
    {
        _logger = logger;
        _characterService = characterService;
        _currencyService = currencyService;
        _equipmentService = equipmentService;
        _storageService = storageService;
    }

    /// 로그인 후 유저 전체 상태 반환.
    [HttpPost]
    public async Task<PkUserInfoResponse> Post([FromHeader] HeaderDTO header)
    {
        var response = new PkUserInfoResponse();

        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;

        _logger.ZLogInformation($"[UserInfo] Uid:{uid}");

        // 캐릭터 목록 조회
        var characters = await _characterService.GetByUidAsync(uid);
        response.Characters = characters.Select(c => new PkUserCharacter
        {
            CharacterTag = c.characterTag,
            IsActive = c.isActive,
            AcquiredAt = c.acquiredAt
        }).ToList();

        // 재화 목록 조회
        response.Currencies = await _currencyService.GetAllAsync(uid);

        // 장비 목록 조회 — EquipmentService의 매핑 재사용
        response.Equipments = await _equipmentService.GetEquipmentListAsync(uid);

        // 장비 보관함 capacity — 클라 N/M 표시 + 확장 버튼 활성/비활성 판단에 사용
        response.EquipmentStorageCapacity = await _storageService.GetCapacityAsync(uid);

        response.Result = ErrorCode.None;
        return response;
    }
}
