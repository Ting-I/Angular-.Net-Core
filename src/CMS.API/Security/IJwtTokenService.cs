namespace CMS.API.Security;

/// <summary>Issues the access token a successful login returns.</summary>
public interface IJwtTokenService
{
    /// <summary>How long an issued token stays valid — 24 hours.</summary>
    TimeSpan Lifetime { get; }

    /// <summary>
    /// Signs a token carrying the user's id, name and role claims. The secret is supplied per call
    /// because it lives in SysConfig and is read at login time, not captured at startup.
    /// </summary>
    string CreateAccessToken(
        string userId,
        string userName,
        IEnumerable<string> roleIds,
        string signingSecret);
}
