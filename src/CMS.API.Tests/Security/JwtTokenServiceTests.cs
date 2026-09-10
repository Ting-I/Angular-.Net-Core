using System.Text;
using CMS.API.Security;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Tests.Security;

/// <summary>
/// Covers the access token itself — claim shape, lifetime, signature and the secret guard — apart
/// from the controller that issues it.
/// </summary>
public class JwtTokenServiceTests
{
    private const string SigningKey = "cloud4fun#123456cloud4fun#123456";

    private static string CreateToken(params string[] roleIds) =>
        new JwtTokenService().CreateAccessToken("helen", "Helen Lin", roleIds, SigningKey);

    [Fact]
    public void Lifetime_Is24Hours()
    {
        Assert.Equal(TimeSpan.FromHours(24), new JwtTokenService().Lifetime);
    }

    [Fact]
    public void CreateAccessToken_StampsExpiryOneLifetimeAfterIssue()
    {
        var token = new JsonWebToken(CreateToken());

        Assert.Equal(TimeSpan.FromHours(24), token.ValidTo - token.ValidFrom);
    }

    [Fact]
    public void CreateAccessToken_SignsWithHmacSha256()
    {
        Assert.Equal(SecurityAlgorithms.HmacSha256, new JsonWebToken(CreateToken()).Alg);
    }

    [Fact]
    public void CreateAccessToken_EmitsOneClaimPerRole()
    {
        var token = new JsonWebToken(CreateToken("Admin", "Editor", "Viewer"));

        var roles = token.Claims
            .Where(c => c.Type == JwtTokenService.RoleClaimType)
            .Select(c => c.Value)
            .Order();

        Assert.Equal(["Admin", "Editor", "Viewer"], roles);
    }

    [Fact]
    public void CreateAccessToken_GivesEachTokenItsOwnJti()
    {
        var first = new JsonWebToken(CreateToken()).GetClaim(JwtRegisteredClaimNames.Jti).Value;
        var second = new JsonWebToken(CreateToken()).GetClaim(JwtRegisteredClaimNames.Jti).Value;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task CreateAccessToken_ProducesATokenThatValidatesAgainstTheSameSecret()
    {
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(
            CreateToken("Admin"),
            new TokenValidationParameters
            {
                // Neither is stamped, so neither may be required — the note on JwtTokenService.
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            });

        Assert.True(result.IsValid);
        Assert.Equal("Admin", result.ClaimsIdentity.FindFirst(JwtTokenService.RoleClaimType)?.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("31-bytes-long-not-quite-enough!")]
    public void IsUsableSecret_RejectsAnythingHs256CannotSignWith(string? secret)
    {
        Assert.False(JwtTokenService.IsUsableSecret(secret));
    }

    [Fact]
    public void IsUsableSecret_AcceptsA32ByteSecret()
    {
        Assert.Equal(32, Encoding.UTF8.GetByteCount(SigningKey));
        Assert.True(JwtTokenService.IsUsableSecret(SigningKey));
    }

    [Fact]
    public void CreateAccessToken_WithAnUnusableSecret_Throws()
    {
        // The controller's 500 arm exists so this never surfaces as an unhandled exception.
        Assert.Throws<ArgumentException>(() =>
            new JwtTokenService().CreateAccessToken("helen", "Helen Lin", [], "too-short"));
    }
}
