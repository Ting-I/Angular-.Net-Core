using System.Data;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class AppRoleRepository : IAppRoleRepository
{
    /// <summary>The real table name, as it goes into RowAudit.TableName.</summary>
    private const string TableName = "AppRole";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IRowAuditWriter _auditWriter;

    public AppRoleRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    {
        _connectionFactory = connectionFactory;
        _auditWriter = auditWriter;
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
            AppRoleSql.SelectUserIds,
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

        var row = await ReadRowAsync(connection, transaction, request.RoleId, cancellationToken);
        if (row is not null)
        {
            await _auditWriter.LogInsertAsync(TableName, row, transaction, cancellationToken);
        }

        transaction.Commit();
        return request.RoleId;
    }

    public async Task<bool> UpdateAsync(AppRoleRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // The "before" is read first and inside the transaction, or the changed-column list would
        // be a comparison against a row somebody else may already have moved.
        var before = await ReadRowAsync(connection, transaction, request.RoleId, cancellationToken);
        if (before is null)
        {
            return false;
        }

        // RoleId is the primary key and is not updatable.
        await connection.ExecuteAsync(new CommandDefinition(
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

        await SyncUserRolesAsync(connection, transaction, request.RoleId, request.UserIds, cancellationToken);

        var after = await ReadRowAsync(connection, transaction, request.RoleId, cancellationToken);
        await _auditWriter.LogUpdateAsync(TableName, before, after!, transaction, cancellationToken);

        transaction.Commit();
        return true;
    }

    public async Task<bool> DeleteAsync(string roleId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // Read first: once the row is gone the trail is the only thing that still says what it was.
        var row = await ReadRowAsync(connection, transaction, roleId, cancellationToken);
        if (row is null)
        {
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AppUserRole WHERE RoleId = @RoleId",
            new { RoleId = roleId },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AppRole WHERE RoleId = @RoleId",
            new { RoleId = roleId },
            transaction,
            cancellationToken: cancellationToken));

        await _auditWriter.LogDeleteAsync(TableName, row, transaction, cancellationToken);

        transaction.Commit();
        return true;
    }

    /// <summary>
    /// The 異動紀錄 snapshot of one role: the row's own columns plus its members, on the caller's
    /// connection and transaction. The membership is part of the snapshot because a save that only
    /// added or removed users changes no column of AppRole at all — without it that save would
    /// write no audit row, which is exactly the change somebody would want the trail to show.
    /// </summary>
    private static async Task<AppRole?> ReadRowAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        string roleId,
        CancellationToken cancellationToken)
    {
        var role = await connection.QuerySingleOrDefaultAsync<AppRole>(new CommandDefinition(
            AppRoleSql.SelectRow,
            new { RoleId = roleId },
            transaction,
            cancellationToken: cancellationToken));

        if (role is null)
        {
            return null;
        }

        var userIds = await connection.QueryAsync<string>(new CommandDefinition(
            AppRoleSql.SelectUserIds,
            new { RoleId = roleId },
            transaction,
            cancellationToken: cancellationToken));

        role.UserIds = userIds.ToList();
        return role;
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
