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

    /// 기본 캐릭터 지급. Constants에서 식별 태그를 조회하여 지급.
    public async Task GrantDefaultAsync(long uid, string createdAt)
    {
        // GdbConst.Character는 CharacterConstants SO 업로드 후 자동 생성되므로
        // 부트스트랩 안전을 위해 카테고리/키를 리터럴로 둔다 (값은 SO에서 데이터 주도).
        string defaultCharacterTag = _gameDataManager.GetConstString("character", "defaultCharacterTag", "Tag.Character.Player");

        var character = new GameUserCharacter
        {
            uid = uid,
            characterTag = defaultCharacterTag,
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
