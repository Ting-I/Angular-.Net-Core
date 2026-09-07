using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>課程 Course CRUD.</summary>
[ApiController]
[Route("api/courses")]
[Produces("application/json")]
public class CoursesController : ControllerBase
{
    private readonly ICourseRepository _repository;

    public CoursesController(ICourseRepository repository)
    {
        _repository = repository;
    }

    /// <summary>All courses.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Course>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Course>>> GetAll(CancellationToken cancellationToken)
        => Ok(await _repository.GetAllAsync(cancellationToken));

    /// <summary>Filtered search.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<Course>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Course>>> Query(
        [FromBody] CourseQuery query,
        CancellationToken cancellationToken)
        => Ok(await _repository.QueryAsync(query ?? new CourseQuery(), cancellationToken));

    /// <summary>Single course by pkid, including both n-n pkid lists.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Course), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Course>> GetById(int id, CancellationToken cancellationToken)
    {
        var course = await _repository.GetByIdAsync(id, cancellationToken);
        return course is null ? NotFound() : Ok(course);
    }

    /// <summary>Create a course. pkid is IDENTITY — any value in the request body is ignored.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Course), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Course>> Create(
        [FromBody] CourseRequest request,
        CancellationToken cancellationToken)
    {
        var pkid = await _repository.CreateAsync(request, cancellationToken);
        var created = await _repository.GetByIdAsync(pkid, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = pkid }, created);
    }

    /// <summary>Update a course. The key (pkid) comes from the body, not the route.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(Course), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Course>> Update(
        [FromBody] CourseRequest request,
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
    /// Delete a course. CourseFAQ, CourseRelatedLink and HotCourse hold enforced FKs, so a
    /// referenced row is rejected with 409 rather than surfacing SQL error 547 as a 500.
    /// CourseRecomm is guarded too: it references CourseId with no FOREIGN KEY behind it, so the
    /// database would let the delete through and silently orphan those recommendation pairs.
    /// The two junction tables are not part of the guard — those rows belong to this course and
    /// the repository deletes them alongside it.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        var references = existing.CourseFaqCount
            + existing.CourseRelatedLinkCount
            + existing.HotCourseCount
            + existing.CourseRecommCount;

        if (references > 0)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "課程仍被使用",
                Detail =
                    $"Course '{id}' is referenced by {existing.CourseFaqCount} CourseFAQ row(s), " +
                    $"{existing.CourseRelatedLinkCount} CourseRelatedLink row(s), " +
                    $"{existing.HotCourseCount} HotCourse row(s) and " +
                    $"{existing.CourseRecommCount} CourseRecomm row(s).",
            });
        }

        return await _repository.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
    }

    /// <summary>
    /// Duplicate a course under a new 簡介代碼, carrying every other scalar and both n-n relations.
    /// FriendlyUrl is copied verbatim — it carries no unique constraint, and silently mangling it
    /// would be worse than leaving an obvious duplicate for the operator to fix.
    /// </summary>
    [HttpPost("{id:int}/copy")]
    [ProducesResponseType(typeof(Course), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Course>> Copy(
        int id,
        [FromBody] CourseCopyRequest request,
        CancellationToken cancellationToken)
    {
        var source = await _repository.GetByIdAsync(id, cancellationToken);
        if (source is null)
        {
            return NotFound();
        }

        var newCourseId = request.NewCourseId.Trim();
        if (await _repository.CourseIdExistsAsync(newCourseId, excludePkid: null, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "簡介代碼已存在",
                Detail = $"CourseId '{newCourseId}' is already in use.",
            });
        }

        var newPkid = await _repository.CopyAsync(id, newCourseId, cancellationToken);
        if (newPkid is not int createdPkid)
        {
            return NotFound();
        }

        var created = await _repository.GetByIdAsync(createdPkid, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = createdPkid }, created);
    }
}
