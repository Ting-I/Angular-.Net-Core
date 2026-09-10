using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory <see cref="ISysConfigRepository"/>. Both values are settable and nullable so the
/// missing-configuration 500 paths are reachable without a database.
/// </summary>
public class FakeSysConfigRepository : ISysConfigRepository
{
    /// <summary>32 bytes — the shortest secret HS256 will sign with.</summary>
    public const string ValidSigningKey = "cloud4fun#123456cloud4fun#123456";

    public string? DefaultPassword { get; set; } = "Uwa@2026";

    public string? SymmetricSecurityKey { get; set; } = ValidSigningKey;

    public int GetDefaultPasswordCallCount { get; private set; }

    public int GetSymmetricSecurityKeyCallCount { get; private set; }

    public Task<string?> GetDefaultPasswordAsync(CancellationToken cancellationToken = default)
    {
        GetDefaultPasswordCallCount++;
        return Task.FromResult(DefaultPassword);
    }

    public Task<string?> GetSymmetricSecurityKeyAsync(CancellationToken cancellationToken = default)
    {
        GetSymmetricSecurityKeyCallCount++;
        return Task.FromResult(SymmetricSecurityKey);
    }
}
