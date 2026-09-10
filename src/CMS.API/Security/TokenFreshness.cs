using System.Security.Claims;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Security;

/// <summary>
/// 舊 token 失效 — refuses a token the account behind it no longer backs.
///
/// A JWT is stateless: the signature and the expiry are the whole of what validation normally
/// looks at, so a token captured earlier would keep working for the rest of its 24 hours whatever
/// happened to the account in the meantime. Clearing it from the browser's session storage is
/// housekeeping, not revocation — it does nothing about a copy taken elsewhere. This is the part
/// that actually revokes, and it covers **three** ways an account stops backing its tokens:
///
/// - **The row is gone.** <see cref="IAuthRepository.GetTokenStateAsync"/> returns null, and a
///   token naming an account that no longer exists is refused. Nothing else in the API would catch
///   it: the content controllers never look the caller up, so a deleted operator kept full CRUD.
/// - **The account is disabled.** <c>AuthController</c> checks 啟用 at login, but login is one
///   moment and the token lasts a day. Without the check here, <c>IsActive = 0</c> — the toggle an
///   administrator reaches for precisely when they want somebody out — changed nothing until the
///   token aged out.
/// - **The password changed.** The original case: a token whose <c>iat</c> predates
///   <c>AppUser.PasswordUpdatedTime</c> was signed against a password that no longer exists, so a
///   change signs out every session holding an earlier token, everywhere.
///
/// **What it deliberately does not cover: 角色.** Role claims are stamped into the token at login
/// and <c>RequireRole</c> reads them from there, so revoking a role through
/// <c>PUT /api/app-users</c> does not take effect until the holder's current token expires. Closing
/// that would mean re-reading AppUserRole here and rebuilding the principal's claims on every
/// request — a second query and a materially different contract — so it is left open on purpose
/// rather than by oversight. Disable or delete the account when the revocation has to be immediate;
/// both are covered above.
///
/// **No schema change stands behind any of it.** IsActive, PasswordUpdatedTime and the row's own
/// existence are all already in <c>AppUser</c>; the token carries <c>iat</c>. It costs one narrow
/// query per authenticated request — <see cref="AuthSql.SelectTokenState"/>, two columns on the
/// primary key. That is the same shape of cost the signing-key read already accepts, and for the
/// same reason: the alternative is trusting a value captured earlier.
/// </summary>
public static class TokenFreshness
{
    /// <summary>
    /// True when the token must be refused because the account no longer backs it — no such row,
    /// the account disabled, or the token older than the password.
    ///
    /// The order is the cheap checks first, but none of them is a short-circuit worth relying on:
    /// all three read from the one row already in hand.
    /// </summary>
    public static bool IsRefused(AppUserTokenState? state, DateTime? issuedAtUtc)
    {
        if (state is null)
        {
            // No AppUser row. The account was deleted inside the token's lifetime, or the token
            // names something that never existed — either way there is nothing left to authorize
            // against, and every other check below would be measuring against a ghost.
            return true;
        }

        if (!state.IsActive)
        {
            return true;
        }

        return IsStale(issuedAtUtc, state.PasswordUpdatedTime);
    }

    /// <summary>
    /// True when the token predates the account's last password change and must be refused.
    ///
    /// Two asymmetries are deliberate. A `null` <paramref name="passwordUpdatedTimeUtc"/> — the
    /// password has never been changed — proves nothing against the token, so it passes and the
    /// endpoint answers for itself. A token with no readable `iat` fails closed once the account
    /// *has* a change to compare against: nothing this API issues lacks `iat`, so such a token did
    /// not come from <see cref="JwtTokenService"/>.
    ///
    /// A missing row is no longer one of the cases here — <see cref="IsRefused"/> answers that
    /// before asking this, because "gone" and "never changed" are different answers and only one
    /// of them is a token to accept.
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
    /// <c>context.Fail</c> rather than a thrown exception, so the outcome is the same plain 401 the
    /// middleware produces for any other invalid token — a refused one must not be distinguishable
    /// from a forged one. The message is for the server log; JwtBearer only puts an
    /// <c>error_description</c> on the challenge for the token-validation exception types it knows,
    /// and a plain failure message is not one of them.
    /// </summary>
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var userId = context.Principal?.FindFirstValue(JwtTokenService.UserIdClaimType);
        if (string.IsNullOrWhiteSpace(userId))
        {
            // Nothing to look the account up by, so there is no account state to judge the token
            // against. The endpoints that act on the caller answer their own 401 for this.
            return;
        }

        var repository = context.HttpContext.RequestServices.GetRequiredService<IAuthRepository>();
        var state = await repository.GetTokenStateAsync(userId, context.HttpContext.RequestAborted);

        if (IsRefused(state, IssuedAtOf(context.SecurityToken)))
        {
            context.Fail("The account behind this token no longer backs it.");
        }
    }
}
