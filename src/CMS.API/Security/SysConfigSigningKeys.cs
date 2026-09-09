using System.Text;
using CMS.API.Repositories;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Security;

/// <summary>
/// Supplies the token-validation key, which is the same SysConfig 'appConfig'
/// <c>symmetricSecurityKey</c> that <see cref="JwtTokenService"/> signs with.
///
/// The key lives in a table, not in configuration, so it cannot be captured into
/// <see cref="TokenValidationParameters"/> at startup: rotating the row has to rotate validation
/// too, and reading it needs a scoped <see cref="ISysConfigRepository"/>. It is therefore read once
/// per bearer-carrying request in <c>OnMessageReceived</c> — the last async point before validation
/// — and parked on <see cref="HttpContext.Items"/>, from where the synchronous
/// <c>IssuerSigningKeyResolver</c> can pick it up without blocking on a database call.
///
/// A request with no bearer token never triggers the read, so an anonymous call to
/// <c>/api/auth/login</c> costs nothing.
/// </summary>
public sealed class SysConfigSigningKeys
{
    /// <summary>Where the per-request key is parked between the two callbacks.</summary>
    public const string HttpContextItemKey = "CMS.API.Security.SigningKey";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public SysConfigSigningKeys(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Reads the configured secret for this request and stores it on the context. Called from the
    /// JwtBearer <c>OnMessageReceived</c> event, which is async and has the request's service scope.
    /// </summary>
    public static async Task LoadAsync(HttpContext httpContext)
    {
        var repository = httpContext.RequestServices.GetRequiredService<ISysConfigRepository>();

        httpContext.Items[HttpContextItemKey] =
            await repository.GetSymmetricSecurityKeyAsync(httpContext.RequestAborted);
    }

    /// <summary>True when the request carries an <c>Authorization: Bearer ...</c> header.</summary>
    public static bool CarriesBearerToken(HttpRequest request) =>
        request.Headers.Authorization.Any(value =>
            value?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true);

    /// <summary>
    /// The <c>IssuerSigningKeyResolver</c> callback. An empty result means "no key to try", which
    /// fails validation and lands as a 401 — the right answer when SysConfig has no usable secret,
    /// since nothing could have been signed with one either.
    /// </summary>
    public IEnumerable<SecurityKey> Resolve(
        string token,
        SecurityToken securityToken,
        string? kid,
        TokenValidationParameters validationParameters) =>
        ToSigningKeys(_httpContextAccessor.HttpContext?.Items[HttpContextItemKey] as string);

    /// <summary>The HS256 key for a secret, or nothing at all when the secret is unusable.</summary>
    public static IEnumerable<SecurityKey> ToSigningKeys(string? signingSecret) =>
        JwtTokenService.IsUsableSecret(signingSecret)
            ? [new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingSecret!))]
            : [];
}
