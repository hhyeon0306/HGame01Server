using System;

// 1000 ~ 19999
public enum ErrorCode : UInt16
{
    None = 0,

    // 공통 에러 (1001~1099)
    UnhandleException = 1001,
    RedisFailException = 1002,
    InValidRequestHttpBody = 1003,
    AuthTokenFailWrongAuthToken = 1006,
    TokenDoesNotExist = 1007,
    ProfileIdDoesNotExist = 1008,
    AuthTokenKeyNotFound = 1009,

    // 로그인 에러 (2001~2010)
    LoginFailException = 2002,
    LoginFailUserNotExist = 2003,
    LoginFailSetAuthToken = 2005,
    LoginFailAddRedis = 2006,

    // 계정 생성 에러 (2021~2030)
    CreateAccountFailException = 2021,
    CreateAccountFailDuplicate = 2022,

    // Admin 에러 (2121~2140)
    AdminFailException = 2121,
    AdminUploadFail = 2122,

    // 상점 에러 (3001~3020)
    ShopItemNotFound = 3001,
    ShopItemAlreadyPurchased = 3002,
    ShopInsufficientCurrency = 3003,
    ShopBuyFailed = 3004,
    ShopInvalidAmount = 3005,

    // 뽑기 에러 (3021~3040)
    GachaInsufficientCurrency = 3021,
    GachaInvalidPullCount = 3022,
    GachaPullFailed = 3023,

    // 재화 에러 (3041~3060)
    CurrencyInsufficientAmount = 3041,
    CurrencyUpdateFailed = 3042,

    // 장비 에러 (3061~3080)
    EquipmentNotFound = 3061,
    EquipmentNotOwned = 3062,
    EquipmentSlotMismatch = 3063,
    EquipmentAlreadyEquipped = 3064,
    EquipmentEquipFailed = 3065,
    CharacterNotOwned = 3066,

    // 우편함 에러 (3081~3100)
    MailNotFound = 3081,
    MailAlreadyClaimed = 3082,
    MailExpired = 3083,
    MailClaimFailed = 3084,
    MailNoClaimable = 3085,

    // 장비 보관함 에러 (3101~3120)
    EquipmentStorageFull = 3101,
    EquipmentStorageExpandFailed = 3102,
}
