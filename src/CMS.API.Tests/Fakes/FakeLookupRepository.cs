using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

public class FakeLookupRepository : ILookupRepository
{
    public List<AppUserLookup> AppUsers { get; init; } = [];

    public List<AppRoleLookup> AppRoles { get; init; } = [];

    public List<PublishStatusLookup> PublishStatuses { get; init; } = [];

    public List<PartnerLookup> Partners { get; init; } = [];

    public List<CourseGroupLookup> CourseGroups { get; init; } = [];

    public List<CertificationLookup> Certifications { get; init; } = [];

    public List<JobCategoryLookup> JobCategories { get; init; } = [];

    public Task<IEnumerable<AppUserLookup>> GetAppUsersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(AppUsers.AsEnumerable());

    public Task<IEnumerable<AppRoleLookup>> GetAppRolesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(AppRoles.AsEnumerable());

    public Task<IEnumerable<PublishStatusLookup>> GetPublishStatusesAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(PublishStatuses.AsEnumerable());

    public Task<IEnumerable<PartnerLookup>> GetPartnersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Partners.AsEnumerable());

    public Task<IEnumerable<CourseGroupLookup>> GetCourseGroupsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CourseGroups.AsEnumerable());

    public Task<IEnumerable<CertificationLookup>> GetCertificationsAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(Certifications.AsEnumerable());

    public Task<IEnumerable<JobCategoryLookup>> GetJobCategoriesAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(JobCategories.AsEnumerable());
}
