using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Security;

/// <summary>
/// Configures the bearer scheme. An <see cref="IConfigureNamedOptions{T}"/> rather than an inline
/// <c>AddJwtBearer(options => ...)</c> lambda because the key resolver is a DI service.
/// </summary>
public sealed class ConfigureJwtBearerOptions : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly SysConfigSigningKeys _signingKeys;

    public ConfigureJwtBearerOptions(SysConfigSigningKeys signingKeys)
    {
        _signingKeys = signingKeys;
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name == JwtBearerDefaults.AuthenticationScheme)
        {
            Configure(options);
        }
    }

    public void Configure(JwtBearerOptions options)
    {
        // Tokens are issued and consumed by this API alone, so there is no second party to name.
        // JwtTokenService stamps neither an issuer nor an audience; requiring one here would
        // reject every token it signs.
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = _signingKeys.Resolve,

            // Match the claim types the token actually carries, so User.Identity.Name and
            // User.IsInRole("Admin") read the login's claims rather than looking for the
            // WS-Federation names that nothing here writes.
            NameClaimType = JwtTokenService.UserIdClaimType,
            RoleClaimType = JwtTokenService.RoleClaimType,

            // The default five minutes of skew would keep a 24-hour token alive past its expiry.
            ClockSkew = TimeSpan.Zero,
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = async context =>
            {
                if (SysConfigSigningKeys.CarriesBearerToken(context.Request))
                {
                    await SysConfigSigningKeys.LoadAsync(context.HttpContext);
                }
            },

            // Signature and expiry are not the whole of validity here: a token signed against a
            // password the account no longer has is refused too. It runs after the cryptographic
            // checks, so the extra query only ever follows a token that was going to be accepted.
            OnTokenValidated = TokenFreshness.ValidateAsync,
        };
    }
}
