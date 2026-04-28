using Microsoft.EntityFrameworkCore;
using ZLogger;
using HGame01Server.Repository;
using HGame01Server.Models;
using HGame01Server.Middleware;
using HGame01Server.Services;

var builder = WebApplication.CreateBuilder(args);

// --- 설정 바인딩 ---
// appsettings.json "DbConfig" 섹션 → IOptions<DbConfig>으로 DI 등록
IConfiguration configuration = builder.Configuration;
builder.Services.Configure<DbConfig>(configuration.GetSection(nameof(DbConfig)));

// --- EF Core DbContext 등록 (MySQL) ---
var connectionString = configuration
    .GetSection(nameof(DbConfig))["GameDB"];
var serverVersion = ServerVersion.AutoDetect(connectionString);

builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

// --- 서비스 DI 등록 ---
// Scoped: 요청당 하나 | Singleton: 앱 전체에서 하나
builder.Services.AddScoped<IGameDB, GameDB>();
builder.Services.AddScoped<CharacterService>();
builder.Services.AddScoped<CurrencyService>();
builder.Services.AddScoped<ShopService>();
builder.Services.AddScoped<GachaService>();
builder.Services.AddScoped<EquipmentService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddSingleton<IMemoryDB, MemoryDB>();
builder.Services.AddSingleton<GameDataManager>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// 로거 설정
SettingLogger();

// --- 앱 빌드 ---
var app = builder.Build();

// --- DB 자동 마이그레이션 (미적용 마이그레이션만 실행) ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    db.Database.Migrate();
}

// 게임 데이터 JSON → 메모리 로드
var gameDataManager = app.Services.GetRequiredService<GameDataManager>();
gameDataManager.LoadAll();

// --- 미들웨어 & 라우팅 ---
app.UseMiddleware<CheckUserAuthAndLoadUserData>();

app.MapControllers();
app.Run();


// --- 로거 설정 (ZLogger: 콘솔 + 파일 출력) ---
void SettingLogger()
{
    ILoggingBuilder logging = builder.Logging;
    logging.ClearProviders();

    string? fileDir = configuration["logdir"];
    if (fileDir == null)
    {
        throw new Exception("logdir is not set in appsettings.json");
    }

    if (!Directory.Exists(fileDir))
    {
        Directory.CreateDirectory(fileDir);
    }

    // 파일 로그: JSON 형식, 일별 롤링, 1MB 분할
    logging.AddZLoggerRollingFile(options =>
    {
        options.UseJsonFormatter();
        options.FilePathSelector = (timestamp, sequenceNumber) =>
            $"{fileDir}{timestamp.ToLocalTime():yyyy-MM-dd}_{sequenceNumber:000}.log";
        options.RollingInterval = ZLogger.Providers.RollingInterval.Day;
        options.RollingSizeKB = 1024;
    });

    // 콘솔 로그: 플레인 텍스트 형식 (시간 [레벨] 카테고리: 메시지)
    logging.AddZLoggerConsole(options =>
    {
        options.UsePlainTextFormatter(formatter =>
        {
            formatter.SetPrefixFormatter($"{0:HH:mm:ss.fff} [{1:short}] {2}: ",
                (in MessageTemplate template, in LogInfo info) =>
                    template.Format(info.Timestamp.Local, info.LogLevel, ShortenCategory(info.Category.Name)));
        });
    });
}

// 카테고리(네임스페이스 포함 클래스 풀네임)에서 마지막 클래스명만 추출
static string ShortenCategory(string category)
{
    int lastDot = category.LastIndexOf('.');
    return lastDot >= 0 ? category[(lastDot + 1)..] : category;
}
