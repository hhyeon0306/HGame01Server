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
    /// 장비 보관함 최대 칸 수. 클라가 보관함 N/M 표시 + 확장 버튼에 사용.
    public int EquipmentStorageCapacity { get; set; }

    /// 유저 레벨. 클라 UserLevelCondition 등 튜토리얼 조건 평가에 사용. 레벨 시스템 미구현 — 항상 0 fallback.
    public int Level { get; set; }

    /// 완료된 튜토리얼 GameplayTag 이름 목록. UserTutorialStore.Apply에서 HashSet으로 흡수 + pending UnionWith로 push 미확인 항목 보호.
    public List<string> CompletedTutorials { get; set; } = new();
}

// ============================================================
// 장비 보관함
// ============================================================

// POST api/EquipmentStorage/ExpandCapacity
public class PkExpandCapacityRequest
{
}

public class PkExpandCapacityResponse : ICurrencyBearingResponse
{
    public ErrorCode Result { get; set; }
    /// 확장 후 새 capacity. 실패 시 0.
    public int NewCapacity { get; set; }
    /// 확장 후 갱신된 재화 목록 (다이아 차감 반영).
    public List<PkCurrency> Currencies { get; set; } = new();
}

public class PkUserCharacter
{
    public string CharacterTag { get; set; } = "";
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

public class PkShopBuyResponse : ICurrencyBearingResponse
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

public class PkGachaPullResponse : ICurrencyBearingResponse
{
    public ErrorCode Result { get; set; }
    public List<PkGachaResultItem> Items { get; set; } = new();
    public List<PkCurrency> Currencies { get; set; } = new();

    /// 이번 뽑기로 획득한 신규 장비 인스턴스 (DB id 포함). 클라가 GetUserInfo 재호출 없이 Equipment Store를 갱신할 수 있도록 함.
    public List<PkUserEquipment> Equipments { get; set; } = new();
}

/// 가챠 결과 1단위. Reward 는 보상 패킷(PkRewardResult)으로 통일하여 다른 보상 흐름(Shop/Mail)과 contract 공유.
/// Grade 는 가챠 등급 부가 정보 — Reward 로 표현 불가능한 가챠 고유 메타.
public class PkGachaResultItem
{
    public PkRewardResult Reward { get; set; } = new();

    /// 가챠 등급. EAbilityGrade enum 값.
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
    /// SO 식별 GameplayTag 이름. 클라 EquipmentResolver에서 EquipmentData로 매핑.
    public string EquipmentTag { get; set; } = "";
    public int Slot { get; set; }
    public bool IsEquipped { get; set; }
    /// 장착된 캐릭터 식별 태그 — 미장착 시 빈 문자열.
    public string EquippedCharacterTag { get; set; } = "";
    public string AcquiredAt { get; set; } = "";
}

/// 서버 → 클라 보상 1단위. RewardType 가 상위 분류, ItemKind 는 RewardType="Item" 일 때만 채워지는 sub-kind.
/// RewardTag 는 SO 식별자(GameplayTag string) — RewardType 에 따라 ItemData/EquipmentData/... 어느 테이블의 tag 인지 결정.
/// Currency Gold/Diamond 식별이 필요하면 RewardTag 로 ItemData 를 lookup 해서 currency_type 필드를 본다 (tag 파싱 X).
public class PkRewardResult
{
    /// 상위 분류. 현재 값: "Equipment" / "Item". 미래 "Pet" / "Treasure" 등 추가.
    public string RewardType { get; set; } = "";

    /// RewardType="Item" 일 때만 채워지는 sub-kind. 현재 값: "Currency". 미래 "Material"/"Ticket"/"Booster"/"Box" 등 추가.
    public string ItemKind { get; set; } = "";

    /// 보상 SO 식별 GameplayTag 이름. RewardType 별로 ItemData/EquipmentData 등 다른 테이블의 tag.
    public string RewardTag { get; set; } = "";

    public int Count { get; set; }
}

// ============================================================
// 장비 장착/해제
// ============================================================

// POST api/Equipment/Equip
public class PkEquipRequest
{
    public long EquipmentDbId { get; set; }
    public string CharacterTag { get; set; } = "";
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

// POST api/Equipment/Sell
public class PkSellRequest
{
    /// 판매할 장비 인스턴스 dbId 목록. 다건 일괄 처리.
    public List<long> EquipmentDbIds { get; set; } = new();
}

public class PkSellResponse : ICurrencyBearingResponse
{
    public ErrorCode Result { get; set; }
    /// 판매 후 갱신된 전체 장비 목록.
    public List<PkUserEquipment> Equipments { get; set; } = new();
    /// 판매 후 갱신된 전체 재화 목록 (골드 증가분 반영).
    public List<PkCurrency> Currencies { get; set; } = new();
    /// 지급된 골드 합계. 클라가 결과 메시지에 표기.
    public long SoldGold { get; set; }
    /// 실제 판매 처리된 개수.
    public int SoldCount { get; set; }
}

// POST api/Equipment/EquipBatch
// 자동 장착(다건 일괄). 슬롯 중복 거절 — 슬롯당 최대 1건. 트랜잭션으로 원자적 적용.
public class PkAutoEquipRequest
{
    /// 일괄 장착할 장비 인스턴스 dbId 목록. 슬롯 중복 시 EquipmentEquipFailed.
    public List<long> EquipmentDbIds { get; set; } = new();
    /// 장착 대상 캐릭터 식별 태그.
    public string CharacterTag { get; set; } = "";
}

public class PkAutoEquipResponse
{
    public ErrorCode Result { get; set; }
    /// 갱신된 전체 장비 목록.
    public List<PkUserEquipment> Equipments { get; set; } = new();
    /// 실제 장착 처리된 개수. 클라가 결과 메시지에 표기.
    public int EquippedCount { get; set; }
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
    /// 보상 SO 식별 GameplayTag 이름. ItemData/EquipmentData/... 어느 테이블이든 보상 tag 단일 키로 식별.
    public string RewardTag { get; set; } = "";
    public int Count { get; set; }
}

// POST api/Mail/Claim
public class PkMailClaimRequest
{
    public string MailId { get; set; } = "";
}

public class PkMailClaimResponse : ICurrencyBearingResponse
{
    public ErrorCode Result { get; set; }
    public string MailId { get; set; } = "";
    public List<PkRewardResult> Rewards { get; set; } = new();
    public List<PkCurrency> Currencies { get; set; } = new();
}

// POST api/Mail/ClaimAll
public class PkMailClaimAllRequest { }

public class PkMailClaimAllResponse : ICurrencyBearingResponse
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


// POST api/Cheat/SetCurrency — 디버그 전용. 단일 통화를 절대값으로 설정.
public class PkCheatSetCurrencyRequest
{
    public int CurrencyType { get; set; }
    public long Amount { get; set; }
}

public class PkCheatSetCurrencyResponse : ICurrencyBearingResponse
{
    public ErrorCode Result { get; set; }
    public List<PkCurrency> Currencies { get; set; } = new();
}


// ============================================================
// 튜토리얼
// ============================================================

// POST api/Tutorial/Complete
public class PkCompleteTutorialRequest
{
    /// 완료된 튜토리얼의 GameplayTag 이름 (예: "Tag.Tutorial.Lobby.First"). 빈 값은 거절.
    public string TutorialTagName { get; set; } = "";
}

public class PkCompleteTutorialResponse
{
    public ErrorCode Result { get; set; }
}

// POST api/Tutorial/CheatResetAll — 치트 전용. 완료 튜토리얼 전체 클리어 → 다음 trigger 시 재발동.
public class PkCheatResetTutorialRequest
{
}

public class PkCheatResetTutorialResponse
{
    public ErrorCode Result { get; set; }
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
