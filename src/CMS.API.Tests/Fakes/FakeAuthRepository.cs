using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IAuthRepository"/> mirroring the real credential read: an exact (and, like
/// SQL Server's default collation, case-insensitive) UserId lookup that returns the row whatever
/// IsActive says, with the role assignments attached. Seeding takes a plaintext password and stores
/// its hash in the format the API writes today; SeedLegacy stores the older one instead.
/// </summary>
public class FakeAuthRepository : IAuthRepository
{
    private readonly Dictionary<string, AppUserCredential> _credentials = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every UserId the controller asked about, in order.</summary>
    public List<string> LookedUpUserIds { get; } = [];

    public FakeAuthRepository Seed(
        string userId,
        string userName,
        string password,
        bool isActive = true,
        params string[] roleIds)
    {
        _credentials[userId] = new AppUserCredential
        {
            UserId = userId,
            UserName = userName,
            IsActive = isActive,
            PasswordHash = PasswordHasher.Hash(password),
            RoleIds = [.. roleIds],
        };

        return this;
    }

    /// <summary>
    /// Seeds a row still holding the legacy unsalted SHA-256 hex, as every row did before
    /// <see cref="PasswordHasher.Hash"/>. This is what the login path upgrades.
    /// </summary>
    public FakeAuthRepository SeedLegacy(
        string userId,
        string userName,
        string password,
        bool isActive = true,
        params string[] roleIds)
    {
        Seed(userId, userName, password, isActive, roleIds);
        _credentials[userId].PasswordHash = PasswordHasher.Sha256Hex(password);
        return this;
    }

    /// <summary>Seeds a legacy row whose stored hash is uppercase hex, as a hand-written row might be.</summary>
    public FakeAuthRepository SeedWithUppercaseHash(string userId, string userName, string password)
    {
        SeedLegacy(userId, userName, password);
        _credentials[userId].PasswordHash = _credentials[userId].PasswordHash.ToUpperInvariant();
        return this;
    }

    /// <summary>
    /// Where PasswordUpdatedTime is read from. In the API both this query and the AppUser CRUD
    /// repository read the one AppUser row, so a password change is visible to both; the fakes
    /// keep separate stores, and TestApiFactory points this at FakeAppUserRepository to put that
    /// link back. Left unset, every user reads as "never changed".
    /// </summary>
    public Func<string, DateTime?>? PasswordUpdatedTimeSource { get; set; }

    /// <summary>Every UserId the token-freshness check asked about, in order.</summary>
    public List<string> FreshnessChecks { get; } = [];

    /// <summary>
    /// Mirrors <c>AuthSql.SelectTokenState</c>: 啟用 and 密碼更新時間 off the one AppUser row, and
    /// **null when there is no such row**. The seeded credentials are this fake's AppUser table, so
    /// existence and IsActive come from there; PasswordUpdatedTime keeps coming from the hook
    /// above, which is what makes a 變更密碼 written through the AppUser repository visible here.
    /// </summary>
    public Task<AppUserTokenState?> GetTokenStateAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        FreshnessChecks.Add(userId);

        var credential = _credentials.GetValueOrDefault(userId);
        if (credential is null)
        {
            return Task.FromResult<AppUserTokenState?>(null);
        }

        return Task.FromResult<AppUserTokenState?>(new AppUserTokenState
        {
            IsActive = credential.IsActive,
            PasswordUpdatedTime = PasswordUpdatedTimeSource?.Invoke(userId),
        });
    }

    /// <summary>
    /// Flips 啟用 off on a seeded row, as <c>PUT /api/app-users</c> with <c>isActive: false</c>
    /// does to the AppUser row — the state an already-issued token then has to be judged against.
    /// </summary>
    public FakeAuthRepository Disable(string userId)
    {
        _credentials[userId].IsActive = false;
        return this;
    }

    /// <summary>
    /// Drops the row, as <c>DELETE /api/app-users/{id}</c> does. Separate from Disable because the
    /// two are different answers to the freshness query — no row at all, versus a disabled one —
    /// and only one of them leaves anything to reset a password on.
    /// </summary>
    public FakeAuthRepository Remove(string userId)
    {
        _credentials.Remove(userId);
        return this;
    }

    public Task<AppUserCredential?> GetCredentialAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        LookedUpUserIds.Add(userId);
        return Task.FromResult(_credentials.GetValueOrDefault(userId));
    }

    /// <summary>Every (UserId, expected, replacement) the login path asked to upgrade, in order.</summary>
    public List<(string UserId, string ExpectedHash, string PasswordHash)> UpgradedHashes { get; } = [];

    /// <summary>
    /// Mirrors <c>AuthSql.UpgradePasswordHash</c>: the write applies only while the row still holds
    /// the hash the caller verified against, and PasswordUpdatedTime is never touched — which is
    /// what lets a test prove the token issued by the same login is not made stale by the upgrade.
    /// </summary>
    public Task<bool> UpgradePasswordHashAsync(
        string userId,
        string expectedHash,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        UpgradedHashes.Add((userId, expectedHash, passwordHash));

        var credential = _credentials.GetValueOrDefault(userId);
        if (credential is null || !string.Equals(credential.PasswordHash, expectedHash, StringComparison.Ordinal))
        {
            return Task.FromResult(false);
        }

        credential.PasswordHash = passwordHash;
        return Task.FromResult(true);
    }

    /// <summary>The hash the row holds now, or null when there is no such row.</summary>
    public string? PasswordHashOf(string userId) => _credentials.GetValueOrDefault(userId)?.PasswordHash;
}
