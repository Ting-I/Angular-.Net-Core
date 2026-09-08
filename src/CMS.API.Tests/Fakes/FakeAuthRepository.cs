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

    public Task<AppUserCredential?> GetCredentialAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        LookedUpUserIds.Add(userId);
        return Task.FromResult(_credentials.GetValueOrDefault(userId));
    }
}
