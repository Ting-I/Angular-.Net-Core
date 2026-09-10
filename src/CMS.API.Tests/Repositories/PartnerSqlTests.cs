using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Repositories;

/// <summary>Covers the list/filter WHERE-clause construction used by the query endpoint.</summary>
public class PartnerSqlTests
{
    [Theory]
    [InlineData("SELECT COUNT(*) FROM Certification ct WHERE ct.Partner_pkid = p.pkid")]
    [InlineData("SELECT COUNT(*) FROM Course c WHERE c.Partner_pkid = p.pkid")]
    [InlineData("SELECT COUNT(*) FROM PartnerCourseGroup g WHERE g.Partner_pkid = p.pkid")]
    [InlineData("SELECT COUNT(*) FROM Promotion2 pr WHERE pr.RelatedPartner_pkid = p.pkid")]
    [InlineData("SELECT COUNT(*) FROM Seminar s WHERE s.Partner_pkid = p.pkid")]
    public void SelectBase_ProjectsEveryReferenceCountSubquery(string subquery)
    {
        Assert.Contains(subquery, PartnerSql.SelectBase);
    }

    [Fact]
    public void SelectBase_ProjectsTheKeyAndEveryColumn()
    {
        Assert.Contains("p.pkid AS Pkid", PartnerSql.SelectBase);
        Assert.Contains("p.Name", PartnerSql.SelectBase);
        Assert.Contains("p.AppKey", PartnerSql.SelectBase);
        Assert.Contains("p.NameOnPartnerMenu", PartnerSql.SelectBase);
        Assert.Contains("p.NameOnCourseDetailPage", PartnerSql.SelectBase);
        Assert.Contains("p.DisplayOrder", PartnerSql.SelectBase);
        Assert.Contains("p.ImageFilename", PartnerSql.SelectBase);
    }

    [Fact]
    public void DefaultOrderBy_SortsByDisplayOrderThenPkid()
    {
        Assert.Equal("ORDER BY p.DisplayOrder ASC, p.pkid ASC", PartnerSql.DefaultOrderBy);
    }

    [Fact]
    public void BuildWhere_WithEmptyQuery_ReturnsNoClauseAndNoParameters()
    {
        var (where, parameters) = PartnerSql.BuildWhere(new PartnerQuery());

        Assert.Equal(string.Empty, where);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildWhere_WithNullQuery_ReturnsNoClause()
    {
        var (where, parameters) = PartnerSql.BuildWhere(null);

        Assert.Equal(string.Empty, where);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildWhere_WithKeyword_SearchesTheFourStringColumnsAsOneOrGroup()
    {
        var (where, parameters) = PartnerSql.BuildWhere(new PartnerQuery { Keyword = "Cisco" });

        Assert.Equal(
            "WHERE (p.Name LIKE @Keyword OR p.AppKey LIKE @Keyword " +
            "OR p.NameOnPartnerMenu LIKE @Keyword OR p.NameOnCourseDetailPage LIKE @Keyword)",
            where);
        Assert.Equal("%Cisco%", parameters["Keyword"]);
    }

    [Theory]
    [InlineData("  Cisco  ", "%Cisco%")]
    [InlineData("50%", "%50[%]%")]
    [InlineData("a_b", "%a[_]b%")]
    [InlineData("x[y", "%x[[]y%")]
    public void BuildWhere_TrimsAndEscapesKeyword(string keyword, string expected)
    {
        var (_, parameters) = PartnerSql.BuildWhere(new PartnerQuery { Keyword = keyword });

        Assert.Equal(expected, parameters["Keyword"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void BuildWhere_IgnoresBlankKeyword(string? keyword)
    {
        var (where, parameters) = PartnerSql.BuildWhere(new PartnerQuery { Keyword = keyword });

        Assert.Equal(string.Empty, where);
        Assert.DoesNotContain("Keyword", parameters.Keys);
    }

    [Fact]
    public void BuildWhere_WithHasImageTrue_RequiresANonBlankFilename()
    {
        var (where, parameters) = PartnerSql.BuildWhere(new PartnerQuery { HasImage = true });

        Assert.Equal("WHERE (p.ImageFilename IS NOT NULL AND p.ImageFilename <> '')", where);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildWhere_WithHasImageFalse_MatchesNullAndBlankFilenames()
    {
        // false is a filter, not an absence of one — a falsy check would silently drop it.
        var (where, parameters) = PartnerSql.BuildWhere(new PartnerQuery { HasImage = false });

        Assert.Equal("WHERE (p.ImageFilename IS NULL OR p.ImageFilename = '')", where);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildWhere_WithHasImageNull_AddsNoClause()
    {
        var (where, parameters) = PartnerSql.BuildWhere(new PartnerQuery { HasImage = null });

        Assert.Equal(string.Empty, where);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildWhere_WithEveryFilter_JoinsClausesWithAnd()
    {
        var (where, parameters) = PartnerSql.BuildWhere(new PartnerQuery
        {
            Keyword = "Cisco",
            HasImage = false,
        });

        Assert.StartsWith("WHERE ", where);
        Assert.Equal(1, where.Split(" AND ").Length - 1);
        Assert.Contains("p.Name LIKE @Keyword", where);
        Assert.Contains("p.ImageFilename IS NULL", where);
        Assert.Single(parameters);
    }
}
