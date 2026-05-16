using HGame01Server.Models;
using HGame01Server.Repository;
using Microsoft.EntityFrameworkCore;

namespace HGame01Server.Services;

/// 장비 보관함 비즈니스 로직.
/// - 현재는 장비(user_equipments)만 카운트하지만, 미래에 다른 보관 row가 추가되면 GetStorageCountAsync 내부만 합산하면 된다.
/// - 다른 보관 타입(BattleItem, Pet 등)은 별도 service/DB로 분리한다 (정책: feedback_inventory_separation.md).
/// - 가챠 진입 시 IsFullAsync로 차단, ExpandCapacityAsync로 다이아 100당 +5칸 확장.
public class EquipmentStorageService
{
    /// 확장 1회 비용(다이아).
    public const int ExpandCostDiamond = 100;
    /// 확장 1회당 늘어나는 칸 수.
    public const int ExpandSlotsPerPurchase = 5;

    private readonly GameDbContext _context;
    private readonly CurrencyService _currencyService;
    private readonly ILogger<EquipmentStorageService> _logger;

    public EquipmentStorageService(GameDbContext context, CurrencyService currencyService, ILogger<EquipmentStorageService> logger)
    {
        _context = context;
        _currencyService = currencyService;
        _logger = logger;
    }


    // ===== 조회 =====

    /// 장비 보관함 최대 칸 수. 유저 미존재 시 0.
    public async Task<int> GetCapacityAsync(long uid)
    {
        return await _context.Users
            .Where(u => u.uid == uid)
            .Select(u => u.equipmentStorageCapacity)
            .FirstOrDefaultAsync();
    }

    /// 장비 보관함 현재 사용 중 칸 수. 현재는 장비 row 수.
    public async Task<int> GetStorageCountAsync(long uid)
    {
        return await _context.UserEquipments.CountAsync(e => e.uid == uid);
    }

    /// 풀 상태 여부. 가챠 진입 시 차단 조건.
    public async Task<bool> IsFullAsync(long uid)
    {
        int capacity = await GetCapacityAsync(uid);
        int count = await GetStorageCountAsync(uid);
        return count >= capacity;
    }


    // ===== 확장 =====

    /// 다이아 차감 + capacity 증가 (트랜잭션 묶음).
    /// 성공 시 갱신된 재화 스냅샷을 응답에 동봉한다 (currency-contract).
    public async Task<PkExpandCapacityResponse> ExpandCapacityAsync(long uid)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var diamondError = await _currencyService.DeductAsync(uid, CurrencyType.Diamond, ExpandCostDiamond);
            if (diamondError != ErrorCode.None)
            {
                return new PkExpandCapacityResponse { Result = ErrorCode.CurrencyInsufficientAmount };
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.uid == uid);
            if (user == null)
            {
                await transaction.RollbackAsync();
                return new PkExpandCapacityResponse { Result = ErrorCode.LoginFailUserNotExist };
            }

            user.equipmentStorageCapacity += ExpandSlotsPerPurchase;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("[EquipmentStorageService] Expand uid={Uid} newCapacity={Capacity}", uid, user.equipmentStorageCapacity);

            // 성공 시에만 재화 스냅샷 동봉 (클라 store 다이아 차감 즉시 반영용).
            var response = new PkExpandCapacityResponse
            {
                Result = ErrorCode.None,
                NewCapacity = user.equipmentStorageCapacity,
            };
            await _currencyService.PopulateCurrenciesAsync(response, uid);
            return response;
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            _logger.LogError(e, "[EquipmentStorageService] ExpandCapacity 예외");
            return new PkExpandCapacityResponse { Result = ErrorCode.EquipmentStorageExpandFailed };
        }
    }
}
