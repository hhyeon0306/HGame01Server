using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HGame01Server.Models;

// ============================================================
// 설정
// ============================================================

public class DbConfig
{
    public string GameDB { get; set; } = "";
    public string Redis { get; set; } = "";
}

// ============================================================
// 유저 테이블 모델
// ============================================================

[Table("users")]
public class GameUser
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long uid { get; set; }
    public string id { get; set; } = "";
    public string pw { get; set; } = "";
}
