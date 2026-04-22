/// 재화 타입 정의.
/// 자동생성 GdbConst.cs와 분리된 별도 파일 — 게임 데이터 업로드 시 덮어씌워지지 않도록 보호.
/// ⚠ ID는 DB의 currencyType 컬럼과 직접 매핑됨. 기존 값 재배치 절대 금지 (append-only).
public static class CurrencyType
{
    public const int Diamond = 0;
    public const int GachaTicket = 1;
    public const int Gold = 2;
}
