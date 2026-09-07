using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class LookupRepository : ILookupRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public LookupRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<AppUserLookup>> GetAppUsersAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<AppUserLookup>(new CommandDefinition(
            """
            SELECT u.UserId, u.UserName, u.IsActive
            FROM AppUser u
            ORDER BY u.UserName ASC
            """,
            cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<PublishStatusLookup>> GetPublishStatusesAsync(
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<PublishStatusLookup>(new CommandDefinition(
            """
            SELECT p.pkid AS Pkid, p.Description
            FROM PublishStatus p
            ORDER BY p.pkid ASC
            """,
            cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<PartnerLookup>> GetPartnersAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<PartnerLookup>(new CommandDefinition(
            """
            SELECT p.pkid AS Pkid, p.Name, p.AppKey
            FROM Partner p
            ORDER BY p.DisplayOrder ASC, p.Name ASC
            """,
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Ordered alphabetically rather than by pkid: unlike PublishStatus, whose keys are meaningful
    /// hand-assigned codes, CourseGroup.pkid is an opaque IDENTITY number.
    /// </summary>
    public async Task<IEnumerable<CourseGroupLookup>> GetCourseGroupsAsync(
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<CourseGroupLookup>(new CommandDefinition(
            """
            SELECT cg.pkid AS Pkid, cg.Description
            FROM CourseGroup cg
            ORDER BY cg.Description ASC
            """,
            cancellationToken: cancellationToken));
    }
}
