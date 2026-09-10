using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CMS.API.Middleware;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Tests.Fakes;
using CMS.API.Tests.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CMS.API.Tests.Middleware;

/// <summary>
/// The API hosted in process with 角色 AppRole backed by a repository that always throws, so a real
/// request through the real pipeline reaches the exception middleware.
/// </summary>
public sealed class ThrowingApiFactory : TestApiFactory
{
    /// <summary>Backs /api/app-roles, and fails every call.</summary>
    public ThrowingAppRoleRepository ThrowingAppRoles { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAppRoleRepository>();
            services.AddSingleton<IAppRoleRepository>(ThrowingAppRoles);
        });
    }
}

/// <summary>
/// The middleware through the whole pipeline: an endpoint whose repository throws answers one
/// generic 500 and leaks nothing, while the answers that were already meaningful — the 401 from
/// the bearer middleware, the 400 from model validation, a controller's 404 — are untouched.
/// </summary>
public class ExceptionHandlingPipelineTests : IClassFixture<ThrowingApiFactory>
{
    private const string Password = "Uwa@2026";

    private readonly ThrowingApiFactory _factory;

    public ExceptionHandlingPipelineTests(ThrowingApiFactory factory)
    {
        _factory = factory;
        _factory.SysConfig.SymmetricSecurityKey = FakeSysConfigRepository.ValidSigningKey;
        _factory.Auth.Seed("helen", "Helen Lin", Password, true, "Admin");
    }

    private HttpClient Client() => _factory.CreateClient();

    /// <summary>A token from the real login endpoint — the same one a browser would hold.</summary>
    private async Task<HttpClient> SignedInClientAsync()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { UserId = "helen", Password = Password });
        response.EnsureSuccessStatusCode();

        var profile = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {profile!.AccessToken}");
        return client;
    }

    // ---------- An endpoint that throws ----------

    [Fact]
    public async Task AnEndpointThatThrows_Answers500()
    {
        var response = await (await SignedInClientAsync()).GetAsync("/api/app-roles");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task AnEndpointThatThrows_AnswersTheGenericMessage()
    {
        var response = await (await SignedInClientAsync()).GetAsync("/api/app-roles");

        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(ExceptionHandlingMiddleware.GenericTitle, problem.GetProperty("title").GetString());
        Assert.Equal(ExceptionHandlingMiddleware.GenericDetail, problem.GetProperty("detail").GetString());
        Assert.Equal(500, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task AnEndpointThatThrows_LeaksNoStackTraceSqlOrConnectionDetail()
    {
        var response = await (await SignedInClientAsync()).GetAsync("/api/app-roles");
        var body = await response.Content.ReadAsStringAsync();

        string[] secrets =
        [
            "SecretRoleTable", "db-prod-01", "CmsSecrets", "SELECT", "Invalid column name",
            "InvalidOperationException", "stackTrace", "   at ", "CMS.API.Repositories",
        ];

        foreach (var secret in secrets)
        {
            Assert.DoesNotContain(secret, body, StringComparison.OrdinalIgnoreCase);
        }

        // Whole-body assertion as well: the four ProblemDetails members and traceId, nothing else.
        var members = JsonDocument.Parse(body).RootElement.EnumerateObject().Select(p => p.Name);
        Assert.Equal(
            ["detail", "instance", "status", "title", "traceId"],
            members.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AnEndpointThatThrows_AnswersAsProblemJson()
    {
        var response = await (await SignedInClientAsync()).GetAsync("/api/app-roles");

        Assert.Equal(ExceptionHandlingMiddleware.ProblemContentType, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AnEndpointThatThrows_KeepsItsCorsHeaders()
    {
        // Without them the browser refuses the response and the Angular interceptor sees a
        // status-0 network error rather than the 500 whose body carries the message.
        var client = await SignedInClientAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/app-roles");
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost:4200");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("http://localhost:4200", response.Headers.GetValues("Access-Control-Allow-Origin"));
    }

    // ---------- The answers that were already meaningful ----------

    [Fact]
    public async Task WithoutAToken_TheThrowingEndpointStillAnswers401AndIsNeverReached()
    {
        var before = _factory.ThrowingAppRoles.Calls;

        var response = await Client().GetAsync("/api/app-roles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(before, _factory.ThrowingAppRoles.Calls);
    }

    [Fact]
    public async Task AnInvalidBody_StillAnswers400WithItsValidationErrors()
    {
        var client = await SignedInClientAsync();

        // Topic and Description are [Required], and Slot is out of its 1..3 range.
        var response = await client.PostAsJsonAsync(
            "/api/featured-promo-items",
            new FeaturedPromoItemRequest
            {
                ScheduleOn = new DateOnly(2026, 9, 9),
                TrainingCenterPkid = 1,
                Slot = 0,
                PromotionPkid = 1,
                Topic = string.Empty,
                Description = string.Empty,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty(nameof(FeaturedPromoItemRequest.Topic), out _));
        Assert.True(errors.TryGetProperty(nameof(FeaturedPromoItemRequest.Slot), out _));
        Assert.NotEqual(ExceptionHandlingMiddleware.GenericDetail, problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task AMalformedBody_StillAnswers400RatherThanBecomingA500()
    {
        var client = await SignedInClientAsync();
        using var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/featured-promo-items", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AMissingRecord_StillAnswers404()
    {
        var response = await (await SignedInClientAsync()).GetAsync("/api/partners/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
