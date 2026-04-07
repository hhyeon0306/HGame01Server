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
}
