using HGame01Server.Models;
using HGame01Server.Repository;

namespace HGame01Server.Services;

/// 재화 비즈니스 로직.
public class CurrencyService
{
    private readonly IGameDB _gameDB;

    public CurrencyService(IGameDB gameDB)
    {
        _gameDB = gameDB;
    }

    /// 유저의 전체 재화 목록 조회.
    public async Task<List<PkCurrency>> GetAllAsync(long uid)
    {
        var currencies = await _gameDB.GetCurrenciesByUidAsync(uid);
        return currencies.Select(c => new PkCurrency
        {
            CurrencyType = c.currencyType,
            Amount = c.amount
        }).ToList();
    }

    /// 특정 재화의 보유량 조회. 없으면 0 반환.
    public async Task<long> GetAmountAsync(long uid, int currencyType)
    {
        var currency = await _gameDB.GetCurrencyAsync(uid, currencyType);
        return currency?.amount ?? 0;
    }

    /// 재화 증가.
    public async Task<ErrorCode> AddAsync(long uid, int currencyType, long amount)
    {
        try
        {
            await _gameDB.UpsertCurrencyAsync(uid, currencyType, amount);
            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            // TODO: ILogger 주입 후 로깅 추가
            _ = ex;
            return ErrorCode.CurrencyUpdateFailed;
        }
    }

    /// 재화 차감. 부족하면 에러 반환.
    public async Task<ErrorCode> DeductAsync(long uid, int currencyType, long amount)
    {
        var success = await _gameDB.DeductCurrencyAsync(uid, currencyType, amount);
        return success ? ErrorCode.None : ErrorCode.CurrencyInsufficientAmount;
    }
}
