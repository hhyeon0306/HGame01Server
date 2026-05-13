using System.Collections.Generic;

namespace HGame01Server.Models.GameData;

// 이 파일은 Admin/UploadGameData API에 의해 자동 생성되었습니다.
// 직접 수정하지 마세요. Unity에서 데이터를 다시 업로드하면 덮어씌워집니다.

public class GdbCharacterData
{
    public int id { get; set; }
    public string tag { get; set; } = "";
    public List<BaseAttributesEntry> base_attributes { get; set; } = new();
    public List<object> action_slots { get; set; } = new();
    public List<object> combos { get; set; } = new();
    public PrefabRef prefab_ref { get; set; }

    public class BaseAttributesEntry
    {
        public string attribute_tag { get; set; } = "";
        public float base_value { get; set; }
    }

    public class PrefabRef
    {
        public string m__asset_g_u_i_d { get; set; } = "";
    }
}
