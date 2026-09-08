using CMS.API.Models;

namespace CMS.API.Repositories;

public interface ILookupRepository
{
    Task<IEnumerable<AppUserLookup>> GetAppUsersAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<AppRoleLookup>> GetAppRolesAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<PublishStatusLookup>> GetPublishStatusesAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<PartnerLookup>> GetPartnersAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<CourseGroupLookup>> GetCourseGroupsAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<CertificationLookup>> GetCertificationsAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<JobCategoryLookup>> GetJobCategoriesAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<TrainingCenterLookup>> GetTrainingCentersAsync(CancellationToken cancellationToken = default);

    /// <summary>Promotions whose PromoCode contains <paramref name="keyword"/>, newest code first, capped for autocomplete.</summary>
    Task<IEnumerable<PromotionLookup>> SearchPromotionsAsync(string? keyword, CancellationToken cancellationToken = default);

    /// <summary>Exact PromoCode match — the lookup that turns a typed code into Promotion_pkid.</summary>
    Task<PromotionLookup?> GetPromotionByCodeAsync(string promoCode, CancellationToken cancellationToken = default);
}
