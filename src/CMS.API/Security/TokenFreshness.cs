using System.Security.Claims;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Security;

/// <summary>
/// 舊 token 失效 — refuses a token that was signed against a password the account no longer has.
///
/// A JWT is stateless: the signature and the expiry are the whole of what validation normally
/// looks at, so a token captured before a password change would keep working for the rest of its
/// 24 hours. Clearing it from the browser's session storage is housekeeping, not revocation — it
/// does nothing about a copy taken elsewhere. This is the part that actually revokes.
///
/// **No schema change stands behind it.** `AppUser.PasswordUpdatedTime` already exists and
/// `ProfileController.ChangePassword` already writes it, so the stored row carries the moment
/// every earlier token stopped counting; the token carries `iat`. Comparing the two needs nothing
/// new in the database and no server-side list of live tokens.
///
/// It costs one narrow query per authenticated request — <see cref="AuthSql.SelectPasswordUpdatedTime"/>,
/// a single column on the primary key. That is the same shape of cost the signing-key read already
/// accepts, and for the same reason: the alternative is trusting a value captured earlier.
/// </summary>
public static class TokenFreshness
{
    /// <summary>
    /// True when the token predates the account's last password change and must be refused.
    ///
    /// Two asymmetries are deliberate. A `null` <paramref name="passwordUpdatedTimeUtc"/> — the
    /// password has never been changed, or the row is gone — proves nothing against the token, so
    /// it passes and the endpoint answers for itself. A token with no readable `iat` fails closed
    /// once the account *has* a change to compare against: nothing this API issues lacks `iat`, so
    /// such a token did not come from <see cref="JwtTokenService"/>.
    /// </summary>
    public static bool IsStale(DateTime? issuedAtUtc, DateTime? passwordUpdatedTimeUtc)
    {
        if (passwordUpdatedTimeUtc is not DateTime changedAt)
        {
            return false;
        }

        if (issuedAtUtc is not DateTime issuedAt)
        {
            return true;
        }

        // `iat` is whole seconds and PasswordUpdatedTime is a SQL `datetime`, so the stored value
        // is truncated down to the second before the comparison. Without that, a password changed
        // at 10:00:00.400 would reject the token from a login at 10:00:00.900 — whose `iat` floors
        // to 10:00:00 — and the operator could not sign back in at all. The cost is a sub-second
        // window in which a token issued earlier in the same second still passes; a token issued
        // any second before the change does not.
        return issuedAt < TruncateToSecond(changedAt);
    }

    /// <summary>The value with its sub-second part dropped, keeping its <see cref="DateTimeKind"/>.</summary>
    public static DateTime TruncateToSecond(DateTime value) =>
        new(value.Ticks - (value.Ticks % TimeSpan.TicksPerSecond), value.Kind);

    /// <summary>
    /// The token's `iat` as UTC, or null when it carries none. Read from the payload rather than
    /// from a claim, because inbound claim mapping is free to rename registered claims and this
    /// one must be found under the name the token actually used.
    /// </summary>
    public static DateTime? IssuedAtOf(SecurityToken? securityToken) =>
        securityToken is JsonWebToken token &&
        token.TryGetPayloadValue<long>(JwtRegisteredClaimNames.Iat, out var iat)
            ? DateTimeOffset.FromUnixTimeSeconds(iat).UtcDateTime
            : null;

    /// <summary>
    /// The JwtBearer <c>OnTokenValidated</c> callback. It runs after the signature and the expiry
    /// have passed, which is what makes the extra query worth doing: an unsigned or expired token
    /// never reaches it.
    ///
    /// <c>context.Fail</c> rather than a thrown exception, so the outcome is the same plain 401
    /// the middleware produces for any other invalid token — a stale one must not be
    /// distinguishable from a forged one.
    /// </summary>
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var userId = context.Principal?.FindFirstValue(JwtTokenService.UserIdClaimType);
        if (string.IsNullOrWhiteSpace(userId))
        {
            // Nothing to look the account up by. The endpoints that act on the caller answer their
            // own 401 for this; there is no password change to measure the token against here.
            return;
        }

        var repository = context.HttpContext.RequestServices.GetRequiredService<IAuthRepository>();
        var passwordUpdatedTime = await repository.GetPasswordUpdatedTimeAsync(
            userId,
            context.HttpContext.RequestAborted);

        if (IsStale(IssuedAtOf(context.SecurityToken), passwordUpdatedTime))
        {
            context.Fail("The password has changed since this token was issued.");
        }
    }
}
