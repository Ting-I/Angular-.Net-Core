using System.Text.Json;
using CMS.API.Data;
using Dapper;

namespace CMS.API.Repositories;

public class SysConfigRepository : ISysConfigRepository
{
    /// <summary>The row holding the application-wide JSON settings object.</summary>
    public const string AppConfigKey = "appConfig";

    /// <summary>The property inside that object holding the password given to new accounts.</summary>
    public const string DefaultPasswordProperty = "defaultPassword";

    /// <summary>The property inside that object holding the JWT signing secret.</summary>
    public const string SymmetricSecurityKeyProperty = "symmetricSecurityKey";

    private readonly IDbConnectionFactory _connectionFactory;

    public SysConfigRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<string?> GetDefaultPasswordAsync(CancellationToken cancellationToken = default)
        => ExtractDefaultPassword(await ReadAppConfigAsync(cancellationToken));

    public async Task<string?> GetSymmetricSecurityKeyAsync(CancellationToken cancellationToken = default)
        => ExtractSymmetricSecurityKey(await ReadAppConfigAsync(cancellationToken));

    /// <summary>The raw configValue of the 'appConfig' row, or null when the row is missing.</summary>
    private async Task<string?> ReadAppConfigAsync(CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT configValue FROM SysConfig WHERE configKey = @ConfigKey",
            new { ConfigKey = AppConfigKey },
            cancellationToken: cancellationToken));
    }

    /// <summary>Pulls defaultPassword out of the configValue JSON object.</summary>
    public static string? ExtractDefaultPassword(string? configValue)
        => ExtractStringProperty(configValue, DefaultPasswordProperty);

    /// <summary>
    /// Pulls symmetricSecurityKey out of the configValue JSON object. Read on every login rather
    /// than cached, so rotating the row rotates the signing key without a restart.
    /// </summary>
    public static string? ExtractSymmetricSecurityKey(string? configValue)
        => ExtractStringProperty(configValue, SymmetricSecurityKeyProperty);

    /// <summary>
    /// Pulls one string property out of the configValue JSON object. Kept static and public so the
    /// parsing rules are unit testable without a database, the way the *Sql.BuildWhere helpers are.
    /// Returns null for every failure mode: no row, unparseable JSON, a JSON value that is not an
    /// object, a missing property, or a property that is not a non-empty string.
    /// </summary>
    public static string? ExtractStringProperty(string? configValue, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(configValue))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(configValue);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                var value = property.Value.GetString();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
