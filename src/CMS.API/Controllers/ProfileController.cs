using System.Security.Claims;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// 個人資料 — what the signed-in operator may change about their own account.
///
/// A controller of its own rather than another action on <see cref="AuthController"/>, even though
/// it shares the /api/auth route prefix. AuthController is [AllowAnonymous], and an
/// [AllowAnonymous] anywhere in an endpoint's metadata short-circuits the authorization middleware
/// — an [Authorize] on a single action there would not put the token requirement back. Here the
/// fallback policy in Program.cs applies as it does to every other controller: protected by
/// omission.
///
/// The account written is the one the validated token names. Nothing in the request body selects a
/// row, so the endpoint cannot be pointed at somebody else's account.
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class ProfileController : ControllerBase
{
    private readonly IAppUserRepository _repository;

    public ProfileController(IAppUserRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Update the signed-in user's 使用者名稱, and nothing else. UserId and role assignments are
    /// not writable here — 使用者 AppUser CRUD is where an administrator changes those.
    /// </summary>
    [HttpPut("profile")]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProfileResponse>> UpdateProfile(
        [FromBody] ProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(JwtTokenService.UserIdClaimType);
        if (string.IsNullOrWhiteSpace(userId))
        {
            // The middleware admits nothing without a validated token, so this is a token that
            // passed validation yet carries no userId claim — nothing issued here does.
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "無法識別登入使用者",
                Detail = "The access token carries no userId claim.",
            });
        }

        // Trimmed before it is judged, so a name of spaces fails the same check an empty one does.
        // The [Required] attribute rejects both through ModelState as well; this repeats it because
        // the trimmed value is what gets written either way.
        var userName = request.UserName?.Trim() ?? string.Empty;
        if (userName.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "使用者名稱為必填",
                Detail = "UserName must not be empty or whitespace.",
            });
        }

        if (!await _repository.UpdateUserNameAsync(userId, userName, cancellationToken))
        {
            // The token outlived the account it names — it is valid for 24 hours and the row can
            // be deleted inside that window.
            return NotFound();
        }

        var user = await _repository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(new ProfileResponse
        {
            UserId = user.UserId,
            UserName = user.UserName,
            RoleIds = user.RoleIds,
        });
    }
}
