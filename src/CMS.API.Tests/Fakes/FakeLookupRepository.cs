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

    public List<TrainingCenterLookup> TrainingCenters { get; init; } = [];

    public List<PromotionLookup> Promotions { get; init; } = [];

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

    public Task<IEnumerable<TrainingCenterLookup>> GetTrainingCentersAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(TrainingCenters.AsEnumerable());

    /// <summary>Mirrors the SQL: LIKE '%keyword%' on PromoCode, newest code first, capped at 20.</summary>
    public Task<IEnumerable<PromotionLookup>> SearchPromotionsAsync(
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        var trimmed = keyword?.Trim() ?? string.Empty;
        return Task.FromResult(Promotions
            .Where(p => p.PromoCode.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.PromoCode, StringComparer.Ordinal)
            .Take(20)
            .AsEnumerable());
    }

    public Task<PromotionLookup?> GetPromotionByCodeAsync(
        string promoCode,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Promotions.FirstOrDefault(p =>
            string.Equals(p.PromoCode, promoCode.Trim(), StringComparison.OrdinalIgnoreCase)));
}
