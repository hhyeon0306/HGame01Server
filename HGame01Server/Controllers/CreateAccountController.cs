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
        _logger.ZLogInformation($"[CreateAccount] ID:{request.ID}");

        var response = new PkCreateAccountResponse();

        ErrorCode errorCode = await _gameDB.CreateAccount(request.ID, request.PW);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        _logger.ZLogInformation($"[CreateAccount] Success ID:{request.ID}");
        return response;
    }
}
