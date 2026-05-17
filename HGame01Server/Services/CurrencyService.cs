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

    /// 재화 변동 응답의 currencies를 "유저 전체 절대 스냅샷"으로 채우는 단일 경로 (currency-contract).
    /// ICurrencyBearingResponse 구현 응답은 직접 currencies 대입 대신 반드시 이 메서드를 거친다.
    /// 부분 목록(변경분만) 응답 시 클라 absolute 교체로 미포함 통화가 0으로 소실되는 사고를 구조적으로 차단.
    public async Task PopulateCurrenciesAsync(ICurrencyBearingResponse response, long uid)
    {
        response.Currencies = await GetAllAsync(uid);
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

    /// 디버그 전용 — 재화를 절대값으로 설정. 음수는 0으로 클램프.
    public async Task<ErrorCode> SetAsync(long uid, int currencyType, long amount)
    {
        try
        {
            await _gameDB.SetCurrencyAsync(uid, currencyType, amount < 0 ? 0 : amount);
            return ErrorCode.None;
        }
        catch (Exception ex)
        {
            _ = ex;
            return ErrorCode.CurrencyUpdateFailed;
        }
    }
}
