using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// 登入 Auth.
///
/// The one endpoint that sees a plaintext password. It hashes what it was given, compares that
/// against AppUser.PasswordHash, and answers with a profile plus a signed JWT. Nothing about the
/// stored credential — its existence, its state, or its hash — reaches the response.
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthRepository _repository;
    private readonly ISysConfigRepository _sysConfigRepository;
    private readonly IJwtTokenService _tokenService;

    public AuthController(
        IAuthRepository repository,
        ISysConfigRepository sysConfigRepository,
        IJwtTokenService tokenService)
    {
        _repository = repository;
        _sysConfigRepository = sysConfigRepository;
        _tokenService = tokenService;
    }

    /// <summary>
    /// Sign in with a UserId and password. Returns the user profile and a 24-hour access token
    /// carrying the account's role claims.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var credential = await _repository.GetCredentialAsync(request.UserId, cancellationToken);

        // One answer for all three failures — unknown user, disabled account, wrong password — so
        // the response cannot be used to enumerate accounts. Every arm still runs the hash compare
        // where it can, rather than short-circuiting on the cheap check first.
        if (credential is null ||
            !credential.IsActive ||
            !PasswordHasher.Matches(request.Password, credential.PasswordHash))
        {
            return InvalidCredentials();
        }

        var signingSecret = await _sysConfigRepository.GetSymmetricSecurityKeyAsync(cancellationToken);
        if (!JwtTokenService.IsUsableSecret(signingSecret))
        {
            return MissingSigningKey();
        }

        var accessToken = _tokenService.CreateAccessToken(
            credential.UserId,
            credential.UserName,
            credential.RoleIds,
            signingSecret!);

        return Ok(new LoginResponse
        {
            UserId = credential.UserId,
            UserName = credential.UserName,
            AccessToken = accessToken,
        });
    }

    /// <summary>The single generic rejection. It must stay free of anything attempt-specific.</summary>
    private ObjectResult InvalidCredentials() => StatusCode(
        StatusCodes.Status401Unauthorized,
        new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "帳號或密碼錯誤",
            Detail = "Invalid credentials.",
        });

    /// <summary>
    /// A foreseeable data condition — no 'appConfig' row, no symmetricSecurityKey in it, or a key
    /// too short for HS256 — answered as a 500 ProblemDetails rather than left to throw, the way
    /// AppUsersController handles a missing defaultPassword.
    /// </summary>
    private ObjectResult MissingSigningKey() => StatusCode(
        StatusCodes.Status500InternalServerError,
        new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "系統設定缺少簽章金鑰",
            Detail = "SysConfig 'appConfig' does not supply a usable symmetricSecurityKey value.",
        });
}
