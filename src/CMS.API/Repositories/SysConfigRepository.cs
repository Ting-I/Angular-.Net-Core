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

    private readonly IDbConnectionFactory _connectionFactory;

    public SysConfigRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<string?> GetDefaultPasswordAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var configValue = await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT configValue FROM SysConfig WHERE configKey = @ConfigKey",
            new { ConfigKey = AppConfigKey },
            cancellationToken: cancellationToken));

        return ExtractDefaultPassword(configValue);
    }

    /// <summary>
    /// Pulls defaultPassword out of the configValue JSON object. Kept static and public so the
    /// parsing rules are unit testable without a database, the way the *Sql.BuildWhere helpers are.
    /// Returns null for every failure mode: no row, unparseable JSON, a JSON value that is not an
    /// object, a missing property, or a property that is not a non-empty string.
    /// </summary>
    public static string? ExtractDefaultPassword(string? configValue)
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
                if (!string.Equals(property.Name, DefaultPasswordProperty, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                var password = property.Value.GetString();
                return string.IsNullOrWhiteSpace(password) ? null : password;
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
