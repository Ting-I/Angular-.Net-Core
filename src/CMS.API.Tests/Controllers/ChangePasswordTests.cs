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
/// Covers POST /api/auth/change-password: the five gates in front of the write, and the promise
/// that a request failing any of them leaves PasswordHash and PasswordUpdatedTime alone.
///
/// The controller arms run against an instance, as the rest of the suite does. The wire arms run
/// through <see cref="TestApiFactory"/>, because what they test is the request body — a
/// <see cref="ChangePasswordRequest"/> has no UserId property, so a controller-level test could
/// not express a body that names another account.
/// </summary>
public class ChangePasswordTests : IClassFixture<TestApiFactory>
{
    private const string CurrentPassword = "Uwa@2026";
    private const string NewPassword = "N3wPass!word";

    /// <summary>Where FakeAppUserRepository.ResetPasswordAsync stamps PasswordUpdatedTime.</summary>
    private static readonly DateTime FakeNow = new(2026, 9, 8, 3, 0, 0, DateTimeKind.Utc);

    private readonly TestApiFactory _factory;

    public ChangePasswordTests(TestApiFactory factory)
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

    /// <summary>
    /// The two repositories the endpoint uses, seeded consistently: the auth fake holds the
    /// credential the current-password check reads, the AppUser fake holds the row the write
    /// lands on.
    /// </summary>
    private sealed record Fixture(
        ProfileController Controller,
        FakeAppUserRepository Users,
        FakeAuthRepository Auth);

    private static Fixture CreateFixture(
        string? signedInUserId = "helen",
        string storedUserId = "helen",
        params string[] roleIds)
    {
        var users = new FakeAppUserRepository { UtcNow = FakeNow }
            .Seed(User(storedUserId, "Helen Lin", roleIds));
        var auth = new FakeAuthRepository().Seed(storedUserId, "Helen Lin", CurrentPassword, true, roleIds);

        List<Claim> claims = signedInUserId is null
            ? []
            : [new Claim(JwtTokenService.UserIdClaimType, signedInUserId)];

        var controller = new ProfileController(users, auth)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")),
                },
            },
        };

        return new Fixture(controller, users, auth);
    }

    private static ChangePasswordRequest Request(
        string current = CurrentPassword,
        string? newPassword = null,
        string? confirm = null)
    {
        var replacement = newPassword ?? NewPassword;
        return new ChangePasswordRequest
        {
            CurrentPassword = current,
            NewPassword = replacement,
            ConfirmNewPassword = confirm ?? replacement,
        };
    }

    private static ProblemDetails AssertBadRequest(IActionResult result)
    {
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        return Assert.IsType<ProblemDetails>(badRequest.Value);
    }

    /// <summary>Asserts the stored credential is untouched: the seeded hash, and no timestamp.</summary>
    private static async Task AssertNothingWritten(Fixture fixture, string userId = "helen")
    {
        Assert.Empty(fixture.Users.ResetUserIds);
        Assert.Equal($"seed-hash-{userId}", fixture.Users.PasswordHashOf(userId));
        Assert.Null((await fixture.Users.GetByIdAsync(userId))!.PasswordUpdatedTime);
    }

    // ---------- 5. The successful change ----------

    [Fact]
    public async Task ChangePassword_WithAValidRequest_WritesAHashOfTheNewPassword()
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.ChangePassword(Request(), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(["helen"], fixture.Users.ResetUserIds);
        Assert.True(PasswordHasher.Matches(NewPassword, fixture.Users.PasswordHashOf("helen")));
    }

    [Fact]
    public async Task ChangePassword_WithAValidRequest_StampsPasswordUpdatedTime()
    {
        var fixture = CreateFixture();
        Assert.Null((await fixture.Users.GetByIdAsync("helen"))!.PasswordUpdatedTime);

        await fixture.Controller.ChangePassword(Request(), CancellationToken.None);

        Assert.Equal(FakeNow, (await fixture.Users.GetByIdAsync("helen"))!.PasswordUpdatedTime);
    }

    [Fact]
    public async Task ChangePassword_TakesTheAccountFromTheTokenAndNowhereElse()
    {
        var fixture = CreateFixture(signedInUserId: "helen");

        await fixture.Controller.ChangePassword(Request(), CancellationToken.None);

        // The credential read and the write both name the token's user.
        Assert.Equal(["helen"], fixture.Auth.LookedUpUserIds);
        Assert.Equal(["helen"], fixture.Users.ResetUserIds);
    }

    [Fact]
    public async Task ChangePassword_LeavesTheNameAndRolesAlone()
    {
        var fixture = CreateFixture(roleIds: ["Admin", "Editor"]);

        await fixture.Controller.ChangePassword(Request(), CancellationToken.None);

        var user = (await fixture.Users.GetByIdAsync("helen"))!;
        Assert.Equal("Helen Lin", user.UserName);
        Assert.Equal(["Admin", "Editor"], user.RoleIds);

        // Neither the n-n rewrite in UpdateAsync nor the rename in UpdateUserNameAsync is reached.
        Assert.Empty(fixture.Users.UpdatedUserIds);
        Assert.Empty(fixture.Users.UpdatedUserNames);
    }

    // ---------- 1. The current password ----------

    [Fact]
    public async Task ChangePassword_WithTheWrongCurrentPassword_Returns400AndWritesNothing()
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.ChangePassword(
            Request(current: "not-the-password"),
            CancellationToken.None);

        Assert.Equal("目前密碼錯誤", AssertBadRequest(result).Title);
        await AssertNothingWritten(fixture);
    }

    [Fact]
    public async Task ChangePassword_WithTheWrongCurrentPassword_IsNotA401()
    {
        // A 401 would trip the UI interceptor into clearing the session — a typo must not sign the
        // operator out.
        var fixture = CreateFixture();

        var result = await fixture.Controller.ChangePassword(
            Request(current: "not-the-password"),
            CancellationToken.None);

        Assert.IsNotType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, AssertBadRequest(result).Status);
    }

    [Fact]
    public async Task ChangePassword_ComparesTheCurrentPasswordCaseSensitively()
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.ChangePassword(
            Request(current: CurrentPassword.ToUpperInvariant()),
            CancellationToken.None);

        Assert.Equal("目前密碼錯誤", AssertBadRequest(result).Title);
        await AssertNothingWritten(fixture);
    }

    [Fact]
    public async Task ChangePassword_ChecksTheCurrentPasswordBeforeTheComplexityRule()
    {
        // Both are wrong. The current-password answer is the one that comes back, so a caller
        // holding a stolen token learns nothing about the policy without knowing the password.
        var fixture = CreateFixture();

        var result = await fixture.Controller.ChangePassword(
            Request(current: "not-the-password", newPassword: "short"),
            CancellationToken.None);

        Assert.Equal("目前密碼錯誤", AssertBadRequest(result).Title);
        await AssertNothingWritten(fixture);
    }

    // ---------- 2. Complexity ----------

    [Theory]
    [InlineData("Ab1!")]        // four classes, too short
    [InlineData("Abc123!")]     // seven characters
    [InlineData("abcdefgh")]    // one class
    [InlineData("Abcdefgh")]    // two classes
    [InlineData("abcdefg1")]    // two classes
    [InlineData("12345678")]    // one class
    public async Task ChangePassword_WithAWeakNewPassword_Returns400AndWritesNothing(string weak)
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.ChangePassword(
            Request(newPassword: weak),
            CancellationToken.None);

        var problem = AssertBadRequest(result);
        Assert.Equal(PasswordPolicy.RequirementMessage, problem.Title);
        Assert.Equal(
            "密碼長度至少需 8 碼，且內容須至少包含四種字元的其中三種：大寫英文／小寫英文／數字／符號",
            problem.Title);

        await AssertNothingWritten(fixture);
    }

    [Theory]
    [InlineData("Abcdefg1")]    // upper + lower + digit
    [InlineData("Abcdefg!")]    // upper + lower + symbol
    [InlineData("ABCDEF1!")]    // upper + digit + symbol
    [InlineData("abcdef1!")]    // lower + digit + symbol
    public async Task ChangePassword_AcceptsExactlyThreeOfTheFourClasses(string acceptable)
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.ChangePassword(
            Request(newPassword: acceptable),
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.True(PasswordHasher.Matches(acceptable, fixture.Users.PasswordHashOf("helen")));
    }

    [Fact]
    public async Task ChangePassword_ChecksComplexityBeforeTheConfirmation()
    {
        // A weak password that also fails to match reports the complexity rule, which is the one
        // the operator has to act on.
        var fixture = CreateFixture();

        var result = await fixture.Controller.ChangePassword(
            Request(newPassword: "short", confirm: "different"),
            CancellationToken.None);

        Assert.Equal(PasswordPolicy.RequirementMessage, AssertBadRequest(result).Title);
        await AssertNothingWritten(fixture);
    }

    // ---------- 3. Differing from the current password ----------

    // Regression: ISSUE-002 — change-password answered 204 to a request that re-submitted the
    // current password, so an operator told to move off the shared SysConfig defaultPassword could
    // retype it and the system recorded a password change that had not happened.
    // Found by /qa on 2026-09-10
    // Report: .gstack/qa-reports/qa-report-localhost-2026-09-10.md

    [Fact]
    public async Task ChangePassword_WhenTheNewPasswordIsTheCurrentOne_Returns400AndWritesNothing()
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.ChangePassword(
            Request(newPassword: CurrentPassword),
            CancellationToken.None);

        Assert.Equal("新密碼不可與目前密碼相同", AssertBadRequest(result).Title);
        await AssertNothingWritten(fixture);
    }

    [Fact]
    public async Task ChangePassword_ComparesAgainstTheCurrentPasswordOrdinally()
    {
        // Same letters, different case. That is a different byte sequence and so a genuine change
        // — the gate refuses a re-submission, not a password that merely resembles the old one.
        var changed = CurrentPassword.ToUpperInvariant();
        Assert.NotEqual(CurrentPassword, changed, StringComparer.Ordinal);

        var fixture = CreateFixture();

        var result = await fixture.Controller.ChangePassword(
            Request(newPassword: changed),
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.True(PasswordHasher.Matches(changed, fixture.Users.PasswordHashOf("helen")));
    }

    [Fact]
    public async Task ChangePassword_ChecksTheDifferenceBeforeTheConfirmation()
    {
        // Both are wrong: the new password is the current one, and the confirmation does not match
        // it either. The re-submission is what the operator has to act on, so it answers first.
        var fixture = CreateFixture();

        var result = await fixture.Controller.ChangePassword(
            Request(newPassword: CurrentPassword, confirm: "N3wPass!word"),
            CancellationToken.None);

        Assert.Equal("新密碼不可與目前密碼相同", AssertBadRequest(result).Title);
        await AssertNothingWritten(fixture);
    }

    // ---------- 4. The confirmation ----------

    [Fact]
    public async Task ChangePassword_WhenTheConfirmationDiffers_Returns400AndWritesNothing()
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.ChangePassword(
            Request(newPassword: NewPassword, confirm: "N3wPass!wordX"),
            CancellationToken.None);

        Assert.Equal("新密碼與確認新密碼不一致", AssertBadRequest(result).Title);
        await AssertNothingWritten(fixture);
    }

    [Fact]
    public async Task ChangePassword_ComparesTheConfirmationOrdinally()
    {
        // Same letters, different case: a mismatch, because the byte sequence is what gets hashed.
        var fixture = CreateFixture();

        var result = await fixture.Controller.ChangePassword(
            Request(newPassword: "N3wPass!word", confirm: "N3wpass!word"),
            CancellationToken.None);

        Assert.Equal("新密碼與確認新密碼不一致", AssertBadRequest(result).Title);
        await AssertNothingWritten(fixture);
    }

    // ---------- The token ----------

    [Fact]
    public async Task ChangePassword_WithNoUserIdClaim_Returns401AndWritesNothing()
    {
        var fixture = CreateFixture(signedInUserId: null);

        var result = await fixture.Controller.ChangePassword(Request(), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);

        // The credential was never even read.
        Assert.Empty(fixture.Auth.LookedUpUserIds);
        await AssertNothingWritten(fixture);
    }

    [Fact]
    public async Task ChangePassword_WhenTheTokensUserNoLongerExists_Returns404()
    {
        // A token is good for 24 hours; the account it names can be deleted inside that window.
        var fixture = CreateFixture(signedInUserId: "helen", storedUserId: "miles");

        var result = await fixture.Controller.ChangePassword(Request(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(fixture.Users.ResetUserIds);
        Assert.Equal("seed-hash-miles", fixture.Users.PasswordHashOf("miles"));
    }

    // ---------- The request model itself ----------

    [Fact]
    public void ChangePasswordRequest_CarriesThreePasswordsAndNoKey()
    {
        // The guard behind "this endpoint cannot be pointed at another account": there is no
        // property for a UserId to bind to.
        Assert.Equal(
            ["ConfirmNewPassword", "CurrentPassword", "NewPassword"],
            typeof(ChangePasswordRequest).GetProperties().Select(p => p.Name).Order());
    }

    // ---------- Through the real pipeline ----------

    private HttpClient Client() => _factory.CreateClient();

    private async Task<string> SignInAsync(string userId, string userName, params string[] roleIds)
    {
        _factory.Auth.Seed(userId, userName, CurrentPassword, true, roleIds);
        _factory.AppUsers.Seed(User(userId, userName, roleIds));

        var response = await Client().PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { UserId = userId, Password = CurrentPassword });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
    }

    private async Task<HttpResponseMessage> PostChangePasswordAsync(string? accessToken, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
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
    public async Task ChangePassword_WithoutAToken_Returns401AndWritesNothing()
    {
        // The endpoint shares AuthController's /api/auth prefix but not its [AllowAnonymous]:
        // it lives on ProfileController precisely so the fallback policy covers it.
        _factory.AppUsers.Seed(User("wire-anon", "Helen Lin"));

        var response = await PostChangePasswordAsync(accessToken: null, new
        {
            currentPassword = CurrentPassword,
            newPassword = NewPassword,
            confirmNewPassword = NewPassword,
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("seed-hash-wire-anon", _factory.AppUsers.PasswordHashOf("wire-anon"));
    }

    [Fact]
    public async Task ChangePassword_IgnoresAUserIdSentInTheRequestBody()
    {
        var accessToken = await SignInAsync("wire-helen", "Helen Lin", "Admin");
        _factory.AppUsers.Seed(User("wire-victim", "Victim Name"));

        var response = await PostChangePasswordAsync(accessToken, new
        {
            userId = "wire-victim",
            currentPassword = CurrentPassword,
            newPassword = NewPassword,
            confirmNewPassword = NewPassword,
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // The token's own account moved; the account named in the body did not.
        Assert.True(PasswordHasher.Matches(NewPassword, _factory.AppUsers.PasswordHashOf("wire-helen")));
        Assert.Equal("seed-hash-wire-victim", _factory.AppUsers.PasswordHashOf("wire-victim"));
    }

    [Fact]
    public async Task ChangePassword_AnswersWithNoBodyAtAll()
    {
        var accessToken = await SignInAsync("wire-empty", "Helen Lin");

        var response = await PostChangePasswordAsync(accessToken, new
        {
            currentPassword = CurrentPassword,
            newPassword = NewPassword,
            confirmNewPassword = NewPassword,
        });

        // 204 and an empty body: no hash can leak through a response that has no content.
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ChangePassword_RejectsAWeakPasswordOnTheWireWithTheBilingualMessage()
    {
        var accessToken = await SignInAsync("wire-weak", "Helen Lin");

        var response = await PostChangePasswordAsync(accessToken, new
        {
            currentPassword = CurrentPassword,
            newPassword = "abcdefgh",
            confirmNewPassword = "abcdefgh",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(PasswordPolicy.RequirementMessage, problem!.Title);
        Assert.Equal(PasswordPolicy.RequirementDetail, problem.Detail);

        Assert.Equal("seed-hash-wire-weak", _factory.AppUsers.PasswordHashOf("wire-weak"));
    }

    [Theory]
    [InlineData("", "N3wPass!word", "N3wPass!word")]
    [InlineData("Uwa@2026", "", "")]
    [InlineData("Uwa@2026", "N3wPass!word", "")]
    public async Task ChangePassword_WithABlankFieldOnTheWire_Returns400(
        string current,
        string newPassword,
        string confirm)
    {
        // [Required(AllowEmptyStrings = false)] answers these through ModelState before the action
        // runs; either way nothing is written.
        var accessToken = await SignInAsync("wire-blank", "Helen Lin");

        var response = await PostChangePasswordAsync(accessToken, new
        {
            currentPassword = current,
            newPassword,
            confirmNewPassword = confirm,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("seed-hash-wire-blank", _factory.AppUsers.PasswordHashOf("wire-blank"));
    }

    // ---------- Revocation: the old token stops working ----------

    private async Task<HttpResponseMessage> GetWithTokenAsync(string url, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");
        return await Client().SendAsync(request);
    }

    [Fact]
    public async Task ChangePassword_MakesEveryTokenIssuedBeforeItUnusable()
    {
        // The change is stamped a minute ahead of the token that made it, which is what a real
        // clock does anyway — the token was issued first. FakeAppUserRepository.UtcNow is the
        // stand-in for GETUTCDATE().
        _factory.AppUsers.UtcNow = DateTime.UtcNow.AddMinutes(1);

        var accessToken = await SignInAsync("revoke-helen", "Helen Lin", "Admin");

        // Before: the token opens a protected endpoint in another controller entirely.
        var before = await GetWithTokenAsync("/api/app-roles", accessToken);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        var change = await PostChangePasswordAsync(accessToken, new
        {
            currentPassword = CurrentPassword,
            newPassword = NewPassword,
            confirmNewPassword = NewPassword,
        });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        // After: the same token, still perfectly signed and hours from expiry, is refused —
        // and refused by the middleware, so it is refused everywhere, not just here.
        var after = await GetWithTokenAsync("/api/app-roles", accessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);

        var reused = await PostChangePasswordAsync(accessToken, new
        {
            currentPassword = NewPassword,
            newPassword = "An0ther!Pass",
            confirmNewPassword = "An0ther!Pass",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_RevokesOnlyTheAccountThatChangedItsPassword()
    {
        _factory.AppUsers.UtcNow = DateTime.UtcNow.AddMinutes(1);

        var helen = await SignInAsync("revoke-scope-helen", "Helen Lin", "Admin");
        var miles = await SignInAsync("revoke-scope-miles", "Miles Sun", "Admin");

        await PostChangePasswordAsync(helen, new
        {
            currentPassword = CurrentPassword,
            newPassword = NewPassword,
            confirmNewPassword = NewPassword,
        });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await GetWithTokenAsync("/api/app-roles", helen)).StatusCode);

        // Nobody else is signed out by somebody else's password change.
        Assert.Equal(
            HttpStatusCode.OK,
            (await GetWithTokenAsync("/api/app-roles", miles)).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_LeavesTheTokenAloneWhenTheChangeWasRefused()
    {
        _factory.AppUsers.UtcNow = DateTime.UtcNow.AddMinutes(1);

        var accessToken = await SignInAsync("revoke-refused", "Helen Lin", "Admin");

        var refused = await PostChangePasswordAsync(accessToken, new
        {
            currentPassword = "not-the-password",
            newPassword = NewPassword,
            confirmNewPassword = NewPassword,
        });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        // Nothing was written, so nothing was revoked — the operator is still signed in to retry.
        Assert.Equal(
            HttpStatusCode.OK,
            (await GetWithTokenAsync("/api/app-roles", accessToken)).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_LetsTheOperatorSignBackInWithTheNewPassword()
    {
        // The change is already a minute old, as it would be by the time anyone retyped a
        // password, so the freshness check must not refuse the token the new login mints.
        _factory.AppUsers.UtcNow = DateTime.UtcNow.AddMinutes(-1);

        var accessToken = await SignInAsync("revoke-return", "Helen Lin", "Admin");

        await PostChangePasswordAsync(accessToken, new
        {
            currentPassword = CurrentPassword,
            newPassword = NewPassword,
            confirmNewPassword = NewPassword,
        });

        // The credential fake is what login reads, so it has to carry the new password too — in
        // the API both sides are the one AppUser row.
        _factory.Auth.Seed("revoke-return", "Helen Lin", NewPassword, true, "Admin");

        var login = await Client().PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { UserId = "revoke-return", Password = NewPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var fresh = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
        Assert.Equal(
            HttpStatusCode.OK,
            (await GetWithTokenAsync("/api/app-roles", fresh)).StatusCode);
    }
}
