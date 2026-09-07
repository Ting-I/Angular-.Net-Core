using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>
/// SQL fragments for <see cref="Partner"/>. The WHERE builder is kept separate from the
/// repository so the filter logic can be unit tested without a database.
/// </summary>
public static class PartnerSql
{
    /// <summary>
    /// Base projection. The five counts are correlated subqueries over every table that points at
    /// Partner.pkid — four enforced FKs plus Seminar, whose reference has no FK constraint.
    /// </summary>
    public const string SelectBase = """
        SELECT p.pkid AS Pkid,
               p.Name,
               p.AppKey,
               p.NameOnPartnerMenu,
               p.NameOnCourseDetailPage,
               p.DisplayOrder,
               p.ImageFilename,
               (SELECT COUNT(*) FROM Certification ct WHERE ct.Partner_pkid = p.pkid) AS CertificationCount,
               (SELECT COUNT(*) FROM Course c WHERE c.Partner_pkid = p.pkid) AS CourseCount,
               (SELECT COUNT(*) FROM PartnerCourseGroup g WHERE g.Partner_pkid = p.pkid) AS CourseGroupCount,
               (SELECT COUNT(*) FROM Promotion2 pr WHERE pr.RelatedPartner_pkid = p.pkid) AS PromotionCount,
               (SELECT COUNT(*) FROM Seminar s WHERE s.Partner_pkid = p.pkid) AS SeminarCount
        FROM Partner p
        """;

    /// <summary>pkid breaks the tie so rows sharing a DisplayOrder still paginate deterministically.</summary>
    public const string DefaultOrderBy = "ORDER BY p.DisplayOrder ASC, p.pkid ASC";

    /// <summary>
    /// Builds the WHERE clause (empty string when the query is unfiltered) plus the
    /// matching Dapper parameter set.
    /// </summary>
    public static (string Where, Dictionary<string, object?> Parameters) BuildWhere(PartnerQuery? query)
    {
        var clauses = new List<string>();
        var parameters = new Dictionary<string, object?>();

        var keyword = query?.Keyword?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            clauses.Add(
                "(p.Name LIKE @Keyword OR p.AppKey LIKE @Keyword " +
                "OR p.NameOnPartnerMenu LIKE @Keyword OR p.NameOnCourseDetailPage LIKE @Keyword)");
            parameters["Keyword"] = $"%{EscapeLike(keyword)}%";
        }

        // Tri-state: false is a filter, not an absence of one. Blank strings count as "no image",
        // because varchar columns in this schema hold '' as readily as NULL.
        if (query?.HasImage is bool hasImage)
        {
            clauses.Add(hasImage
                ? "(p.ImageFilename IS NOT NULL AND p.ImageFilename <> '')"
                : "(p.ImageFilename IS NULL OR p.ImageFilename = '')");
        }

        var where = clauses.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", clauses);
        return (where, parameters);
    }

    /// <summary>Escapes LIKE wildcards so a keyword such as "50%" matches literally.</summary>
    private static string EscapeLike(string value) =>
        value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
}
