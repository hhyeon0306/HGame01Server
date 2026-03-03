using System.Text;
using System.Text.Json;
using ZLogger;

namespace HGame01Server.Services;

/// <summary>
/// 게임 데이터(정적 데이터) 관리
/// - Admin API로 JSON 수신 → 파일 저장 + 메모리 적재 + C# 클래스 자동 생성
/// - 서버 시작 시 JSON 파일 → 메모리 자동 로드
/// </summary>
public class GameDataManager
{
    private readonly string _dataDir;
    private readonly string _modelDir;
    private readonly ILogger<GameDataManager> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    // 메모리 저장소: key = 데이터 타입명, value = JSON 데이터
    private Dictionary<string, JsonElement> _gameData = new();

    public GameDataManager(IConfiguration configuration, ILogger<GameDataManager> logger)
    {
        _logger = logger;

        string? dataDir = configuration["gameDataDir"];
        if (string.IsNullOrEmpty(dataDir))
        {
            throw new Exception("gameDataDir is not set in appsettings.json");
        }

        _dataDir = dataDir;
        _modelDir = Path.Combine("Models", "GameData");

        if (!Directory.Exists(_dataDir))
        {
            Directory.CreateDirectory(_dataDir);
        }

        if (!Directory.Exists(_modelDir))
        {
            Directory.CreateDirectory(_modelDir);
        }
    }

    // ============================================================
    // 서버 시작 시 game_data/ 폴더의 모든 JSON → 메모리 로드
    // ============================================================

    public void LoadAll()
    {
        var newData = new Dictionary<string, JsonElement>();

        foreach (var filePath in Directory.GetFiles(_dataDir, "*.json"))
        {
            string key = Path.GetFileNameWithoutExtension(filePath);
            string json = File.ReadAllText(filePath);

            var doc = JsonDocument.Parse(json);
            newData[key] = doc.RootElement.Clone();

            _logger.ZLogInformation($"[GameDataManager] 로드: {key} ({doc.RootElement.GetArrayLength()}건)");
        }

        _gameData = newData;

        _logger.ZLogInformation($"[GameDataManager] 로드 완료 — 총 {_gameData.Count}종");
    }

    // ============================================================
    // Admin API → JSON 저장 + 메모리 갱신 + C# 클래스 자동 생성
    // ============================================================

    public ErrorCode Upload(Dictionary<string, JsonElement> gameData)
    {
        try
        {
            foreach (var (key, value) in gameData)
            {
                // JSON 파일 저장
                string fileName = $"{key}.json";
                string path = Path.Combine(_dataDir, fileName);
                string json = JsonSerializer.Serialize(value, _jsonOptions);
                File.WriteAllText(path, json);

                // 메모리 갱신
                _gameData[key] = value.Clone();

                // C# 클래스 파일 자동 생성
                GenerateModelClass(key, value);

                _logger.ZLogInformation($"[GameDataManager] 저장: {key} ({value.GetArrayLength()}건)");
            }

            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _logger.ZLogError($"[GameDataManager] Upload 실패: {ex.Message}");
            return ErrorCode.AdminUploadFail;
        }
    }

    // ============================================================
    // 조회 — 키로 JSON 데이터 반환
    // ============================================================

    public JsonElement? GetData(string key)
    {
        return _gameData.TryGetValue(key, out var data) ? data : null;
    }

    // ============================================================
    // JSON 배열의 첫 번째 요소를 분석하여 C# 클래스 파일 생성
    // 예: "Characters" → Models/GameData/GdbCharacterData.cs
    // ============================================================

    private void GenerateModelClass(string key, JsonElement jsonArray)
    {
        if (jsonArray.ValueKind != JsonValueKind.Array || jsonArray.GetArrayLength() == 0)
        {
            return;
        }

        // 첫 번째 요소에서 프로퍼티 구조 추출
        var firstElement = jsonArray[0];
        if (firstElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        // 클래스명 생성: "Characters" → "GdbCharacterData", "Stages" → "GdbStageData"
        string className = $"Gdb{ToSingular(key)}Data";
        string filePath = Path.Combine(_modelDir, $"{className}.cs");

        var sb = new StringBuilder();
        sb.AppendLine("namespace HGame01Server.Models.GameData;");
        sb.AppendLine();
        sb.AppendLine("// 이 파일은 Admin/UploadGameData API에 의해 자동 생성되었습니다.");
        sb.AppendLine("// 직접 수정하지 마세요. Unity에서 데이터를 다시 업로드하면 덮어씌워집니다.");
        sb.AppendLine();
        sb.AppendLine($"public class {className}");
        sb.AppendLine("{");

        foreach (var property in firstElement.EnumerateObject())
        {
            string csType = InferCSharpType(property.Value);
            string defaultValue = GetDefaultValue(csType);
            sb.AppendLine($"    public {csType} {property.Name} {{ get; set; }}{defaultValue}");
        }

        sb.AppendLine("}");

        File.WriteAllText(filePath, sb.ToString());

        _logger.ZLogInformation($"[GameDataManager] 클래스 생성: {filePath}");
    }

    // JSON 값 → C# 타입 추론
    private static string InferCSharpType(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                return "string";
            case JsonValueKind.Number:
                if (value.TryGetInt32(out _))
                {
                    return "int";
                }
                if (value.TryGetInt64(out _))
                {
                    return "long";
                }
                return "float";
            case JsonValueKind.True:
            case JsonValueKind.False:
                return "bool";
            default:
                return "object";
        }
    }

    // 타입별 기본값
    private static string GetDefaultValue(string csType)
    {
        return csType switch
        {
            "string" => " = \"\";",
            _ => ""
        };
    }

    // 복수형 → 단수형 (간단한 규칙)
    private static string ToSingular(string plural)
    {
        if (plural.EndsWith("ies"))
        {
            return plural[..^3] + "y";
        }
        if (plural.EndsWith("ses") || plural.EndsWith("xes") || plural.EndsWith("zes"))
        {
            return plural[..^2];
        }
        if (plural.EndsWith("s") && !plural.EndsWith("ss"))
        {
            return plural[..^1];
        }
        return plural;
    }
}
