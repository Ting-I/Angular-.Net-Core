using System.Data;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class AppUserRepository : IAppUserRepository
{
    /// <summary>The real table name, as it goes into RowAudit.TableName.</summary>
    private const string TableName = "AppUser";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IRowAuditWriter _auditWriter;

    public AppUserRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    {
        _connectionFactory = connectionFactory;
        _auditWriter = auditWriter;
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

        var row = await ReadRowAsync(connection, transaction, request.UserId, cancellationToken);
        if (row is not null)
        {
            await _auditWriter.LogInsertAsync(TableName, row, transaction, cancellationToken);
        }

        transaction.Commit();
        return request.UserId;
    }

    public async Task<bool> UpdateAsync(AppUserRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // The "before" is read first and inside the transaction, or the changed-column list would
        // be a comparison against a row somebody else may already have moved.
        var before = await ReadRowAsync(connection, transaction, request.UserId, cancellationToken);
        if (before is null)
        {
            return false;
        }

        // UserId is the primary key and is not updatable. PasswordHash and PasswordUpdatedTime are
        // deliberately absent from the SET list — only ResetPasswordAsync writes them.
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE AppUser
            SET UserName = @UserName,
                IsActive = @IsActive
            WHERE UserId = @UserId;
            """,
            new { request.UserId, request.UserName, request.IsActive },
            transaction,
            cancellationToken: cancellationToken));

        await SyncUserRolesAsync(connection, transaction, request.UserId, request.RoleIds, cancellationToken);

        var after = await ReadRowAsync(connection, transaction, request.UserId, cancellationToken);
        await _auditWriter.LogUpdateAsync(TableName, before, after!, transaction, cancellationToken);

        transaction.Commit();
        return true;
    }

    public async Task<bool> UpdateUserNameAsync(
        string userId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // One column, one row, and AppUserRole is not touched at all — which is why this exists
        // instead of calling UpdateAsync with a built-up request, whose delete-then-reinsert would
        // clear the user's roles. The transaction is here for the 異動紀錄 row: an operator
        // renaming themselves is a change to AppUser and belongs in the trail like any other.
        var before = await ReadRowAsync(connection, transaction, userId, cancellationToken);
        if (before is null)
        {
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE AppUser
            SET UserName = @UserName
            WHERE UserId = @UserId;
            """,
            new { UserId = userId, UserName = userName },
            transaction,
            cancellationToken: cancellationToken));

        var after = await ReadRowAsync(connection, transaction, userId, cancellationToken);
        await _auditWriter.LogUpdateAsync(TableName, before, after!, transaction, cancellationToken);

        transaction.Commit();
        return true;
    }

    /// <summary>
    /// Writes PasswordHash and PasswordUpdatedTime and nothing else.
    ///
    /// The 異動紀錄 row it leaves reads "PasswordUpdatedTime", not "PasswordHash": the snapshot
    /// projection may not select the hash column (AuthSql.SelectCredential is the only query that
    /// may), so the changed-column list cannot name it. The stamped time moves on every reset, so
    /// the row is written regardless — a password change is never silent in the trail.
    /// </summary>
    public async Task<bool> ResetPasswordAsync(
        string userId,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var before = await ReadRowAsync(connection, transaction, userId, cancellationToken);
        if (before is null)
        {
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE AppUser
            SET PasswordHash = @PasswordHash,
                PasswordUpdatedTime = GETUTCDATE()
            WHERE UserId = @UserId;
            """,
            new { UserId = userId, PasswordHash = passwordHash },
            transaction,
            cancellationToken: cancellationToken));

        var after = await ReadRowAsync(connection, transaction, userId, cancellationToken);
        await _auditWriter.LogUpdateAsync(TableName, before, after!, transaction, cancellationToken);

        transaction.Commit();
        return true;
    }

    public async Task<bool> DeleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // Read first: once the row is gone the trail is the only thing that still says what it was.
        var row = await ReadRowAsync(connection, transaction, userId, cancellationToken);
        if (row is null)
        {
            return false;
        }

        // FK_AppUserRole_AppUser carries no ON DELETE action, so the junction rows must go first
        // or SQL error 547 follows. Nothing else in the schema references AppUser.
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AppUserRole WHERE UserId = @UserId",
            new { UserId = userId },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AppUser WHERE UserId = @UserId",
            new { UserId = userId },
            transaction,
            cancellationToken: cancellationToken));

        await _auditWriter.LogDeleteAsync(TableName, row, transaction, cancellationToken);

        transaction.Commit();
        return true;
    }

    /// <summary>
    /// The 異動紀錄 snapshot of one account: the row's own columns plus its roles, on the caller's
    /// connection and transaction. The roles are part of the snapshot because a save that only
    /// added or removed them changes no column of AppUser at all — without it that save would write
    /// no audit row, which is exactly the change somebody would want the trail to show.
    /// </summary>
    private static async Task<AppUser?> ReadRowAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        string userId,
        CancellationToken cancellationToken)
    {
        var user = await connection.QuerySingleOrDefaultAsync<AppUser>(new CommandDefinition(
            AppUserSql.SelectRow,
            new { UserId = userId },
            transaction,
            cancellationToken: cancellationToken));

        if (user is null)
        {
            return null;
        }

        var roleIds = await connection.QueryAsync<string>(new CommandDefinition(
            AppUserSql.SelectRoleIds,
            new { UserId = userId },
            transaction,
            cancellationToken: cancellationToken));

        user.RoleIds = roleIds.ToList();
        return user;
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
