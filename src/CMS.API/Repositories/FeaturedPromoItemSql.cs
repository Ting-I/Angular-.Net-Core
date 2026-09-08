using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>
/// SQL fragments for <see cref="FeaturedPromoItem"/>. The WHERE builder and the week arithmetic
/// are kept separate from the repository so both can be unit tested without a database.
/// </summary>
public static class FeaturedPromoItemSql
{
    /// <summary>
    /// Base projection. Both FK nav blocks come last and each opens with <c>pkid AS Pkid</c>, which
    /// is what <see cref="SplitOn"/> keys the multi-map on. Both FKs are NOT NULL, so INNER JOINs.
    /// </summary>
    public const string SelectBase = """
        SELECT fpi.pkid AS Pkid,
               fpi.ScheduleOn,
               fpi.TrainingCenter_pkid AS TrainingCenterPkid,
               fpi.Slot,
               fpi.Promotion_pkid AS PromotionPkid,
               fpi.Topic,
               fpi.Description,
               tc.pkid AS Pkid, tc.Name, tc.AppKey, tc.IsDefault,
               p.pkid AS Pkid, p.PromoCode, p.Topic, p.Description
        FROM FeaturedPromoItem fpi
        INNER JOIN TrainingCenter tc ON tc.pkid = fpi.TrainingCenter_pkid
        INNER JOIN Promotion2 p ON p.pkid = fpi.Promotion_pkid
        """;

    /// <summary>Split points for the two nav objects appended to the projection.</summary>
    public const string SplitOn = "Pkid,Pkid";

    /// <summary>Day, then centre, then slot — the order the week grid renders in.</summary>
    public const string DefaultOrderBy =
        "ORDER BY fpi.ScheduleOn ASC, fpi.TrainingCenter_pkid ASC, fpi.Slot ASC";

    /// <summary>
    /// Monday..Sunday of the week containing <paramref name="date"/>. .NET numbers Sunday as 0, so
    /// the offset is rotated to make Monday the first day.
    /// </summary>
    public static (DateOnly Start, DateOnly End) WeekOf(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        var start = date.AddDays(-daysSinceMonday);
        return (start, start.AddDays(6));
    }

    /// <summary>
    /// Builds the WHERE clause (empty string when the query is unfiltered) plus the
    /// matching Dapper parameter set.
    /// </summary>
    public static (string Where, Dictionary<string, object?> Parameters) BuildWhere(FeaturedPromoItemQuery? query)
    {
        var clauses = new List<string>();
        var parameters = new Dictionary<string, object?>();

        if (query?.TrainingCenterPkid is short trainingCenterPkid)
        {
            clauses.Add("fpi.TrainingCenter_pkid = @TrainingCenterPkid");
            parameters["TrainingCenterPkid"] = trainingCenterPkid;
        }

        // One week, Monday to Sunday, both ends inclusive — ScheduleOn is a date column, so a
        // closed range needs no end-of-day handling.
        if (query?.WeekOf is DateOnly weekOf)
        {
            var (start, end) = WeekOf(weekOf);
            clauses.Add("fpi.ScheduleOn >= @WeekStart AND fpi.ScheduleOn <= @WeekEnd");
            parameters["WeekStart"] = start;
            parameters["WeekEnd"] = end;
        }

        var where = clauses.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", clauses);
        return (where, parameters);
    }
}
