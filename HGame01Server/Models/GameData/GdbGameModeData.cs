using System.Collections.Generic;

namespace HGame01Server.Models.GameData;

// 이 파일은 Admin/UploadGameData API에 의해 자동 생성되었습니다.
// 직접 수정하지 마세요. Unity에서 데이터를 다시 업로드하면 덮어씌워집니다.

public class GdbGameModeData
{
    public int id { get; set; }
    public string tag { get; set; } = "";
    public ModeConfig mode_config { get; set; }
    public List<FeaturesEntry> features { get; set; } = new();

    public class ModeConfig
    {
        public string dummy_data { get; set; } = "";
        public float respawn_delay { get; set; }
    }

    public class FeaturesEntry
    {
        public Config config { get; set; }

        public class Config
        {
            public int base_max_a_p { get; set; }
            public int ap_scale_per_level { get; set; }
            public int ap_per_kill { get; set; }
        }
    }
}
