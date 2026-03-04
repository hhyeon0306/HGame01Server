using CloudStructures;
using CloudStructures.Structures;
using Microsoft.Extensions.Options;
using ZLogger;
using HGame01Server.Models;

namespace HGame01Server.Repository;

public class MemoryDB : IMemoryDB
{
    public RedisConnection _redisConn;
    readonly ILogger<MemoryDB> _logger;

    public MemoryDB(ILogger<MemoryDB> logger, IOptions<DbConfig> dbConfig)
    {
        _logger = logger;

        RedisConfig config = new("default", dbConfig.Value.Redis);
        _redisConn = new RedisConnection(config);
    }

    public void Dispose()
    {
    }

    public async Task<ErrorCode> RegistUserAsync(string profileId, string authToken, long uid)
    {
        string key = MemoryDbKeyMaker.MakeUserDataKey(profileId);
        ErrorCode result = ErrorCode.None;

        MdbUserData user = new()
        {
            AuthToken = authToken,
            UId = uid,
        };

        try
        {
            var expiryTime = TimeSpan.FromMinutes((60 * 24));
            RedisString<MdbUserData> redis = new(_redisConn, key, expiryTime);
            if (await redis.SetAsync(user, expiryTime) == false)
            {
                result = ErrorCode.LoginFailAddRedis;
                return result;
            }
        }
        catch
        {
            result = ErrorCode.LoginFailAddRedis;
            return result;
        }

        return result;
    }

    public async Task<(bool, MdbUserData)> GetUserAsync(string profileId)
    {
        var key = MemoryDbKeyMaker.MakeUserDataKey(profileId);

        try
        {
            RedisString<MdbUserData> redis = new(_redisConn, key, null);
            RedisResult<MdbUserData> user = await redis.GetAsync();
            if (!user.HasValue)
            {
                _logger.ZLogError(
                    $"[GetUserAsync] ProfileId = {profileId}, ErrorMessage = Not Assigned User, RedisString get Error");
                return (false, null);
            }

            return (true, user.Value);
        }
        catch
        {
            _logger.ZLogError($"[GetUserAsync] ProfileId:{profileId}, ErrorMessage:ID does Not Exist");
            return (false, null);
        }
    }
}


public class MdbUserData
{
    public string AuthToken { get; set; } = "";
    public Int64 UId { get; set; } = 0;
}


public class MemoryDbKeyMaker
{
    public static string MakeUserDataKey(string profileId)
    {
        return "HGame01_PROFILEID_" + profileId;
    }
}
