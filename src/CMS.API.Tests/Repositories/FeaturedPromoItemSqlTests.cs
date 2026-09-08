using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Repositories;

/// <summary>Covers the week arithmetic and the list/filter WHERE-clause construction.</summary>
public class FeaturedPromoItemSqlTests
{
    private const string WeekClause = "fpi.ScheduleOn >= @WeekStart AND fpi.ScheduleOn <= @WeekEnd";

    // ---------- Projection ----------

    [Theory]
    [InlineData("fpi.pkid AS Pkid")]
    [InlineData("fpi.ScheduleOn")]
    [InlineData("fpi.TrainingCenter_pkid AS TrainingCenterPkid")]
    [InlineData("fpi.Slot")]
    [InlineData("fpi.Promotion_pkid AS PromotionPkid")]
    [InlineData("fpi.Topic")]
    [InlineData("fpi.Description")]
    public void SelectBase_ProjectsTheKeyAndEveryColumn(string column)
    {
        Assert.Contains(column, FeaturedPromoItemSql.SelectBase);
    }

    /// <summary>Both FKs are NOT NULL, so both joins are INNER.</summary>
    [Theory]
    [InlineData("INNER JOIN TrainingCenter tc ON tc.pkid = fpi.TrainingCenter_pkid")]
    [InlineData("INNER JOIN Promotion2 p ON p.pkid = fpi.Promotion_pkid")]
    public void SelectBase_InnerJoinsBothNavTables(string join)
    {
        Assert.Contains(join, FeaturedPromoItemSql.SelectBase);
    }

    [Fact]
    public void SelectBase_ProjectsThePromoCodeFromTheNavBlock()
    {
        Assert.Contains("p.pkid AS Pkid, p.PromoCode, p.Topic, p.Description", FeaturedPromoItemSql.SelectBase);
        Assert.Equal("Pkid,Pkid", FeaturedPromoItemSql.SplitOn);
    }

    [Fact]
    public void DefaultOrderBy_SortsByDayCentreThenSlot()
    {
        Assert.Equal(
            "ORDER BY fpi.ScheduleOn ASC, fpi.TrainingCenter_pkid ASC, fpi.Slot ASC",
            FeaturedPromoItemSql.DefaultOrderBy);
    }

    // ---------- Week arithmetic ----------

    /// <summary>2026-03-16 is a Monday. Every day of that week maps back to it.</summary>
    [Theory]
    [InlineData(16)] // Monday
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(19)]
    [InlineData(20)]
    [InlineData(21)] // Saturday
    [InlineData(22)] // Sunday
    public void WeekOf_MapsEveryDayToItsMondayAndSunday(int day)
    {
        var (start, end) = FeaturedPromoItemSql.WeekOf(new DateOnly(2026, 3, day));

        Assert.Equal(new DateOnly(2026, 3, 16), start);
        Assert.Equal(new DateOnly(2026, 3, 22), end);
        Assert.Equal(DayOfWeek.Monday, start.DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, end.DayOfWeek);
    }

    /// <summary>.NET numbers Sunday as 0; it must land at the end of the week, not start a new one.</summary>
    [Fact]
    public void WeekOf_TreatsSundayAsTheLastDay()
    {
        var (start, end) = FeaturedPromoItemSql.WeekOf(new DateOnly(2026, 3, 22));

        Assert.Equal(new DateOnly(2026, 3, 16), start);
        Assert.Equal(new DateOnly(2026, 3, 22), end);
    }

    [Fact]
    public void WeekOf_TheNextMondayStartsANewWeek()
    {
        var (start, _) = FeaturedPromoItemSql.WeekOf(new DateOnly(2026, 3, 23));

        Assert.Equal(new DateOnly(2026, 3, 23), start);
    }

    [Fact]
    public void WeekOf_CrossesMonthAndYearBoundaries()
    {
        var (start, end) = FeaturedPromoItemSql.WeekOf(new DateOnly(2027, 1, 1)); // a Friday

        Assert.Equal(new DateOnly(2026, 12, 28), start);
        Assert.Equal(new DateOnly(2027, 1, 3), end);
    }

    // ---------- WHERE ----------

    [Fact]
    public void BuildWhere_WithEmptyQuery_ReturnsNoClauseAndNoParameters()
    {
        var (where, parameters) = FeaturedPromoItemSql.BuildWhere(new FeaturedPromoItemQuery());

        Assert.Equal(string.Empty, where);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildWhere_WithNullQuery_ReturnsNoClause()
    {
        var (where, parameters) = FeaturedPromoItemSql.BuildWhere(null);

        Assert.Equal(string.Empty, where);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildWhere_WithTrainingCenter_FiltersTheForeignKey()
    {
        var (where, parameters) = FeaturedPromoItemSql.BuildWhere(
            new FeaturedPromoItemQuery { TrainingCenterPkid = 2 });

        Assert.Equal("WHERE fpi.TrainingCenter_pkid = @TrainingCenterPkid", where);
        Assert.Equal((short)2, parameters["TrainingCenterPkid"]);
    }

    /// <summary>The one-week filter: a mid-week date is widened to Monday..Sunday inclusive.</summary>
    [Fact]
    public void BuildWhere_WithWeekOf_BoundsScheduleOnToMondayThroughSunday()
    {
        var (where, parameters) = FeaturedPromoItemSql.BuildWhere(
            new FeaturedPromoItemQuery { WeekOf = new DateOnly(2026, 3, 18) });

        Assert.Equal($"WHERE {WeekClause}", where);
        Assert.Equal(new DateOnly(2026, 3, 16), parameters["WeekStart"]);
        Assert.Equal(new DateOnly(2026, 3, 22), parameters["WeekEnd"]);
    }

    [Fact]
    public void BuildWhere_WithWeekOfOnAMonday_StartsThatSameDay()
    {
        var (_, parameters) = FeaturedPromoItemSql.BuildWhere(
            new FeaturedPromoItemQuery { WeekOf = new DateOnly(2026, 3, 16) });

        Assert.Equal(new DateOnly(2026, 3, 16), parameters["WeekStart"]);
        Assert.Equal(new DateOnly(2026, 3, 22), parameters["WeekEnd"]);
    }

    [Fact]
    public void BuildWhere_WithWeekOfAndTrainingCenter_AndsBothClauses()
    {
        var (where, parameters) = FeaturedPromoItemSql.BuildWhere(new FeaturedPromoItemQuery
        {
            TrainingCenterPkid = 1,
            WeekOf = new DateOnly(2026, 3, 20),
        });

        Assert.Equal($"WHERE fpi.TrainingCenter_pkid = @TrainingCenterPkid AND {WeekClause}", where);
        Assert.Equal(3, parameters.Count);
        Assert.Equal((short)1, parameters["TrainingCenterPkid"]);
        Assert.Equal(new DateOnly(2026, 3, 16), parameters["WeekStart"]);
    }
}
