using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Security;

/// <summary>
/// HS256 access tokens signed with the SysConfig 'appConfig' symmetricSecurityKey.
///
/// The token is issued only; nothing in this API validates one yet. Whatever adds
/// <c>AddJwtBearer</c> later must set <c>NameClaimType</c>/<c>RoleClaimType</c> to
/// <see cref="UserIdClaimType"/> and <see cref="RoleClaimType"/>, and must not require an issuer or
/// audience — neither is stamped here, because there is no second party to name.
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    /// <summary>使用者代碼 claim.</summary>
    public const string UserIdClaimType = "userId";

    /// <summary>使用者名稱 claim.</summary>
    public const string UserNameClaimType = "userName";

    /// <summary>角色 claim — one per AppUserRole.RoleId; repeats serialize as a JSON array.</summary>
    public const string RoleClaimType = "role";

    /// <summary>
    /// HS256 refuses a key shorter than its 256-bit output (IDX10653), so a too-short configured
    /// secret is a configuration fault to report, not an exception to let escape.
    /// </summary>
    public const int MinimumSecretBytes = 32;

    /// <summary>24 hours, per the login spec.</summary>
    public TimeSpan Lifetime => TimeSpan.FromHours(24);

    /// <summary>True when the configured secret exists and is long enough to sign with.</summary>
    public static bool IsUsableSecret(string? signingSecret) =>
        !string.IsNullOrWhiteSpace(signingSecret) &&
        Encoding.UTF8.GetByteCount(signingSecret) >= MinimumSecretBytes;

    public string CreateAccessToken(
        string userId,
        string userName,
        IEnumerable<string> roleIds,
        string signingSecret)
    {
        if (!IsUsableSecret(signingSecret))
        {
            throw new ArgumentException(
                $"The signing secret must be at least {MinimumSecretBytes} bytes for HMAC-SHA256.",
                nameof(signingSecret));
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(UserIdClaimType, userId),
            new Claim(UserNameClaimType, userName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        ]);

        // One claim per role, so the set survives as an array rather than a joined string.
        identity.AddClaims(roleIds.Select(roleId => new Claim(RoleClaimType, roleId)));

        var issuedAt = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = identity,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = issuedAt.Add(Lifetime),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingSecret)),
                SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
