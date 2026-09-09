using System.Globalization;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

/// <summary>
/// The read side of 異動紀錄. One query, no transaction: nothing here writes, and the trail is
/// read outside whatever change produced it.
/// </summary>
public class RowAuditRepository : IRowAuditRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RowAuditRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// PrimaryKeyValues is nvarchar, and <see cref="AuditHelper.PrimaryKeyValue"/> filled it with
    /// the pkid rendered as text — so the key is rendered the same way here rather than left to a
    /// widening conversion the server would apply per row. InvariantCulture because the value that
    /// went in was digits and the value that matches it has to be too.
    /// </summary>
    public async Task<IEnumerable<RowAuditHistoryEntry>> GetForRecordAsync(
        string tableName,
        int pkid,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await connection.QueryAsync<RowAuditHistoryEntry>(new CommandDefinition(
            RowAuditSql.SelectForRecord,
            new
            {
                TableName = tableName,
                PrimaryKeyValues = pkid.ToString(CultureInfo.InvariantCulture),
            },
            cancellationToken: cancellationToken));
    }
}
