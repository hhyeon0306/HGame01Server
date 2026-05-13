using System.Collections.Generic;

namespace HGame01Server.Models.GameData;

// 이 파일은 Admin/UploadGameData API에 의해 자동 생성되었습니다.
// 직접 수정하지 마세요. Unity에서 데이터를 다시 업로드하면 덮어씌워집니다.

public class GdbQuestData
{
    public int id { get; set; }
    public string tag { get; set; } = "";
    public string quest_tag { get; set; } = "";
    public string title_key { get; set; } = "";
    public string description_key { get; set; } = "";
    public string icon_name { get; set; } = "";
    public Condition condition { get; set; }
    public RewardSequence reward_sequence { get; set; }
    public string reward_item { get; set; } = "";
    public int reward_count { get; set; }
    public int required_count { get; set; }

    public class Condition
    {
        public object stage_tag { get; set; }
        public int min_difficulty { get; set; }
        public int required_count { get; set; }
    }

    public class RewardSequence
    {
        public List<StepsEntry> steps { get; set; } = new();

        public class StepsEntry
        {
            public int count { get; set; }
            public float start_jitter_radius { get; set; }
            public float spread { get; set; }
            public float arc_height { get; set; }
            public float per_coin_duration { get; set; }
            public float spawn_interval { get; set; }
            public string mode { get; set; } = "";
        }
    }
}
