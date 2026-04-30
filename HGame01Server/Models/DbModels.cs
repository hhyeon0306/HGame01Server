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
    /// 캐릭터 식별자 — 클라 GameplayTag 이름과 동일 (예: "Tag.Character.Player").
    public string characterTag { get; set; } = "";
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
    /// 장착된 캐릭터의 식별 태그 — 미장착 시 빈 문자열.
    public string equippedCharacterTag { get; set; } = "";
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
    public string shopItemId { get; set; } = "";
    public string purchasedAt { get; set; } = "";
}

// ============================================================
// 우편함 테이블
// ============================================================

[Table("user_mails")]
public class GameUserMail
{
    /// GUID 문자열 (Guid.NewGuid().ToString("N")). 외부 노출 PK.
    [Key]
    public string mailId { get; set; } = "";

    public long uid { get; set; }
    public string titleKey { get; set; } = "";

    /// 메일 발송 사유 분류. 클라 EMailKind enum 문자열 (예: "CashPurchase"). 빈 값은 "Generic" fallback.
    /// 받기 시점 클라가 RewardSequence 매핑 SO 룩업 키로 사용.
    public string mailKind { get; set; } = "Generic";

    /// JSON 직렬화된 List<MailRewardEntry>. MailService 가 양방향 변환 담당.
    public string rewardsJson { get; set; } = "[]";

    /// 셀 아이콘 atlas 이름. "ShopProductAtlas" / "ItemAtlas" / 빈문자열(보상 itemTag로 자동).
    public string iconAtlas { get; set; } = "";
    public string iconKey { get; set; } = "";

    /// "System" / "Event" / "CS" / "Compensation"
    public string senderType { get; set; } = "";

    /// 모두 yyyy-MM-dd HH:mm:ss UTC. claimedAt 빈문자열 = 미수령. 다른 테이블과 동일 컨벤션.
    public string sentAt { get; set; } = "";
    public string expireAt { get; set; } = "";
    public string claimedAt { get; set; } = "";
}

/// 메일 보상 1단위. rewardsJson 직렬화 대상.
public class MailRewardEntry
{
    public string itemTag { get; set; } = "";
    public int count { get; set; }
}
