using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>
/// Reads what a login attempt needs. Not an entity CRUD repository — AppUser CRUD lives in
/// <see cref="IAppUserRepository"/>, which never selects PasswordHash.
/// </summary>
public interface IAuthRepository
{
    /// <summary>
    /// The credential row and role assignments for a UserId, or null when no such user exists.
    /// The row is returned whatever IsActive says: the caller decides, so that an inactive account
    /// and a wrong password produce the same answer.
    /// </summary>
    Task<AppUserCredential?> GetCredentialAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 啟用 and 密碼更新時間 for a UserId, or **null when there is no such user** — the two are not
    /// the same answer and the caller acts on the difference. Read on every authenticated request
    /// to decide whether the account still backs the caller's token: see
    /// <see cref="Security.TokenFreshness"/>.
    /// </summary>
    Task<AppUserTokenState?> GetTokenStateAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rewrites a verified credential from the legacy unsalted hash into the current format,
    /// leaving 密碼更新時間 alone: the stored representation changed, the password did not.
    ///
    /// <paramref name="expectedHash"/> is the value the login just verified against, and the write
    /// applies only while the row still holds it. Returns true when the row was rewritten and
    /// false when it was not — a concurrent 變更密碼 already replaced it, or the account is gone.
    /// Neither outcome is an error: the sign-in that triggered it succeeds either way.
    /// </summary>
    Task<bool> UpgradePasswordHashAsync(
        string userId,
        string expectedHash,
        string passwordHash,
        CancellationToken cancellationToken = default);
}
