using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

public class FakeLookupRepository : ILookupRepository
{
    public List<AppUserLookup> AppUsers { get; init; } = [];

    public List<PublishStatusLookup> PublishStatuses { get; init; } = [];

    public Task<IEnumerable<AppUserLookup>> GetAppUsersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(AppUsers.AsEnumerable());

    public Task<IEnumerable<PublishStatusLookup>> GetPublishStatusesAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(PublishStatuses.AsEnumerable());
}
