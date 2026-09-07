using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class PublishStatusRepository : IPublishStatusRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PublishStatusRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
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
    /// </summary>
    public async Task<byte> CreateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

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
            cancellationToken: cancellationToken));

        return request.Pkid;
    }

    public async Task<bool> UpdateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // pkid is the primary key and is not updatable.
        var affected = await connection.ExecuteAsync(new CommandDefinition(
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
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(byte pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // No junction rows to clear first; Course and Promotion2 hold enforced FKs, and the
        // controller blocks the call before it can raise SQL error 547.
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM PublishStatus WHERE pkid = @Pkid",
            new { Pkid = pkid },
            cancellationToken: cancellationToken));

        return affected > 0;
    }
}
