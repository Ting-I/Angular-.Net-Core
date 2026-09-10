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
    /// 啟用 and 密碼更新時間 for one UserId — the account state behind a bearer token. No row means
    /// no such user, which the caller must be able to tell apart from a user whose password has
    /// never been changed; returning a row rather than a bare column is what makes that possible.
    ///
    /// This is the check behind every authenticated request: a token outlives neither the account
    /// it names, nor that account being enabled, nor the password it was signed against. It selects
    /// two columns and no hash, so it does not weaken the rule that SelectCredential is the only
    /// query returning one.
    ///
    /// IsActive is here rather than only in <see cref="SelectCredential"/> because login is not
    /// the only moment it matters: a token issued while the account was enabled stays signed and
    /// unexpired for its full lifetime, so an account disabled a minute later would otherwise keep
    /// working until the token aged out.
    /// </summary>
    public const string SelectTokenState = """
        SELECT u.IsActive,
               u.PasswordUpdatedTime
        FROM AppUser u
        WHERE u.UserId = @UserId
        """;

    /// <summary>
    /// Rewrites PasswordHash in place when a verified login is still stored in the legacy unsalted
    /// format — see <see cref="Security.PasswordHasher"/>.
    ///
    /// Two things it deliberately does not do. It does not touch PasswordUpdatedTime: the password
    /// did not change, only the way it is stored, and moving that column would make
    /// <see cref="Security.TokenFreshness"/> revoke the token the very login is about to issue.
    /// And it does not write blind — <c>AND u.PasswordHash = @ExpectedHash</c> means a row somebody
    /// else changed between the credential read and this write is left alone, so a concurrent
    /// 變更密碼 cannot be overwritten by the older value this request verified against.
    /// </summary>
    public const string UpgradePasswordHash = """
        UPDATE u
        SET u.PasswordHash = @PasswordHash
        FROM AppUser u
        WHERE u.UserId = @UserId
          AND u.PasswordHash = @ExpectedHash
        """;

    /// <summary>角色 — read on the same connection as the credential, as the n-n reads elsewhere do.</summary>
    public const string SelectRoleIds = """
        SELECT ur.RoleId
        FROM AppUserRole ur
        WHERE ur.UserId = @UserId
        ORDER BY ur.RoleId ASC
        """;
}
