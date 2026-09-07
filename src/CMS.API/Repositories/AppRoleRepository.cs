using System.Data;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class AppRoleRepository : IAppRoleRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AppRoleRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<AppRole>> GetAllAsync(CancellationToken cancellationToken = default)
        => await QueryAsync(new AppRoleQuery(), cancellationToken);

    public async Task<IEnumerable<AppRole>> QueryAsync(AppRoleQuery query, CancellationToken cancellationToken = default)
    {
        var (where, parameters) = AppRoleSql.BuildWhere(query);
        var sql = $"{AppRoleSql.SelectBase}\n{where}\n{AppRoleSql.DefaultOrderBy}";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<AppRole>(
            new CommandDefinition(sql, new DynamicParameters(parameters), cancellationToken: cancellationToken));
    }

    public async Task<AppRole?> GetByIdAsync(string roleId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var role = await connection.QuerySingleOrDefaultAsync<AppRole>(new CommandDefinition(
            $"{AppRoleSql.SelectBase}\nWHERE r.RoleId = @RoleId",
            new { RoleId = roleId },
            cancellationToken: cancellationToken));

        if (role is null)
        {
            return null;
        }

        // n-n: separate query on the same connection.
        var userIds = await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT ur.UserId FROM AppUserRole ur WHERE ur.RoleId = @RoleId ORDER BY ur.UserId ASC",
            new { RoleId = roleId },
            cancellationToken: cancellationToken));

        role.UserIds = userIds.ToList();
        return role;
    }

    public async Task<bool> ExistsAsync(string roleId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM AppRole WHERE RoleId = @RoleId",
            new { RoleId = roleId },
            cancellationToken: cancellationToken));
        return count > 0;
    }

    public async Task<string> CreateAsync(AppRoleRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO AppRole (RoleId, RoleName, PermissionLevel, Description)
            VALUES (@RoleId, @RoleName, @PermissionLevel, @Description);
            """,
            new { request.RoleId, request.RoleName, request.PermissionLevel, request.Description },
            transaction,
            cancellationToken: cancellationToken));

        await SyncUserRolesAsync(connection, transaction, request.RoleId, request.UserIds, cancellationToken);

        transaction.Commit();
        return request.RoleId;
    }

    public async Task<bool> UpdateAsync(AppRoleRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // RoleId is the primary key and is not updatable.
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE AppRole
            SET RoleName = @RoleName,
                PermissionLevel = @PermissionLevel,
                Description = @Description
            WHERE RoleId = @RoleId;
            """,
            new { request.RoleId, request.RoleName, request.PermissionLevel, request.Description },
            transaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
        {
            transaction.Rollback();
            return false;
        }

        await SyncUserRolesAsync(connection, transaction, request.RoleId, request.UserIds, cancellationToken);

        transaction.Commit();
        return true;
    }

    public async Task<bool> DeleteAsync(string roleId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AppUserRole WHERE RoleId = @RoleId",
            new { RoleId = roleId },
            transaction,
            cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AppRole WHERE RoleId = @RoleId",
            new { RoleId = roleId },
            transaction,
            cancellationToken: cancellationToken));

        transaction.Commit();
        return affected > 0;
    }

    /// <summary>n-n write pattern: delete-then-reinsert.</summary>
    private static async Task SyncUserRolesAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        string roleId,
        IEnumerable<string>? userIds,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AppUserRole WHERE RoleId = @RoleId",
            new { RoleId = roleId },
            transaction,
            cancellationToken: cancellationToken));

        var rows = (userIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => new { RoleId = roleId, UserId = id })
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
