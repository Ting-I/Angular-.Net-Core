using CMS.API.Models;

namespace CMS.API.Repositories;

public interface ILookupRepository
{
    Task<IEnumerable<AppUserLookup>> GetAppUsersAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<PublishStatusLookup>> GetPublishStatusesAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<PartnerLookup>> GetPartnersAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<CourseGroupLookup>> GetCourseGroupsAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<CertificationLookup>> GetCertificationsAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<JobCategoryLookup>> GetJobCategoriesAsync(CancellationToken cancellationToken = default);
}
