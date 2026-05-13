using System.Collections.Generic;

namespace HGame01Server.Models.GameData;

// 이 파일은 Admin/UploadGameData API에 의해 자동 생성되었습니다.
// 직접 수정하지 마세요. Unity에서 데이터를 다시 업로드하면 덮어씌워집니다.

public class GdbTutorialData
{
    public int id { get; set; }
    public string tag { get; set; } = "";
    public Trigger trigger { get; set; }
    public List<object> conditions { get; set; } = new();
    public List<StepsEntry> steps { get; set; } = new();

    public class Trigger
    {
        public string scene_tag { get; set; } = "";
    }

    public class StepsEntry
    {
        public string dialog_tag { get; set; } = "";
    }
}
