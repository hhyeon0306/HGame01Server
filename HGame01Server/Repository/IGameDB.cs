namespace HGame01Server.Repository;

public interface IGameDB
{
    public Task<Tuple<ErrorCode, long>> AuthCheck(string profileId);
    public Task<ErrorCode> CreateAccount(string profileId, string name, string createdAt);
}
