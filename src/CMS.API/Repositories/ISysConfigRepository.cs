namespace CMS.API.Repositories;

/// <summary>
/// Reads SysConfig. Not an entity CRUD repository — it exists because AppUser needs the default
/// password out of the 'appConfig' row, and login needs the JWT signing key out of the same row.
/// </summary>
public interface ISysConfigRepository
{
    /// <summary>
    /// The defaultPassword property of the 'appConfig' JSON object, or null when the row is
    /// missing, the JSON does not parse, or the property is absent or blank.
    /// </summary>
    Task<string?> GetDefaultPasswordAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The symmetricSecurityKey property of the 'appConfig' JSON object — the JWT signing secret —
    /// or null under the same failure modes. Read at login time; never hard-coded or cached.
    /// </summary>
    Task<string?> GetSymmetricSecurityKeyAsync(CancellationToken cancellationToken = default);
}
