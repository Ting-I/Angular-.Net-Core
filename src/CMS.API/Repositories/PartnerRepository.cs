using System.Data;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class PartnerRepository : IPartnerRepository
{
    /// <summary>The real table name, as it goes into RowAudit.TableName.</summary>
    private const string TableName = "Partner";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IRowAuditWriter _auditWriter;

    public PartnerRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    {
        _connectionFactory = connectionFactory;
        _auditWriter = auditWriter;
    }

    public async Task<IEnumerable<Partner>> GetAllAsync(CancellationToken cancellationToken = default)
        => await QueryAsync(new PartnerQuery(), cancellationToken);

    public async Task<IEnumerable<Partner>> QueryAsync(
        PartnerQuery query,
        CancellationToken cancellationToken = default)
    {
        var (where, parameters) = PartnerSql.BuildWhere(query);
        var sql = $"{PartnerSql.SelectBase}\n{where}\n{PartnerSql.DefaultOrderBy}";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<Partner>(
            new CommandDefinition(sql, new DynamicParameters(parameters), cancellationToken: cancellationToken));
    }

    public async Task<Partner?> GetByIdAsync(short pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Partner>(new CommandDefinition(
            $"{PartnerSql.SelectBase}\nWHERE p.pkid = @Pkid",
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
    public async Task<short> CreateAsync(PartnerRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var pkid = await connection.ExecuteScalarAsync<short>(new CommandDefinition(
            """
            INSERT INTO Partner (Name, AppKey, NameOnPartnerMenu, NameOnCourseDetailPage, DisplayOrder, ImageFilename)
            VALUES (@Name, @AppKey, @NameOnPartnerMenu, @NameOnCourseDetailPage, @DisplayOrder, @ImageFilename);
            SELECT CAST(SCOPE_IDENTITY() AS smallint);
            """,
            new
            {
                request.Name,
                request.AppKey,
                request.NameOnPartnerMenu,
                request.NameOnCourseDetailPage,
                request.DisplayOrder,
                request.ImageFilename,
            },
            transaction,
            cancellationToken: cancellationToken));

        await AuditInsertAsync(connection, transaction, pkid, cancellationToken);

        transaction.Commit();
        return pkid;
    }

    public async Task<bool> UpdateAsync(PartnerRequest request, CancellationToken cancellationToken = default)
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
            UPDATE Partner
            SET Name = @Name,
                AppKey = @AppKey,
                NameOnPartnerMenu = @NameOnPartnerMenu,
                NameOnCourseDetailPage = @NameOnCourseDetailPage,
                DisplayOrder = @DisplayOrder,
                ImageFilename = @ImageFilename
            WHERE pkid = @Pkid;
            """,
            new
            {
                request.Pkid,
                request.Name,
                request.AppKey,
                request.NameOnPartnerMenu,
                request.NameOnCourseDetailPage,
                request.DisplayOrder,
                request.ImageFilename,
            },
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

        // No junction rows to clear first; Certification, Course, PartnerCourseGroup and Promotion2
        // hold enforced FKs, and the controller blocks the call before it can raise SQL error 547.
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM Partner WHERE pkid = @Pkid",
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
    private static Task<Partner?> ReadRowAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        short pkid,
        CancellationToken cancellationToken)
        => connection.QuerySingleOrDefaultAsync<Partner>(new CommandDefinition(
            PartnerSql.SelectRow,
            new { Pkid = pkid },
            transaction,
            cancellationToken: cancellationToken));
}
