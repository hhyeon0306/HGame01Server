namespace HGame01Server.Repository;

public interface IGameDB
{
    public Task<Tuple<ErrorCode, long>> AuthCheck(string email, string pw);
    public Task<ErrorCode> CreateAccount(string id, string pw);
}
