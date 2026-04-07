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
    public string profileId { get; set; } = "";
    public string name { get; set; } = "";
    public string createdAt { get; set; } = "";
}

// ============================================================
// 유저 캐릭터 소유 테이블
// ============================================================

[Table("user_characters")]
public class GameUserCharacter
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long id { get; set; }
    public long uid { get; set; }
    public int characterId { get; set; }
    public bool isActive { get; set; }
    public string acquiredAt { get; set; } = "";
}

// ============================================================
// 유저 재화 테이블
// ============================================================

[Table("user_currencies")]
public class GameUserCurrency
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long id { get; set; }
    public long uid { get; set; }
    public int currencyType { get; set; }
    public long amount { get; set; }
}

// ============================================================
// 유저 장비 소유 테이블
// ============================================================

[Table("user_equipments")]
public class GameUserEquipment
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long id { get; set; }
    public long uid { get; set; }
    public int equipmentId { get; set; }
    public int slot { get; set; }
    public bool isEquipped { get; set; }
    public int equippedCharacterId { get; set; }
    public string acquiredAt { get; set; } = "";
}

// ============================================================
// 상점 구매 기록 테이블
// ============================================================

[Table("user_shop_purchases")]
public class GameUserShopPurchase
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long id { get; set; }
    public long uid { get; set; }
    public int shopItemId { get; set; }
    public string purchasedAt { get; set; } = "";
}
