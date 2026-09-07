using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Repositories;

/// <summary>Covers the list/filter WHERE-clause construction used by the query endpoint.</summary>
public class CourseSqlTests
{
    // ---------- Projection ----------

    [Theory]
    [InlineData("SELECT COUNT(*) FROM CourseFAQ f WHERE f.Course_pkid = c.pkid")]
    [InlineData("SELECT COUNT(*) FROM CourseRelatedLink rl WHERE rl.Course_pkid = c.pkid")]
    [InlineData("SELECT COUNT(*) FROM HotCourse hc WHERE hc.Course_pkid = c.pkid")]
    public void SelectBase_ProjectsEveryEnforcedChildCountSubquery(string subquery)
    {
        Assert.Contains(subquery, CourseSql.SelectBase);
    }

    /// <summary>
    /// CourseRecomm joins on CourseId with no FK behind it and can name a course on either side of
    /// the pair, so the count has to match both columns — a course that is only recommended by
    /// others is still protected by the delete guard.
    /// </summary>
    [Fact]
    public void SelectBase_CountsCourseRecommOnBothSidesOfThePair()
    {
        Assert.Contains(
            "SELECT COUNT(*) FROM CourseRecomm cr WHERE cr.CourseId = c.CourseId OR cr.RecommCourseId = c.CourseId",
            CourseSql.SelectBase);
    }

    [Theory]
    [InlineData("c.Partner_pkid AS PartnerPkid")]
    [InlineData("c.CourseGroup_pkid AS CourseGroupPkid")]
    [InlineData("c.PublishStatus_pkid AS PublishStatusPkid")]
    public void SelectBase_AliasesEveryForeignKeyColumn(string alias)
    {
        Assert.Contains(alias, CourseSql.SelectBase);
    }

    [Theory]
    [InlineData("c.pkid AS Pkid")]
    [InlineData("c.Title")]
    [InlineData("c.OfficialTitle")]
    [InlineData("c.CourseId")]
    [InlineData("c.ProdCourseId")]
    [InlineData("c.FriendlyUrl")]
    [InlineData("c.DisplayOrder")]
    [InlineData("c.ScheduleOn")]
    [InlineData("c.ScheduleOff")]
    [InlineData("c.Hour")]
    [InlineData("c.ListPrice")]
    [InlineData("c.LearningCredit")]
    [InlineData("c.Material")]
    [InlineData("c.Objective")]
    [InlineData("c.Target")]
    [InlineData("c.Prerequisites")]
    [InlineData("c.Outline")]
    [InlineData("c.TowardCertOrExam")]
    [InlineData("c.Note")]
    [InlineData("c.OtherInfo")]
    [InlineData("c.CanRepeat")]
    [InlineData("FROM Course c")]
    public void SelectBase_ProjectsEveryScalarColumn(string fragment)
    {
        Assert.Contains(fragment, CourseSql.SelectBase);
    }

    [Theory]
    [InlineData("pt.pkid AS Pkid, pt.Name, pt.AppKey")]
    [InlineData("cg.pkid AS Pkid, cg.Description")]
    [InlineData("ps.pkid AS Pkid, ps.Description")]
    public void SelectBase_ProjectsEachNavObjectBlockOpeningWithItsKey(string block)
    {
        Assert.Contains(block, CourseSql.SelectBase);
    }

    /// <summary>CourseGroup_pkid is the one nullable FK, so only that JOIN may be a LEFT JOIN.</summary>
    [Fact]
    public void SelectBase_JoinsTheNullableForeignKeyWithLeftJoinAndTheOthersWithInner()
    {
        Assert.Contains("INNER JOIN Partner pt ON pt.pkid = c.Partner_pkid", CourseSql.SelectBase);
        Assert.Contains("LEFT JOIN CourseGroup cg ON cg.pkid = c.CourseGroup_pkid", CourseSql.SelectBase);
        Assert.Contains("INNER JOIN PublishStatus ps ON ps.pkid = c.PublishStatus_pkid", CourseSql.SelectBase);
    }

    [Fact]
    public void SplitOn_HasOneKeyPerNavObject()
    {
        Assert.Equal("Pkid,Pkid,Pkid", CourseSql.SplitOn);
    }

    [Fact]
    public void DefaultOrderBy_SortsByDisplayOrderThenPkid()
    {
        Assert.Equal("ORDER BY c.DisplayOrder ASC, c.pkid ASC", CourseSql.DefaultOrderBy);
    }

    [Fact]
    public void JunctionReads_TargetTheirOwnTablesWithAStableOrder()
    {
        Assert.Contains("FROM CourseInCertification", CourseSql.SelectCertificationPkids);
        Assert.Contains("WHERE Course_pkid = @Pkid", CourseSql.SelectCertificationPkids);
        Assert.Contains("ORDER BY Certification_pkid", CourseSql.SelectCertificationPkids);

        Assert.Contains("FROM CourseJobCategories", CourseSql.SelectJobCategoryPkids);
        Assert.Contains("WHERE Course_pkid = @Pkid", CourseSql.SelectJobCategoryPkids);
        Assert.Contains("ORDER BY JobCategory_pkid", CourseSql.SelectJobCategoryPkids);
    }

    // ---------- BuildWhere: no filters ----------

    [Fact]
    public void BuildWhere_WithEmptyQuery_ReturnsNoClauseAndNoParameters()
    {
        var (where, parameters) = CourseSql.BuildWhere(new CourseQuery());

        Assert.Equal(string.Empty, where);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildWhere_WithNullQuery_ReturnsNoClause()
    {
        var (where, parameters) = CourseSql.BuildWhere(null);

        Assert.Equal(string.Empty, where);
        Assert.Empty(parameters);
    }

    // ---------- BuildWhere: keyword ----------

    [Fact]
    public void BuildWhere_WithKeyword_SearchesTheFiveShortColumnsAsOneGroup()
    {
        var (where, parameters) = CourseSql.BuildWhere(new CourseQuery { Keyword = "Azure" });

        Assert.Equal(
            "WHERE (c.Title LIKE @Keyword OR c.OfficialTitle LIKE @Keyword OR c.CourseId LIKE @Keyword " +
            "OR c.ProdCourseId LIKE @Keyword OR c.FriendlyUrl LIKE @Keyword)",
            where);
        Assert.Equal("%Azure%", parameters["Keyword"]);
    }

    /// <summary>
    /// The four nvarchar(4000) and two nvarchar(max) columns are deliberately outside the keyword
    /// search — scanning them is slow and nobody looks a course up by its outline.
    /// </summary>
    [Theory]
    [InlineData("Material")]
    [InlineData("Objective")]
    [InlineData("Target")]
    [InlineData("Prerequisites")]
    [InlineData("Outline")]
    [InlineData("TowardCertOrExam")]
    [InlineData("Note")]
    [InlineData("OtherInfo")]
    public void BuildWhere_WithKeyword_ExcludesTheLongTextColumns(string column)
    {
        var (where, _) = CourseSql.BuildWhere(new CourseQuery { Keyword = "Azure" });

        Assert.DoesNotContain($"c.{column} LIKE", where);
    }

    [Fact]
    public void BuildWhere_TrimsTheKeyword()
    {
        var (_, parameters) = CourseSql.BuildWhere(new CourseQuery { Keyword = "  Azure  " });

        Assert.Equal("%Azure%", parameters["Keyword"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildWhere_WithBlankKeyword_IsNotAFilter(string keyword)
    {
        var (where, parameters) = CourseSql.BuildWhere(new CourseQuery { Keyword = keyword });

        Assert.Equal(string.Empty, where);
        Assert.Empty(parameters);
    }

    [Theory]
    [InlineData("50%", "%50[%]%")]
    [InlineData("a_b", "%a[_]b%")]
    [InlineData("[x]", "%[[]x]%")]
    public void BuildWhere_EscapesLikeWildcardsSoAKeywordMatchesLiterally(string keyword, string expected)
    {
        var (_, parameters) = CourseSql.BuildWhere(new CourseQuery { Keyword = keyword });

        Assert.Equal(expected, parameters["Keyword"]);
    }

    // ---------- BuildWhere: foreign keys ----------

    [Fact]
    public void BuildWhere_WithPartnerPkid_MatchesTheColumnExactly()
    {
        var (where, parameters) = CourseSql.BuildWhere(new CourseQuery { PartnerPkid = 3 });

        Assert.Equal("WHERE c.Partner_pkid = @PartnerPkid", where);
        Assert.Equal((short)3, parameters["PartnerPkid"]);
    }

    [Fact]
    public void BuildWhere_WithCourseGroupPkid_MatchesTheColumnExactly()
    {
        var (where, parameters) = CourseSql.BuildWhere(new CourseQuery { CourseGroupPkid = 7 });

        Assert.Equal("WHERE c.CourseGroup_pkid = @CourseGroupPkid", where);
        Assert.Equal((short)7, parameters["CourseGroupPkid"]);
    }

    [Fact]
    public void BuildWhere_WithPublishStatusPkid_MatchesTheColumnExactly()
    {
        var (where, parameters) = CourseSql.BuildWhere(new CourseQuery { PublishStatusPkid = 2 });

        Assert.Equal("WHERE c.PublishStatus_pkid = @PublishStatusPkid", where);
        Assert.Equal((byte)2, parameters["PublishStatusPkid"]);
    }

    // ---------- BuildWhere: date ranges ----------

    [Fact]
    public void BuildWhere_WithBothScheduleOnBounds_EmitsAnInclusiveRange()
    {
        var (where, parameters) = CourseSql.BuildWhere(new CourseQuery
        {
            ScheduleOnFrom = new DateOnly(2026, 1, 1),
            ScheduleOnTo = new DateOnly(2026, 12, 31),
        });

        Assert.Equal("WHERE c.ScheduleOn >= @ScheduleOnFrom AND c.ScheduleOn <= @ScheduleOnTo", where);
        Assert.Equal(new DateOnly(2026, 1, 1), parameters["ScheduleOnFrom"]);
        Assert.Equal(new DateOnly(2026, 12, 31), parameters["ScheduleOnTo"]);
    }

    /// <summary>Each bound stands alone, so an open-ended range is a valid filter.</summary>
    [Fact]
    public void BuildWhere_WithOnlyTheLowerScheduleOnBound_EmitsOnlyThatClause()
    {
        var (where, parameters) = CourseSql.BuildWhere(new CourseQuery
        {
            ScheduleOnFrom = new DateOnly(2026, 1, 1),
        });

        Assert.Equal("WHERE c.ScheduleOn >= @ScheduleOnFrom", where);
        Assert.Single(parameters);
    }

    [Fact]
    public void BuildWhere_WithOnlyTheUpperScheduleOffBound_EmitsOnlyThatClause()
    {
        var (where, parameters) = CourseSql.BuildWhere(new CourseQuery
        {
            ScheduleOffTo = new DateOnly(2027, 6, 30),
        });

        Assert.Equal("WHERE c.ScheduleOff <= @ScheduleOffTo", where);
        Assert.Single(parameters);
    }

    [Fact]
    public void BuildWhere_WithBothScheduleOffBounds_EmitsAnInclusiveRange()
    {
        var (where, _) = CourseSql.BuildWhere(new CourseQuery
        {
            ScheduleOffFrom = new DateOnly(2026, 1, 1),
            ScheduleOffTo = new DateOnly(2026, 12, 31),
        });

        Assert.Equal("WHERE c.ScheduleOff >= @ScheduleOffFrom AND c.ScheduleOff <= @ScheduleOffTo", where);
    }

    // ---------- BuildWhere: tri-state bool ----------

    [Fact]
    public void BuildWhere_WithCanRepeatTrue_MatchesTheColumn()
    {
        var (where, parameters) = CourseSql.BuildWhere(new CourseQuery { CanRepeat = true });

        Assert.Equal("WHERE c.CanRepeat = @CanRepeat", where);
        Assert.Equal(true, parameters["CanRepeat"]);
    }

    /// <summary>false (不允許) is a real filter — a falsy check would silently drop it.</summary>
    [Fact]
    public void BuildWhere_WithCanRepeatFalse_IsStillAFilter()
    {
        var (where, parameters) = CourseSql.BuildWhere(new CourseQuery { CanRepeat = false });

        Assert.Equal("WHERE c.CanRepeat = @CanRepeat", where);
        Assert.Equal(false, parameters["CanRepeat"]);
    }

    [Fact]
    public void BuildWhere_WithCanRepeatNull_IsNotAFilter()
    {
        var (where, parameters) = CourseSql.BuildWhere(new CourseQuery { CanRepeat = null });

        Assert.Equal(string.Empty, where);
        Assert.Empty(parameters);
    }

    // ---------- BuildWhere: combinations ----------

    [Fact]
    public void BuildWhere_WithEveryFilter_AndsThemAllTogether()
    {
        var (where, parameters) = CourseSql.BuildWhere(new CourseQuery
        {
            Keyword = "Azure",
            PartnerPkid = 1,
            CourseGroupPkid = 2,
            PublishStatusPkid = 3,
            ScheduleOnFrom = new DateOnly(2026, 1, 1),
            ScheduleOnTo = new DateOnly(2026, 12, 31),
            ScheduleOffFrom = new DateOnly(2027, 1, 1),
            ScheduleOffTo = new DateOnly(2027, 12, 31),
            CanRepeat = false,
        });

        Assert.StartsWith("WHERE ", where);
        // Nine clauses, so eight AND separators.
        Assert.Equal(9, where.Split(" AND ").Length);
        Assert.Contains("c.Partner_pkid = @PartnerPkid", where);
        Assert.Contains("c.CourseGroup_pkid = @CourseGroupPkid", where);
        Assert.Contains("c.PublishStatus_pkid = @PublishStatusPkid", where);
        Assert.Contains("c.ScheduleOn >= @ScheduleOnFrom", where);
        Assert.Contains("c.ScheduleOn <= @ScheduleOnTo", where);
        Assert.Contains("c.ScheduleOff >= @ScheduleOffFrom", where);
        Assert.Contains("c.ScheduleOff <= @ScheduleOffTo", where);
        Assert.Contains("c.CanRepeat = @CanRepeat", where);
        Assert.Equal(9, parameters.Count);
    }

    [Fact]
    public void BuildWhere_WithKeywordAndPartner_JoinsTheClausesWithAnd()
    {
        var (where, parameters) = CourseSql.BuildWhere(new CourseQuery
        {
            Keyword = "Azure",
            PartnerPkid = 1,
        });

        Assert.EndsWith(" AND c.Partner_pkid = @PartnerPkid", where);
        Assert.Equal(2, parameters.Count);
    }
}
