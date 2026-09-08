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
}
