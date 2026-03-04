namespace HGame01Server.Repository;

public interface IMemoryDB : IDisposable
{
    public Task<ErrorCode> RegistUserAsync(string profileId, string authToken, long uid);

    public Task<(bool, MdbUserData)> GetUserAsync(string profileId);
}
