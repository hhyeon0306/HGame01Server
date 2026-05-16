using System.Collections.Generic;

namespace HGame01Server.Models;

/// 재화가 변동되는 모든 서버 응답이 구현하는 공통 규약.
///
/// currencies는 반드시 "유저 전체 재화 절대 스냅샷"이어야 한다 (이번에 바뀐 통화만 X).
/// 클라 UserCurrencyStore.UpdateFromServer가 absolute 전체 교체라서,
/// 부분 목록을 보내면 응답에 없는 통화가 클라 화면에서 0으로 소실된다 (SeasonPass Gold 0 사고 사례).
///
/// 채우기는 CurrencyService.PopulateCurrenciesAsync 단일 경로로만 수행한다.
/// 응답에 Currencies를 직접 대입하지 말 것 (재발방지 규약 — currency-contract).
public interface ICurrencyBearingResponse
{
    List<PkCurrency> Currencies { get; set; }
}
