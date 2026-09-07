using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>
/// SQL fragments for <see cref="Course"/>. The WHERE builder is kept separate from the repository
/// so the filter logic can be unit tested without a database.
/// </summary>
public static class CourseSql
{
    /// <summary>
    /// Base projection. The three FK nav blocks come last and each opens with <c>pkid AS Pkid</c>,
    /// which is what <see cref="SplitOn"/> keys the multi-map on. CourseGroup is a LEFT JOIN
    /// because CourseGroup_pkid is the one nullable FK.
    /// </summary>
    public const string SelectBase = """
        SELECT c.pkid AS Pkid,
               c.Title,
               c.OfficialTitle,
               c.CourseId,
               c.ProdCourseId,
               c.FriendlyUrl,
               c.DisplayOrder,
               c.Partner_pkid AS PartnerPkid,
               c.CourseGroup_pkid AS CourseGroupPkid,
               c.PublishStatus_pkid AS PublishStatusPkid,
               c.ScheduleOn,
               c.ScheduleOff,
               c.Hour,
               c.ListPrice,
               c.LearningCredit,
               c.Material,
               c.Objective,
               c.Target,
               c.Prerequisites,
               c.Outline,
               c.TowardCertOrExam,
               c.Note,
               c.OtherInfo,
               c.CanRepeat,
               (SELECT COUNT(*) FROM CourseFAQ f WHERE f.Course_pkid = c.pkid) AS CourseFaqCount,
               (SELECT COUNT(*) FROM CourseRelatedLink rl WHERE rl.Course_pkid = c.pkid) AS CourseRelatedLinkCount,
               (SELECT COUNT(*) FROM HotCourse hc WHERE hc.Course_pkid = c.pkid) AS HotCourseCount,
               (SELECT COUNT(*) FROM CourseRecomm cr WHERE cr.CourseId = c.CourseId OR cr.RecommCourseId = c.CourseId) AS CourseRecommCount,
               pt.pkid AS Pkid, pt.Name, pt.AppKey,
               cg.pkid AS Pkid, cg.Description,
               ps.pkid AS Pkid, ps.Description
        FROM Course c
        INNER JOIN Partner pt ON pt.pkid = c.Partner_pkid
        LEFT JOIN CourseGroup cg ON cg.pkid = c.CourseGroup_pkid
        INNER JOIN PublishStatus ps ON ps.pkid = c.PublishStatus_pkid
        """;

    /// <summary>Split points for the three nav objects appended to the projection.</summary>
    public const string SplitOn = "Pkid,Pkid,Pkid";

    /// <summary>pkid breaks the tie so rows sharing a DisplayOrder still paginate deterministically.</summary>
    public const string DefaultOrderBy = "ORDER BY c.DisplayOrder ASC, c.pkid ASC";

    /// <summary>對應認證 — read on GetById only, on the same connection as the record.</summary>
    public const string SelectCertificationPkids = """
        SELECT Certification_pkid
        FROM CourseInCertification
        WHERE Course_pkid = @Pkid
        ORDER BY Certification_pkid
        """;

    /// <summary>對應職務類別 — read on GetById only, on the same connection as the record.</summary>
    public const string SelectJobCategoryPkids = """
        SELECT JobCategory_pkid
        FROM CourseJobCategories
        WHERE Course_pkid = @Pkid
        ORDER BY JobCategory_pkid
        """;

    /// <summary>
    /// Builds the WHERE clause (empty string when the query is unfiltered) plus the
    /// matching Dapper parameter set.
    /// </summary>
    public static (string Where, Dictionary<string, object?> Parameters) BuildWhere(CourseQuery? query)
    {
        var clauses = new List<string>();
        var parameters = new Dictionary<string, object?>();

        var keyword = query?.Keyword?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            // Only the short identifying columns. The eight long-text columns (four nvarchar(4000)
            // and two nvarchar(max)) are excluded — scanning them is slow and nobody searches by them.
            clauses.Add(
                "(c.Title LIKE @Keyword OR c.OfficialTitle LIKE @Keyword OR c.CourseId LIKE @Keyword " +
                "OR c.ProdCourseId LIKE @Keyword OR c.FriendlyUrl LIKE @Keyword)");
            parameters["Keyword"] = $"%{EscapeLike(keyword)}%";
        }

        if (query?.PartnerPkid is short partnerPkid)
        {
            clauses.Add("c.Partner_pkid = @PartnerPkid");
            parameters["PartnerPkid"] = partnerPkid;
        }

        if (query?.CourseGroupPkid is short courseGroupPkid)
        {
            clauses.Add("c.CourseGroup_pkid = @CourseGroupPkid");
            parameters["CourseGroupPkid"] = courseGroupPkid;
        }

        if (query?.PublishStatusPkid is byte publishStatusPkid)
        {
            clauses.Add("c.PublishStatus_pkid = @PublishStatusPkid");
            parameters["PublishStatusPkid"] = publishStatusPkid;
        }

        // Each bound is emitted independently so an open-ended range works. Both are inclusive.
        if (query?.ScheduleOnFrom is DateOnly scheduleOnFrom)
        {
            clauses.Add("c.ScheduleOn >= @ScheduleOnFrom");
            parameters["ScheduleOnFrom"] = scheduleOnFrom;
        }

        if (query?.ScheduleOnTo is DateOnly scheduleOnTo)
        {
            clauses.Add("c.ScheduleOn <= @ScheduleOnTo");
            parameters["ScheduleOnTo"] = scheduleOnTo;
        }

        if (query?.ScheduleOffFrom is DateOnly scheduleOffFrom)
        {
            clauses.Add("c.ScheduleOff >= @ScheduleOffFrom");
            parameters["ScheduleOffFrom"] = scheduleOffFrom;
        }

        if (query?.ScheduleOffTo is DateOnly scheduleOffTo)
        {
            clauses.Add("c.ScheduleOff <= @ScheduleOffTo");
            parameters["ScheduleOffTo"] = scheduleOffTo;
        }

        // Tri-state: false (不允許) is a filter, not an absence of one.
        if (query?.CanRepeat is bool canRepeat)
        {
            clauses.Add("c.CanRepeat = @CanRepeat");
            parameters["CanRepeat"] = canRepeat;
        }

        var where = clauses.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", clauses);
        return (where, parameters);
    }

    /// <summary>Escapes LIKE wildcards so a keyword such as "50%" matches literally.</summary>
    private static string EscapeLike(string value) =>
        value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
}
