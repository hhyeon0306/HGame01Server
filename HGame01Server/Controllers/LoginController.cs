using HGame01Server.Repository;
using HGame01Server.Models;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using ZLogger;

namespace HGame01Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class LoginController : ControllerBase
{
    private readonly IGameDB _gameDB;
    private readonly IMemoryDB _memoryDB;
    private readonly ILogger<LoginController> _logger;

    public LoginController(ILogger<LoginController> logger, IGameDB gameDB, IMemoryDB memoryDB)
    {
        _gameDB = gameDB;
        _memoryDB = memoryDB;
        _logger = logger;
    }

    [HttpPost]
    public async Task<PkLoginResponse> Post(PkLoginRequest request)
    {
        _logger.ZLogInformation($"[Request Login] ProfileId:{request.ProfileId}");

        var response = new PkLoginResponse();

        // profileId로 유저 조회
        (ErrorCode errorCode, long uid) = await _gameDB.AuthCheck(request.ProfileId);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        string authToken = CreateAuthToken();
        errorCode = await _memoryDB.RegistUserAsync(request.ProfileId, authToken, uid);
        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            return response;
        }

        response.AuthToken = authToken;
        return response;
    }

    private const string AllowableCharacters = "abcdefghijklmnopqrstuvwxyz0123456789";

    public string CreateAuthToken()
    {
        var bytes = new byte[25];
        using (var random = RandomNumberGenerator.Create())
        {
            random.GetBytes(bytes);
        }

        return new string(bytes.Select(x => AllowableCharacters[x % AllowableCharacters.Length]).ToArray());
    }
}
