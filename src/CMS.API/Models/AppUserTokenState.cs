namespace CMS.API.Models;

/// <summary>
/// What an authenticated request needs to know about the account behind the bearer token: whether
/// the row is still there, whether it is still 啟用, and when its password last changed.
///
/// Read once per validated request by <see cref="Security.TokenFreshness"/>. **The type is returned
/// nullable and that null is load-bearing** — it means "no such AppUser row". Folding that case
/// into a null 密碼更新時間, as the earlier <c>Task&lt;DateTime?&gt;</c> shape had to, made a deleted
/// account indistinguishable from one whose password had never been changed, and the second of
/// those is a token to accept.
/// </summary>
public class AppUserTokenState
{
    /// <summary>啟用 — false once an administrator disables the account.</summary>
    public bool IsActive { get; set; }

    /// <summary>密碼更新時間, or null when the password has never been changed.</summary>
    public DateTime? PasswordUpdatedTime { get; set; }
}
