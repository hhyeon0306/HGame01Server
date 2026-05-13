using System.Collections.Generic;

namespace HGame01Server.Models.GameData;

// 이 파일은 Admin/UploadGameData API에 의해 자동 생성되었습니다.
// 직접 수정하지 마세요. Unity에서 데이터를 다시 업로드하면 덮어씌워집니다.

public class GdbSeasonPassData
{
    public int id { get; set; }
    public string tag { get; set; } = "";
    public string season_name_key { get; set; } = "";
    public string start_utc { get; set; } = "";
    public string end_utc { get; set; } = "";
    public List<LevelRewardsEntry> level_rewards { get; set; } = new();

    public class LevelRewardsEntry
    {
        public int level { get; set; }
        public string basic_reward { get; set; } = "";
        public int basic_count { get; set; }
        public string premium_reward { get; set; } = "";
        public int premium_count { get; set; }
    }
}
