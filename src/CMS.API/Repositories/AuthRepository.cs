using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class AuthRepository : IAuthRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AuthRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<AppUserCredential?> GetCredentialAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var credential = await connection.QuerySingleOrDefaultAsync<AppUserCredential>(new CommandDefinition(
            AuthSql.SelectCredential,
            new { UserId = userId },
            cancellationToken: cancellationToken));

        if (credential is null)
        {
            return null;
        }

        // n-n: separate query on the same connection.
        var roleIds = await connection.QueryAsync<string>(new CommandDefinition(
            AuthSql.SelectRoleIds,
            new { UserId = userId },
            cancellationToken: cancellationToken));

        credential.RoleIds = roleIds.ToList();
        return credential;
    }

    public async Task<AppUserTokenState?> GetTokenStateAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // Two columns, one row, on the primary key. It runs once per authenticated request, so it
        // has to stay this cheap — and QuerySingleOrDefaultAsync of a reference type is what makes
        // "no such row" a null the caller can act on, rather than a default-constructed state that
        // would read as an enabled account.
        return await connection.QuerySingleOrDefaultAsync<AppUserTokenState>(new CommandDefinition(
            AuthSql.SelectTokenState,
            new { UserId = userId },
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// No RowAudit row and no transaction. A format upgrade is not an operator's change to the
    /// record — RowAudit answers "who changed this row", and stamping one here would name whoever
    /// happened to sign in as having edited an account they only logged into. It is the same call
    /// <c>POST /api/courses/{id}/sheet</c> makes for the same reason: a structured log line, at the
    /// caller, rather than a trail entry asserting a change that did not happen. One statement,
    /// so there is nothing for a transaction to hold together either.
    /// </summary>
    public async Task<bool> UpgradePasswordHashAsync(
        string userId,
        string expectedHash,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var rows = await connection.ExecuteAsync(new CommandDefinition(
            AuthSql.UpgradePasswordHash,
            new { UserId = userId, ExpectedHash = expectedHash, PasswordHash = passwordHash },
            cancellationToken: cancellationToken));

        return rows > 0;
    }
}
