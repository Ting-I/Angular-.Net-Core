using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// Slim lists used to populate select / multiselect options.
///
/// **The one controller in the API where the Admin policy sits on actions rather than on the
/// class.** Everywhere else the attribute goes at controller level so an action added later is
/// covered by omission, and that is still the rule — but this controller is deliberately mixed:
/// 發布狀態, 原廠, 課程群組, 認證, 職務類別, 訓練中心 and 活動 are needed by every operator filling
/// in a 課程 form, while 使用者 and 角色 are the same identity data
/// <see cref="AppUsersController"/> and <see cref="AppRolesController"/> keep behind
/// <see cref="AuthorizationPolicies.Admin"/>. A class-level attribute would break the 課程 form;
/// no attribute at all served the account roster to any token holder, which is what it did until
/// now.
///
/// So the two admin lookups say so themselves, and the cost of that shape is explicit: an action
/// added here is **not** covered by omission. If it reads a 系統管理 Admin table, it needs the
/// attribute, and <c>AuthorizationTests</c> names both current ones by reflection so a third
/// added without it fails there rather than shipping open.
/// </summary>
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

    /// <summary>
    /// 使用者 AppUser lookup list — 系統管理 Admin.
    ///
    /// The whole account roster, unfiltered: every 使用者代碼, which is the exact value
    /// <c>POST /api/auth/login</c> takes. Its only callers are the 角色 AppRole detail and form
    /// pages, both behind <c>adminGuard</c>, so nothing legitimate loses by requiring the role —
    /// and without it the endpoint handed a spray list to the lowest-privilege token in the system,
    /// which <c>GET /api/app-users</c> refuses in the same breath.
    /// </summary>
    [HttpGet("app-users")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(IEnumerable<AppUserLookup>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<AppUserLookup>>> GetAppUsers(CancellationToken cancellationToken)
        => Ok(await _repository.GetAppUsersAsync(cancellationToken));

    /// <summary>
    /// 角色 AppRole lookup list — 系統管理 Admin.
    ///
    /// Carries 權限等級 PermissionLevel and is ordered by it, so it is the authority ranking of
    /// every role in the system as well as the set of strings a <c>roleIds</c> write would name.
    /// Its only callers are the 使用者 AppUser list, detail and form pages, all behind
    /// <c>adminGuard</c>.
    /// </summary>
    [HttpGet("app-roles")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(IEnumerable<AppRoleLookup>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
