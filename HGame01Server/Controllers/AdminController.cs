using HGame01Server.Models;
using HGame01Server.Services;
using Microsoft.AspNetCore.Mvc;
using ZLogger;

namespace HGame01Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminController : ControllerBase
{
    private readonly GameDataManager _gameDataManager;
    private readonly ILogger<AdminController> _logger;

    public AdminController(ILogger<AdminController> logger, GameDataManager gameDataManager)
    {
        _gameDataManager = gameDataManager;
        _logger = logger;
    }

    /// <summary>
    /// Unity Editor에서 게임 데이터를 서버에 업로드
    /// </summary>
    [HttpPost("UploadGameData")]
    public PkUploadGameDataResponse UploadGameData(PkUploadGameDataRequest request)
    {
        var response = new PkUploadGameDataResponse();

        _logger.ZLogInformation($"[Admin/UploadGameData] 시작");

        var errorCode = _gameDataManager.Upload(request.GameData);

        if (errorCode != ErrorCode.None)
        {
            response.Result = errorCode;
            _logger.ZLogError($"[Admin/UploadGameData] 실패: {errorCode}");
            return response;
        }

        _logger.ZLogInformation($"[Admin/UploadGameData] 완료");
        return response;
    }
}
