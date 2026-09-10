using System.Data;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class CourseGroupRepository : ICourseGroupRepository
{
    /// <summary>The real table name, as it goes into RowAudit.TableName.</summary>
    private const string TableName = "CourseGroup";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IRowAuditWriter _auditWriter;

    public CourseGroupRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    {
        _connectionFactory = connectionFactory;
        _auditWriter = auditWriter;
    }

    public async Task<IEnumerable<CourseGroup>> GetAllAsync(CancellationToken cancellationToken = default)
        => await QueryAsync(new CourseGroupQuery(), cancellationToken);

    public async Task<IEnumerable<CourseGroup>> QueryAsync(
        CourseGroupQuery query,
        CancellationToken cancellationToken = default)
    {
        var (where, parameters) = CourseGroupSql.BuildWhere(query);
        var sql = $"{CourseGroupSql.SelectBase}\n{where}\n{CourseGroupSql.DefaultOrderBy}";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<CourseGroup>(
            new CommandDefinition(sql, new DynamicParameters(parameters), cancellationToken: cancellationToken));
    }

    public async Task<CourseGroup?> GetByIdAsync(short pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<CourseGroup>(new CommandDefinition(
            $"{CourseGroupSql.SelectBase}\nWHERE cg.pkid = @Pkid",
            new { Pkid = pkid },
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// pkid is smallint IDENTITY, so it is omitted from the INSERT and read back through
    /// SCOPE_IDENTITY(), which returns decimal and needs the explicit CAST.
    ///
    /// The transaction is here for the 異動紀錄 row rather than for the INSERT: the two commit
    /// together or neither does, so the trail can never claim a row that was rolled back.
    /// </summary>
    public async Task<short> CreateAsync(CourseGroupRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var pkid = await connection.ExecuteScalarAsync<short>(new CommandDefinition(
            """
            INSERT INTO CourseGroup (Description)
            VALUES (@Description);
            SELECT CAST(SCOPE_IDENTITY() AS smallint);
            """,
            new { request.Description },
            transaction,
            cancellationToken: cancellationToken));

        await AuditInsertAsync(connection, transaction, pkid, cancellationToken);

        transaction.Commit();
        return pkid;
    }

    public async Task<bool> UpdateAsync(CourseGroupRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // The "before" is read first and inside the transaction, or the changed-column list would
        // be a comparison against a row somebody else may already have moved.
        var before = await ReadRowAsync(connection, transaction, request.Pkid, cancellationToken);
        if (before is null)
        {
            return false;
        }

        // pkid is the primary key and is not updatable.
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE CourseGroup
            SET Description = @Description
            WHERE pkid = @Pkid;
            """,
            new { request.Pkid, request.Description },
            transaction,
            cancellationToken: cancellationToken));

        var after = await ReadRowAsync(connection, transaction, request.Pkid, cancellationToken);
        await _auditWriter.LogUpdateAsync(TableName, before, after!, transaction, cancellationToken);

        transaction.Commit();
        return true;
    }

    public async Task<bool> DeleteAsync(short pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // Read first: once the row is gone the trail is the only thing that still says what it was.
        var row = await ReadRowAsync(connection, transaction, pkid, cancellationToken);
        if (row is null)
        {
            return false;
        }

        // No junction rows to clear first. The controller must have checked the reference counts
        // before reaching this method: FK_Course_CourseGroup is ON DELETE CASCADE, so the database
        // would accept this statement and take every referencing Course row with it.
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CourseGroup WHERE pkid = @Pkid",
            new { Pkid = pkid },
            transaction,
            cancellationToken: cancellationToken));

        await _auditWriter.LogDeleteAsync(TableName, row, transaction, cancellationToken);

        transaction.Commit();
        return true;
    }

    /// <summary>Reads the created row back so the trail describes what was stored, not what was asked for.</summary>
    private async Task AuditInsertAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        short pkid,
        CancellationToken cancellationToken)
    {
        var row = await ReadRowAsync(connection, transaction, pkid, cancellationToken);
        if (row is not null)
        {
            await _auditWriter.LogInsertAsync(TableName, row, transaction, cancellationToken);
        }
    }

    /// <summary>The 異動紀錄 snapshot of one row, on the caller's connection and transaction.</summary>
    private static Task<CourseGroup?> ReadRowAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        short pkid,
        CancellationToken cancellationToken)
        => connection.QuerySingleOrDefaultAsync<CourseGroup>(new CommandDefinition(
            CourseGroupSql.SelectRow,
            new { Pkid = pkid },
            transaction,
            cancellationToken: cancellationToken));
}
