namespace CMS.API.Models;

/// <summary>
/// 使用者 AppUser — response model.
/// PasswordHash is deliberately absent: it is written by the server only and never leaves it.
/// </summary>
public class AppUser
{
    /// <summary>主代碼 — non-key IDENTITY column.</summary>
    public int Pkid { get; set; }

    /// <summary>使用者代碼 (primary key)</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>使用者名稱</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>啟用</summary>
    public bool IsActive { get; set; }

    /// <summary>密碼更新時間 — read-only; written only when the hash is written (UTC).</summary>
    public DateTime? PasswordUpdatedTime { get; set; }

    /// <summary>角色數 — count of AppUserRole rows for this user.</summary>
    public int RoleCount { get; set; }

    /// <summary>角色 — populated on GET by id only.</summary>
    public List<string> RoleIds { get; set; } = [];
}
