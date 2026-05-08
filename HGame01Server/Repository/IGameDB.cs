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
    public Task UnequipSlotAsync(long uid, string characterTag, int slot);

    /// 다건 장비 삭제. 본 user 소유분만 안전하게 제거 (uid 가드 내재).
    /// 반환: 실제 삭제된 row 수.
    public Task<int> RemoveEquipmentsBatchAsync(long uid, List<long> equipmentIds);

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

    // ===== Quest 인스턴스 =====

    /// 사용자의 모든 활성 quest 인스턴스 조회 — Active endpoint + Reconcile.
    public Task<List<GameUserQuestInstance>> GetQuestInstancesByUidAsync(long uid);

    /// 단건 조회 — Claim 검증.
    public Task<GameUserQuestInstance?> GetQuestInstanceAsync(long uid, string instanceId);

    /// 신규 슬롯 발급 (RefreshDaily / 시즌 시작).
    public Task AddQuestInstanceAsync(GameUserQuestInstance instance);

    /// 다건 발급.
    public Task AddQuestInstancesBatchAsync(List<GameUserQuestInstance> instances);

    /// 진행/상태 갱신 — EventsBatch 적용 후, Claim 후.
    public Task UpdateQuestInstanceAsync(GameUserQuestInstance instance);

    /// 다건 갱신 — EventsBatch 한 번에 N개 인스턴스 업데이트.
    public Task UpdateQuestInstancesBatchAsync(List<GameUserQuestInstance> instances);

    /// 만료된 인스턴스 일괄 만료 처리 (status="Expired"). lazy eviction.
    public Task<int> ExpireQuestInstancesAsync(long uid, string nowStr);

    // ===== Quest 멱등 dedup =====

    /// (uid, eventClientId) 이미 적용됐는지 사전 batch 조회. 중복 검증을 한 round-trip으로 끝낸다.
    /// 반환: 이미 적용된 eventClientId 집합 (HashSet 비교는 호출자 책임).
    public Task<HashSet<string>> GetAppliedEventClientIdsAsync(long uid, IReadOnlyList<string> eventClientIds);

    /// 이벤트 적용 기록 + 인스턴스 갱신을 단일 SaveChanges로 묶어 atomic 보장.
    /// applieds INSERT + instances UPDATE가 같은 EF transaction 안에서 commit/rollback.
    /// (uid, eventClientId) unique 가드 위반(race) 시 DbUpdateException throw — 호출자가 재시도/duplicate 재분류 결정.
    public Task ApplyQuestEventBatchAsync(
        List<GameUserQuestEventApplied> applieds,
        List<GameUserQuestInstance> instances);

    /// 7일 이전 dedup entry 일괄 삭제 — 주기 GC.
    public Task<int> GcQuestEventAppliedAsync(string sinceStr);
}
