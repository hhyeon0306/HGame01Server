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
    public int ShopItemId { get; set; }
    public bool Purchased { get; set; }
}

// POST api/Shop/Buy
public class PkShopBuyRequest
{
    public int ShopItemId { get; set; }
}

public class PkShopBuyResponse
{
    public ErrorCode Result { get; set; }
    public List<PkCurrency> Currencies { get; set; } = new();
    public PkRewardResult Reward { get; set; } = new();
}

// POST api/Shop/BuyDiamond
public class PkBuyDiamondRequest
{
    public string ProductId { get; set; } = "";
    public int Amount { get; set; }
}

public class PkBuyDiamondResponse
{
    public ErrorCode Result { get; set; }
    public long DiamondAmount { get; set; }
}

// ============================================================
// 뽑기
// ============================================================

// POST api/Gacha/Pull
public class PkGachaPullRequest
{
    public int PullCount { get; set; }
    public bool UseTicket { get; set; }
}

public class PkGachaPullResponse
{
    public ErrorCode Result { get; set; }
    public List<PkGachaResultItem> Items { get; set; } = new();
    public List<PkCurrency> Currencies { get; set; } = new();
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
    public long Id { get; set; }
    public int EquipmentId { get; set; }
    public int Slot { get; set; }
    public bool IsEquipped { get; set; }
    public int EquippedCharacterId { get; set; }
    public string AcquiredAt { get; set; } = "";
}

public class PkRewardResult
{
    public int RewardType { get; set; }
    public int Amount { get; set; }
    public int EquipmentId { get; set; }
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
