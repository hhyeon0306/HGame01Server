using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace HGame01Server.Models;

// ============================================================
// 공통 헤더
// ============================================================

public class HeaderDTO
{
    [FromHeader]
    public string UserID { get; set; } = "";
    [FromHeader]
    public string AuthToken { get; set; } = "";
}

// ============================================================
// 계정
// ============================================================

// POST api/Login
public class PkLoginRequest
{
    public string ID { get; set; } = "";
    public string PW { get; set; } = "";
}

public class PkLoginResponse
{
    public ErrorCode Result { get; set; }
    public string AuthToken { get; set; } = "";
}

// POST api/CreateAccount
public class PkCreateAccountRequest
{
    public string ID { get; set; } = "";
    public string PW { get; set; } = "";
}

public class PkCreateAccountResponse
{
    public ErrorCode Result { get; set; }
}

// POST api/UserInfo
public class PkUserInfoRequest
{
}

public class PkUserInfoResponse
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
