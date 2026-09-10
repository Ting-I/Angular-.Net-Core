using System.Net;
using System.Net.Http.Json;
using CMS.API.Models;
using CMS.API.Tests.Controllers;
using CMS.API.Tests.Fakes;

namespace CMS.API.Tests.Security;

/// <summary>
/// POST /api/courses/{id}/sheet through the real pipeline. The endpoint carries no authorization
/// attribute of its own — it is protected by the global FallbackPolicy, i.e. by omission — and that is
/// exactly the kind of claim worth proving once for the branch's first new endpoint, because a 401
/// from middleware is invisible to a controller test.
/// </summary>
public class CourseSheetEndpointTests : IClassFixture<TestApiFactory>
{
    private const string Password = "Uwa@2026";

    private readonly TestApiFactory _factory;

    public CourseSheetEndpointTests(TestApiFactory factory)
    {
        _factory = factory;
        _factory.SysConfig.SymmetricSecurityKey = FakeSysConfigRepository.ValidSigningKey;
        _factory.Auth.Seed("helen", "Helen Lin", Password, true, "Admin");
        _factory.AppUsers.Seed(new AppUser
        {
            Pkid = 1,
            UserId = "helen",
            UserName = "Helen Lin",
            IsActive = true,
        });
        _factory.Courses.Seed(CoursesControllerTests.MakeCourse(1, courseId: "AZ-104"));
    }

    private HttpClient Client() => _factory.CreateClient();

    private async Task<string> LoginAsync()
    {
        var response = await Client().PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { UserId = "helen", Password = Password });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
    }

    private static HttpRequestMessage Post(string url, string? accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (accessToken is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");
        }

        return request;
    }

    [Fact]
    public async Task SheetEndpoint_WithoutAToken_Returns401()
    {
        var response = await Client().SendAsync(Post("/api/courses/1/sheet", accessToken: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SheetEndpoint_WithAValidToken_Returns204()
    {
        var response = await Client().SendAsync(Post("/api/courses/1/sheet", await LoginAsync()));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task SheetEndpoint_WithAValidToken_Returns404ForAMissingCourse()
    {
        var response = await Client().SendAsync(Post("/api/courses/999/sheet", await LoginAsync()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// A valid signature is not the whole of a valid token: TokenFreshness refuses one issued before
    /// the account's PasswordUpdatedTime. The new endpoint inherits that with no code of its own, and
    /// this is what says so.
    /// </summary>
    [Fact]
    public async Task SheetEndpoint_WithATokenOlderThanThePasswordChange_Returns401()
    {
        var accessToken = await LoginAsync();

        // Reset the password *after* the token was issued: the fake stamps PasswordUpdatedTime from
        // its own clock, so push that past the token's iat.
        _factory.AppUsers.UtcNow = DateTime.UtcNow.AddMinutes(5);
        await _factory.AppUsers.ResetPasswordAsync("helen", "new-hash");

        try
        {
            var response = await Client().SendAsync(Post("/api/courses/1/sheet", accessToken));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            _factory.AppUsers.UtcNow = default;
            await _factory.AppUsers.ResetPasswordAsync("helen", "seed-hash-helen");
        }
    }
}
