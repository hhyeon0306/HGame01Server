using Microsoft.EntityFrameworkCore;
using HGame01Server.Models;

namespace HGame01Server.Repository;

public class GameDB : IGameDB
{
    private readonly GameDbContext _context;

    public GameDB(GameDbContext context)
    {
        _context = context;
    }

    public async Task<(ErrorCode, long)> AuthCheck(string profileId)
    {
        try
        {
            var userInfo = await _context.Users
                .FirstOrDefaultAsync(u => u.profileId == profileId);

            if (userInfo == null || userInfo.uid == 0)
            {
                return (ErrorCode.LoginFailUserNotExist, 0);
            }

            return (ErrorCode.None, userInfo.uid);
        }
        catch (Exception ex)
        {
            // TODO: ILogger 주입 후 로깅 추가
            _ = ex;
            return (ErrorCode.LoginFailException, 0);
        }
    }

    public async Task<(ErrorCode error, long uid)> CreateUserAsync(string profileId, string name, string createdAt)
    {
        // 중복 체크
        var existing = await _context.Users
            .FirstOrDefaultAsync(u => u.profileId == profileId);

        if (existing != null && existing.uid != 0)
        {
            return (ErrorCode.CreateAccountFailDuplicate, 0);
        }

        // 유저 생성
        var user = new GameUser { profileId = profileId, name = name, createdAt = createdAt };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return (ErrorCode.None, user.uid);
    }


    // ===== 캐릭터 =====

    public async Task AddCharacterAsync(GameUserCharacter character)
    {
        _context.UserCharacters.Add(character);
        await _context.SaveChangesAsync();
    }

    public async Task<List<GameUserCharacter>> GetCharactersByUidAsync(long uid)
    {
        return await _context.UserCharacters
            .Where(c => c.uid == uid)
            .ToListAsync();
    }


    // ===== 재화 =====

    public async Task<List<GameUserCurrency>> GetCurrenciesByUidAsync(long uid)
    {
        return await _context.UserCurrencies
            .Where(c => c.uid == uid)
            .ToListAsync();
    }

    public async Task<GameUserCurrency?> GetCurrencyAsync(long uid, int currencyType)
    {
        return await _context.UserCurrencies
            .FirstOrDefaultAsync(c => c.uid == uid && c.currencyType == currencyType);
    }

    public async Task UpsertCurrencyAsync(long uid, int currencyType, long delta)
    {
        var currency = await GetCurrencyAsync(uid, currencyType);
        if (currency == null)
        {
            currency = new GameUserCurrency
            {
                uid = uid,
                currencyType = currencyType,
                amount = delta
            };
            _context.UserCurrencies.Add(currency);
        }
        else
        {
            currency.amount += delta;
        }
        await _context.SaveChangesAsync();
    }

    /// 조건부 원자 차감 — 단일 UPDATE 쿼리로 race condition 방지.
    /// "amount >= cost" 조건을 SQL WHERE에 두어 동시 요청 중 하나만 성공하도록 한다.
    /// 영향 행 수가 0이면 잔액 부족 또는 row 미존재로 간주.
    public async Task<bool> DeductCurrencyAsync(long uid, int currencyType, long amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        int affected = await _context.UserCurrencies
            .Where(c => c.uid == uid && c.currencyType == currencyType && c.amount >= amount)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.amount, c => c.amount - amount));

        if (affected > 0)
        {
            // 동일 컨텍스트가 캐시한 엔티티가 있으면 stale일 수 있으므로 무효화
            var tracked = _context.ChangeTracker.Entries<GameUserCurrency>()
                .FirstOrDefault(e => e.Entity.uid == uid && e.Entity.currencyType == currencyType);
            if (tracked != null)
            {
                await tracked.ReloadAsync();
            }
        }

        return affected > 0;
    }


    // ===== 장비 =====

    public async Task<List<GameUserEquipment>> GetEquipmentsByUidAsync(long uid)
    {
        return await _context.UserEquipments
            .Where(e => e.uid == uid)
            .ToListAsync();
    }

    public async Task AddEquipmentAsync(GameUserEquipment equipment)
    {
        _context.UserEquipments.Add(equipment);
        await _context.SaveChangesAsync();
    }

    public async Task AddEquipmentBatchAsync(List<GameUserEquipment> equipments)
    {
        _context.UserEquipments.AddRange(equipments);
        await _context.SaveChangesAsync();
    }


    // ===== 장비 장착 =====

    public async Task<GameUserEquipment?> GetEquipmentByIdAsync(long uid, long equipmentId)
    {
        return await _context.UserEquipments
            .FirstOrDefaultAsync(e => e.uid == uid && e.id == equipmentId);
    }

    public async Task UpdateEquipmentAsync(GameUserEquipment equipment)
    {
        _context.UserEquipments.Update(equipment);
        await _context.SaveChangesAsync();
    }

    public async Task UnequipSlotAsync(long uid, string characterTag, int slot)
    {
        var equipped = await _context.UserEquipments
            .Where(e => e.uid == uid && e.equippedCharacterTag == characterTag && e.slot == slot && e.isEquipped)
            .ToListAsync();

        foreach (var item in equipped)
        {
            item.isEquipped = false;
            item.equippedCharacterTag = "";
        }

        await _context.SaveChangesAsync();
    }

    public async Task<int> RemoveEquipmentsBatchAsync(long uid, List<long> equipmentIds)
    {
        if (equipmentIds == null || equipmentIds.Count == 0)
        {
            return 0;
        }

        // uid 가드를 WHERE에 포함해 타 유저 row가 섞여 들어와도 차단.
        return await _context.UserEquipments
            .Where(e => e.uid == uid && equipmentIds.Contains(e.id))
            .ExecuteDeleteAsync();
    }


    // ===== 상점 구매 =====

    public async Task<List<GameUserShopPurchase>> GetPurchasesSinceAsync(long uid, string sinceStr)
    {
        return await _context.UserShopPurchases
            .Where(p => p.uid == uid && string.Compare(p.purchasedAt, sinceStr) >= 0)
            .ToListAsync();
    }

    public async Task AddPurchaseAsync(GameUserShopPurchase purchase)
    {
        _context.UserShopPurchases.Add(purchase);
        await _context.SaveChangesAsync();
    }

    public async Task<int> DeletePurchasesSinceAsync(long uid, string sinceStr)
    {
        var rows = await _context.UserShopPurchases
            .Where(p => p.uid == uid && string.Compare(p.purchasedAt, sinceStr) >= 0)
            .ToListAsync();

        if (rows.Count == 0)
        {
            return 0;
        }

        _context.UserShopPurchases.RemoveRange(rows);
        await _context.SaveChangesAsync();
        return rows.Count;
    }


    // ===== 우편함 =====

    public async Task AddMailAsync(GameUserMail mail)
    {
        _context.UserMails.Add(mail);
        await _context.SaveChangesAsync();
    }

    public async Task<List<GameUserMail>> GetMailsByUidAsync(long uid)
    {
        return await _context.UserMails
            .Where(m => m.uid == uid)
            .ToListAsync();
    }

    public async Task<GameUserMail?> GetMailAsync(long uid, string mailId)
    {
        return await _context.UserMails
            .FirstOrDefaultAsync(m => m.uid == uid && m.mailId == mailId);
    }

    public async Task<List<GameUserMail>> GetClaimableMailsAsync(long uid, string nowStr)
    {
        return await _context.UserMails
            .Where(m => m.uid == uid && m.claimedAt == "" && string.Compare(m.expireAt, nowStr) > 0)
            .ToListAsync();
    }

    public async Task UpdateMailClaimedAsync(GameUserMail mail)
    {
        _context.UserMails.Update(mail);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateMailsClaimedBatchAsync(List<GameUserMail> mails)
    {
        _context.UserMails.UpdateRange(mails);
        await _context.SaveChangesAsync();
    }

    public async Task<int> DeleteExpiredMailsAsync(long uid, string nowStr)
    {
        var expired = await _context.UserMails
            .Where(m => m.uid == uid && string.Compare(m.expireAt, nowStr) < 0)
            .ToListAsync();

        if (expired.Count == 0)
        {
            return 0;
        }

        _context.UserMails.RemoveRange(expired);
        await _context.SaveChangesAsync();
        return expired.Count;
    }

    // ===== Quest 인스턴스 =====

    public async Task<List<GameUserQuestInstance>> GetActiveQuestInstancesByUidAsync(long uid)
    {
        // Claimed는 자정 회전 전까지 사용자가 "받았다" 시각 확인할 수 있도록 응답에 포함.
        // Expired만 제외 — RefreshDaily/lazy 만료 처리된 row는 클라 스토리지 절감 + dedup 정합.
        return await _context.UserQuestInstances
            .Where(q => q.uid == uid && q.status != "Expired")
            .ToListAsync();
    }

    public async Task<GameUserQuestInstance?> GetQuestInstanceAsync(long uid, string instanceId)
    {
        return await _context.UserQuestInstances
            .FirstOrDefaultAsync(q => q.uid == uid && q.instanceId == instanceId);
    }

    public async Task UpdateQuestInstanceAsync(GameUserQuestInstance instance)
    {
        _context.UserQuestInstances.Update(instance);
        await _context.SaveChangesAsync();
    }

    public async Task ClaimQuestTransactionAsync(
        GameUserQuestInstance instance,
        long uid,
        int currencyTypeId,
        long currencyDelta)
    {
        _context.UserQuestInstances.Update(instance);

        // currencyTypeId == -1 = 통화 없음 sentinel. CurrencyType.Diamond=0은 valid 값이라 0 가드 절대 금지.
        if (currencyTypeId >= 0 && currencyDelta != 0)
        {
            // user_currencies upsert — Update 패턴 합치 (CurrencyService.UpsertCurrencyAsync 동등).
            var row = await _context.UserCurrencies
                .FirstOrDefaultAsync(c => c.uid == uid && c.currencyType == currencyTypeId);
            if (row == null)
            {
                _context.UserCurrencies.Add(new GameUserCurrency
                {
                    uid = uid,
                    currencyType = currencyTypeId,
                    amount = currencyDelta,
                });
            }
            else
            {
                row.amount += currencyDelta;
                _context.UserCurrencies.Update(row);
            }
        }

        // 단일 SaveChanges = EF Core implicit transaction. UPDATE/INSERT 모두 atomic commit/rollback.
        // 부분 실패 시 통화만 누적되고 인스턴스 Claimed 전환 안 되는 사고 차단.
        await _context.SaveChangesAsync();
    }

    public async Task RefreshDailyQuestsTransactionAsync(
        List<GameUserQuestInstance> toExpire,
        List<GameUserQuestInstance> toAdd)
    {
        bool anyExpire = toExpire != null && toExpire.Count > 0;
        bool anyAdd = toAdd != null && toAdd.Count > 0;
        if (!anyExpire && !anyAdd)
        {
            return;
        }
        if (anyExpire)
        {
            _context.UserQuestInstances.UpdateRange(toExpire!);
        }
        if (anyAdd)
        {
            _context.UserQuestInstances.AddRange(toAdd!);
        }
        // 단일 SaveChanges = EF Core implicit transaction. UPDATE + INSERT atomic commit/rollback.
        // 부분 실패 시 만료만 적용되고 신규 0건이 되는 사고 차단.
        await _context.SaveChangesAsync();
    }

    public async Task<int> ExpireQuestInstancesAsync(long uid, string nowStr)
    {
        var stale = await _context.UserQuestInstances
            .Where(q => q.uid == uid
                && q.status != "Expired"
                && q.status != "Claimed"
                && q.expiresAtUtc != ""
                && string.Compare(q.expiresAtUtc, nowStr) < 0)
            .ToListAsync();
        if (stale.Count == 0)
        {
            return 0;
        }
        foreach (var s in stale)
        {
            s.status = "Expired";
            s.lastUpdatedUtc = nowStr;
        }
        await _context.SaveChangesAsync();
        return stale.Count;
    }

    public async Task<string> GetLastDailyBundleClaimedDateAsync(long uid)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.uid == uid);
        return user?.lastDailyBundleClaimedDateUtc ?? "";
    }

    public async Task ClearDailyBundleClaimedDateAsync(long uid)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.uid == uid);
        if (user == null || user.lastDailyBundleClaimedDateUtc == "")
        {
            return;
        }
        user.lastDailyBundleClaimedDateUtc = "";
        await _context.SaveChangesAsync();
    }

    public async Task ClaimDailyBundleTransactionAsync(
        long uid,
        string claimedDateUtc,
        IReadOnlyList<(int currencyTypeId, long delta)> currencyDeltas)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.uid == uid);
        if (user == null)
        {
            // user 미발견은 호출 직전 단계에서 차단됨 — 도달 시 데이터 결함.
            throw new InvalidOperationException($"[GameDB] ClaimDailyBundleTransactionAsync: uid={uid} 미발견.");
        }
        user.lastDailyBundleClaimedDateUtc = claimedDateUtc;

        if (currencyDeltas != null && currencyDeltas.Count > 0)
        {
            // currencyType별 row 1회 조회 — 같은 type 중복 호출 시 호출자가 미리 sum 책임. 본 메서드는 그대로 누적.
            for (int i = 0; i < currencyDeltas.Count; i++)
            {
                var (currencyTypeId, delta) = currencyDeltas[i];
                if (currencyTypeId < 0 || delta == 0)
                {
                    continue;
                }
                var row = await _context.UserCurrencies
                    .FirstOrDefaultAsync(c => c.uid == uid && c.currencyType == currencyTypeId);
                if (row == null)
                {
                    _context.UserCurrencies.Add(new GameUserCurrency
                    {
                        uid = uid,
                        currencyType = currencyTypeId,
                        amount = delta,
                    });
                }
                else
                {
                    row.amount += delta;
                }
            }
        }

        // 단일 SaveChanges = EF Core implicit transaction. UPDATE/INSERT 모두 atomic commit/rollback.
        // 부분 실패 시 통화만 누적되고 lastDailyBundleClaimedDateUtc 미갱신으로 재호출 시 중복 지급되는 사고 차단.
        await _context.SaveChangesAsync();
    }

    // ===== Quest 멱등 dedup =====

    public async Task<HashSet<string>> GetAppliedEventClientIdsAsync(long uid, IReadOnlyList<string> eventClientIds)
    {
        if (eventClientIds == null || eventClientIds.Count == 0)
        {
            return new HashSet<string>();
        }
        var ids = eventClientIds.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new HashSet<string>();
        }
        var found = await _context.UserQuestEventsApplied
            .Where(e => e.uid == uid && ids.Contains(e.eventClientId))
            .Select(e => e.eventClientId)
            .ToListAsync();
        return new HashSet<string>(found);
    }

    public async Task ApplyQuestEventBatchAsync(
        List<GameUserQuestEventApplied> applieds,
        List<GameUserQuestInstance> instances)
    {
        bool anyApplied = applieds != null && applieds.Count > 0;
        bool anyInstance = instances != null && instances.Count > 0;
        if (!anyApplied && !anyInstance)
        {
            return;
        }
        if (anyApplied)
        {
            _context.UserQuestEventsApplied.AddRange(applieds!);
        }
        if (anyInstance)
        {
            _context.UserQuestInstances.UpdateRange(instances!);
        }
        // 단일 SaveChanges = EF Core implicit transaction. INSERT + UPDATE 모두 atomic commit/rollback.
        // unique 가드 위반(race) 시 DbUpdateException — 전체 rollback 후 호출자에게 throw.
        await _context.SaveChangesAsync();
    }

    public async Task<int> GcQuestEventAppliedAsync(string sinceStr)
    {
        var stale = await _context.UserQuestEventsApplied
            .Where(e => string.Compare(e.appliedAtUtc, sinceStr) < 0)
            .ToListAsync();
        if (stale.Count == 0)
        {
            return 0;
        }
        _context.UserQuestEventsApplied.RemoveRange(stale);
        await _context.SaveChangesAsync();
        return stale.Count;
    }


    // ===== 튜토리얼 =====

    public async Task<string> GetCompletedTutorialsJsonAsync(long uid)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.uid == uid);
        return user?.completedTutorialsJson ?? "[]";
    }

    public async Task<bool> AddCompletedTutorialAsync(long uid, string tutorialTagName)
    {
        if (string.IsNullOrEmpty(tutorialTagName))
        {
            return false;
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.uid == uid);
        if (user == null)
        {
            // 인증 미들웨어 통과 후라 도달 시 데이터 결함.
            throw new InvalidOperationException($"[GameDB] AddCompletedTutorialAsync: uid={uid} 미발견.");
        }

        var current = DeserializeTutorialList(user.completedTutorialsJson);
        if (current.Contains(tutorialTagName))
        {
            return false;
        }

        current.Add(tutorialTagName);
        user.completedTutorialsJson = SerializeTutorialList(current);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task ClearCompletedTutorialsAsync(long uid)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.uid == uid);
        if (user == null || user.completedTutorialsJson == "[]" || string.IsNullOrEmpty(user.completedTutorialsJson))
        {
            return;
        }
        user.completedTutorialsJson = "[]";
        await _context.SaveChangesAsync();
    }

    private static List<string> DeserializeTutorialList(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new List<string>();
        }
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static string SerializeTutorialList(List<string> list)
    {
        return System.Text.Json.JsonSerializer.Serialize(list);
    }
}
