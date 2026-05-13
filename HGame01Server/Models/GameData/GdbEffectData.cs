using System.Collections.Generic;

namespace HGame01Server.Models.GameData;

// 이 파일은 Admin/UploadGameData API에 의해 자동 생성되었습니다.
// 직접 수정하지 마세요. Unity에서 데이터를 다시 업로드하면 덮어씌워집니다.

public class GdbEffectData
{
    public int id { get; set; }
    public string tag { get; set; } = "";
    public GrantsTags grants_tags { get; set; }
    public string category { get; set; } = "";
    public string icon_name { get; set; } = "";
    public string duration_policy { get; set; } = "";
    public List<ModifiersEntry> modifiers { get; set; } = new();
    public List<ModulesEntry> modules { get; set; } = new();

    public class GrantsTags
    {
        public List<object> tags { get; set; } = new();
    }

    public class ModifiersEntry
    {
        public string attribute { get; set; } = "";
        public string apply_time { get; set; } = "";
        public string duration_type { get; set; } = "";
        public string operation { get; set; } = "";
        public float magnitude { get; set; }
        public object magnitude_calculation { get; set; }
    }

    public class ModulesEntry
    {
    }
}
