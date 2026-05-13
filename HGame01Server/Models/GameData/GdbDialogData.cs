using System.Collections.Generic;

namespace HGame01Server.Models.GameData;

// 이 파일은 Admin/UploadGameData API에 의해 자동 생성되었습니다.
// 직접 수정하지 마세요. Unity에서 데이터를 다시 업로드하면 덮어씌워집니다.

public class GdbDialogData
{
    public int id { get; set; }
    public string tag { get; set; } = "";
    public List<EntriesEntry> entries { get; set; } = new();

    public class EntriesEntry
    {
        public string npc_name_key { get; set; } = "";
        public string portrait_sprite_key { get; set; } = "";
        public string text_key { get; set; } = "";
    }
}
