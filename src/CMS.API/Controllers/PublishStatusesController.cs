using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// 發布狀態 PublishStatus CRUD.
///
/// 系統管理 Admin, so the policy is on the controller. Every operator still reads the list through
/// <c>GET /api/lookups/publish-statuses</c>, which the 課程 Course form needs and which stays open
/// to any signed-in caller; what is behind the policy is editing the set itself.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/publish-statuses")]
[Produces("application/json")]
public class PublishStatusesController : ControllerBase
{
    private readonly IPublishStatusRepository _repository;

    public PublishStatusesController(IPublishStatusRepository repository)
    {
        _repository = repository;
    }

    /// <summary>All publish statuses.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PublishStatus>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PublishStatus>>> GetAll(CancellationToken cancellationToken)
        => Ok(await _repository.GetAllAsync(cancellationToken));

    /// <summary>Filtered search.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<PublishStatus>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PublishStatus>>> Query(
        [FromBody] PublishStatusQuery query,
        CancellationToken cancellationToken)
        => Ok(await _repository.QueryAsync(query ?? new PublishStatusQuery(), cancellationToken));

    /// <summary>Single publish status by pkid.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PublishStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublishStatus>> GetById(byte id, CancellationToken cancellationToken)
    {
        var status = await _repository.GetByIdAsync(id, cancellationToken);
        return status is null ? NotFound() : Ok(status);
    }

    /// <summary>Create a publish status. pkid is supplied by the caller — the column is not IDENTITY.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PublishStatus), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PublishStatus>> Create(
        [FromBody] PublishStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (await _repository.ExistsAsync(request.Pkid, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "主代碼已存在",
                Detail = $"PublishStatus '{request.Pkid}' already exists.",
            });
        }

        var pkid = await _repository.CreateAsync(request, cancellationToken);
        var created = await _repository.GetByIdAsync(pkid, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = (int)pkid }, created);
    }

    /// <summary>Update a publish status. The key (pkid) comes from the body, not the route.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(PublishStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublishStatus>> Update(
        [FromBody] PublishStatusRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _repository.UpdateAsync(request, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        return Ok(await _repository.GetByIdAsync(request.Pkid, cancellationToken));
    }

    /// <summary>
    /// Delete a publish status. Course and Promotion2 hold enforced FKs, so a referenced row is
    /// rejected with 409 rather than surfacing SQL error 547 as a 500.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(byte id, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        if (existing.CourseCount + existing.PromotionCount > 0)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "發布狀態仍被使用",
                Detail =
                    $"PublishStatus '{id}' is referenced by {existing.CourseCount} Course row(s) " +
                    $"and {existing.PromotionCount} Promotion2 row(s).",
            });
        }

        return await _repository.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
    }
}
