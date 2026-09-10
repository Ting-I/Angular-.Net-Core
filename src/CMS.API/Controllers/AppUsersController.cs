using System.Security.Claims;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// 使用者 AppUser CRUD.
///
/// The controller owns the plaintext default password for the duration of one call and hands the
/// repository a hash; no password value ever enters or leaves this API.
///
/// 系統管理 Admin, so the policy is on the controller rather than on each action — an action added
/// later is covered by omission. Two guards sit inside the policy rather than beside it, because
/// holding the Admin role is not licence to do these things to your own account:
///
/// - **An operator may not rewrite their own 角色.** <see cref="Update"/> takes RoleIds from the
///   body and the repository rewrites AppUserRole from it, so without this an account could hand
///   itself a role it was not given — and an administrator could strip their own Admin role and
///   lock the sub-system away from everybody.
/// - **An operator may not reset their own password.** <see cref="ResetPassword"/> writes the
///   shared SysConfig default, which is the weakest value in the system;
///   <c>POST /api/auth/change-password</c> is where you change your own, and it asks for the
///   current one first.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
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
        if (IsSelf(request.UserId))
        {
            // Read before the write, and only on this path: the comparison is against what the
            // account holds now, not against what the request says it holds.
            var self = await _repository.GetByIdAsync(request.UserId, cancellationToken);
            if (self is null)
            {
                return NotFound();
            }

            if (ChangesRoles(self.RoleIds, request.RoleIds))
            {
                return CannotChangeOwnRoles();
            }
        }

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
        if (IsSelf(id))
        {
            return CannotResetOwnPassword();
        }

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

    /// <summary>
    /// True when the request names the signed-in operator's own account. UserId is compared
    /// case-insensitively, because SQL Server's default collation is and every other lookup on the
    /// key behaves the same way — a guard that "Helen" slipped past as "helen" would be no guard.
    /// A request with no userId claim matches nothing: the fallback policy admits nothing without a
    /// validated token, and the endpoints that act on the caller answer their own 401 for it.
    /// </summary>
    private bool IsSelf(string? userId)
    {
        var caller = HttpContext?.User?.FindFirstValue(JwtTokenService.UserIdClaimType);
        return !string.IsNullOrWhiteSpace(caller) &&
               !string.IsNullOrWhiteSpace(userId) &&
               string.Equals(caller, userId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True when the requested 角色 set differs from the stored one. Order and duplicates do not
    /// count as a difference — the repository writes a distinct, case-insensitive set — so a form
    /// that round-trips the same roles in another order is an ordinary edit, not an escalation.
    /// </summary>
    private static bool ChangesRoles(IEnumerable<string> current, IEnumerable<string>? requested)
    {
        var before = Normalize(current);
        var after = Normalize(requested ?? []);
        return !before.SetEquals(after);
    }

    private static HashSet<string> Normalize(IEnumerable<string> roleIds) =>
        roleIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 403 rather than 400: the request is well formed and the caller is authenticated and an
    /// administrator — it is this particular target that is refused.
    /// </summary>
    private ObjectResult CannotChangeOwnRoles() => StatusCode(
        StatusCodes.Status403Forbidden,
        new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "無法變更自己的角色",
            Detail = "An operator cannot change the role assignments of their own account.",
        });

    /// <summary>403, for the same reason, and it names where the operator should go instead.</summary>
    private ObjectResult CannotResetOwnPassword() => StatusCode(
        StatusCodes.Status403Forbidden,
        new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "無法重設自己的密碼，請使用變更密碼",
            Detail = "An operator cannot reset their own password; use POST /api/auth/change-password.",
        });

    /// <summary>The SysConfig default password, hashed; null when it is unavailable.</summary>
    private async Task<string?> HashDefaultPasswordAsync(CancellationToken cancellationToken)
    {
        var defaultPassword = await _sysConfigRepository.GetDefaultPasswordAsync(cancellationToken);
        return string.IsNullOrEmpty(defaultPassword) ? null : PasswordHasher.Hash(defaultPassword);
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
