using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace HGame01Server.Models;

// ============================================================
// 공통 헤더
// ============================================================

public class HeaderDTO
{
    [FromHeader]
    public string ProfileId { get; set; } = "";
    [FromHeader]
    public string AuthToken { get; set; } = "";
}

// ============================================================
// 계정
// ============================================================

// POST api/Login
public class PkLoginRequest
{
    public string ProfileId { get; set; } = "";
}

public class PkLoginResponse
{
    public ErrorCode Result { get; set; }
    public string AuthToken { get; set; } = "";
}

// POST api/CreateAccount
public class PkCreateAccountRequest
{
    public string ProfileId { get; set; } = "";
    public string Name { get; set; } = "";
}

public class PkCreateAccountResponse
{
    public ErrorCode Result { get; set; }
    public string CreatedAt { get; set; } = "";
}

// POST api/UserInfo
public class PkUserInfoRequest
{
}

public class PkUserInfoResponse
{
    public ErrorCode Result { get; set; }
    public List<PkUserCharacter> Characters { get; set; } = new();
    public List<PkCurrency> Currencies { get; set; } = new();
    public List<PkUserEquipment> Equipments { get; set; } = new();
}

public class PkUserCharacter
{
    public int CharacterId { get; set; }
    public bool IsActive { get; set; }
    public string AcquiredAt { get; set; } = "";
}

// ============================================================
// 상점
// ============================================================

// POST api/Shop/DailyList, api/Shop/WeeklyList
public class PkShopListRequest { }

public class PkShopListResponse
{
    public ErrorCode Result { get; set; }
    public List<PkShopItemState> Items { get; set; } = new();
}

public class PkShopItemState
{
    public string ShopItemId { get; set; } = "";

    /// 오늘 누적 구매 횟수. dailyLimit 비교는 클라가 ShopData.dailyLimit과 매칭.
    public int PurchasedCount { get; set; }
}

// POST api/Shop/Buy
public class PkShopBuyRequest
{
    public string ShopItemId { get; set; } = "";
}

public class PkShopBuyResponse
{
    public ErrorCode Result { get; set; }
    public List<PkCurrency> Currencies { get; set; } = new();
    public PkRewardResult Reward { get; set; } = new();
}

// POST api/Shop/BuyDiamond — shopItemTag로 식별, 다이아 양은 서버가 GdbShopData lookup으로 결정 (가격 변조 방지)
public class PkBuyDiamondRequest
{
    public string ShopItemTag { get; set; } = "";
}

public class PkBuyDiamondResponse
{
    public ErrorCode Result { get; set; }
}

// POST api/Shop/CheatResetDaily — 치트 전용. 오늘 자 일일 구매 기록 삭제 → 셀 잔여 횟수 복구.
public class PkCheatResetDailyRequest
{
}

public class PkCheatResetDailyResponse
{
    public ErrorCode Result { get; set; }
    public int DeletedCount { get; set; }
}

// ============================================================
// 뽑기
// ============================================================

// POST api/Gacha/Pull
public class PkGachaPullRequest
{
    public int PullCount { get; set; }
}

public class PkGachaPullResponse
{
    public ErrorCode Result { get; set; }
    public List<PkGachaResultItem> Items { get; set; } = new();
    public List<PkCurrency> Currencies { get; set; } = new();

    /// 이번 뽑기로 획득한 신규 장비 인스턴스 (DB id 포함). 클라가 GetUserInfo 재호출 없이 Equipment Store를 갱신할 수 있도록 함.
    public List<PkUserEquipment> Equipments { get; set; } = new();
}

public class PkGachaResultItem
{
    public int EquipmentId { get; set; }
    public int Grade { get; set; }
}

// ============================================================
// 공통 (재화, 장비, 보상)
// ============================================================

public class PkCurrency
{
    public int CurrencyType { get; set; }
    public long Amount { get; set; }
}

public class PkUserEquipment
{
    /// DB 고유 식별자. 장착/해제 등 인스턴스 구분에 사용. 클라이언트의 dbId 필드와 1:1 매핑.
    public long DbId { get; set; }
    public int EquipmentId { get; set; }
    public int Slot { get; set; }
    public bool IsEquipped { get; set; }
    public int EquippedCharacterId { get; set; }
    public string AcquiredAt { get; set; } = "";
}

public class PkRewardResult
{
    public string ItemKind { get; set; } = "";       // "Currency" / "Equipment" / "BattleItem"
    public string ItemTag { get; set; } = "";        // 보상 ItemData tag
    public string CurrencyType { get; set; } = "";   // ItemKind=Currency 일 때 ("Diamond" / "Gold")
    public string EquipmentRef { get; set; } = "";   // ItemKind=Equipment 일 때 tag
    public string BattleItemRef { get; set; } = "";  // ItemKind=BattleItem 일 때 tag
    public int Count { get; set; }
}

// ============================================================
// 장비 장착/해제
// ============================================================

// POST api/Equipment/Equip
public class PkEquipRequest
{
    public long EquipmentDbId { get; set; }
    public int CharacterId { get; set; }
}

public class PkEquipResponse
{
    public ErrorCode Result { get; set; }
    public List<PkUserEquipment> Equipments { get; set; } = new();
}

// POST api/Equipment/Unequip
public class PkUnequipRequest
{
    public long EquipmentDbId { get; set; }
}

public class PkUnequipResponse
{
    public ErrorCode Result { get; set; }
    public List<PkUserEquipment> Equipments { get; set; } = new();
}

// ============================================================
// 우편함
// ============================================================

// POST api/Mail/List
public class PkMailListRequest { }

public class PkMailListResponse
{
    public ErrorCode Result { get; set; }
    public List<PkMailEntry> Mails { get; set; } = new();
}

public class PkMailEntry
{
    public string MailId { get; set; } = "";
    public string TitleKey { get; set; } = "";

    /// 메일 발송 사유. 클라 EMailKind enum 문자열 ("Generic" / "CashPurchase" 등). 빈 값은 클라가 Generic 으로 fallback.
    public string MailKind { get; set; } = "";

    public List<PkMailReward> Rewards { get; set; } = new();
    public string IconAtlas { get; set; } = "";
    public string IconKey { get; set; } = "";
    public string SentAt { get; set; } = "";
    public string ExpireAt { get; set; } = "";
    public string ClaimedAt { get; set; } = "";   // "" = 미수령
    public string SenderType { get; set; } = "";
}

public class PkMailReward
{
    public string ItemTag { get; set; } = "";
    public int Count { get; set; }
}

// POST api/Mail/Claim
public class PkMailClaimRequest
{
    public string MailId { get; set; } = "";
}

public class PkMailClaimResponse
{
    public ErrorCode Result { get; set; }
    public string MailId { get; set; } = "";
    public List<PkRewardResult> Rewards { get; set; } = new();
    public List<PkCurrency> Currencies { get; set; } = new();
}

// POST api/Mail/ClaimAll
public class PkMailClaimAllRequest { }

public class PkMailClaimAllResponse
{
    public ErrorCode Result { get; set; }
    public List<string> ClaimedMailIds { get; set; } = new();
    public List<PkRewardResult> Rewards { get; set; } = new();
    public List<PkCurrency> Currencies { get; set; } = new();
}

// POST api/Mail/CheatSendMail — 치트 전용 (DEBUG 빌드 한정)
public class PkMailCheatSendRequest
{
    public string TitleKey { get; set; } = "";

    /// 치트 메일 발송 분류. 빈 값 = Generic. 클라 EMailKind enum 문자열.
    public string MailKind { get; set; } = "";

    public List<PkMailReward> Rewards { get; set; } = new();
    public int ExpireMinutes { get; set; }    // 0 또는 음수면 기본값 (7일)
    public string IconAtlas { get; set; } = "";
    public string IconKey { get; set; } = "";
}

public class PkMailCheatSendResponse
{
    public ErrorCode Result { get; set; }
    public string MailId { get; set; } = "";
}


// ============================================================
// Admin
// ============================================================

// POST api/Admin/UploadGameData
public class PkUploadGameDataRequest
{
    public Dictionary<string, JsonElement> GameData { get; set; } = new();
}

public class PkUploadGameDataResponse
{
    public ErrorCode Result { get; set; }
}
