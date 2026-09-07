using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>
/// SQL fragments for <see cref="PublishStatus"/>. The WHERE builder is kept separate from the
/// repository so the filter logic can be unit tested without a database.
/// </summary>
public static class PublishStatusSql
{
    /// <summary>
    /// Base projection. CourseCount and PromotionCount are correlated subqueries over the two
    /// tables that hold a FK to PublishStatus.
    /// </summary>
    public const string SelectBase = """
        SELECT p.pkid AS Pkid,
               p.Description,
               p.IsDraft,
               p.IsPublished,
               p.IsDiscontinued,
               (SELECT COUNT(*) FROM Course c WHERE c.PublishStatus_pkid = p.pkid) AS CourseCount,
               (SELECT COUNT(*) FROM Promotion2 pr WHERE pr.PublishStatus_pkid = p.pkid) AS PromotionCount
        FROM PublishStatus p
        """;

    public const string DefaultOrderBy = "ORDER BY p.pkid ASC";

    /// <summary>
    /// Builds the WHERE clause (empty string when the query is unfiltered) plus the
    /// matching Dapper parameter set.
    /// </summary>
    public static (string Where, Dictionary<string, object?> Parameters) BuildWhere(PublishStatusQuery? query)
    {
        var clauses = new List<string>();
        var parameters = new Dictionary<string, object?>();

        var keyword = query?.Keyword?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            clauses.Add("p.Description LIKE @Keyword");
            parameters["Keyword"] = $"%{EscapeLike(keyword)}%";
        }

        // Tri-state: false is a filter, not an absence of one.
        if (query?.IsDraft is bool isDraft)
        {
            clauses.Add("p.IsDraft = @IsDraft");
            parameters["IsDraft"] = isDraft;
        }

        if (query?.IsPublished is bool isPublished)
        {
            clauses.Add("p.IsPublished = @IsPublished");
            parameters["IsPublished"] = isPublished;
        }

        if (query?.IsDiscontinued is bool isDiscontinued)
        {
            clauses.Add("p.IsDiscontinued = @IsDiscontinued");
            parameters["IsDiscontinued"] = isDiscontinued;
        }

        var where = clauses.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", clauses);
        return (where, parameters);
    }

    /// <summary>Escapes LIKE wildcards so a keyword such as "50%" matches literally.</summary>
    private static string EscapeLike(string value) =>
        value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
}
