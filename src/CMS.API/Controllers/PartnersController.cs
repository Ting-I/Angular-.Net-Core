using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>原廠 Partner CRUD.</summary>
[ApiController]
[Route("api/partners")]
[Produces("application/json")]
public class PartnersController : ControllerBase
{
    private readonly IPartnerRepository _repository;

    public PartnersController(IPartnerRepository repository)
    {
        _repository = repository;
    }

    /// <summary>All partners.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Partner>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Partner>>> GetAll(CancellationToken cancellationToken)
        => Ok(await _repository.GetAllAsync(cancellationToken));

    /// <summary>Filtered search.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<Partner>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Partner>>> Query(
        [FromBody] PartnerQuery query,
        CancellationToken cancellationToken)
        => Ok(await _repository.QueryAsync(query ?? new PartnerQuery(), cancellationToken));

    /// <summary>Single partner by pkid.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Partner), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Partner>> GetById(short id, CancellationToken cancellationToken)
    {
        var partner = await _repository.GetByIdAsync(id, cancellationToken);
        return partner is null ? NotFound() : Ok(partner);
    }

    /// <summary>Create a partner. pkid is IDENTITY — any value in the request body is ignored.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Partner), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Partner>> Create(
        [FromBody] PartnerRequest request,
        CancellationToken cancellationToken)
    {
        var pkid = await _repository.CreateAsync(request, cancellationToken);
        var created = await _repository.GetByIdAsync(pkid, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = (int)pkid }, created);
    }

    /// <summary>Update a partner. The key (pkid) comes from the body, not the route.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(Partner), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Partner>> Update(
        [FromBody] PartnerRequest request,
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
    /// Delete a partner. Certification, Course, PartnerCourseGroup and Promotion2 hold enforced FKs,
    /// so a referenced row is rejected with 409 rather than surfacing SQL error 547 as a 500.
    /// Seminar is guarded too: its reference has no FK constraint, so the database would let the
    /// delete through and silently orphan those rows.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(short id, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        var references = existing.CertificationCount
            + existing.CourseCount
            + existing.CourseGroupCount
            + existing.PromotionCount
            + existing.SeminarCount;

        if (references > 0)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "原廠仍被使用",
                Detail =
                    $"Partner '{id}' is referenced by {existing.CertificationCount} Certification row(s), " +
                    $"{existing.CourseCount} Course row(s), {existing.CourseGroupCount} PartnerCourseGroup row(s), " +
                    $"{existing.PromotionCount} Promotion2 row(s) and {existing.SeminarCount} Seminar row(s).",
            });
        }

        return await _repository.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
    }
}
