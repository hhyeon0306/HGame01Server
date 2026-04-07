using HGame01Server.Models;
using HGame01Server.Repository;

namespace HGame01Server.Services;

/// 장비 장착/해제 비즈니스 로직.
public class EquipmentService
{
    private readonly GameDbContext _context;
    private readonly IGameDB _gameDB;

    public EquipmentService(GameDbContext context, IGameDB gameDB)
    {
        _context = context;
        _gameDB = gameDB;
    }

    /// 장비 장착. 같은 슬롯의 기존 장비는 자동 해제.
    public async Task<(ErrorCode error, List<PkUserEquipment> equipments)> EquipAsync(long uid, long equipmentDbId, int characterId)
    {
        // 1. 장비 소유 확인
        var equipment = await _gameDB.GetEquipmentByIdAsync(uid, equipmentDbId);
        if (equipment == null)
        {
            return (ErrorCode.EquipmentNotOwned, new());
        }

        // 2. 캐릭터 소유 확인
        var characters = await _gameDB.GetCharactersByUidAsync(uid);
        if (!characters.Any(c => c.characterId == characterId))
        {
            return (ErrorCode.CharacterNotOwned, new());
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 3. 해당 캐릭터의 같은 슬롯에 이미 장착된 장비 해제
            await _gameDB.UnequipSlotAsync(uid, characterId, equipment.slot);

            // 4. 새 장비 장착
            equipment.isEquipped = true;
            equipment.equippedCharacterId = characterId;
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
        equipment.equippedCharacterId = 0;
        await _gameDB.UpdateEquipmentAsync(equipment);

        // 3. 갱신된 전체 장비 목록 반환
        var allEquipments = await GetEquipmentListAsync(uid);
        return (ErrorCode.None, allEquipments);
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
            Id = e.id,
            EquipmentId = e.equipmentId,
            Slot = e.slot,
            IsEquipped = e.isEquipped,
            EquippedCharacterId = e.equippedCharacterId,
            AcquiredAt = e.acquiredAt
        }).ToList();
    }
}
