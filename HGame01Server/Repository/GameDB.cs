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

    public async Task UnequipSlotAsync(long uid, int characterId, int slot)
    {
        var equipped = await _context.UserEquipments
            .Where(e => e.uid == uid && e.equippedCharacterId == characterId && e.slot == slot && e.isEquipped)
            .ToListAsync();

        foreach (var item in equipped)
        {
            item.isEquipped = false;
            item.equippedCharacterId = 0;
        }

        await _context.SaveChangesAsync();
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
}
