using System.Data;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class PublishStatusRepository : IPublishStatusRepository
{
    /// <summary>The real table name, as it goes into RowAudit.TableName.</summary>
    private const string TableName = "PublishStatus";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IRowAuditWriter _auditWriter;

    public PublishStatusRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    {
        _connectionFactory = connectionFactory;
        _auditWriter = auditWriter;
    }

    public async Task<IEnumerable<PublishStatus>> GetAllAsync(CancellationToken cancellationToken = default)
        => await QueryAsync(new PublishStatusQuery(), cancellationToken);

    public async Task<IEnumerable<PublishStatus>> QueryAsync(
        PublishStatusQuery query,
        CancellationToken cancellationToken = default)
    {
        var (where, parameters) = PublishStatusSql.BuildWhere(query);
        var sql = $"{PublishStatusSql.SelectBase}\n{where}\n{PublishStatusSql.DefaultOrderBy}";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<PublishStatus>(
            new CommandDefinition(sql, new DynamicParameters(parameters), cancellationToken: cancellationToken));
    }

    public async Task<PublishStatus?> GetByIdAsync(byte pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<PublishStatus>(new CommandDefinition(
            $"{PublishStatusSql.SelectBase}\nWHERE p.pkid = @Pkid",
            new { Pkid = pkid },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> ExistsAsync(byte pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM PublishStatus WHERE pkid = @Pkid",
            new { Pkid = pkid },
            cancellationToken: cancellationToken));
        return count > 0;
    }

    /// <summary>
    /// pkid is not an IDENTITY column, so it is inserted explicitly and there is no
    /// SCOPE_IDENTITY() round trip — the caller's key is the key.
    ///
    /// The transaction is here for the 異動紀錄 row rather than for the INSERT: the two commit
    /// together or neither does, so the trail can never claim a row that was rolled back.
    /// </summary>
    public async Task<byte> CreateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO PublishStatus (pkid, Description, IsDraft, IsPublished, IsDiscontinued)
            VALUES (@Pkid, @Description, @IsDraft, @IsPublished, @IsDiscontinued);
            """,
            new
            {
                request.Pkid,
                request.Description,
                request.IsDraft,
                request.IsPublished,
                request.IsDiscontinued,
            },
            transaction,
            cancellationToken: cancellationToken));

        await AuditInsertAsync(connection, transaction, request.Pkid, cancellationToken);

        transaction.Commit();
        return request.Pkid;
    }

    public async Task<bool> UpdateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default)
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
            UPDATE PublishStatus
            SET Description = @Description,
                IsDraft = @IsDraft,
                IsPublished = @IsPublished,
                IsDiscontinued = @IsDiscontinued
            WHERE pkid = @Pkid;
            """,
            new
            {
                request.Pkid,
                request.Description,
                request.IsDraft,
                request.IsPublished,
                request.IsDiscontinued,
            },
            transaction,
            cancellationToken: cancellationToken));

        var after = await ReadRowAsync(connection, transaction, request.Pkid, cancellationToken);
        await _auditWriter.LogUpdateAsync(TableName, before, after!, transaction, cancellationToken);

        transaction.Commit();
        return true;
    }

    public async Task<bool> DeleteAsync(byte pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // Read first: once the row is gone the trail is the only thing that still says what it was.
        var row = await ReadRowAsync(connection, transaction, pkid, cancellationToken);
        if (row is null)
        {
            return false;
        }

        // No junction rows to clear first; Course and Promotion2 hold enforced FKs, and the
        // controller blocks the call before it can raise SQL error 547.
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM PublishStatus WHERE pkid = @Pkid",
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
        byte pkid,
        CancellationToken cancellationToken)
    {
        var row = await ReadRowAsync(connection, transaction, pkid, cancellationToken);
        if (row is not null)
        {
            await _auditWriter.LogInsertAsync(TableName, row, transaction, cancellationToken);
        }
    }

    /// <summary>The 異動紀錄 snapshot of one row, on the caller's connection and transaction.</summary>
    private static Task<PublishStatus?> ReadRowAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        byte pkid,
        CancellationToken cancellationToken)
        => connection.QuerySingleOrDefaultAsync<PublishStatus>(new CommandDefinition(
            PublishStatusSql.SelectRow,
            new { Pkid = pkid },
            transaction,
            cancellationToken: cancellationToken));
}
