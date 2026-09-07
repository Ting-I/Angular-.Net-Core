using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IPublishStatusRepository"/> that mirrors the real repository's contract
/// (client-supplied tinyint PK, tri-state bool filtering, pkid ASC ordering, Course/Promotion2
/// reference counts) so controller behaviour can be tested without SQL Server.
/// </summary>
public class FakePublishStatusRepository : IPublishStatusRepository
{
    private readonly Dictionary<byte, PublishStatus> _statuses = [];

    public List<byte> CreatedPkids { get; } = [];
    public List<byte> UpdatedPkids { get; } = [];
    public List<byte> DeletedPkids { get; } = [];

    public FakePublishStatusRepository Seed(params PublishStatus[] statuses)
    {
        foreach (var status in statuses)
        {
            _statuses[status.Pkid] = status;
        }

        return this;
    }

    public Task<IEnumerable<PublishStatus>> GetAllAsync(CancellationToken cancellationToken = default)
        => QueryAsync(new PublishStatusQuery(), cancellationToken);

    public Task<IEnumerable<PublishStatus>> QueryAsync(
        PublishStatusQuery query,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<PublishStatus> results = _statuses.Values;

        var keyword = query.Keyword?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            results = results.Where(s =>
                s.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (query.IsDraft is bool isDraft)
        {
            results = results.Where(s => s.IsDraft == isDraft);
        }

        if (query.IsPublished is bool isPublished)
        {
            results = results.Where(s => s.IsPublished == isPublished);
        }

        if (query.IsDiscontinued is bool isDiscontinued)
        {
            results = results.Where(s => s.IsDiscontinued == isDiscontinued);
        }

        return Task.FromResult(results.OrderBy(s => s.Pkid).AsEnumerable());
    }

    public Task<PublishStatus?> GetByIdAsync(byte pkid, CancellationToken cancellationToken = default)
        => Task.FromResult(_statuses.GetValueOrDefault(pkid));

    public Task<bool> ExistsAsync(byte pkid, CancellationToken cancellationToken = default)
        => Task.FromResult(_statuses.ContainsKey(pkid));

    public Task<byte> CreateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default)
    {
        CreatedPkids.Add(request.Pkid);
        _statuses[request.Pkid] = ToStatus(request);
        return Task.FromResult(request.Pkid);
    }

    public Task<bool> UpdateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default)
    {
        if (!_statuses.TryGetValue(request.Pkid, out var existing))
        {
            return Task.FromResult(false);
        }

        UpdatedPkids.Add(request.Pkid);
        var updated = ToStatus(request);
        // Reference counts are projected from other tables, so an update never changes them.
        updated.CourseCount = existing.CourseCount;
        updated.PromotionCount = existing.PromotionCount;
        _statuses[request.Pkid] = updated;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(byte pkid, CancellationToken cancellationToken = default)
    {
        var removed = _statuses.Remove(pkid);
        if (removed)
        {
            DeletedPkids.Add(pkid);
        }

        return Task.FromResult(removed);
    }

    private static PublishStatus ToStatus(PublishStatusRequest request) => new()
    {
        Pkid = request.Pkid,
        Description = request.Description,
        IsDraft = request.IsDraft,
        IsPublished = request.IsPublished,
        IsDiscontinued = request.IsDiscontinued,
    };
}
