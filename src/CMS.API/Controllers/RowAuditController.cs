using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// 異動紀錄 RowAudit — read-only, and one record at a time.
///
/// There is no list endpoint and no write endpoint here by design. The trail is written by the
/// repository that made the change, through <see cref="IRowAuditWriter"/>, so an endpoint that
/// could add to it would be an endpoint that could lie about what happened.
/// </summary>
[ApiController]
[Route("api/rowaudit")]
[Produces("application/json")]
public class RowAuditController : ControllerBase
{
    private readonly IRowAuditRepository _repository;

    public RowAuditController(IRowAuditRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// One record's audit trail, newest first: <c>GET /api/rowaudit?tableName=Course&amp;pkid=123</c>.
    ///
    /// Both parameters are required and neither has a usable default. pkid is nullable rather than
    /// a plain int so that a missing one is a 400 instead of silently becoming a query for pkid 0 —
    /// which is a real key for a table whose pkid is not IDENTITY, and would answer a malformed
    /// request with a straight face.
    ///
    /// An unknown table or key is an empty array, not a 404: "this record has no history yet" is
    /// the answer the badge renders, and it is not an error.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RowAuditHistoryEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RowAuditHistoryEntry>>> GetForRecord(
        [FromQuery] string? tableName,
        [FromQuery] int? pkid,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "缺少資料表名稱",
                Detail = "tableName is required.",
            });
        }

        if (pkid is not int key)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "缺少主代碼",
                Detail = "pkid is required.",
            });
        }

        return Ok(await _repository.GetForRecordAsync(tableName.Trim(), key, cancellationToken));
    }
}
