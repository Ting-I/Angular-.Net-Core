namespace CMS.API.Models;

/// <summary>
/// 登入結果 — the profile returned by a successful login.
/// PasswordHash has no property here, and must never gain one; the roles travel inside the token
/// rather than as a field of this object.
/// </summary>
public class LoginResponse
{
    /// <summary>使用者代碼</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>使用者名稱</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>存取權杖 — the signed JWT, valid for 24 hours from issue.</summary>
    public string AccessToken { get; set; } = string.Empty;
}
