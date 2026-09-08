namespace CMS.API.Repositories;

/// <summary>
/// Reads SysConfig. Not an entity CRUD repository — it exists because AppUser needs the default
/// password out of the 'appConfig' row, and a later login feature will need the same value.
/// </summary>
public interface ISysConfigRepository
{
    /// <summary>
    /// The defaultPassword property of the 'appConfig' JSON object, or null when the row is
    /// missing, the JSON does not parse, or the property is absent or blank.
    /// </summary>
    Task<string?> GetDefaultPasswordAsync(CancellationToken cancellationToken = default);
}
