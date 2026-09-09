using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IAuthRepository"/> mirroring the real credential read: an exact (and, like
/// SQL Server's default collation, case-insensitive) UserId lookup that returns the row whatever
/// IsActive says, with the role assignments attached. Seeding takes a plaintext password and stores
/// its SHA-256, which is what the AppUser row would hold.
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
            PasswordHash = PasswordHasher.Sha256Hex(password),
            RoleIds = [.. roleIds],
        };

        return this;
    }

    /// <summary>Seeds a row whose stored hash is uppercase hex, as a hand-written row might be.</summary>
    public FakeAuthRepository SeedWithUppercaseHash(string userId, string userName, string password)
    {
        Seed(userId, userName, password);
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

    public Task<DateTime?> GetPasswordUpdatedTimeAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        FreshnessChecks.Add(userId);
        return Task.FromResult(PasswordUpdatedTimeSource?.Invoke(userId));
    }

    public Task<AppUserCredential?> GetCredentialAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        LookedUpUserIds.Add(userId);
        return Task.FromResult(_credentials.GetValueOrDefault(userId));
    }
}
