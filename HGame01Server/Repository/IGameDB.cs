using HGame01Server.Models;

namespace HGame01Server.Repository;

public interface IGameDB
{
    // ===== 계정 =====
    public Task<(ErrorCode, long)> AuthCheck(string profileId);
    public Task<(ErrorCode error, long uid)> CreateUserAsync(string profileId, string name, string createdAt);

    // ===== 캐릭터 =====
    public Task AddCharacterAsync(GameUserCharacter character);
    public Task<List<GameUserCharacter>> GetCharactersByUidAsync(long uid);

    // ===== 재화 =====
    public Task<List<GameUserCurrency>> GetCurrenciesByUidAsync(long uid);
    public Task<GameUserCurrency?> GetCurrencyAsync(long uid, int currencyType);
    public Task UpsertCurrencyAsync(long uid, int currencyType, long delta);
    public Task<bool> DeductCurrencyAsync(long uid, int currencyType, long amount);

    // ===== 장비 =====
    public Task<List<GameUserEquipment>> GetEquipmentsByUidAsync(long uid);
    public Task AddEquipmentAsync(GameUserEquipment equipment);
    public Task AddEquipmentBatchAsync(List<GameUserEquipment> equipments);

    // ===== 장비 장착 =====
    public Task<GameUserEquipment?> GetEquipmentByIdAsync(long uid, long equipmentId);
    public Task UpdateEquipmentAsync(GameUserEquipment equipment);
    public Task UnequipSlotAsync(long uid, int characterId, int slot);

    // ===== 상점 구매 =====
    public Task<List<GameUserShopPurchase>> GetPurchasesSinceAsync(long uid, string sinceStr);
    public Task AddPurchaseAsync(GameUserShopPurchase purchase);

    /// 치트 전용. 지정 사용자의 sinceStr 이후 구매 기록 모두 삭제 → 일일 카운터 초기화 효과.
    public Task<int> DeletePurchasesSinceAsync(long uid, string sinceStr);

    // ===== 우편함 =====

    public Task AddMailAsync(GameUserMail mail);
    public Task<List<GameUserMail>> GetMailsByUidAsync(long uid);
    public Task<GameUserMail?> GetMailAsync(long uid, string mailId);

    /// claimedAt == "" && expireAt > now. ClaimAll 의 후보 메일 검색.
    public Task<List<GameUserMail>> GetClaimableMailsAsync(long uid, string nowStr);

    /// 단건 claim 처리 — claimedAt 채움. row 는 보관 (CS / 이력 추적).
    public Task UpdateMailClaimedAsync(GameUserMail mail);

    /// 다건 claim 처리 — claimedAt 채움.
    public Task UpdateMailsClaimedBatchAsync(List<GameUserMail> mails);

    /// expireAt &lt; nowStr 인 사용자 메일 일괄 삭제 (lazy eviction). List 호출 첫 단계에서 한 번 호출.
    public Task<int> DeleteExpiredMailsAsync(long uid, string nowStr);
}
