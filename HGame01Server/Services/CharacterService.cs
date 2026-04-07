using HGame01Server.Models;
using HGame01Server.Models.GameData;
using HGame01Server.Repository;

namespace HGame01Server.Services;

/// 캐릭터 비즈니스 로직.
public class CharacterService
{
    private readonly IGameDB _gameDB;
    private readonly GameDataManager _gameDataManager;

    public CharacterService(IGameDB gameDB, GameDataManager gameDataManager)
    {
        _gameDB = gameDB;
        _gameDataManager = gameDataManager;
    }

    /// 기본 캐릭터 지급. Constants에서 TID를 조회하여 지급.
    public async Task GrantDefaultAsync(long uid, string createdAt)
    {
        int defaultCharacterId = _gameDataManager.GetConstInt(GdbConst.Character.Category, GdbConst.Character.DefaultCharacterId, 101);

        var character = new GameUserCharacter
        {
            uid = uid,
            characterId = defaultCharacterId,
            isActive = true,
            acquiredAt = createdAt
        };
        await _gameDB.AddCharacterAsync(character);
    }

    /// uid로 소유 캐릭터 목록 조회.
    public Task<List<GameUserCharacter>> GetByUidAsync(long uid)
    {
        return _gameDB.GetCharactersByUidAsync(uid);
    }
}
