using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IPartnerRepository"/> that mirrors the real repository's contract
/// (smallint IDENTITY key assignment, keyword search across the four string columns, tri-state
/// hasImage filtering that treats a blank filename as "no image", DisplayOrder/pkid ordering and
/// the five child-table reference counts) so controller behaviour can be tested without SQL Server.
/// </summary>
public class FakePartnerRepository : IPartnerRepository
{
    private readonly Dictionary<short, Partner> _partners = [];
    private short _nextPkid = 1;

    public List<short> CreatedPkids { get; } = [];
    public List<short> UpdatedPkids { get; } = [];
    public List<short> DeletedPkids { get; } = [];

    public FakePartnerRepository Seed(params Partner[] partners)
    {
        foreach (var partner in partners)
        {
            _partners[partner.Pkid] = partner;
            if (partner.Pkid >= _nextPkid)
            {
                _nextPkid = (short)(partner.Pkid + 1);
            }
        }

        return this;
    }

    public Task<IEnumerable<Partner>> GetAllAsync(CancellationToken cancellationToken = default)
        => QueryAsync(new PartnerQuery(), cancellationToken);

    public Task<IEnumerable<Partner>> QueryAsync(PartnerQuery query, CancellationToken cancellationToken = default)
    {
        IEnumerable<Partner> results = _partners.Values;

        var keyword = query.Keyword?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            results = results.Where(p =>
                Contains(p.Name, keyword)
                || Contains(p.AppKey, keyword)
                || Contains(p.NameOnPartnerMenu, keyword)
                || Contains(p.NameOnCourseDetailPage, keyword));
        }

        if (query.HasImage is bool hasImage)
        {
            results = results.Where(p => !string.IsNullOrEmpty(p.ImageFilename) == hasImage);
        }

        return Task.FromResult(results
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.Pkid)
            .AsEnumerable());
    }

    public Task<Partner?> GetByIdAsync(short pkid, CancellationToken cancellationToken = default)
        => Task.FromResult(_partners.GetValueOrDefault(pkid));

    /// <summary>The key is server-generated, exactly as the IDENTITY column is.</summary>
    public Task<short> CreateAsync(PartnerRequest request, CancellationToken cancellationToken = default)
    {
        var pkid = _nextPkid++;
        CreatedPkids.Add(pkid);

        var created = ToPartner(request);
        created.Pkid = pkid;
        _partners[pkid] = created;

        return Task.FromResult(pkid);
    }

    public Task<bool> UpdateAsync(PartnerRequest request, CancellationToken cancellationToken = default)
    {
        if (!_partners.TryGetValue(request.Pkid, out var existing))
        {
            return Task.FromResult(false);
        }

        UpdatedPkids.Add(request.Pkid);
        var updated = ToPartner(request);
        updated.Pkid = request.Pkid;
        // Reference counts are projected from other tables, so an update never changes them.
        updated.CertificationCount = existing.CertificationCount;
        updated.CourseCount = existing.CourseCount;
        updated.CourseGroupCount = existing.CourseGroupCount;
        updated.PromotionCount = existing.PromotionCount;
        updated.SeminarCount = existing.SeminarCount;
        _partners[request.Pkid] = updated;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(short pkid, CancellationToken cancellationToken = default)
    {
        var removed = _partners.Remove(pkid);
        if (removed)
        {
            DeletedPkids.Add(pkid);
        }

        return Task.FromResult(removed);
    }

    private static bool Contains(string value, string keyword)
        => value.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private static Partner ToPartner(PartnerRequest request) => new()
    {
        Name = request.Name,
        AppKey = request.AppKey,
        NameOnPartnerMenu = request.NameOnPartnerMenu,
        NameOnCourseDetailPage = request.NameOnCourseDetailPage,
        DisplayOrder = request.DisplayOrder,
        ImageFilename = request.ImageFilename,
    };
}
