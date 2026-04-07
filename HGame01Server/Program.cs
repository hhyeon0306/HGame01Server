using Microsoft.EntityFrameworkCore;
using ZLogger;
using HGame01Server.Repository;
using HGame01Server.Models;
using HGame01Server.Middleware;
using HGame01Server.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// 1. 설정 바인딩
//    appsettings.json의 "DbConfig" 섹션을 DbConfig 클래스에 이름 매칭으로 자동 바인딩
//    JSON "GameDB" → DbConfig.GameDB, JSON "Redis" → DbConfig.Redis
//    바인딩된 객체를 IOptions<DbConfig> 래퍼로 감싸서 DI에 등록
//    → 이후 어떤 클래스든 생성자에서 IOptions<DbConfig>로 주입받아 .Value로 접근
//    → 환경별 설정 분리 가능 (Development.json vs Production.json)
// ============================================================
IConfiguration configuration = builder.Configuration;
builder.Services.Configure<DbConfig>(configuration.GetSection(nameof(DbConfig)));

// ============================================================
// 2. EF Core DbContext 등록 (MySQL 연결)
//    connectionString: appsettings.json에서 MySQL 연결 문자열을 직접 꺼냄
//    ServerVersion.AutoDetect: MySQL에 잠깐 접속해서 버전(8.0 등)을 알아냄
//      → Pomelo 프로바이더가 버전별로 다른 SQL 문법을 생성하기 위해 필요
//    AddDbContext: DI 컨테이너에 GameDbContext를 Scoped(요청당 하나)로 등록
//      → EF Core 자체는 DB 종류를 모름. UseMySql()로 "MySQL을 쓴다"고 알려줌
//      → UseNpgsql()이면 PostgreSQL, UseSqlite()이면 SQLite
// ============================================================
var connectionString = configuration
    .GetSection(nameof(DbConfig))["GameDB"];
var serverVersion = ServerVersion.AutoDetect(connectionString);

builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

// ============================================================
// 3. 서비스 DI 등록
//    DI(Dependency Injection): 클래스가 필요한 객체를 직접 만들지 않고,
//    생성자 파라미터로 선언만 하면 DI 컨테이너가 자동으로 만들어서 넣어줌
//    → 각 클래스가 자기 역할에만 집중할 수 있게 해주는 패턴
//
//    Scoped: 요청당 하나, 요청 끝나면 소멸 (GameDB는 DbContext와 수명을 맞춰야 함)
//    Singleton: 앱 전체에서 딱 하나 (Redis 연결은 공유해도 안전하고, 매번 새로 연결하면 비용이 큼)
// ============================================================
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

// 로거 설정 (아래 함수 참고)
SettingLogger();

// ============================================================
// 4. 앱 빌드 — 여기서 DI 컨테이너가 확정됨
// ============================================================
var app = builder.Build();

// ============================================================
// 5. DB 자동 마이그레이션
//    Migrations/ 폴더의 코드를 읽어서 아직 적용 안 된 것만 실행
//    → 첫 실행: 테이블 전부 생성
//    → 이후: 변경된 부분(컬럼 추가 등)만 적용
// ============================================================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    db.Database.Migrate();
}

// 게임 데이터 JSON → 메모리 로드
var gameDataManager = app.Services.GetRequiredService<GameDataManager>();
gameDataManager.LoadAll();

// ============================================================
// 6. 미들웨어 & 라우팅 설정
//    모든 요청이 Controller에 도달하기 전에 인증 미들웨어를 거침
// ============================================================
app.UseMiddleware<CheckUserAuthAndLoadUserData>();

app.MapControllers();
app.Run();


// ============================================================
// 로거 설정
//   ZLogger: 고성능 로깅 라이브러리 (Unity의 ZLogger와 동일 제작자)
//   ClearProviders: .NET 기본 로그 제공자를 제거하고 ZLogger로 교체
//   설정 후 어떤 클래스든 ILogger<T>를 주입받아 사용 가능
//     예: _logger.ZLogInformation($"[Login] ID:{request.ID}");
//     → 콘솔 + 파일 양쪽에 JSON 로그 출력
// ============================================================
void SettingLogger()
{
    ILoggingBuilder logging = builder.Logging;
    logging.ClearProviders();

    // appsettings.json의 "logdir"에서 로그 디렉토리 경로를 읽음
    string? fileDir = configuration["logdir"];
    if (fileDir == null)
    {
        throw new Exception("logdir is not set in appsettings.json");
    }

    if (!Directory.Exists(fileDir))
    {
        Directory.CreateDirectory(fileDir);
    }

    // 파일 로그: JSON 형식, 하루마다 새 파일, 1MB 넘으면 분할 (_001, _002...)
    // 예: ./log/2026-03-04_000.log
    logging.AddZLoggerRollingFile(options =>
    {
        options.UseJsonFormatter();
        options.FilePathSelector = (timestamp, sequenceNumber) =>
            $"{fileDir}{timestamp.ToLocalTime():yyyy-MM-dd}_{sequenceNumber:000}.log";
        options.RollingInterval = ZLogger.Providers.RollingInterval.Day;
        options.RollingSizeKB = 1024;
    });

    // 콘솔 로그: 사람이 읽기 쉬운 플레인 텍스트 형식
    // 예: "05:41:54 [INF] [GameDataManager] 타입 로드: GdbCharacterData (2건)"
    logging.AddZLoggerConsole(options =>
    {
        options.UsePlainTextFormatter(formatter =>
        {
            formatter.SetPrefixFormatter($"{0:local-longdate} [{1:short}] ",
                (in MessageTemplate template, in LogInfo info) => template.Format(info.Timestamp, info.LogLevel));
        });
    });
}
