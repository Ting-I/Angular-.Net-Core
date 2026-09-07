using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>
/// SQL fragments for <see cref="CourseGroup"/>. The WHERE builder is kept separate from the
/// repository so the filter logic can be unit tested without a database.
/// </summary>
public static class CourseGroupSql
{
    /// <summary>
    /// Base projection. The two counts are correlated subqueries over the tables that point at
    /// CourseGroup.pkid; the controller reads them to guard the delete.
    /// </summary>
    public const string SelectBase = """
        SELECT cg.pkid AS Pkid,
               cg.Description,
               (SELECT COUNT(*) FROM Course c WHERE c.CourseGroup_pkid = cg.pkid) AS CourseCount,
               (SELECT COUNT(*) FROM PartnerCourseGroup g WHERE g.CourseGroup_pkid = cg.pkid) AS PartnerCourseGroupCount
        FROM CourseGroup cg
        """;

    /// <summary>
    /// There is no DisplayOrder column and no date to fall back on, so pkid keeps the order the
    /// rows were added in — the same call PublishStatusSql makes for its code table.
    /// </summary>
    public const string DefaultOrderBy = "ORDER BY cg.pkid ASC";

    /// <summary>
    /// Builds the WHERE clause (empty string when the query is unfiltered) plus the
    /// matching Dapper parameter set.
    /// </summary>
    public static (string Where, Dictionary<string, object?> Parameters) BuildWhere(CourseGroupQuery? query)
    {
        var clauses = new List<string>();
        var parameters = new Dictionary<string, object?>();

        var keyword = query?.Keyword?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            // One column today, but the parentheses keep the clause safe to extend with an OR arm.
            clauses.Add("(cg.Description LIKE @Keyword)");
            parameters["Keyword"] = $"%{EscapeLike(keyword)}%";
        }

        // Tri-state: false is a filter (show the orphans), not an absence of one. EXISTS rather than
        // the COUNT(*) subqueries so the test short-circuits on the first matching child row.
        if (query?.InUse is bool inUse)
        {
            clauses.Add(inUse
                ? "(EXISTS (SELECT 1 FROM Course c WHERE c.CourseGroup_pkid = cg.pkid) " +
                  "OR EXISTS (SELECT 1 FROM PartnerCourseGroup g WHERE g.CourseGroup_pkid = cg.pkid))"
                : "(NOT EXISTS (SELECT 1 FROM Course c WHERE c.CourseGroup_pkid = cg.pkid) " +
                  "AND NOT EXISTS (SELECT 1 FROM PartnerCourseGroup g WHERE g.CourseGroup_pkid = cg.pkid))");
        }

        var where = clauses.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", clauses);
        return (where, parameters);
    }

    /// <summary>Escapes LIKE wildcards so a keyword such as "50%" matches literally.</summary>
    private static string EscapeLike(string value) =>
        value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
}
