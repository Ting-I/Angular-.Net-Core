namespace CMS.API.Models;

/// <summary>
/// The credential row behind one login attempt: the only model in the API that carries
/// PasswordHash. It is a repository-to-controller value and is never serialized into a response —
/// <see cref="LoginResponse"/> is what leaves the server. Keeping the hash out of
/// <see cref="AppUser"/> is what lets AppUserSql.SelectBase stay hash-free.
/// </summary>
public class AppUserCredential
{
    /// <summary>使用者代碼 (primary key)</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>使用者名稱</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>啟用 — a false here fails the login exactly like a bad password does.</summary>
    public bool IsActive { get; set; }

    /// <summary>The value stored in AppUser.PasswordHash, in whichever format that row holds.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>角色 — every AppUserRole.RoleId for this user; becomes the token's role claims.</summary>
    public List<string> RoleIds { get; set; } = [];
}
