using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// 使用者 AppUser CRUD.
///
/// The controller owns the plaintext default password for the duration of one call and hands the
/// repository a hash; no password value ever enters or leaves this API.
/// </summary>
[ApiController]
[Route("api/app-users")]
[Produces("application/json")]
public class AppUsersController : ControllerBase
{
    private readonly IAppUserRepository _repository;
    private readonly ISysConfigRepository _sysConfigRepository;

    public AppUsersController(IAppUserRepository repository, ISysConfigRepository sysConfigRepository)
    {
        _repository = repository;
        _sysConfigRepository = sysConfigRepository;
    }

    /// <summary>All users.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AppUser>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppUser>>> GetAll(CancellationToken cancellationToken)
        => Ok(await _repository.GetAllAsync(cancellationToken));

    /// <summary>Filtered search.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<AppUser>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppUser>>> Query(
        [FromBody] AppUserQuery query,
        CancellationToken cancellationToken)
        => Ok(await _repository.QueryAsync(query ?? new AppUserQuery(), cancellationToken));

    /// <summary>Single user by UserId (string PK — no :int constraint).</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AppUser), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppUser>> GetById(string id, CancellationToken cancellationToken)
    {
        var user = await _repository.GetByIdAsync(id, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>
    /// Create a user. The account is given the SysConfig default password, hashed here; the request
    /// carries no password of any kind.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppUser), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AppUser>> Create(
        [FromBody] AppUserRequest request,
        CancellationToken cancellationToken)
    {
        if (await _repository.ExistsAsync(request.UserId, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "使用者代碼已存在",
                Detail = $"AppUser '{request.UserId}' already exists.",
            });
        }

        var passwordHash = await HashDefaultPasswordAsync(cancellationToken);
        if (passwordHash is null)
        {
            return MissingDefaultPassword();
        }

        var userId = await _repository.CreateAsync(request, passwordHash, cancellationToken);
        var created = await _repository.GetByIdAsync(userId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = userId }, created);
    }

    /// <summary>
    /// Update a user. The key (UserId) comes from the body, not the route, and PasswordHash is
    /// left exactly as it was — only <see cref="ResetPassword"/> changes it.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(AppUser), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppUser>> Update(
        [FromBody] AppUserRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _repository.UpdateAsync(request, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        return Ok(await _repository.GetByIdAsync(request.UserId, cancellationToken));
    }

    /// <summary>Reset the password back to the SysConfig default. Takes no body and returns none.</summary>
    [HttpPost("{id}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ResetPassword(string id, CancellationToken cancellationToken)
    {
        if (!await _repository.ExistsAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var passwordHash = await HashDefaultPasswordAsync(cancellationToken);
        if (passwordHash is null)
        {
            return MissingDefaultPassword();
        }

        return await _repository.ResetPasswordAsync(id, passwordHash, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    /// <summary>Delete a user. Junction rows go with it, inside the same transaction.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
        => await _repository.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    /// <summary>The SysConfig default password, hashed; null when it is unavailable.</summary>
    private async Task<string?> HashDefaultPasswordAsync(CancellationToken cancellationToken)
    {
        var defaultPassword = await _sysConfigRepository.GetDefaultPasswordAsync(cancellationToken);
        return string.IsNullOrEmpty(defaultPassword) ? null : PasswordHasher.Sha256Hex(defaultPassword);
    }

    /// <summary>
    /// A foreseeable data condition — no 'appConfig' row, or no defaultPassword in it — answered as
    /// a 500 ProblemDetails rather than left to surface as an unhandled exception.
    /// </summary>
    private ObjectResult MissingDefaultPassword() => StatusCode(
        StatusCodes.Status500InternalServerError,
        new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "系統設定缺少預設密碼",
            Detail = "SysConfig 'appConfig' does not supply a usable defaultPassword value.",
        });
}
