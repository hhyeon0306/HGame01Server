namespace HGame01Server.Models.GameData;

// 이 파일은 Admin/UploadGameData API에 의해 자동 생성되었습니다.
// 직접 수정하지 마세요. Unity에서 데이터를 다시 업로드하면 덮어씌워집니다.

public class GdbProjectileData
{
    public int id { get; set; }
    public string tag { get; set; } = "";
    public string movement_type { get; set; } = "";
    public float max_distance { get; set; }
    public float lifetime { get; set; }
    public float gravity { get; set; }
    public float launch_angle { get; set; }
    public float homing_turn_speed { get; set; }
    public float initial_arc_angle { get; set; }
    public float target_height_offset { get; set; }
}
