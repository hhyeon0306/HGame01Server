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
    public string reward_item { get; set; } = "";
    public int reward_count { get; set; }
    public int required_count { get; set; }
}
