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

    public async Task<bool> DeductCurrencyAsync(long uid, int currencyType, long amount)
    {
        var currency = await GetCurrencyAsync(uid, currencyType);
        if (currency == null || currency.amount < amount)
        {
            return false;
        }

        currency.amount -= amount;
        await _context.SaveChangesAsync();
        return true;
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
}
