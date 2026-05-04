using HGame01Server.Models;
using HGame01Server.Models.GameData;
using HGame01Server.Repository;

namespace HGame01Server.Services;

/// 장비 장착/해제/판매 비즈니스 로직.
public class EquipmentService
{
    private readonly GameDbContext _context;
    private readonly IGameDB _gameDB;
    private readonly CurrencyService _currencyService;
    private readonly GameDataManager _gameDataManager;

    public EquipmentService(GameDbContext context, IGameDB gameDB, CurrencyService currencyService, GameDataManager gameDataManager)
    {
        _context = context;
        _gameDB = gameDB;
        _currencyService = currencyService;
        _gameDataManager = gameDataManager;
    }

    /// 장비 장착. 같은 슬롯의 기존 장비는 자동 해제.
    public async Task<(ErrorCode error, List<PkUserEquipment> equipments)> EquipAsync(long uid, long equipmentDbId, string characterTag)
    {
        // 1. 장비 소유 확인
        var equipment = await _gameDB.GetEquipmentByIdAsync(uid, equipmentDbId);
        if (equipment == null)
        {
            return (ErrorCode.EquipmentNotOwned, new());
        }

        // 2. 캐릭터 소유 확인
        var characters = await _gameDB.GetCharactersByUidAsync(uid);
        if (!characters.Any(c => c.characterTag == characterTag))
        {
            return (ErrorCode.CharacterNotOwned, new());
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 3. 해당 캐릭터의 같은 슬롯에 이미 장착된 장비 해제
            await _gameDB.UnequipSlotAsync(uid, characterTag, equipment.slot);

            // 4. 새 장비 장착
            equipment.isEquipped = true;
            equipment.equippedCharacterTag = characterTag;
            await _gameDB.UpdateEquipmentAsync(equipment);

            await transaction.CommitAsync();

            // 5. 갱신된 전체 장비 목록 반환
            var allEquipments = await GetEquipmentListAsync(uid);
            return (ErrorCode.None, allEquipments);
        }
        catch (Exception ex)
        {
            // TODO: ILogger 주입 후 로깅 추가
            _ = ex;
            await transaction.RollbackAsync();
            return (ErrorCode.EquipmentEquipFailed, new());
        }
    }

    /// 장비 해제.
    public async Task<(ErrorCode error, List<PkUserEquipment> equipments)> UnequipAsync(long uid, long equipmentDbId)
    {
        // 1. 장비 소유 확인
        var equipment = await _gameDB.GetEquipmentByIdAsync(uid, equipmentDbId);
        if (equipment == null)
        {
            return (ErrorCode.EquipmentNotOwned, new());
        }

        if (!equipment.isEquipped)
        {
            return (ErrorCode.EquipmentNotFound, new());
        }

        // 2. 장비 해제
        equipment.isEquipped = false;
        equipment.equippedCharacterTag = "";
        await _gameDB.UpdateEquipmentAsync(equipment);

        // 3. 갱신된 전체 장비 목록 반환
        var allEquipments = await GetEquipmentListAsync(uid);
        return (ErrorCode.None, allEquipments);
    }

    /// 장비 다건 판매.
    /// - 장착 중인 장비가 하나라도 포함되면 전체 거절 (EquipmentSellEquipped).
    /// - 본 user 미소유 dbId가 섞이면 EquipmentNotOwned로 거절 — 소유 검증 실패 시 부분 진행 금지.
    /// - 골드 합산은 GdbEquipmentData.sell_price (서버 SSoT) 기반. 클라 변조 차단.
    public async Task<(ErrorCode error, List<PkUserEquipment> equipments, List<PkCurrency> currencies, long soldGold, int soldCount)> SellAsync(long uid, List<long> equipmentDbIds)
    {
        if (equipmentDbIds == null || equipmentDbIds.Count == 0)
        {
            return (ErrorCode.None, await GetEquipmentListAsync(uid), await _currencyService.GetAllAsync(uid), 0, 0);
        }

        var allUserEquipments = await _gameDB.GetEquipmentsByUidAsync(uid);
        var byId = allUserEquipments.ToDictionary(e => e.id);

        // 1. 소유/장착 검증 — 한 건이라도 깨지면 전체 거절.
        var targets = new List<GameUserEquipment>();
        foreach (var dbId in equipmentDbIds)
        {
            if (!byId.TryGetValue(dbId, out var equipment))
            {
                return (ErrorCode.EquipmentNotOwned, new(), new(), 0, 0);
            }
            if (equipment.isEquipped)
            {
                return (ErrorCode.EquipmentSellEquipped, new(), new(), 0, 0);
            }
            targets.Add(equipment);
        }

        // 2. 골드 합산 — 카탈로그(GdbEquipmentData)의 sell_price를 신뢰.
        long totalGold = 0;
        var catalog = _gameDataManager.GetList<GdbEquipmentData>();
        foreach (var t in targets)
        {
            var meta = catalog?.FirstOrDefault(c => c.tag == t.equipmentTag);
            if (meta != null)
            {
                totalGold += meta.sell_price;
            }
        }

        // 3. 트랜잭션: DELETE → 골드 추가. 한쪽 실패면 롤백.
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            int deleted = await _gameDB.RemoveEquipmentsBatchAsync(uid, equipmentDbIds);
            if (deleted != targets.Count)
            {
                await transaction.RollbackAsync();
                return (ErrorCode.EquipmentSellFailed, new(), new(), 0, 0);
            }

            if (totalGold > 0)
            {
                var addError = await _currencyService.AddAsync(uid, CurrencyType.Gold, totalGold);
                if (addError != ErrorCode.None)
                {
                    await transaction.RollbackAsync();
                    return (addError, new(), new(), 0, 0);
                }
            }

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            _ = ex;
            await transaction.RollbackAsync();
            return (ErrorCode.EquipmentSellFailed, new(), new(), 0, 0);
        }

        // 4. 갱신된 전체 장비 + 재화 반환 — 클라가 두 store 일괄 갱신.
        var equipments = await GetEquipmentListAsync(uid);
        var currencies = await _currencyService.GetAllAsync(uid);
        return (ErrorCode.None, equipments, currencies, totalGold, targets.Count);
    }

    /// 자동 장착(다건 일괄). 슬롯 중복 거절 + 본 user 소유 검증 + 트랜잭션 원자성.
    /// - 클라가 슬롯별 최강 1개씩 결정한 dbId 목록을 받아 한 번에 장착.
    /// - 같은 슬롯에 이미 장착된 다른 장비는 자동 해제 (Equip 단건과 동일 룰).
    /// - 단일 슬롯 중복(동일 슬롯 dbId 2건) 입력은 EquipmentEquipFailed로 거절.
    public async Task<(ErrorCode error, List<PkUserEquipment> equipments, int equippedCount)> AutoEquipAsync(long uid, List<long> equipmentDbIds, string characterTag)
    {
        if (equipmentDbIds == null || equipmentDbIds.Count == 0)
        {
            return (ErrorCode.None, await GetEquipmentListAsync(uid), 0);
        }

        // 1. 캐릭터 소유 확인
        var characters = await _gameDB.GetCharactersByUidAsync(uid);
        if (!characters.Any(c => c.characterTag == characterTag))
        {
            return (ErrorCode.CharacterNotOwned, new(), 0);
        }

        // 2. 장비 소유 + 슬롯 중복 검증 — 한 건이라도 깨지면 전체 거절.
        var allUserEquipments = await _gameDB.GetEquipmentsByUidAsync(uid);
        var byId = allUserEquipments.ToDictionary(e => e.id);

        var targets = new List<GameUserEquipment>();
        var seenSlots = new HashSet<int>();
        foreach (var dbId in equipmentDbIds)
        {
            if (!byId.TryGetValue(dbId, out var equipment))
            {
                return (ErrorCode.EquipmentNotOwned, new(), 0);
            }
            if (!seenSlots.Add(equipment.slot))
            {
                return (ErrorCode.EquipmentEquipFailed, new(), 0);
            }
            targets.Add(equipment);
        }

        // 3. 트랜잭션: 슬롯별 unequip → 새 장비 장착. 한쪽 실패면 롤백.
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            foreach (var t in targets)
            {
                await _gameDB.UnequipSlotAsync(uid, characterTag, t.slot);
            }

            foreach (var t in targets)
            {
                t.isEquipped = true;
                t.equippedCharacterTag = characterTag;
                await _gameDB.UpdateEquipmentAsync(t);
            }

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            _ = ex;
            await transaction.RollbackAsync();
            return (ErrorCode.EquipmentEquipFailed, new(), 0);
        }

        var allEquipments = await GetEquipmentListAsync(uid);
        return (ErrorCode.None, allEquipments, targets.Count);
    }

    /// DB 장비 목록을 패킷 모델로 변환.
    public async Task<List<PkUserEquipment>> GetEquipmentListAsync(long uid)
    {
        var dbEquipments = await _gameDB.GetEquipmentsByUidAsync(uid);
        return MapToPacket(dbEquipments);
    }

    /// DB 장비 엔티티 리스트를 패킷 모델로 매핑.
    public static List<PkUserEquipment> MapToPacket(List<GameUserEquipment> dbEquipments)
    {
        return dbEquipments.Select(e => new PkUserEquipment
        {
            DbId = e.id,
            EquipmentTag = e.equipmentTag,
            Slot = e.slot,
            IsEquipped = e.isEquipped,
            EquippedCharacterTag = e.equippedCharacterTag,
            AcquiredAt = e.acquiredAt
        }).ToList();
    }
}
