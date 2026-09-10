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

    /// <summary>
    /// Ordered by PermissionLevel first: an operator picking roles reads them by authority, not
    /// alphabetically. RoleName breaks ties.
    /// </summary>
    public async Task<IEnumerable<AppRoleLookup>> GetAppRolesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<AppRoleLookup>(new CommandDefinition(
            """
            SELECT r.RoleId, r.RoleName, r.PermissionLevel
            FROM AppRole r
            ORDER BY r.PermissionLevel ASC, r.RoleName ASC
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

    /// <summary>
    /// Certification.Title is nchar(100) NULL, so it is RTRIMmed and coalesced to an empty string.
    /// Ordered under the owning partner, which is how an operator reads a certification list.
    /// </summary>
    public async Task<IEnumerable<CertificationLookup>> GetCertificationsAsync(
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<CertificationLookup>(new CommandDefinition(
            """
            SELECT ct.pkid AS Pkid,
                   RTRIM(ISNULL(ct.Title, '')) AS Title,
                   ct.Partner_pkid AS PartnerPkid,
                   pt.Name AS PartnerName
            FROM Certification ct
            INNER JOIN Partner pt ON pt.pkid = ct.Partner_pkid
            ORDER BY pt.DisplayOrder ASC, pt.Name ASC, Title ASC
            """,
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Ordered alphabetically rather than by pkid, for the same reason as CourseGroup: the key is
    /// an opaque IDENTITY number that means nothing to the operator picking from the list.
    /// </summary>
    public async Task<IEnumerable<JobCategoryLookup>> GetJobCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<JobCategoryLookup>(new CommandDefinition(
            """
            SELECT jc.pkid AS Pkid, jc.Description
            FROM JobCategory jc
            ORDER BY jc.Description ASC
            """,
            cancellationToken: cancellationToken));
    }

    /// <summary>Ordered the way the centres appear on the public site; Name breaks ties.</summary>
    public async Task<IEnumerable<TrainingCenterLookup>> GetTrainingCentersAsync(
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<TrainingCenterLookup>(new CommandDefinition(
            """
            SELECT tc.pkid AS Pkid, tc.Name, tc.AppKey, tc.IsDefault
            FROM TrainingCenter tc
            ORDER BY tc.DisplayOrder ASC, tc.Name ASC
            """,
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Autocomplete feed. PromoCodes start with a date stamp (e.g. 20251204_SkillTrainAI), so
    /// descending code order puts the most recent promotions first; the TOP keeps the panel short.
    /// </summary>
    public async Task<IEnumerable<PromotionLookup>> SearchPromotionsAsync(
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        var trimmed = keyword?.Trim() ?? string.Empty;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<PromotionLookup>(new CommandDefinition(
            """
            SELECT TOP (20) p.pkid AS Pkid, p.PromoCode, p.Topic, p.Description
            FROM Promotion2 p
            WHERE p.PromoCode LIKE @Keyword
            ORDER BY p.PromoCode DESC
            """,
            new { Keyword = $"%{EscapeLike(trimmed)}%" },
            cancellationToken: cancellationToken));
    }

    public async Task<PromotionLookup?> GetPromotionByCodeAsync(
        string promoCode,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<PromotionLookup>(new CommandDefinition(
            """
            SELECT p.pkid AS Pkid, p.PromoCode, p.Topic, p.Description
            FROM Promotion2 p
            WHERE p.PromoCode = @PromoCode
            """,
            new { PromoCode = promoCode.Trim() },
            cancellationToken: cancellationToken));
    }

    /// <summary>Escapes LIKE wildcards so a keyword such as "50%" matches literally.</summary>
    private static string EscapeLike(string value) =>
        value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
}
