namespace CMS.API.Security;

/// <summary>
/// The named authorization policies, and the role names behind them.
///
/// The fallback policy in <c>Program.cs</c> answers "is anybody signed in", which is the whole of
/// what every endpoint required until now: the Angular shell hid 系統管理 Admin from operators
/// without the role, but hiding a menu is presentation — the API is what refuses, and it was not
/// refusing. Any token holder could call <c>PUT /api/app-users</c> with their own UserId and
/// <c>roleIds: ["Admin"]</c>, and <c>SyncUserRolesAsync</c> would write it.
///
/// So the admin sub-system says so at the controller. Constants rather than literals because the
/// same string is the policy name in one file, the attribute argument in three others and the
/// AppRole.RoleId a token carries — a typo in any of them fails open, quietly.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// 系統管理 Admin — the sub-system that administers accounts, roles and publish statuses.
    /// Applied at controller level so an action added later is covered by omission, the same way
    /// the fallback policy covers a controller added later.
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// The AppRole.RoleId the policy requires, as it appears in the token's role claims and in
    /// <c>ADMIN_ROLE</c> on the Angular side. <c>ConfigureJwtBearerOptions</c> already points
    /// RoleClaimType at <see cref="JwtTokenService.RoleClaimType"/>, so RequireRole reads the
    /// claims the login actually writes.
    /// </summary>
    public const string AdminRole = "Admin";
}
