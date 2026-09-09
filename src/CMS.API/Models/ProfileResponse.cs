namespace CMS.API.Models;

/// <summary>
/// 個人資料 — the signed-in user's own profile, as PUT /api/auth/profile answers with it.
///
/// The same three fields the page shows. Like <see cref="LoginResponse"/> it must never gain a
/// credential property; unlike it, the roles are named here, because the page displays them.
/// They are read back from the database after the write, so the response also shows that the
/// update left them alone.
/// </summary>
public class ProfileResponse
{
    /// <summary>使用者代碼 — taken from the token, never from the request.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>使用者名稱</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>角色 — display only; this endpoint cannot change them.</summary>
    public List<string> RoleIds { get; set; } = [];
}
