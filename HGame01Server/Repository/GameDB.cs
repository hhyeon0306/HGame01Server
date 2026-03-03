using Microsoft.EntityFrameworkCore;
using HGame01Server.Models;

namespace HGame01Server.Repository;

public class GameDB : IGameDB
{
    private readonly GameDbContext _context;

    public GameDB(GameDbContext context)
    {
        _context = context;
    }

    public async Task<Tuple<ErrorCode, long>> AuthCheck(string userID, string pw)
    {
        try
        {
            var userInfo = await _context.Users
                .FirstOrDefaultAsync(u => u.id == userID);

            if (userInfo == null || userInfo.uid == 0)
            {
                return new Tuple<ErrorCode, long>(ErrorCode.LoginFailUserNotExist, 0);
            }

            if (userInfo.pw != pw)
            {
                return new Tuple<ErrorCode, long>(ErrorCode.LoginFailPwNotMatch, 0);
            }

            return new Tuple<ErrorCode, long>(ErrorCode.None, userInfo.uid);
        }
        catch
        {
            return new Tuple<ErrorCode, long>(ErrorCode.LoginFailException, 0);
        }
    }

    public async Task<ErrorCode> CreateAccount(string id, string pw)
    {
        try
        {
            // 중복 체크
            var existing = await _context.Users
                .FirstOrDefaultAsync(u => u.id == id);

            if (existing != null && existing.uid != 0)
            {
                return ErrorCode.CreateAccountFailDuplicate;
            }

            // 유저 생성
            var user = new GameUser { id = id, pw = pw };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return ErrorCode.None;
        }
        catch
        {
            return ErrorCode.CreateAccountFailException;
        }
    }
}
