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
/// The account written is the one the validated token names. Nothing in either request body
/// selects a row, so neither endpoint can be pointed at somebody else's account.
///
/// PUT /api/auth/profile writes 使用者名稱; POST /api/auth/change-password writes PasswordHash and
/// PasswordUpdatedTime. The second is the only place outside login that reads a stored hash, and
/// no hash appears in either request or either response.
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class ProfileController : ControllerBase
{
    private readonly IAppUserRepository _repository;
    private readonly IAuthRepository _authRepository;

    public ProfileController(IAppUserRepository repository, IAuthRepository authRepository)
    {
        _repository = repository;
        _authRepository = authRepository;
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
            // The middleware admits nothing without a validated token, so getting here means a
            // token that passed validation yet carries no userId claim.
            return NoUserIdClaim();
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
            // The row went away between the token being validated and this write. TokenFreshness
            // now refuses a token whose AppUser row is gone, so this is no longer the 24-hour
            // window it used to be — it is the race inside one request, which is still real.
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

    /// <summary>
    /// 變更密碼 — replaces the signed-in operator's own password.
    ///
    /// Five gates, in the order the spec sets them out: the current password must hash to the
    /// stored value, the new password must clear <see cref="PasswordPolicy"/>, it must differ from
    /// the current one, the confirmation must match it, and only then is anything written. Every
    /// failure returns before the write, so a rejected request leaves PasswordHash and
    /// PasswordUpdatedTime exactly as they were.
    ///
    /// Answers 204, because there is nothing to return. No hash — neither the stored one nor the
    /// new one — reaches the response, the same rule that keeps PasswordHash out of
    /// <see cref="AppUserSql.SelectBase"/>.
    /// </summary>
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(JwtTokenService.UserIdClaimType);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return NoUserIdClaim();
        }

        // AuthSql.SelectCredential is the only query in the API that returns a hash; the value it
        // brings back is compared here and goes no further.
        var credential = await _authRepository.GetCredentialAsync(userId, cancellationToken);
        if (credential is null)
        {
            // Same race as UpdateProfile above: TokenFreshness refuses a token naming a row that
            // is already gone, so reaching here means it went in between.
            return NotFound();
        }

        // 1. The current password must match what is stored. A 400 rather than a 401: the caller's
        // token is perfectly good, and a 401 would trip the UI's interceptor into clearing the
        // session and bouncing to /login — throwing the operator out over a typo.
        if (!PasswordHasher.Matches(request.CurrentPassword, credential.PasswordHash))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "目前密碼錯誤",
                Detail = "The current password does not match the stored credential.",
            });
        }

        // 2. Complexity of the new password.
        if (!PasswordPolicy.IsAcceptable(request.NewPassword))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = PasswordPolicy.RequirementMessage,
                Detail = PasswordPolicy.RequirementDetail,
            });
        }

        // 3. The new password has to actually be a different one. Without this the endpoint
        // answers 204 to a request that re-submits the current password, and the value an operator
        // is most likely to re-submit is the SysConfig defaultPassword their account was created
        // with — shared by every account, and the one password worth moving off. The comparison is
        // against the plaintext the caller sent rather than the stored hash, which is equivalent
        // here: gate 1 has already proven CurrentPassword hashes to that row.
        //
        // It sits after the complexity check, not before it, so an operator retyping something
        // that fails both is told the rule first — the half they have to satisfy either way.
        if (string.Equals(request.NewPassword, request.CurrentPassword, StringComparison.Ordinal))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "新密碼不可與目前密碼相同",
                Detail = "NewPassword must differ from CurrentPassword.",
            });
        }

        // 4. Confirmation. Ordinal, so a password differing only in case or in a combining mark is
        // a mismatch — what gets hashed is the byte sequence, and the login compare is exact too.
        if (!string.Equals(request.NewPassword, request.ConfirmNewPassword, StringComparison.Ordinal))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "新密碼與確認新密碼不一致",
                Detail = "NewPassword and ConfirmNewPassword must match.",
            });
        }

        // 5. Write. ResetPasswordAsync sets PasswordHash and PasswordUpdatedTime and nothing else —
        // not the name, and not the role assignments UpdateAsync would rewrite from a request that
        // carries none.
        if (!await _repository.ResetPasswordAsync(
                userId,
                PasswordHasher.Hash(request.NewPassword),
                cancellationToken))
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>
    /// A token that passed validation yet carries no userId claim. Nothing issued here does, so
    /// this is a token from elsewhere signed with the same key.
    /// </summary>
    private UnauthorizedObjectResult NoUserIdClaim() => Unauthorized(new ProblemDetails
    {
        Status = StatusCodes.Status401Unauthorized,
        Title = "無法識別登入使用者",
        Detail = "The access token carries no userId claim.",
    });
}
