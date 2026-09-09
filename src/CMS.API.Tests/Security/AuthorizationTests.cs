using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Security;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Tests.Security;

/// <summary>
/// The global authorization rule: every endpoint needs a bearer token signed with the SysConfig
/// key, except <see cref="AuthController"/>, which has to stay reachable or nobody could ever get
/// a token. Run through the real pipeline, because the 401 comes from middleware rather than from
/// any controller.
/// </summary>
public class AuthorizationTests : IClassFixture<TestApiFactory>
{
    private const string Password = "Uwa@2026";
    private const string SigningKey = FakeSysConfigRepository.ValidSigningKey;

    /// <summary>A protected endpoint from each controller shape the API exposes.</summary>
    public static TheoryData<string> ProtectedEndpoints =>
    [
        "/api/app-roles",
        "/api/app-roles/Admin",
        "/api/app-users",
        "/api/courses",
        "/api/publish-statuses",
        "/api/lookups/app-roles",
        "/api/rowaudit?tableName=Course&pkid=1",
    ];

    private readonly TestApiFactory _factory;

    public AuthorizationTests(TestApiFactory factory)
    {
        _factory = factory;
        _factory.SysConfig.SymmetricSecurityKey = SigningKey;
        _factory.Auth.Seed("helen", "Helen Lin", Password, true, "Admin");
    }

    private HttpClient Client() => _factory.CreateClient();

    private static HttpRequestMessage Get(string url, string? accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (accessToken is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");
        }

        return request;
    }

    /// <summary>A token from the real login endpoint — the same one a browser would hold.</summary>
    private async Task<string> LoginAsync()
    {
        var response = await Client().PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { UserId = "helen", Password = Password });

        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return profile!.AccessToken;
    }

    /// <summary>A hand-signed token, for the arms the login endpoint cannot produce.</summary>
    private static string TokenSignedWith(string signingKey, DateTime? expires = null)
    {
        var issuedAt = expires is null ? DateTime.UtcNow : expires.Value.AddHours(-24);

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(JwtTokenService.UserIdClaimType, "helen")]),
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expires ?? issuedAt.AddHours(24),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256),
        });
    }

    // ---------- Without a token ----------

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task ProtectedEndpoint_WithoutAToken_Returns401(string url)
    {
        var response = await Client().SendAsync(Get(url, accessToken: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutAToken_ChallengesWithBearer()
    {
        var response = await Client().SendAsync(Get("/api/app-roles", accessToken: null));

        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
    }

    [Fact]
    public async Task ProtectedWriteEndpoint_WithoutAToken_Returns401AndWritesNothing()
    {
        var response = await Client().PostAsJsonAsync(
            "/api/app-roles",
            new AppRoleRequest { RoleId = "Sneaky", RoleName = "Sneaky", PermissionLevel = 1 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_factory.AppRoles.CreatedRoleIds);
    }

    // ---------- With a token ----------

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task ProtectedEndpoint_WithAValidToken_IsNotUnauthorized(string url)
    {
        var response = await Client().SendAsync(Get(url, await LoginAsync()));

        // A 404 for an unseeded key is a legitimate answer here; a 401 is not.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithAValidToken_Returns200()
    {
        var response = await Client().SendAsync(Get("/api/app-roles", await LoginAsync()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(await response.Content.ReadFromJsonAsync<List<AppRole>>());
    }

    // ---------- Tokens that must not be accepted ----------

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("")]
    public async Task ProtectedEndpoint_WithAMalformedToken_Returns401(string accessToken)
    {
        var response = await Client().SendAsync(Get("/api/app-roles", accessToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithATokenSignedByAnotherKey_Returns401()
    {
        var response = await Client().SendAsync(
            Get("/api/app-roles", TokenSignedWith("another4fun#123456another4fun#12")));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithAnExpiredToken_Returns401()
    {
        // ClockSkew is zero, so an hour past expiry is past expiry.
        var response = await Client().SendAsync(
            Get("/api/app-roles", TokenSignedWith(SigningKey, DateTime.UtcNow.AddHours(-1))));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WhenSysConfigHasNoUsableKey_Returns401RatherThanAccepting()
    {
        var accessToken = TokenSignedWith(SigningKey);
        _factory.SysConfig.SymmetricSecurityKey = null;

        try
        {
            var response = await Client().SendAsync(Get("/api/app-roles", accessToken));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            _factory.SysConfig.SymmetricSecurityKey = SigningKey;
        }
    }

    // ---------- The validation key is the SysConfig one ----------

    [Fact]
    public async Task ValidationReadsTheSigningKeyFromSysConfigPerRequest()
    {
        var before = _factory.SysConfig.GetSymmetricSecurityKeyCallCount;

        await Client().SendAsync(Get("/api/app-roles", TokenSignedWith(SigningKey)));

        // Read for the validation itself — no login took part in this call.
        Assert.Equal(before + 1, _factory.SysConfig.GetSymmetricSecurityKeyCallCount);
    }

    [Fact]
    public async Task ARequestWithNoBearerHeaderNeverReadsTheSigningKey()
    {
        var before = _factory.SysConfig.GetSymmetricSecurityKeyCallCount;

        await Client().SendAsync(Get("/api/app-roles", accessToken: null));

        Assert.Equal(before, _factory.SysConfig.GetSymmetricSecurityKeyCallCount);
    }

    // ---------- Auth stays anonymous ----------

    [Fact]
    public async Task Login_WithoutAToken_Succeeds()
    {
        var response = await Client().PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { UserId = "helen", Password = Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.Equal("Helen Lin", profile!.UserName);
        Assert.False(string.IsNullOrWhiteSpace(profile.AccessToken));
    }

    [Fact]
    public async Task Login_WithBadCredentials_Returns401FromTheControllerNotTheMiddleware()
    {
        var response = await Client().PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { UserId = "helen", Password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // The controller's own body, which the middleware's bare challenge would not carry.
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("帳號或密碼錯誤", problem!.Title);
    }

    // ---------- The rule itself ----------

    [Fact]
    public void TheFallbackPolicyRequiresAnAuthenticatedUser()
    {
        var options = _factory.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        Assert.NotNull(options.FallbackPolicy);
        Assert.Contains(
            options.FallbackPolicy!.Requirements,
            requirement => requirement is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public void AuthControllerIsTheOnlyControllerThatAllowsAnonymousAccess()
    {
        var anonymous = typeof(Program).Assembly
            .GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .Where(type => type.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null)
            .Select(type => type.Name)
            .Order()
            .ToArray();

        Assert.Equal([nameof(AuthController)], anonymous);
    }

    [Fact]
    public void NoActionOutsideAuthControllerAllowsAnonymousAccess()
    {
        // A per-action opt-out would open a hole the controller-level check above cannot see.
        var actions = typeof(Program).Assembly
            .GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .Where(type => type != typeof(AuthController))
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
            .ToArray();

        Assert.Empty(actions);
    }
}
