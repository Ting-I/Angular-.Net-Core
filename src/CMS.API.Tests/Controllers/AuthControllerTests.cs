using System.Text;
using System.Text.Json;
using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Security;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Tests.Controllers;

/// <summary>
/// Covers POST /api/auth/login: the one success arm, the three rejection arms that must be
/// indistinguishable, the claims the issued token carries, and the promise that no hash escapes.
/// The real <see cref="JwtTokenService"/> is used — it touches nothing external, and a stub token
/// would test nothing worth testing.
/// </summary>
public class AuthControllerTests
{
    private const string Password = "Uwa@2026";
    private const string SigningKey = FakeSysConfigRepository.ValidSigningKey;

    private static (AuthController Controller, FakeAuthRepository Repository, FakeSysConfigRepository SysConfig)
        CreateController(FakeAuthRepository? repository = null)
    {
        var (controller, authRepository, sysConfig, _) = CreateControllerWithLogger(repository);
        return (controller, authRepository, sysConfig);
    }

    /// <summary>
    /// The same controller with its logger in hand, for the hash-upgrade arm — the only behaviour
    /// here whose only outward sign is a log line.
    /// </summary>
    private static (
        AuthController Controller,
        FakeAuthRepository Repository,
        FakeSysConfigRepository SysConfig,
        CapturingLogger<AuthController> Logger)
        CreateControllerWithLogger(FakeAuthRepository? repository = null)
    {
        var authRepository = repository ?? new FakeAuthRepository();
        var sysConfig = new FakeSysConfigRepository { SymmetricSecurityKey = SigningKey };
        var logger = new CapturingLogger<AuthController>();
        return (new AuthController(authRepository, sysConfig, new JwtTokenService(), logger), authRepository, sysConfig, logger);
    }

    private static LoginRequest Login(string userId, string password) =>
        new() { UserId = userId, Password = password };

    private static LoginResponse AssertOk(ActionResult<LoginResponse> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<LoginResponse>(ok.Value);
    }

    private static ProblemDetails AssertStatus(ActionResult<LoginResponse> result, int statusCode)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(statusCode, objectResult.StatusCode);
        return Assert.IsType<ProblemDetails>(objectResult.Value);
    }

    private static JsonWebToken Decode(string accessToken) => new(accessToken);

    private static string[] RolesOf(JsonWebToken token) =>
        [.. token.Claims.Where(c => c.Type == JwtTokenService.RoleClaimType).Select(c => c.Value).Order()];

    // ---------- Success ----------

    [Fact]
    public async Task Login_WithValidActiveUser_ReturnsTheProfileAndAToken()
    {
        var (controller, _, _) = CreateController(
            new FakeAuthRepository().Seed("helen", "Helen Lin", Password));

        var profile = AssertOk(await controller.Login(Login("helen", Password), CancellationToken.None));

        Assert.Equal("helen", profile.UserId);
        Assert.Equal("Helen Lin", profile.UserName);
        Assert.False(string.IsNullOrWhiteSpace(profile.AccessToken));
    }

    [Fact]
    public async Task Login_AcceptsAStoredHashInUppercaseHex()
    {
        var (controller, _, _) = CreateController(
            new FakeAuthRepository().SeedWithUppercaseHash("helen", "Helen Lin", Password));

        var result = await controller.Login(Login("helen", Password), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    // ---------- Rejection: one generic 401 for every cause ----------

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var (controller, _, _) = CreateController(
            new FakeAuthRepository().Seed("helen", "Helen Lin", Password));

        var problem = AssertStatus(
            await controller.Login(Login("helen", "not-the-password"), CancellationToken.None),
            StatusCodes.Status401Unauthorized);

        Assert.Equal("帳號或密碼錯誤", problem.Title);
    }

    [Fact]
    public async Task Login_WithUnknownUserId_Returns401()
    {
        var (controller, _, _) = CreateController(
            new FakeAuthRepository().Seed("helen", "Helen Lin", Password));

        AssertStatus(
            await controller.Login(Login("nobody", Password), CancellationToken.None),
            StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Login_WithInactiveUser_Returns401EvenWhenThePasswordIsRight()
    {
        var (controller, _, _) = CreateController(
            new FakeAuthRepository().Seed("helen", "Helen Lin", Password, isActive: false, "Admin"));

        AssertStatus(
            await controller.Login(Login("helen", Password), CancellationToken.None),
            StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Login_WithBlankPassword_Returns401()
    {
        var (controller, _, _) = CreateController(
            new FakeAuthRepository().Seed("helen", "Helen Lin", Password));

        AssertStatus(
            await controller.Login(Login("helen", string.Empty), CancellationToken.None),
            StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Login_RejectionsAreIndistinguishable()
    {
        // The point of the generic message: nothing in the three bodies says which check failed,
        // so the endpoint cannot be used to enumerate accounts.
        var (controller, _, _) = CreateController(new FakeAuthRepository()
            .Seed("helen", "Helen Lin", Password)
            .Seed("miles", "Miles Sun", Password, isActive: false));

        var wrongPassword = AssertStatus(
            await controller.Login(Login("helen", "wrong"), CancellationToken.None), 401);
        var unknownUser = AssertStatus(
            await controller.Login(Login("nobody", Password), CancellationToken.None), 401);
        var inactive = AssertStatus(
            await controller.Login(Login("miles", Password), CancellationToken.None), 401);

        Assert.Equal(wrongPassword.Title, unknownUser.Title);
        Assert.Equal(wrongPassword.Title, inactive.Title);
        Assert.Equal(wrongPassword.Detail, unknownUser.Detail);
        Assert.Equal(wrongPassword.Detail, inactive.Detail);
    }

    [Fact]
    public async Task Login_WhenRejected_IssuesNoToken()
    {
        var (controller, _, sysConfig) = CreateController(
            new FakeAuthRepository().Seed("helen", "Helen Lin", Password));

        await controller.Login(Login("helen", "wrong"), CancellationToken.None);

        // The signing key is never even read on a failed attempt.
        Assert.Equal(0, sysConfig.GetSymmetricSecurityKeyCallCount);
    }

    // ---------- The issued token ----------

    [Fact]
    public async Task Login_TokenCarriesTheUserIdAndUserNameClaims()
    {
        var (controller, _, _) = CreateController(
            new FakeAuthRepository().Seed("helen", "Helen Lin", Password));

        var profile = AssertOk(await controller.Login(Login("helen", Password), CancellationToken.None));
        var token = Decode(profile.AccessToken);

        Assert.Equal("helen", token.GetClaim(JwtTokenService.UserIdClaimType).Value);
        Assert.Equal("Helen Lin", token.GetClaim(JwtTokenService.UserNameClaimType).Value);
        Assert.Equal("helen", token.GetClaim(JwtRegisteredClaimNames.Sub).Value);
    }

    [Fact]
    public async Task Login_TokenCarriesEveryRoleAssignedToTheUser()
    {
        var (controller, _, _) = CreateController(
            new FakeAuthRepository().Seed("helen", "Helen Lin", Password, true, "Admin", "Editor"));

        var profile = AssertOk(await controller.Login(Login("helen", Password), CancellationToken.None));

        Assert.Equal(["Admin", "Editor"], RolesOf(Decode(profile.AccessToken)));
    }

    [Fact]
    public async Task Login_WithNoRoles_TokenCarriesNoRoleClaims()
    {
        var (controller, _, _) = CreateController(
            new FakeAuthRepository().Seed("helen", "Helen Lin", Password));

        var profile = AssertOk(await controller.Login(Login("helen", Password), CancellationToken.None));

        Assert.Empty(RolesOf(Decode(profile.AccessToken)));
    }

    [Fact]
    public async Task Login_TokenExpires24HoursAfterIssue()
    {
        var (controller, _, _) = CreateController(
            new FakeAuthRepository().Seed("helen", "Helen Lin", Password));

        var issuedAround = DateTime.UtcNow;
        var profile = AssertOk(await controller.Login(Login("helen", Password), CancellationToken.None));
        var token = Decode(profile.AccessToken);

        // A minute of slack: the token is stamped from the wall clock, not an injected one.
        Assert.Equal(issuedAround.AddHours(24), token.ValidTo, TimeSpan.FromMinutes(1));
        Assert.Equal(issuedAround, token.ValidFrom, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Login_TokenIsSignedWithTheSysConfigKey()
    {
        var (controller, _, sysConfig) = CreateController(
            new FakeAuthRepository().Seed("helen", "Helen Lin", Password));

        var profile = AssertOk(await controller.Login(Login("helen", Password), CancellationToken.None));

        Assert.Equal(1, sysConfig.GetSymmetricSecurityKeyCallCount);
        Assert.True((await Validate(profile.AccessToken, SigningKey)).IsValid);

        // ...and only with that key.
        Assert.False((await Validate(profile.AccessToken, "another4fun#123456another4fun#12")).IsValid);
    }

    private static Task<TokenValidationResult> Validate(string accessToken, string signingKey) =>
        new JsonWebTokenHandler().ValidateTokenAsync(accessToken, new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
        });

    // ---------- The hash never leaves ----------

    [Fact]
    public async Task Login_ResponseNeverCarriesThePasswordHash()
    {
        var (controller, _, _) = CreateController(
            new FakeAuthRepository().Seed("helen", "Helen Lin", Password, true, "Admin"));

        var profile = AssertOk(await controller.Login(Login("helen", Password), CancellationToken.None));

        // Serialized, because a leak would arrive as a property on the wire, not as a typed member.
        var json = JsonSerializer.Serialize(profile);
        var hash = PasswordHasher.Sha256Hex(Password);

        Assert.DoesNotContain(hash, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);

        // Nor is it hidden inside the token's payload.
        Assert.DoesNotContain(hash, Decode(profile.AccessToken).EncodedPayload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoginResponse_CarriesOnlyTheThreeSpecifiedProperties()
    {
        // A guard on the model itself: adding a password-bearing property breaks this before it can
        // reach a response.
        Assert.Equal(
            ["AccessToken", "UserId", "UserName"],
            typeof(LoginResponse).GetProperties().Select(p => p.Name).Order());
    }

    // ---------- Configuration faults ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too-short-for-hs256")]
    public async Task Login_WhenTheSigningKeyIsUnusable_Returns500(string? signingKey)
    {
        var repository = new FakeAuthRepository().Seed("helen", "Helen Lin", Password);
        var sysConfig = new FakeSysConfigRepository { SymmetricSecurityKey = signingKey };
        var controller = new AuthController(
            repository,
            sysConfig,
            new JwtTokenService(),
            new CapturingLogger<AuthController>());

        var problem = AssertStatus(
            await controller.Login(Login("helen", Password), CancellationToken.None),
            StatusCodes.Status500InternalServerError);

        Assert.Equal("系統設定缺少簽章金鑰", problem.Title);
    }

    // ---------- Upgrading a legacy password hash on sign-in ----------

    [Fact]
    public async Task Login_WithALegacyHash_RewritesItInTheCurrentFormat()
    {
        var repository = new FakeAuthRepository().SeedLegacy("helen", "Helen Lin", Password);
        var (controller, _, _, _) = CreateControllerWithLogger(repository);
        var before = repository.PasswordHashOf("helen");

        AssertOk(await controller.Login(Login("helen", Password), CancellationToken.None));

        var after = repository.PasswordHashOf("helen");
        Assert.NotEqual(before, after);
        Assert.StartsWith(PasswordHasher.Pbkdf2Prefix, after);
        Assert.False(PasswordHasher.NeedsUpgrade(after));

        // The upgraded row still verifies the same password — the point of the exercise.
        Assert.True(PasswordHasher.Matches(Password, after));
    }

    [Fact]
    public async Task Login_WithALegacyHash_PassesTheVerifiedHashAsTheExpectedValue()
    {
        var repository = new FakeAuthRepository().SeedLegacy("helen", "Helen Lin", Password);
        var (controller, _, _, _) = CreateControllerWithLogger(repository);

        AssertOk(await controller.Login(Login("helen", Password), CancellationToken.None));

        var upgrade = Assert.Single(repository.UpgradedHashes);
        Assert.Equal("helen", upgrade.UserId);

        // The guard on the UPDATE: the row is only rewritten while it still holds what was verified.
        Assert.Equal(PasswordHasher.Sha256Hex(Password), upgrade.ExpectedHash);
    }

    [Fact]
    public async Task Login_WithALegacyHash_LogsTheUpgradeWithoutEitherHash()
    {
        var repository = new FakeAuthRepository().SeedLegacy("helen", "Helen Lin", Password);
        var (controller, _, _, logger) = CreateControllerWithLogger(repository);

        AssertOk(await controller.Login(Login("helen", Password), CancellationToken.None));

        var entry = logger.Single(LogLevel.Information);
        Assert.Equal("helen", entry.Text("UserId"));
        Assert.DoesNotContain(PasswordHasher.Sha256Hex(Password), entry.Message);
        Assert.DoesNotContain(Password, entry.Message);
    }

    [Fact]
    public async Task Login_WithACurrentHash_UpgradesNothing()
    {
        var repository = new FakeAuthRepository().Seed("helen", "Helen Lin", Password);
        var (controller, _, _, logger) = CreateControllerWithLogger(repository);
        var before = repository.PasswordHashOf("helen");

        AssertOk(await controller.Login(Login("helen", Password), CancellationToken.None));

        Assert.Empty(repository.UpgradedHashes);
        Assert.Empty(logger.Entries);
        Assert.Equal(before, repository.PasswordHashOf("helen"));
    }

    [Fact]
    public async Task Login_WithTheWrongPassword_UpgradesNothing()
    {
        // The rewrite is only safe where the plaintext has just been proven correct.
        var repository = new FakeAuthRepository().SeedLegacy("helen", "Helen Lin", Password);
        var (controller, _, _, _) = CreateControllerWithLogger(repository);

        AssertStatus(
            await controller.Login(Login("helen", "wrong"), CancellationToken.None),
            StatusCodes.Status401Unauthorized);

        Assert.Empty(repository.UpgradedHashes);
        Assert.Equal(PasswordHasher.Sha256Hex(Password), repository.PasswordHashOf("helen"));
    }

    [Fact]
    public async Task Login_WithALegacyHash_LeavesPasswordUpdatedTimeAlone()
    {
        // Moving it would make TokenFreshness reject the token this very login returns.
        var repository = new FakeAuthRepository().SeedLegacy("helen", "Helen Lin", Password);
        var changedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        repository.PasswordUpdatedTimeSource = _ => changedAt;
        var (controller, _, _, _) = CreateControllerWithLogger(repository);

        AssertOk(await controller.Login(Login("helen", Password), CancellationToken.None));

        Assert.Equal(changedAt, (await repository.GetTokenStateAsync("helen"))?.PasswordUpdatedTime);
    }
}
