using System.Collections.Generic;

namespace HGame01Server.Models.GameData;

// 이 파일은 Admin/UploadGameData API에 의해 자동 생성되었습니다.
// 직접 수정하지 마세요. Unity에서 데이터를 다시 업로드하면 덮어씌워집니다.

public class GdbChapterData
{
    public int id { get; set; }
    public string tag { get; set; } = "";
    public string game_mode_tag { get; set; } = "";
    public string field_tag { get; set; } = "";
    public int chapter_index { get; set; }
    public int stage_count { get; set; }
    public List<PhasesEntry> phases { get; set; } = new();
    public string boss_tag { get; set; } = "";
    public StatMultiplierCurve stat_multiplier_curve { get; set; }
    public List<string> drop_table { get; set; } = new();
    public float drop_chance { get; set; }

    public class PhasesEntry
    {
        public string enemy_tag { get; set; } = "";
        public int total_count { get; set; }
        public int sub_wave_count { get; set; }
    }

    public class StatMultiplierCurve
    {
    }
}
