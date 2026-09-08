using System.Data;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class AppUserRepository : IAppUserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AppUserRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<AppUser>> GetAllAsync(CancellationToken cancellationToken = default)
        => await QueryAsync(new AppUserQuery(), cancellationToken);

    public async Task<IEnumerable<AppUser>> QueryAsync(AppUserQuery query, CancellationToken cancellationToken = default)
    {
        var (where, parameters) = AppUserSql.BuildWhere(query);
        var sql = $"{AppUserSql.SelectBase}\n{where}\n{AppUserSql.DefaultOrderBy}";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<AppUser>(
            new CommandDefinition(sql, new DynamicParameters(parameters), cancellationToken: cancellationToken));
    }

    public async Task<AppUser?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var user = await connection.QuerySingleOrDefaultAsync<AppUser>(new CommandDefinition(
            $"{AppUserSql.SelectBase}\nWHERE u.UserId = @UserId",
            new { UserId = userId },
            cancellationToken: cancellationToken));

        if (user is null)
        {
            return null;
        }

        // n-n: separate query on the same connection.
        var roleIds = await connection.QueryAsync<string>(new CommandDefinition(
            AppUserSql.SelectRoleIds,
            new { UserId = userId },
            cancellationToken: cancellationToken));

        user.RoleIds = roleIds.ToList();
        return user;
    }

    public async Task<bool> ExistsAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM AppUser WHERE UserId = @UserId",
            new { UserId = userId },
            cancellationToken: cancellationToken));
        return count > 0;
    }

    public async Task<string> CreateAsync(
        AppUserRequest request,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // No SCOPE_IDENTITY() round trip: the key is the client-supplied UserId, and pkid — while
        // an IDENTITY column — is not the key.
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO AppUser (UserId, UserName, IsActive, PasswordHash, PasswordUpdatedTime)
            VALUES (@UserId, @UserName, @IsActive, @PasswordHash, GETUTCDATE());
            """,
            new { request.UserId, request.UserName, request.IsActive, PasswordHash = passwordHash },
            transaction,
            cancellationToken: cancellationToken));

        await SyncUserRolesAsync(connection, transaction, request.UserId, request.RoleIds, cancellationToken);

        transaction.Commit();
        return request.UserId;
    }

    public async Task<bool> UpdateAsync(AppUserRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // UserId is the primary key and is not updatable. PasswordHash and PasswordUpdatedTime are
        // deliberately absent from the SET list — only ResetPasswordAsync writes them.
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE AppUser
            SET UserName = @UserName,
                IsActive = @IsActive
            WHERE UserId = @UserId;
            """,
            new { request.UserId, request.UserName, request.IsActive },
            transaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
        {
            transaction.Rollback();
            return false;
        }

        await SyncUserRolesAsync(connection, transaction, request.UserId, request.RoleIds, cancellationToken);

        transaction.Commit();
        return true;
    }

    public async Task<bool> UpdateUserNameAsync(
        string userId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // One column, one row, no transaction: nothing else changes and AppUserRole is not touched
        // at all — which is why this exists instead of calling UpdateAsync with a built-up request,
        // whose delete-then-reinsert would clear the user's roles.
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE AppUser
            SET UserName = @UserName
            WHERE UserId = @UserId;
            """,
            new { UserId = userId, UserName = userName },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> ResetPasswordAsync(
        string userId,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE AppUser
            SET PasswordHash = @PasswordHash,
                PasswordUpdatedTime = GETUTCDATE()
            WHERE UserId = @UserId;
            """,
            new { UserId = userId, PasswordHash = passwordHash },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // FK_AppUserRole_AppUser carries no ON DELETE action, so the junction rows must go first
        // or SQL error 547 follows. Nothing else in the schema references AppUser.
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AppUserRole WHERE UserId = @UserId",
            new { UserId = userId },
            transaction,
            cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AppUser WHERE UserId = @UserId",
            new { UserId = userId },
            transaction,
            cancellationToken: cancellationToken));

        transaction.Commit();
        return affected > 0;
    }

    /// <summary>
    /// n-n write pattern: delete-then-reinsert, scoped to this user. AppRoleForm rewrites the same
    /// junction scoped to a RoleId; each side only ever touches its own slice.
    /// </summary>
    private static async Task SyncUserRolesAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        string userId,
        IEnumerable<string>? roleIds,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AppUserRole WHERE UserId = @UserId",
            new { UserId = userId },
            transaction,
            cancellationToken: cancellationToken));

        var rows = (roleIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => new { UserId = userId, RoleId = id })
            .ToList();

        if (rows.Count == 0)
        {
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO AppUserRole (UserId, RoleId) VALUES (@UserId, @RoleId)",
            rows,
            transaction,
            cancellationToken: cancellationToken));
    }
}
