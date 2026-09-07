using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>課程群組 CourseGroup CRUD.</summary>
[ApiController]
[Route("api/course-groups")]
[Produces("application/json")]
public class CourseGroupsController : ControllerBase
{
    private readonly ICourseGroupRepository _repository;

    public CourseGroupsController(ICourseGroupRepository repository)
    {
        _repository = repository;
    }

    /// <summary>All course groups.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CourseGroup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CourseGroup>>> GetAll(CancellationToken cancellationToken)
        => Ok(await _repository.GetAllAsync(cancellationToken));

    /// <summary>Filtered search.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<CourseGroup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CourseGroup>>> Query(
        [FromBody] CourseGroupQuery query,
        CancellationToken cancellationToken)
        => Ok(await _repository.QueryAsync(query ?? new CourseGroupQuery(), cancellationToken));

    /// <summary>Single course group by pkid.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CourseGroup), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseGroup>> GetById(short id, CancellationToken cancellationToken)
    {
        var courseGroup = await _repository.GetByIdAsync(id, cancellationToken);
        return courseGroup is null ? NotFound() : Ok(courseGroup);
    }

    /// <summary>Create a course group. pkid is IDENTITY — any value in the request body is ignored.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CourseGroup), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CourseGroup>> Create(
        [FromBody] CourseGroupRequest request,
        CancellationToken cancellationToken)
    {
        var pkid = await _repository.CreateAsync(request, cancellationToken);
        var created = await _repository.GetByIdAsync(pkid, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = (int)pkid }, created);
    }

    /// <summary>Update a course group. The key (pkid) comes from the body, not the route.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(CourseGroup), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseGroup>> Update(
        [FromBody] CourseGroupRequest request,
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
    /// Delete a course group, refusing with 409 while anything still references it.
    ///
    /// This guard is load-bearing, not cosmetic. PartnerCourseGroup holds an ordinary enforced FK
    /// and would raise SQL error 547 on its own, but FK_Course_CourseGroup is ON DELETE CASCADE:
    /// the database would accept the DELETE and destroy every Course row filed under this group
    /// (and, transitively, their CourseInCertification and CourseJobCategories rows). Do not relax
    /// this check to a bare DELETE.
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

        var references = existing.CourseCount + existing.PartnerCourseGroupCount;
        if (references > 0)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "課程群組仍被使用",
                Detail =
                    $"CourseGroup '{id}' is referenced by {existing.CourseCount} Course row(s) and " +
                    $"{existing.PartnerCourseGroupCount} PartnerCourseGroup row(s). The Course foreign key " +
                    "cascades on delete, so removing this group would delete those courses.",
            });
        }

        return await _repository.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
    }
}
