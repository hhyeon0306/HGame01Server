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

    // ===== SeasonPass =====

    /// 유저 × 시즌 단위 row 조회. 없으면 null — Service가 ResolveOrCreate로 신규 발급.
    public Task<GameUserSeasonPass?> GetUserSeasonPassAsync(long uid, string seasonId);

    /// 신규 row insert 또는 기존 row update. Service가 hydrate/premium 토글 시 사용.
    public Task UpsertUserSeasonPassAsync(GameUserSeasonPass row);

    /// 멱등 dedup 조회. ApplyStageResult 재시도 시 expGained 재산출 회피 (이미 적립된 amount 그대로 반환).
    public Task<GameUserSeasonPassStageRun?> GetSeasonPassStageRunAsync(long uid, string stageRunId);

    /// dedup row insert + 시즌 row 갱신을 단일 SaveChanges로 묶어 atomic 보장.
    /// 부분 실패 시 exp만 누적되고 dedup row 없는 사고 → 같은 stageRunId 재요청에서 중복 누적 차단.
    public Task SaveSeasonPassAndStageRunAsync(GameUserSeasonPass row, GameUserSeasonPassStageRun runRow);

    // ===== Quest 인스턴스 =====

    /// 사용자의 활성(InProgress/Completed/Claimed) quest 인스턴스 조회 — Active endpoint + Reconcile.
    /// Expired만 제외 — Claimed는 자정 회전 전까지 "받았다" 시각 확인용으로 응답에 포함.
    /// Hydrate dedup status 우선순위 정렬이 stale → fresh 채택을 차단.
    public Task<List<GameUserQuestInstance>> GetActiveQuestInstancesByUidAsync(long uid);

    /// 단건 조회 — Claim 검증.
    public Task<GameUserQuestInstance?> GetQuestInstanceAsync(long uid, string instanceId);

    /// 진행/상태 갱신 — Claim 후.
    public Task UpdateQuestInstanceAsync(GameUserQuestInstance instance);

    /// 일일 슬롯 회전 — 기존 InProgress 만료 + 신규 InProgress 발급을 단일 SaveChanges로 묶어 atomic 보장.
    /// 부분 실패 시 만료만 적용되고 신규 0건이 되는 사고 차단.
    public Task RefreshDailyQuestsTransactionAsync(
        List<GameUserQuestInstance> toExpire,
        List<GameUserQuestInstance> toAdd);

    /// 보상 수령 — Currency 누적 + 인스턴스 Claimed 전환을 단일 SaveChanges로 묶어 atomic 보장.
    /// 부분 실패 시 통화만 누적되고 인스턴스는 Completed 잔존 → 재시도 시 중복 지급 사고 차단.
    /// currencyTypeId<=0이면 통화 없는 보상(Item/Equipment)으로 인스턴스만 갱신.
    public Task ClaimQuestTransactionAsync(
        GameUserQuestInstance instance,
        long uid,
        int currencyTypeId,
        long currencyDelta);

    /// 만료된 인스턴스 일괄 만료 처리 (status="Expired"). lazy eviction.
    /// status 무관 (InProgress/Completed/Claimed 모두 도과 시 만료). Completed 보상 보호는 호출자가 ExpireCompletedWithMailsAsync 선행으로 처리.
    public Task<int> ExpireQuestInstancesAsync(long uid, string nowStr);

    /// cheat 전용 — 인스턴스 N건 일괄 UPDATE (status/progress/lastUpdatedUtc) atomic.
    public Task UpdateQuestInstancesRangeAsync(List<GameUserQuestInstance> instances);

    /// expiresAtUtc 도과 + status=="Completed"인 슬롯 조회. 호출자가 우편함 발송 + atomic commit에 사용.
    public Task<List<GameUserQuestInstance>> GetCompletedExpiringInstancesAsync(long uid, string nowStr);

    /// 만료 대상 인스턴스 Expired 마킹 + 우편함 INSERT 단일 SaveChanges atomic.
    /// 부분 실패 시 우편함만 발송되고 슬롯이 활성으로 남아 중복 발송되는 사고 차단.
    public Task ExpireCompletedWithMailsAsync(
        List<GameUserQuestInstance> toExpire,
        List<GameUserMail> mailsToAdd);

    /// 일일 종합 보상 자동 우편함 발송 — Mail INSERT(N건) + lastDailyBundleClaimedDateUtc 갱신 단일 SaveChanges atomic.
    /// mails는 currency별 개별 발송 N통.
    public Task SendBundleMailAtomicAsync(long uid, string dateToSet, List<GameUserMail> mails);

    /// users.lastDailyBundleClaimedDateUtc 조회 — 일일 종합 보상 1회 제한 가드용.
    /// 빈 문자열이면 미수령. user 미발견 시 빈 문자열로 fallback.
    public Task<string> GetLastDailyBundleClaimedDateAsync(long uid);

    /// users.lastDailyBundleClaimedDateUtc 빈 문자열로 클리어 — cheat resetdaily가 자연 자정 회전과 동등 효과 보장.
    /// 자연 자정 회전은 IResetSchedule.Current가 새 일자라 컬럼을 안 건드려도 비교 미일치로 자동 false. cheat는 시각이 그대로라 명시적 클리어 필요.
    public Task ClearDailyBundleClaimedDateAsync(long uid);

    /// 일일 종합 보상 atomic 수령 — users 컬럼 갱신 + 다중 Currency 누적을 단일 SaveChanges로 묶음.
    /// 부분 실패 시 통화만 누적되고 컬럼 미갱신 → 재호출 시 중복 지급 사고 차단.
    /// currencyDeltas는 (currencyTypeId, delta) 튜플 리스트 — 동일 currencyTypeId는 호출자가 사전에 sum해야 함.
    public Task ClaimDailyBundleTransactionAsync(
        long uid,
        string claimedDateUtc,
        IReadOnlyList<(int currencyTypeId, long delta)> currencyDeltas);

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

    // ===== 튜토리얼 =====

    /// 완료된 튜토리얼 태그 이름 JSON 조회 — UserInfo 응답 + Complete idempotent 검증용.
    /// user 미발견 시 "[]" fallback. JSON 파싱은 호출자(TutorialService) 책임.
    public Task<string> GetCompletedTutorialsJsonAsync(long uid);

    /// 완료 튜토리얼 추가 — 이미 존재하면 무시(idempotent). 단일 SaveChanges로 row read-modify-write.
    /// 반환: 실제 추가됐는지 (false면 중복).
    public Task<bool> AddCompletedTutorialAsync(long uid, string tutorialTagName);

    /// 완료 튜토리얼 전체 클리어 — cheat ResetAll. users.completedTutorialsJson = "[]".
    public Task ClearCompletedTutorialsAsync(long uid);
}
