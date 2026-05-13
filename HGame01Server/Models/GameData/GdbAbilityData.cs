using System.Collections.Generic;

namespace HGame01Server.Models.GameData;

// 이 파일은 Admin/UploadGameData API에 의해 자동 생성되었습니다.
// 직접 수정하지 마세요. Unity에서 데이터를 다시 업로드하면 덮어씌워집니다.

public class GdbAbilityData
{
    public int id { get; set; }
    public string tag { get; set; } = "";
    public string name_key { get; set; } = "";
    public string description_key { get; set; } = "";
    public string icon_sprite { get; set; } = "";
    public string grade { get; set; } = "";
    public int max_pick_count { get; set; }
    public List<BehaviorsEntry> behaviors { get; set; } = new();

    public class BehaviorsEntry
    {
        public string effect_ref { get; set; } = "";
        public float duration { get; set; }
        public float period { get; set; }
        public float base_magnitude { get; set; }
    }
}
