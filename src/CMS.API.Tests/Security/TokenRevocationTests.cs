using System.Net;
using System.Net.Http.Json;
using CMS.API.Models;
using CMS.API.Tests.Fakes;

namespace CMS.API.Tests.Security;

/// <summary>
/// Deprovisioning an account has to end its sessions, and a JWT does not notice on its own.
///
/// <c>ChangePasswordTests</c> covers the password-change arm of <c>TokenFreshness</c>. This covers
/// the two that were missing: an account **disabled** and an account **deleted**. Both used to
/// leave the operator's existing token fully valid for the rest of its 24 hours — the freshness
/// query read PasswordUpdatedTime alone, so a deleted row came back as null and read as "password
/// never changed", and 啟用 was consulted only at login.
///
/// Run through <see cref="TestApiFactory"/>: the refusal is produced by the bearer middleware, so
/// it is only observable through the real pipeline, and that is also what proves it applies to
/// every controller rather than to whichever one the test happened to call.
///
/// The account state is changed on <see cref="FakeAuthRepository"/> directly, because that fake's
/// credential store *is* its AppUser table — the one row both <c>SelectCredential</c> and
/// <c>SelectTokenState</c> read in the real repository. What is under test is the middleware's
/// answer to a given row state, not which endpoint wrote it.
/// </summary>
public class TokenRevocationTests : IClassFixture<TestApiFactory>
{
    private const string Password = "Uwa@2026";

    private readonly TestApiFactory _factory;

    public TokenRevocationTests(TestApiFactory factory)
    {
        _factory = factory;
        _factory.SysConfig.SymmetricSecurityKey = FakeSysConfigRepository.ValidSigningKey;
    }

    private HttpClient Client() => _factory.CreateClient();

    /// <summary>Signs in an enabled account and returns the token a browser would then hold.</summary>
    private async Task<string> SignInAsync(string userId, params string[] roleIds)
    {
        _factory.Auth.Seed(userId, "Helen Lin", Password, true, roleIds);

        var response = await Client().PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { UserId = userId, Password = Password });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
    }

    private async Task<HttpResponseMessage> GetWithTokenAsync(string url, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");
        return await Client().SendAsync(request);
    }

    // ---------- Disabled ----------

    [Fact]
    public async Task DisablingTheAccount_RefusesTheTokenItAlreadyIssued()
    {
        var accessToken = await SignInAsync("revoke-disable", "Admin");

        Assert.Equal(
            HttpStatusCode.OK,
            (await GetWithTokenAsync("/api/app-roles", accessToken)).StatusCode);

        // 啟用 off — what an administrator does through PUT /api/app-users precisely when they want
        // somebody out. It leaves PasswordUpdatedTime alone, which is why the old check missed it.
        _factory.Auth.Disable("revoke-disable");

        var after = await GetWithTokenAsync("/api/app-roles", accessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task DisablingTheAccount_RefusesTheTokenOnEveryController()
    {
        // The middleware answers, so this is not one endpoint remembering to check.
        var accessToken = await SignInAsync("revoke-disable-all", "Admin");
        _factory.Auth.Disable("revoke-disable-all");

        foreach (var url in new[]
        {
            "/api/app-users",
            "/api/courses",
            "/api/partners",
            "/api/publish-statuses",
            "/api/lookups/publish-statuses",
            "/api/rowaudit?tableName=Course&pkid=1",
        })
        {
            var response = await GetWithTokenAsync(url, accessToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task DisablingTheAccount_RefusesATokenNewerThanAnyPasswordChange()
    {
        // The stale-token comparison would pass this token: its iat is later than every
        // PasswordUpdatedTime there is. 啟用 has to be its own answer, not a side effect of the clock.
        var accessToken = await SignInAsync("revoke-disable-fresh", "Admin");
        _factory.AppUsers.Seed(new AppUser
        {
            Pkid = 1,
            UserId = "revoke-disable-fresh",
            UserName = "Helen Lin",
            IsActive = false,
            PasswordUpdatedTime = DateTime.UtcNow.AddHours(-1),
        });
        _factory.Auth.Disable("revoke-disable-fresh");

        var after = await GetWithTokenAsync("/api/app-roles", accessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    // ---------- Deleted ----------

    [Fact]
    public async Task DeletingTheAccount_RefusesTheTokenItAlreadyIssued()
    {
        var accessToken = await SignInAsync("revoke-delete", "Admin");

        Assert.Equal(
            HttpStatusCode.OK,
            (await GetWithTokenAsync("/api/app-roles", accessToken)).StatusCode);

        // The row is gone, as DELETE /api/app-users/{id} leaves it. This is the worse of the two
        // cases: there is no longer anything to reset a password on, so before this check the token
        // was not revocable at all — it simply had to age out.
        _factory.Auth.Remove("revoke-delete");

        var after = await GetWithTokenAsync("/api/app-roles", accessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task DeletingTheAccount_RefusesTheTokenEvenThoughNoPasswordWasEverChanged()
    {
        // The exact conflation the old shape had: PasswordUpdatedTime null meant both "no such row"
        // and "never changed", and the second is a token to accept. Nothing here ever changed a
        // password, so the refusal can only be coming from the row's absence.
        var accessToken = await SignInAsync("revoke-delete-nochange", "Admin");
        Assert.Null(_factory.AppUsers.PasswordUpdatedTimeOf("revoke-delete-nochange"));

        _factory.Auth.Remove("revoke-delete-nochange");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await GetWithTokenAsync("/api/app-roles", accessToken)).StatusCode);
    }

    // ---------- The refusal says nothing ----------

    [Fact]
    public async Task ARefusedTokenIsIndistinguishableFromAForgedOne()
    {
        // context.Fail, not a thrown exception: a caller must not be able to tell a deprovisioned
        // account from a bad signature, or the 401 becomes an account-state oracle.
        var accessToken = await SignInAsync("revoke-quiet", "Admin");
        _factory.Auth.Remove("revoke-quiet");

        var refused = await GetWithTokenAsync("/api/app-roles", accessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.Empty(await refused.Content.ReadAsStringAsync());
        Assert.DoesNotContain(
            refused.Headers.WwwAuthenticate.ToString(),
            new[] { "account", "disabled", "deleted" },
            StringComparer.OrdinalIgnoreCase);
    }

    // ---------- The cost of it ----------

    [Fact]
    public async Task TheAccountStateIsReadOncePerAuthenticatedRequest()
    {
        var accessToken = await SignInAsync("revoke-cost", "Admin");
        var before = _factory.Auth.FreshnessChecks.Count;

        await GetWithTokenAsync("/api/app-roles", accessToken);

        Assert.Equal(before + 1, _factory.Auth.FreshnessChecks.Count);
        Assert.Equal("revoke-cost", _factory.Auth.FreshnessChecks[^1]);
    }

    [Fact]
    public async Task ARequestWithNoTokenNeverReadsTheAccountState()
    {
        // The query hangs off OnTokenValidated, so an anonymous login costs nothing extra.
        var before = _factory.Auth.FreshnessChecks.Count;

        await Client().GetAsync("/api/app-roles");

        Assert.Equal(before, _factory.Auth.FreshnessChecks.Count);
    }

    // ---------- 角色 is deliberately not covered ----------

    [Fact]
    public async Task RemovingARoleDoesNotTakeEffectUntilTheTokenExpires()
    {
        // Pinned as a known limit, not as desired behaviour: role claims are stamped at login and
        // RequireRole reads them from the token, so this is what "revoke the role" actually does
        // today. TokenFreshness says so in its own doc comment. Disable or delete the account when
        // the revocation has to be immediate — the two tests above are why that now works.
        var accessToken = await SignInAsync("revoke-role", "Admin");

        _factory.Auth.Seed("revoke-role", "Helen Lin", Password, true);

        var after = await GetWithTokenAsync("/api/app-users", accessToken);

        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }
}
