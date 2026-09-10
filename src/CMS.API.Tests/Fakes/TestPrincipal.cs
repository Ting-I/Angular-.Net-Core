using System.Security.Claims;
using CMS.API.Security;
using Microsoft.AspNetCore.Http;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// The <see cref="IHttpContextAccessor"/> the audit writer reads the operator's name from. Shaped
/// exactly the way ConfigureJwtBearerOptions shapes a validated principal — NameClaimType on the
/// *userId* claim — because that difference is the thing the writer has to get right.
/// </summary>
public static class TestPrincipal
{
    /// <summary>An accessor holding a validated principal shaped the way JwtTokenService issues one.</summary>
    public static IHttpContextAccessor SignedIn(string userId, string userName)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(JwtTokenService.UserIdClaimType, userId),
                new Claim(JwtTokenService.UserNameClaimType, userName),
            ],
            authenticationType: "Test",
            nameType: JwtTokenService.UserIdClaimType,
            roleType: JwtTokenService.RoleClaimType);

        return new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
    }

    /// <summary>An accessor with no signed-in user; the writer falls back to "system".</summary>
    public static IHttpContextAccessor Anonymous(HttpContext? httpContext = null) =>
        new HttpContextAccessor { HttpContext = httpContext };
}
