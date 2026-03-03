using HGame01Server.Repository;
using HGame01Server.Models;
using Microsoft.AspNetCore.Mvc;
using ZLogger;

namespace HGame01Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UserInfoController : ControllerBase
{
    private readonly ILogger<UserInfoController> _logger;

    public UserInfoController(ILogger<UserInfoController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 로그인 후 유저 전체 상태 반환
    /// </summary>
    [HttpPost]
    public PkUserInfoResponse Post([FromHeader] HeaderDTO header)
    {
        var response = new PkUserInfoResponse();

        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;

        _logger.ZLogInformation($"[UserInfo] Uid:{uid}");
        return response;
    }
}
