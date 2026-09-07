using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Repositories;

/// <summary>Covers the list/filter WHERE-clause construction used by the query endpoint.</summary>
public class PublishStatusSqlTests
{
    [Fact]
    public void SelectBase_ProjectsBothReferenceCountSubqueries()
    {
        Assert.Contains(
            "SELECT COUNT(*) FROM Course c WHERE c.PublishStatus_pkid = p.pkid",
            PublishStatusSql.SelectBase);
        Assert.Contains(
            "SELECT COUNT(*) FROM Promotion2 pr WHERE pr.PublishStatus_pkid = p.pkid",
            PublishStatusSql.SelectBase);
        Assert.Contains("p.pkid AS Pkid", PublishStatusSql.SelectBase);
    }

    [Fact]
    public void DefaultOrderBy_SortsByPkidAscending()
    {
        Assert.Equal("ORDER BY p.pkid ASC", PublishStatusSql.DefaultOrderBy);
    }

    [Fact]
    public void BuildWhere_WithEmptyQuery_ReturnsNoClauseAndNoParameters()
    {
        var (where, parameters) = PublishStatusSql.BuildWhere(new PublishStatusQuery());

        Assert.Equal(string.Empty, where);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildWhere_WithNullQuery_ReturnsNoClause()
    {
        var (where, parameters) = PublishStatusSql.BuildWhere(null);

        Assert.Equal(string.Empty, where);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildWhere_WithKeyword_SearchesDescriptionOnly()
    {
        var (where, parameters) = PublishStatusSql.BuildWhere(new PublishStatusQuery { Keyword = "草稿" });

        Assert.Equal("WHERE p.Description LIKE @Keyword", where);
        Assert.Equal("%草稿%", parameters["Keyword"]);
    }

    [Theory]
    [InlineData("  草稿  ", "%草稿%")]
    [InlineData("50%", "%50[%]%")]
    [InlineData("a_b", "%a[_]b%")]
    [InlineData("x[y", "%x[[]y%")]
    public void BuildWhere_TrimsAndEscapesKeyword(string keyword, string expected)
    {
        var (_, parameters) = PublishStatusSql.BuildWhere(new PublishStatusQuery { Keyword = keyword });

        Assert.Equal(expected, parameters["Keyword"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void BuildWhere_IgnoresBlankKeyword(string? keyword)
    {
        var (where, parameters) = PublishStatusSql.BuildWhere(new PublishStatusQuery { Keyword = keyword });

        Assert.Equal(string.Empty, where);
        Assert.DoesNotContain("Keyword", parameters.Keys);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BuildWhere_WithIsDraft_AddsClauseForBothTrueAndFalse(bool value)
    {
        var (where, parameters) = PublishStatusSql.BuildWhere(new PublishStatusQuery { IsDraft = value });

        Assert.Equal("WHERE p.IsDraft = @IsDraft", where);
        Assert.Equal(value, parameters["IsDraft"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BuildWhere_WithIsPublished_AddsClauseForBothTrueAndFalse(bool value)
    {
        var (where, parameters) = PublishStatusSql.BuildWhere(new PublishStatusQuery { IsPublished = value });

        Assert.Equal("WHERE p.IsPublished = @IsPublished", where);
        Assert.Equal(value, parameters["IsPublished"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BuildWhere_WithIsDiscontinued_AddsClauseForBothTrueAndFalse(bool value)
    {
        var (where, parameters) = PublishStatusSql.BuildWhere(
            new PublishStatusQuery { IsDiscontinued = value });

        Assert.Equal("WHERE p.IsDiscontinued = @IsDiscontinued", where);
        Assert.Equal(value, parameters["IsDiscontinued"]);
    }

    [Fact]
    public void BuildWhere_WithNullBools_AddsNoBoolClauses()
    {
        var (where, parameters) = PublishStatusSql.BuildWhere(new PublishStatusQuery
        {
            IsDraft = null,
            IsPublished = null,
            IsDiscontinued = null,
        });

        Assert.Equal(string.Empty, where);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildWhere_WithEveryFilter_JoinsClausesWithAnd()
    {
        var (where, parameters) = PublishStatusSql.BuildWhere(new PublishStatusQuery
        {
            Keyword = "草稿",
            IsDraft = true,
            IsPublished = false,
            IsDiscontinued = false,
        });

        Assert.StartsWith("WHERE ", where);
        Assert.Equal(3, where.Split(" AND ").Length - 1);
        Assert.Contains("p.Description LIKE @Keyword", where);
        Assert.Contains("p.IsDraft = @IsDraft", where);
        Assert.Contains("p.IsPublished = @IsPublished", where);
        Assert.Contains("p.IsDiscontinued = @IsDiscontinued", where);
        Assert.Equal(4, parameters.Count);
    }
}
