using HGame01Server.Repository;
using HGame01Server.Models;
using Microsoft.AspNetCore.Mvc;
using ZLogger;

namespace HGame01Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CreateAccountController : ControllerBase
{
    private readonly IGameDB _gameDB;
    private readonly ILogger<CreateAccountController> _logger;

    public CreateAccountController(ILogger<CreateAccountController> logger, IGameDB gameDB)
    {
        _gameDB = gameDB;
        _logger = logger;
    }

    [HttpPost]
    public async Task<PkCreateAccountResponse> Post(PkCreateAccountRequest request)
    {
        _logger.ZLogInformation($"[CreateAccount] ProfileId:{request.ProfileId}");

        var response = new PkCreateAccountResponse();

        // 서버에서 계정 생성 시각 생성
        string createdAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        ErrorCode errorCode = await _gameDB.CreateAccount(request.ProfileId, request.Name, createdAt);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        response.CreatedAt = createdAt;
        _logger.ZLogInformation($"[CreateAccount] Success ProfileId:{request.ProfileId}");
        return response;
    }
}
