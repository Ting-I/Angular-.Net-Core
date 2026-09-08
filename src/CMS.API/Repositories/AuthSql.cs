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

    /// <summary>角色 — read on the same connection as the credential, as the n-n reads elsewhere do.</summary>
    public const string SelectRoleIds = """
        SELECT ur.RoleId
        FROM AppUserRole ur
        WHERE ur.UserId = @UserId
        ORDER BY ur.RoleId ASC
        """;
}
