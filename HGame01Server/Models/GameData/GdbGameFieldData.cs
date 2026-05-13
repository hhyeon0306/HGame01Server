using System.Collections.Generic;

namespace HGame01Server.Models.GameData;

// 이 파일은 Admin/UploadGameData API에 의해 자동 생성되었습니다.
// 직접 수정하지 마세요. Unity에서 데이터를 다시 업로드하면 덮어씌워집니다.

public class GdbGameFieldData
{
    public int id { get; set; }
    public string tag { get; set; } = "";
    public MapPrefabRef map_prefab_ref { get; set; }
    public BgmRef bgm_ref { get; set; }

    public class MapPrefabRef
    {
        public string m__asset_g_u_i_d { get; set; } = "";
    }

    public class BgmRef
    {
        public string m__asset_g_u_i_d { get; set; } = "";
        public string m__sub_object_name { get; set; } = "";
        public string m__sub_object_type { get; set; } = "";
        public string m__sub_object_g_u_i_d { get; set; } = "";
        public bool m__editor_asset_changed { get; set; }
    }
}
