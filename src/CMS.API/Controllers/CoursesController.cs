using System.Security.Claims;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>課程 Course CRUD, plus the write-free 課程簡介 export record.</summary>
[ApiController]
[Route("api/courses")]
[Produces("application/json")]
public class CoursesController : ControllerBase
{
    /// <summary>
    /// One constant template with named placeholders, never an interpolated string: interpolation
    /// would flatten the values into the message and throw away the structure this line exists for.
    /// </summary>
    private const string SheetExportMessage =
        "Course sheet print requested: operator {UserId}/{UserName}, course {Pkid}/{CourseId} at {At}";

    /// <summary>`CourseId` is `varchar(50)` in the schema; UserName matches the audit column.</summary>
    private const int CourseIdLength = 50;

    private readonly ICourseRepository _repository;
    private readonly IAppUserRepository _appUsers;
    private readonly ILogger<CoursesController> _logger;
    private readonly TimeProvider _timeProvider;

    public CoursesController(
        ICourseRepository repository,
        IAppUserRepository appUsers,
        ILogger<CoursesController> logger,
        TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _appUsers = appUsers;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
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

    /// <summary>
    /// Records that an operator asked their browser for this course's 課程簡介 PDF.
    ///
    /// **It writes nothing.** No table is touched, so there is no 異動紀錄 row and no
    /// <see cref="IRowAuditWriter"/> call: the house rule binds Insert / Update / Delete, and
    /// RowAudit's own shape — TableName, PrimaryKeyValues, ActionType — would assert a change that
    /// did not happen. RowAudit answers "who changed this row"; this answers "is this button used".
    ///
    /// **What it is, precisely.** One structured log line, and a *print requested* one at that:
    /// the browser's print dialog is where the operator chooses Save or Cancel, and that choice is
    /// not observable from a page, so this cannot claim a document was produced, let alone sent.
    /// There is no durable log sink configured in this application either, so today the line lands
    /// wherever the host's console logger points and nothing retains it — see the 異動紀錄 section
    /// of `spec/conventions/backend.md`, and TODOS.md for the sink.
    ///
    /// The operator's 使用者名稱 is read from AppUser rather than taken from the token, for the same
    /// reason the audit writer reads it: the userName claim is only as fresh as the login that issued
    /// it, and a rename re-issues nothing. There is no transaction here to read it on, so this is a
    /// plain repository call, and it falls back to the claim and then to "system".
    /// </summary>
    [HttpPost("{id:int}/sheet")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LogSheetExport(int id, CancellationToken cancellationToken)
    {
        var course = await _repository.GetByIdAsync(id, cancellationToken);
        if (course is null)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(JwtTokenService.UserIdClaimType);
        var operatorId = Loggable(userId, RowAuditEntry.UserNameLength);

        _logger.LogInformation(
            SheetExportMessage,
            operatorId.Length > 0 ? operatorId : RowAuditWriterDefaults.SystemUserName,
            Loggable(await ResolveUserNameAsync(userId, cancellationToken), RowAuditEntry.UserNameLength),
            course.Pkid,
            Loggable(course.CourseId, CourseIdLength),
            _timeProvider.GetLocalNow().ToString("O"));

        return NoContent();
    }

    /// <summary>
    /// 使用者名稱 as AppUser holds it now, falling back to the token's claim and then to
    /// <see cref="RowAuditWriterDefaults.SystemUserName"/> — a request with no userId claim cannot
    /// have reached here through the normal pipeline, but the log line still has to say something.
    /// </summary>
    private async Task<string> ResolveUserNameAsync(string? userId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var user = await _appUsers.GetByIdAsync(userId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(user?.UserName))
            {
                return user.UserName;
            }
        }

        var claimed = User.FindFirstValue(JwtTokenService.UserNameClaimType);
        return string.IsNullOrWhiteSpace(claimed) ? RowAuditWriterDefaults.SystemUserName : claimed;
    }

    /// <summary>
    /// Makes one value safe to log. `CourseId` is operator-entered and UserName is operator-editable
    /// through PUT /api/auth/profile, so either can carry a newline — and a formatter renders the
    /// value verbatim even when the placeholder kept it out of the message template, which is how a
    /// CR/LF forges whole log lines inside the one record meant as evidence. Control characters
    /// become spaces and the value is truncated to its column width, the same stance
    /// <see cref="AuditHelper.Truncate"/> takes: a record never breaks its own consumer.
    /// </summary>
    private static string Loggable(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var flattened = string.Concat(value.Select(c => char.IsControl(c) ? ' ' : c)).Trim();
        return AuditHelper.Truncate(flattened, maxLength) ?? string.Empty;
    }
}
