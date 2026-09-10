using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory <see cref="ICourseGroupRepository"/> that mirrors the real repository's contract
/// (smallint IDENTITY key assignment, keyword search on Description, tri-state inUse filtering
/// driven by the two reference counts, and pkid ordering) so controller behaviour can be tested
/// without SQL Server.
/// </summary>
public class FakeCourseGroupRepository : ICourseGroupRepository
{
    private readonly Dictionary<short, CourseGroup> _courseGroups = [];
    private short _nextPkid = 1;

    public List<short> CreatedPkids { get; } = [];
    public List<short> UpdatedPkids { get; } = [];
    public List<short> DeletedPkids { get; } = [];

    public FakeCourseGroupRepository Seed(params CourseGroup[] courseGroups)
    {
        foreach (var courseGroup in courseGroups)
        {
            _courseGroups[courseGroup.Pkid] = courseGroup;
            if (courseGroup.Pkid >= _nextPkid)
            {
                _nextPkid = (short)(courseGroup.Pkid + 1);
            }
        }

        return this;
    }

    public Task<IEnumerable<CourseGroup>> GetAllAsync(CancellationToken cancellationToken = default)
        => QueryAsync(new CourseGroupQuery(), cancellationToken);

    public Task<IEnumerable<CourseGroup>> QueryAsync(
        CourseGroupQuery query,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<CourseGroup> results = _courseGroups.Values;

        var keyword = query.Keyword?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            results = results.Where(cg =>
                cg.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (query.InUse is bool inUse)
        {
            results = results.Where(cg =>
                (cg.CourseCount + cg.PartnerCourseGroupCount > 0) == inUse);
        }

        return Task.FromResult(results.OrderBy(cg => cg.Pkid).AsEnumerable());
    }

    public Task<CourseGroup?> GetByIdAsync(short pkid, CancellationToken cancellationToken = default)
        => Task.FromResult(_courseGroups.GetValueOrDefault(pkid));

    /// <summary>The key is server-generated, exactly as the IDENTITY column is.</summary>
    public Task<short> CreateAsync(CourseGroupRequest request, CancellationToken cancellationToken = default)
    {
        var pkid = _nextPkid++;
        CreatedPkids.Add(pkid);

        _courseGroups[pkid] = new CourseGroup { Pkid = pkid, Description = request.Description };

        return Task.FromResult(pkid);
    }

    public Task<bool> UpdateAsync(CourseGroupRequest request, CancellationToken cancellationToken = default)
    {
        if (!_courseGroups.TryGetValue(request.Pkid, out var existing))
        {
            return Task.FromResult(false);
        }

        UpdatedPkids.Add(request.Pkid);
        _courseGroups[request.Pkid] = new CourseGroup
        {
            Pkid = request.Pkid,
            Description = request.Description,
            // Reference counts are projected from other tables, so an update never changes them.
            CourseCount = existing.CourseCount,
            PartnerCourseGroupCount = existing.PartnerCourseGroupCount,
        };

        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(short pkid, CancellationToken cancellationToken = default)
    {
        var removed = _courseGroups.Remove(pkid);
        if (removed)
        {
            DeletedPkids.Add(pkid);
        }

        return Task.FromResult(removed);
    }
}
