namespace CMS.API.Repositories;

/// <summary>
/// SQL for the login credential read. Deliberately separate from <see cref="AppUserSql"/>: that
/// projection is the one place that keeps PasswordHash off the wire, so the single query that does
/// need the hash lives here instead of weakening it.
/// </summary>
public static class AuthSql
{
    /// <summary>
    /// The credential row for one UserId. UserId matches exactly — SQL Server's default collation
    /// is case-insensitive, and this query does not change that; it is the same lookup the rest of
    /// the API does on the key.
    /// </summary>
    public const string SelectCredential = """
        SELECT u.UserId,
               u.UserName,
               u.IsActive,
               u.PasswordHash
        FROM AppUser u
        WHERE u.UserId = @UserId
        """;

    /// <summary>
    /// 密碼更新時間 for one UserId, or null when the password has never been changed (or no such
    /// user exists — the caller cannot act on the difference and does not need to).
    ///
    /// This is the freshness check behind every authenticated request: a token signed before this
    /// moment was signed against a password that no longer exists. It selects one column and no
    /// hash, so it does not weaken the rule that SelectCredential is the only query returning one.
    /// </summary>
    public const string SelectPasswordUpdatedTime = """
        SELECT u.PasswordUpdatedTime
        FROM AppUser u
        WHERE u.UserId = @UserId
        """;

    /// <summary>角色 — read on the same connection as the credential, as the n-n reads elsewhere do.</summary>
    public const string SelectRoleIds = """
        SELECT ur.RoleId
        FROM AppUserRole ur
        WHERE ur.UserId = @UserId
        ORDER BY ur.RoleId ASC
        """;
}
