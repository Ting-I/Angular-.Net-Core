using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Security;
using CMS.API.Tests.Fakes;
using CMS.API.Tests.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Tests.Controllers;

/// <summary>
/// Covers PUT /api/auth/profile: the signed-in operator renaming themselves, and the three things
/// they must not be able to do with it — rename somebody else, change their own roles, or blank
/// the name.
///
/// Most arms run against a controller instance, as the rest of the suite does. The ones that turn
/// on what the request body carried on the wire run through <see cref="TestApiFactory"/> instead:
/// <see cref="ProfileRequest"/> has no UserId property, so a controller-level test could not even
/// express a body that names one — the deserializer is the thing being tested there.
/// </summary>
public class ProfileControllerTests : IClassFixture<TestApiFactory>
{
    private const string Password = "Uwa@2026";

    private readonly TestApiFactory _factory;

    public ProfileControllerTests(TestApiFactory factory)
    {
        _factory = factory;
        _factory.SysConfig.SymmetricSecurityKey = FakeSysConfigRepository.ValidSigningKey;
    }

    private static AppUser User(string userId, string userName, params string[] roleIds) => new()
    {
        Pkid = 1,
        UserId = userId,
        UserName = userName,
        IsActive = true,
        RoleIds = [.. roleIds],
        RoleCount = roleIds.Length,
    };

    // ---------- Controller instance ----------

    /// <summary>A controller whose User is the token's ClaimsPrincipal, as the middleware sets it.</summary>
    private static ProfileController CreateController(
        FakeAppUserRepository repository,
        string? signedInUserId,
        FakeAuthRepository? authRepository = null)
    {
        List<Claim> claims = signedInUserId is null
            ? []
            : [new Claim(JwtTokenService.UserIdClaimType, signedInUserId)];

        return new ProfileController(repository, authRepository ?? new FakeAuthRepository())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")),
                },
            },
        };
    }

    private static ProfileResponse AssertOk(ActionResult<ProfileResponse> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<ProfileResponse>(ok.Value);
    }

    [Fact]
    public async Task UpdateProfile_WritesTheUserNameForTheJwtUser()
    {
        var repository = new FakeAppUserRepository().Seed(User("helen", "Helen Lin", "Admin"));
        var controller = CreateController(repository, "helen");

        var profile = AssertOk(await controller.UpdateProfile(
            new ProfileRequest { UserName = "Helen Chen" },
            CancellationToken.None));

        Assert.Equal("helen", profile.UserId);
        Assert.Equal("Helen Chen", profile.UserName);
        Assert.Equal([("helen", "Helen Chen")], repository.UpdatedUserNames);
    }

    [Fact]
    public async Task UpdateProfile_WritesNoOtherAccount()
    {
        // Two accounts, one token: only the row the token names may move.
        var repository = new FakeAppUserRepository()
            .Seed(User("helen", "Helen Lin"), User("miles", "Miles Sun"));
        var controller = CreateController(repository, "helen");

        await controller.UpdateProfile(new ProfileRequest { UserName = "Helen Chen" }, CancellationToken.None);

        Assert.Equal("Miles Sun", (await repository.GetByIdAsync("miles"))!.UserName);
        Assert.Equal([("helen", "Helen Chen")], repository.UpdatedUserNames);
    }

    [Fact]
    public async Task UpdateProfile_LeavesTheRolesAlone()
    {
        var repository = new FakeAppUserRepository().Seed(User("helen", "Helen Lin", "Admin", "Editor"));
        var controller = CreateController(repository, "helen");

        var profile = AssertOk(await controller.UpdateProfile(
            new ProfileRequest { UserName = "Helen Chen" },
            CancellationToken.None));

        Assert.Equal(["Admin", "Editor"], profile.RoleIds);
        Assert.Equal(["Admin", "Editor"], (await repository.GetByIdAsync("helen"))!.RoleIds);

        // The n-n rewrite lives in UpdateAsync, and this endpoint must never reach it.
        Assert.Empty(repository.UpdatedUserIds);
    }

    [Fact]
    public async Task UpdateProfile_TrimsTheUserName()
    {
        var repository = new FakeAppUserRepository().Seed(User("helen", "Helen Lin"));
        var controller = CreateController(repository, "helen");

        var profile = AssertOk(await controller.UpdateProfile(
            new ProfileRequest { UserName = "  Helen Chen  " },
            CancellationToken.None));

        Assert.Equal("Helen Chen", profile.UserName);
        Assert.Equal([("helen", "Helen Chen")], repository.UpdatedUserNames);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task UpdateProfile_WithABlankUserName_Returns400AndWritesNothing(string userName)
    {
        var repository = new FakeAppUserRepository().Seed(User("helen", "Helen Lin"));
        var controller = CreateController(repository, "helen");

        var result = await controller.UpdateProfile(
            new ProfileRequest { UserName = userName },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal("使用者名稱為必填", problem.Title);

        Assert.Empty(repository.UpdatedUserNames);
        Assert.Equal("Helen Lin", (await repository.GetByIdAsync("helen"))!.UserName);
    }

    [Fact]
    public async Task UpdateProfile_WhenTheTokensUserNoLongerExists_Returns404()
    {
        // A token is good for 24 hours; the account it names can be deleted inside that window.
        var repository = new FakeAppUserRepository().Seed(User("miles", "Miles Sun"));
        var controller = CreateController(repository, "helen");

        var result = await controller.UpdateProfile(
            new ProfileRequest { UserName = "Helen Chen" },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        Assert.Empty(repository.UpdatedUserNames);
    }

    [Fact]
    public async Task UpdateProfile_WithNoUserIdClaim_Returns401AndWritesNothing()
    {
        var repository = new FakeAppUserRepository().Seed(User("helen", "Helen Lin"));
        var controller = CreateController(repository, signedInUserId: null);

        var result = await controller.UpdateProfile(
            new ProfileRequest { UserName = "Helen Chen" },
            CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
        Assert.Empty(repository.UpdatedUserNames);
    }

    // ---------- The request model itself ----------

    [Fact]
    public void ProfileRequest_CarriesUserNameAndNothingElse()
    {
        // The guard behind "UserId cannot be changed through this endpoint": there is no property
        // for a UserId — or a role — to bind to in the first place.
        Assert.Equal(["UserName"], typeof(ProfileRequest).GetProperties().Select(p => p.Name).Order());
    }

    // ---------- Through the real pipeline, where the JSON body matters ----------

    private HttpClient Client() => _factory.CreateClient();

    /// <summary>Signs in through the real login endpoint and returns the token a browser would hold.</summary>
    private async Task<string> SignInAsync(string userId, string userName, params string[] roleIds)
    {
        _factory.Auth.Seed(userId, userName, Password, true, roleIds);
        _factory.AppUsers.Seed(User(userId, userName, roleIds));

        var response = await Client().PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { UserId = userId, Password = Password });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
    }

    private async Task<HttpResponseMessage> PutProfileAsync(string? accessToken, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/auth/profile")
        {
            Content = JsonContent.Create(body),
        };

        if (accessToken is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");
        }

        return await Client().SendAsync(request);
    }

    [Fact]
    public async Task UpdateProfile_IgnoresAUserIdSentInTheRequestBody()
    {
        var accessToken = await SignInAsync("body-helen", "Helen Lin", "Admin");
        _factory.AppUsers.Seed(User("body-victim", "Victim Name"));

        // The body names another account, plus roles it must not be able to grant itself.
        var response = await PutProfileAsync(accessToken, new
        {
            userId = "body-victim",
            userName = "Helen Chen",
            roleIds = new[] { "Admin", "SuperUser" },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.Equal("body-helen", profile!.UserId);
        Assert.Equal("Helen Chen", profile.UserName);
        Assert.Equal(["Admin"], profile.RoleIds);

        // The named account is untouched, and the token's own account is the only one written.
        Assert.Equal("Victim Name", (await _factory.AppUsers.GetByIdAsync("body-victim"))!.UserName);
        Assert.Equal("Helen Chen", (await _factory.AppUsers.GetByIdAsync("body-helen"))!.UserName);
    }

    [Fact]
    public async Task UpdateProfile_WithoutAToken_Returns401AndWritesNothing()
    {
        // The endpoint shares AuthController's /api/auth prefix but not its [AllowAnonymous]:
        // ProfileController is a controller of its own precisely so the fallback policy applies.
        _factory.AppUsers.Seed(User("anon-helen", "Helen Lin"));

        var response = await PutProfileAsync(accessToken: null, new { userName = "Sneaky" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Helen Lin", (await _factory.AppUsers.GetByIdAsync("anon-helen"))!.UserName);
    }

    [Fact]
    public async Task UpdateProfile_WithABlankUserNameOnTheWire_Returns400()
    {
        var accessToken = await SignInAsync("blank-helen", "Helen Lin");

        var response = await PutProfileAsync(accessToken, new { userName = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Helen Lin", (await _factory.AppUsers.GetByIdAsync("blank-helen"))!.UserName);
    }
}
