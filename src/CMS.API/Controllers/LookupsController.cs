using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>Slim lists used to populate select / multiselect options.</summary>
[ApiController]
[Route("api/lookups")]
[Produces("application/json")]
public class LookupsController : ControllerBase
{
    private readonly ILookupRepository _repository;

    public LookupsController(ILookupRepository repository)
    {
        _repository = repository;
    }

    /// <summary>使用者 AppUser lookup list.</summary>
    [HttpGet("app-users")]
    [ProducesResponseType(typeof(IEnumerable<AppUserLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppUserLookup>>> GetAppUsers(CancellationToken cancellationToken)
        => Ok(await _repository.GetAppUsersAsync(cancellationToken));

    /// <summary>角色 AppRole lookup list.</summary>
    [HttpGet("app-roles")]
    [ProducesResponseType(typeof(IEnumerable<AppRoleLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppRoleLookup>>> GetAppRoles(CancellationToken cancellationToken)
        => Ok(await _repository.GetAppRolesAsync(cancellationToken));

    /// <summary>發布狀態 PublishStatus lookup list.</summary>
    [HttpGet("publish-statuses")]
    [ProducesResponseType(typeof(IEnumerable<PublishStatusLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PublishStatusLookup>>> GetPublishStatuses(
        CancellationToken cancellationToken)
        => Ok(await _repository.GetPublishStatusesAsync(cancellationToken));

    /// <summary>原廠 Partner lookup list.</summary>
    [HttpGet("partners")]
    [ProducesResponseType(typeof(IEnumerable<PartnerLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PartnerLookup>>> GetPartners(CancellationToken cancellationToken)
        => Ok(await _repository.GetPartnersAsync(cancellationToken));

    /// <summary>課程群組 CourseGroup lookup list.</summary>
    [HttpGet("course-groups")]
    [ProducesResponseType(typeof(IEnumerable<CourseGroupLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CourseGroupLookup>>> GetCourseGroups(
        CancellationToken cancellationToken)
        => Ok(await _repository.GetCourseGroupsAsync(cancellationToken));

    /// <summary>認證 Certification lookup list.</summary>
    [HttpGet("certifications")]
    [ProducesResponseType(typeof(IEnumerable<CertificationLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CertificationLookup>>> GetCertifications(
        CancellationToken cancellationToken)
        => Ok(await _repository.GetCertificationsAsync(cancellationToken));

    /// <summary>職務類別 JobCategory lookup list.</summary>
    [HttpGet("job-categories")]
    [ProducesResponseType(typeof(IEnumerable<JobCategoryLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<JobCategoryLookup>>> GetJobCategories(
        CancellationToken cancellationToken)
        => Ok(await _repository.GetJobCategoriesAsync(cancellationToken));

    /// <summary>訓練中心 TrainingCenter lookup list — the 上稿作業 tabs.</summary>
    [HttpGet("training-centers")]
    [ProducesResponseType(typeof(IEnumerable<TrainingCenterLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TrainingCenterLookup>>> GetTrainingCenters(
        CancellationToken cancellationToken)
        => Ok(await _repository.GetTrainingCentersAsync(cancellationToken));

    /// <summary>活動 Promotion2 autocomplete — codes containing the keyword, newest first.</summary>
    [HttpGet("promotions")]
    [ProducesResponseType(typeof(IEnumerable<PromotionLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PromotionLookup>>> SearchPromotions(
        [FromQuery] string? keyword,
        CancellationToken cancellationToken)
        => Ok(await _repository.SearchPromotionsAsync(keyword, cancellationToken));

    /// <summary>
    /// Resolve one 活動代碼 PromoCode to its Promotion2 row. PromoCode is nvarchar, so the route has
    /// no type constraint and the Angular service must encodeURIComponent it.
    /// </summary>
    [HttpGet("promotions/{promoCode}")]
    [ProducesResponseType(typeof(PromotionLookup), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromotionLookup>> GetPromotionByCode(
        string promoCode,
        CancellationToken cancellationToken)
    {
        var promotion = await _repository.GetPromotionByCodeAsync(promoCode, cancellationToken);
        return promotion is null ? NotFound() : Ok(promotion);
    }
}
