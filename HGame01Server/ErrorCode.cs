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
}
