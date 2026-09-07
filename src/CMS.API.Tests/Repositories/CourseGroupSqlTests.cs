using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Repositories;

/// <summary>Covers the list/filter WHERE-clause construction used by the query endpoint.</summary>
public class CourseGroupSqlTests
{
    private const string CourseExists = "EXISTS (SELECT 1 FROM Course c WHERE c.CourseGroup_pkid = cg.pkid)";
    private const string PartnerGroupExists =
        "EXISTS (SELECT 1 FROM PartnerCourseGroup g WHERE g.CourseGroup_pkid = cg.pkid)";

    [Theory]
    [InlineData("SELECT COUNT(*) FROM Course c WHERE c.CourseGroup_pkid = cg.pkid")]
    [InlineData("SELECT COUNT(*) FROM PartnerCourseGroup g WHERE g.CourseGroup_pkid = cg.pkid")]
    public void SelectBase_ProjectsEveryReferenceCountSubquery(string subquery)
    {
        Assert.Contains(subquery, CourseGroupSql.SelectBase);
    }

    [Fact]
    public void SelectBase_ProjectsTheKeyAndEveryColumn()
    {
        Assert.Contains("cg.pkid AS Pkid", CourseGroupSql.SelectBase);
        Assert.Contains("cg.Description", CourseGroupSql.SelectBase);
        Assert.Contains("FROM CourseGroup cg", CourseGroupSql.SelectBase);
    }

    [Fact]
    public void DefaultOrderBy_SortsByPkid()
    {
        Assert.Equal("ORDER BY cg.pkid ASC", CourseGroupSql.DefaultOrderBy);
    }

    [Fact]
    public void BuildWhere_WithEmptyQuery_ReturnsNoClauseAndNoParameters()
    {
        var (where, parameters) = CourseGroupSql.BuildWhere(new CourseGroupQuery());

        Assert.Equal(string.Empty, where);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildWhere_WithNullQuery_ReturnsNoClause()
    {
        var (where, parameters) = CourseGroupSql.BuildWhere(null);

        Assert.Equal(string.Empty, where);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildWhere_WithKeyword_SearchesDescriptionAsAParenthesisedGroup()
    {
        var (where, parameters) = CourseGroupSql.BuildWhere(new CourseGroupQuery { Keyword = "雲端" });

        Assert.Equal("WHERE (cg.Description LIKE @Keyword)", where);
        Assert.Equal("%雲端%", parameters["Keyword"]);
    }

    [Theory]
    [InlineData("  雲端  ", "%雲端%")]
    [InlineData("50%", "%50[%]%")]
    [InlineData("a_b", "%a[_]b%")]
    [InlineData("x[y", "%x[[]y%")]
    public void BuildWhere_TrimsAndEscapesKeyword(string keyword, string expected)
    {
        var (_, parameters) = CourseGroupSql.BuildWhere(new CourseGroupQuery { Keyword = keyword });

        Assert.Equal(expected, parameters["Keyword"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildWhere_WithBlankKeyword_AddsNoClause(string keyword)
    {
        var (where, parameters) = CourseGroupSql.BuildWhere(new CourseGroupQuery { Keyword = keyword });

        Assert.Equal(string.Empty, where);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildWhere_WithInUseTrue_RequiresEitherChildTableToHaveARow()
    {
        var (where, parameters) = CourseGroupSql.BuildWhere(new CourseGroupQuery { InUse = true });

        Assert.Equal($"WHERE ({CourseExists} OR {PartnerGroupExists})", where);
        Assert.Empty(parameters);
    }

    /// <summary>
    /// false is a real filter (show the orphans), not an absent one — a falsy check would silently
    /// drop it and return every row.
    /// </summary>
    [Fact]
    public void BuildWhere_WithInUseFalse_RequiresBothChildTablesToBeEmpty()
    {
        var (where, parameters) = CourseGroupSql.BuildWhere(new CourseGroupQuery { InUse = false });

        Assert.Equal($"WHERE (NOT {CourseExists} AND NOT {PartnerGroupExists})", where);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildWhere_UsesExistsRatherThanRecountingTheSubqueries()
    {
        var (where, _) = CourseGroupSql.BuildWhere(new CourseGroupQuery { InUse = true });

        Assert.DoesNotContain("COUNT(*)", where);
    }

    [Fact]
    public void BuildWhere_WithKeywordAndInUse_AndsBothClauses()
    {
        var (where, parameters) = CourseGroupSql.BuildWhere(new CourseGroupQuery
        {
            Keyword = "雲端",
            InUse = false,
        });

        Assert.Equal(
            $"WHERE (cg.Description LIKE @Keyword) AND (NOT {CourseExists} AND NOT {PartnerGroupExists})",
            where);
        Assert.Equal("%雲端%", parameters["Keyword"]);
    }
}
