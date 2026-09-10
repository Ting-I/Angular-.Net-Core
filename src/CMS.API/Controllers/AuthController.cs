using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// 登入 Auth.
///
/// The one endpoint that sees a plaintext password. It hashes what it was given, compares that
/// against AppUser.PasswordHash, and answers with a profile plus a signed JWT. Nothing about the
/// stored credential — its existence, its state, or its hash — reaches the response.
///
/// The only controller marked [AllowAnonymous]. Everything else is covered by the fallback policy
/// in Program.cs, so this one has to opt out or nobody could obtain a token in the first place.
/// </summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    /// <summary>Log message for a credential rewritten out of the legacy hash format.</summary>
    public const string PasswordHashUpgradedMessage =
        "Upgraded the stored password hash for {UserId} to the current format on sign-in.";

    private readonly IAuthRepository _repository;
    private readonly ISysConfigRepository _sysConfigRepository;
    private readonly IJwtTokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthRepository repository,
        ISysConfigRepository sysConfigRepository,
        IJwtTokenService tokenService,
        ILogger<AuthController> logger)
    {
        _repository = repository;
        _sysConfigRepository = sysConfigRepository;
        _tokenService = tokenService;
        _logger = logger;
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

        // The one moment the plaintext exists and has just been proven correct, which is the only
        // moment a legacy unsalted row can be rewritten in the current format. It happens after
        // the decision to admit the caller and cannot change it: a failed write is logged and the
        // sign-in proceeds, because the credential is valid either way.
        await UpgradePasswordHashIfNeededAsync(credential, request.Password, cancellationToken);

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

    /// <summary>
    /// Rewrites a verified credential still stored as the legacy unsalted SHA-256 hex, so the
    /// account stops depending on a format that a leaked table turns into plaintext in minutes.
    ///
    /// It is deliberately best-effort. The repository refuses the write if the row no longer holds
    /// the hash this request verified against — a 變更密碼 that landed in between — and neither
    /// that nor a genuine failure has any bearing on whether this sign-in is legitimate, so
    /// neither changes the answer. 密碼更新時間 is untouched, or the token issued two lines below
    /// would be stale before it was returned.
    /// </summary>
    private async Task UpgradePasswordHashIfNeededAsync(
        AppUserCredential credential,
        string password,
        CancellationToken cancellationToken)
    {
        if (!PasswordHasher.NeedsUpgrade(credential.PasswordHash))
        {
            return;
        }

        var upgraded = await _repository.UpgradePasswordHashAsync(
            credential.UserId,
            credential.PasswordHash,
            PasswordHasher.Hash(password),
            cancellationToken);

        if (upgraded)
        {
            // The UserId only — the hash, old or new, is not something to write to a log.
            _logger.LogInformation(PasswordHashUpgradedMessage, credential.UserId);
        }
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
