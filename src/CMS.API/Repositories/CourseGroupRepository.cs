using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class CourseGroupRepository : ICourseGroupRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CourseGroupRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
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
    /// </summary>
    public async Task<short> CreateAsync(CourseGroupRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<short>(new CommandDefinition(
            """
            INSERT INTO CourseGroup (Description)
            VALUES (@Description);
            SELECT CAST(SCOPE_IDENTITY() AS smallint);
            """,
            new { request.Description },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(CourseGroupRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // pkid is the primary key and is not updatable.
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE CourseGroup
            SET Description = @Description
            WHERE pkid = @Pkid;
            """,
            new { request.Pkid, request.Description },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(short pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // No junction rows to clear first. The controller must have checked the reference counts
        // before reaching this method: FK_Course_CourseGroup is ON DELETE CASCADE, so the database
        // would accept this statement and take every referencing Course row with it.
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CourseGroup WHERE pkid = @Pkid",
            new { Pkid = pkid },
            cancellationToken: cancellationToken));

        return affected > 0;
    }
}
