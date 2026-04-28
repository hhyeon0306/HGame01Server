using HGame01Server.Models.GameData;
using HGame01Server.Repository;

namespace HGame01Server.Services;

/// 계정 생성 비즈니스 로직. 유저 생성 + 초기 데이터 지급을 트랜잭션으로 묶는다.
public class AccountService
{
    private readonly GameDbContext _context;
    private readonly IGameDB _gameDB;
    private readonly CharacterService _characterService;
    private readonly CurrencyService _currencyService;

    public AccountService(GameDbContext context, IGameDB gameDB, CharacterService characterService, CurrencyService currencyService)
    {
        _context = context;
        _gameDB = gameDB;
        _characterService = characterService;
        _currencyService = currencyService;
    }

    /// 계정 생성 + 초기 데이터 지급. 이미 존재하면 Duplicate 반환.
    public async Task<(ErrorCode error, string createdAt)> CreateAccountAsync(string profileId, string name)
    {
        string createdAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var (error, uid) = await _gameDB.CreateUserAsync(profileId, name, createdAt);
            if (error != ErrorCode.None)
            {
                return (error, "");
            }

            // 초기 데이터 지급
            await _characterService.GrantDefaultAsync(uid, createdAt);

            // 초기 재화 지급 (골드/다이아 구분 확인용 숫자 — Gold 5000 / Diamond 300)
            await _currencyService.AddAsync(uid, CurrencyType.Diamond, 300);
            await _currencyService.AddAsync(uid, CurrencyType.Gold, 5000);

            await transaction.CommitAsync();
            return (ErrorCode.None, createdAt);
        }
        catch (Exception ex)
        {
            // TODO: ILogger 주입 후 로깅 추가
            _ = ex;
            await transaction.RollbackAsync();
            return (ErrorCode.CreateAccountFailException, "");
        }
    }
}
