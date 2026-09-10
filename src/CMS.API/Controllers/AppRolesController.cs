using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// 角色 AppRole CRUD.
///
/// 系統管理 Admin, so the policy is on the controller: a role definition decides what every other
/// account may do, and an operator who could rewrite one could grant themselves anything.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/app-roles")]
[Produces("application/json")]
public class AppRolesController : ControllerBase
{
    private readonly IAppRoleRepository _repository;

    public AppRolesController(IAppRoleRepository repository)
    {
        _repository = repository;
    }

    /// <summary>All roles.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AppRole>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppRole>>> GetAll(CancellationToken cancellationToken)
        => Ok(await _repository.GetAllAsync(cancellationToken));

    /// <summary>Filtered search.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<AppRole>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppRole>>> Query(
        [FromBody] AppRoleQuery query,
        CancellationToken cancellationToken)
        => Ok(await _repository.QueryAsync(query ?? new AppRoleQuery(), cancellationToken));

    /// <summary>Single role by RoleId (string PK — no :int constraint).</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AppRole), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppRole>> GetById(string id, CancellationToken cancellationToken)
    {
        var role = await _repository.GetByIdAsync(id, cancellationToken);
        return role is null ? NotFound() : Ok(role);
    }

    /// <summary>Create a role.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppRole), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppRole>> Create(
        [FromBody] AppRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (await _repository.ExistsAsync(request.RoleId, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "角色代碼已存在",
                Detail = $"AppRole '{request.RoleId}' already exists.",
            });
        }

        var roleId = await _repository.CreateAsync(request, cancellationToken);
        var created = await _repository.GetByIdAsync(roleId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = roleId }, created);
    }

    /// <summary>Update a role. The key (RoleId) comes from the body, not the route.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(AppRole), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppRole>> Update(
        [FromBody] AppRoleRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _repository.UpdateAsync(request, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        return Ok(await _repository.GetByIdAsync(request.RoleId, cancellationToken));
    }

    /// <summary>
    /// Delete a role. A role still assigned to somebody is refused with 409.
    ///
    /// This is the house rule for a destructive write, and it matters more here than for an
    /// ordinary record: <see cref="IAppRoleRepository.DeleteAsync"/> deletes the AppUserRole rows
    /// itself before deleting AppRole, so <c>FK_AppUserRole_AppRole</c> never fires and error 547
    /// never surfaces. Without this the delete simply succeeded with 204 and every account holding
    /// the role silently lost it — role claims come only from AppUserRole, so the next login for
    /// each of them would carry one fewer. <c>UserCount</c> is projected in
    /// <see cref="AppRoleSql.SelectBase"/> for exactly this check and was going unread.
    ///
    /// gstack-shortcut(dec-f9874fa0): completeness 7/10. Deleting
    /// <see cref="AuthorizationPolicies.AdminRole"/> is not refused outright, only refused while it
    /// has members. That is a narrower gap than it sounds: <c>AppUsersController</c> already
    /// refuses a caller rewriting their **own** RoleIds, so an administrator cannot strip their own
    /// Admin row and UserCount cannot be walked down to zero through the API while an
    /// administrator exists to make the calls. What is left is a role row that is *already*
    /// memberless — seeded that way, or edited straight in SQL — which can still be deleted, and
    /// for the Admin role that is unrecoverable in-band: no future token could satisfy
    /// <c>RequireRole("Admin")</c>, and recreating the role needs this very controller. Upgrade to
    /// naming the Admin role as undeletable if a bootstrap path is ever added, or if role
    /// assignments start being maintained outside the API.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        if (existing.UserCount > 0)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "角色仍被使用",
                Detail =
                    $"AppRole '{id}' is assigned to {existing.UserCount} AppUser row(s). " +
                    "Remove the assignments first — deleting the role would delete them.",
            });
        }

        return await _repository.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
    }
}
