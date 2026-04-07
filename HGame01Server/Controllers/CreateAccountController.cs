using HGame01Server.Models;
using HGame01Server.Services;
using Microsoft.AspNetCore.Mvc;
using ZLogger;

namespace HGame01Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CreateAccountController : ControllerBase
{
    private readonly AccountService _accountService;
    private readonly ILogger<CreateAccountController> _logger;

    public CreateAccountController(ILogger<CreateAccountController> logger, AccountService accountService)
    {
        _accountService = accountService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<PkCreateAccountResponse> Post(PkCreateAccountRequest request)
    {
        _logger.ZLogInformation($"[CreateAccount] ProfileId:{request.ProfileId}");

        var (errorCode, createdAt) = await _accountService.CreateAccountAsync(request.ProfileId, request.Name);

        var response = new PkCreateAccountResponse { Result = errorCode, CreatedAt = createdAt };

        if (errorCode == ErrorCode.None)
        {
            _logger.ZLogInformation($"[CreateAccount] Success ProfileId:{request.ProfileId}");
        }

        return response;
    }
}
