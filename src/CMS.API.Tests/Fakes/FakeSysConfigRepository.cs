using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory <see cref="ISysConfigRepository"/>. DefaultPassword is settable and nullable so the
/// missing-configuration 500 path is reachable without a database.
/// </summary>
public class FakeSysConfigRepository : ISysConfigRepository
{
    public string? DefaultPassword { get; set; } = "Uwa@2026";

    public int GetDefaultPasswordCallCount { get; private set; }

    public Task<string?> GetDefaultPasswordAsync(CancellationToken cancellationToken = default)
    {
        GetDefaultPasswordCallCount++;
        return Task.FromResult(DefaultPassword);
    }
}
