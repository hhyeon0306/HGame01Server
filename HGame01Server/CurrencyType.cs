/// 재화 타입 정의.
/// 자동생성 GdbConst.cs와 분리된 별도 파일 — 게임 데이터 업로드 시 덮어씌워지지 않도록 보호.
/// ⚠ ID는 DB의 currencyType 컬럼과 직접 매핑됨. 기존 값 재배치 절대 금지 (append-only).
/// GachaTicket(=1)은 가챠 다이아 결제 단일화에 따라 제거됐다 — Gold=2 값은 보존.
public static class CurrencyType
{
    public const int Diamond = 0;
    public const int Gold = 2;
}
