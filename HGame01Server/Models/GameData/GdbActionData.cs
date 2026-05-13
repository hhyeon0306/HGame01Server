using System.Collections.Generic;

namespace HGame01Server.Models.GameData;

// 이 파일은 Admin/UploadGameData API에 의해 자동 생성되었습니다.
// 직접 수정하지 마세요. Unity에서 데이터를 다시 업로드하면 덮어씌워집니다.

public class GdbActionData
{
    public int id { get; set; }
    public string tag { get; set; } = "";
    public string icon_name { get; set; } = "";
    public float cooldown_time { get; set; }
    public bool auto_start { get; set; }
    public string input_mode { get; set; } = "";
    public bool bypass_cannot_act { get; set; }
    public GrantsTags grants_tags { get; set; }
    public BlockedTags blocked_tags { get; set; }
    public RequiredTags required_tags { get; set; }
    public object cost_attribute { get; set; }
    public float cost_amount { get; set; }
    public float range { get; set; }
    public string target_mode { get; set; } = "";
    public List<EffectsEntry> effects { get; set; } = new();
    public int priority { get; set; }
    public CancelTags cancel_tags { get; set; }
    public bool can_be_cancelled { get; set; }
    public float interrupt_cooldown_rate { get; set; }
    public ActionTemplate action_template { get; set; }
    public object extension { get; set; }

    public class GrantsTags
    {
        public List<object> tags { get; set; } = new();
    }

    public class BlockedTags
    {
        public List<object> tags { get; set; } = new();
    }

    public class RequiredTags
    {
        public List<object> tags { get; set; } = new();
    }

    public class EffectsEntry
    {
        public string effect_data { get; set; } = "";
        public string delivery { get; set; } = "";
        public float duration { get; set; }
        public float period { get; set; }
        public string cue_tag { get; set; } = "";
        public float base_magnitude { get; set; }
        public string feedback { get; set; } = "";
    }

    public class CancelTags
    {
        public List<object> tags { get; set; } = new();
    }

    public class ActionTemplate
    {
    }
}
