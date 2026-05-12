using System.Text.Json;
using HGame01Server.Repository;
using ZLogger;

namespace HGame01Server.Services;

/// 튜토리얼 완료 추적 — JSON 컬럼 (users.completedTutorialsJson) 캡슐화.
/// 그룹 단위만 저장 — 단계 중단점은 보존하지 않음 (클라 설계: 중단 시 다음 세션 처음부터 재시작).
public class TutorialService
{
    private readonly IGameDB _gameDB;
    private readonly ILogger<TutorialService> _logger;

    public TutorialService(IGameDB gameDB, ILogger<TutorialService> logger)
    {
        _gameDB = gameDB;
        _logger = logger;
    }

    /// 완료 튜토리얼 태그 이름 목록 조회 — UserInfo 응답용.
    public async Task<List<string>> GetCompletedAsync(long uid)
    {
        var json = await _gameDB.GetCompletedTutorialsJsonAsync(uid);
        return Deserialize(json);
    }

    /// 완료 push — idempotent. 빈 태그 거절. 이미 있으면 skip.
    public async Task<ErrorCode> CompleteAsync(long uid, string tutorialTagName)
    {
        if (string.IsNullOrEmpty(tutorialTagName))
        {
            return ErrorCode.TutorialInvalidTag;
        }

        try
        {
            bool added = await _gameDB.AddCompletedTutorialAsync(uid, tutorialTagName);
            _logger.ZLogInformation($"[Tutorial/Complete] Uid:{uid} Tag:{tutorialTagName} Added:{added}");
            return ErrorCode.None;
        }
        catch (Exception e)
        {
            _logger.ZLogError($"[Tutorial/Complete] Uid:{uid} Tag:{tutorialTagName} 예외: {e.Message}");
            return ErrorCode.TutorialPersistFailed;
        }
    }

    /// 치트 전용 — 완료 튜토리얼 전체 클리어. 다음 trigger 시 재발동.
    public async Task<ErrorCode> CheatResetAllAsync(long uid)
    {
        try
        {
            await _gameDB.ClearCompletedTutorialsAsync(uid);
            _logger.ZLogInformation($"[Tutorial/CheatResetAll] Uid:{uid} 완료 튜토리얼 전체 클리어");
            return ErrorCode.None;
        }
        catch (Exception e)
        {
            _logger.ZLogError($"[Tutorial/CheatResetAll] Uid:{uid} 예외: {e.Message}");
            return ErrorCode.TutorialPersistFailed;
        }
    }

    private static List<string> Deserialize(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new List<string>();
        }
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}
