using System.Collections.Generic;

namespace HGame01Server.Models.GameData;

// 이 파일은 Admin/UploadGameData API에 의해 자동 생성되었습니다.
// 직접 수정하지 마세요. Unity에서 데이터를 다시 업로드하면 덮어씌워집니다.

public class GdbMailRewardSequenceMapData
{
    public int id { get; set; }
    public string tag { get; set; } = "";
    public List<EntriesEntry> entries { get; set; } = new();

    public class EntriesEntry
    {
        public string kind { get; set; } = "";
        public Sequence sequence { get; set; }

        public class Sequence
        {
            public List<StepsEntry> steps { get; set; } = new();

            public class StepsEntry
            {
                public float delay_before_apply { get; set; }
                public string mode { get; set; } = "";
            }
        }
    }
}
