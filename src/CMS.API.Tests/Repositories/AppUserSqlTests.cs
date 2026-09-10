using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Repositories;

/// <summary>Covers the list/filter WHERE-clause construction used by the query endpoint.</summary>
public class AppUserSqlTests
{
    [Fact]
    public void SelectBase_ProjectsRoleCountFromJunctionTable()
    {
        Assert.Contains("SELECT COUNT(*) FROM AppUserRole ur WHERE ur.UserId = u.UserId", AppUserSql.SelectBase);
        Assert.Contains("u.pkid AS Pkid", AppUserSql.SelectBase);
        Assert.Contains("u.PasswordUpdatedTime", AppUserSql.SelectBase);
    }

    /// <summary>
    /// The projection is the single place that keeps the hash off the wire. If it ever appears
    /// here it appears in GetAll, Query and GetById at once.
    /// </summary>
    [Fact]
    public void SelectBase_NeverProjectsPasswordHash()
    {
        Assert.DoesNotContain("PasswordHash", AppUserSql.SelectBase);
    }

    [Fact]
    public void DefaultOrderBy_SortsByUserId()
    {
        Assert.Equal("ORDER BY u.UserId ASC", AppUserSql.DefaultOrderBy);
    }

    [Fact]
    public void SelectRoleIds_ReadsTheJunctionForOneUser()
    {
        Assert.Contains("FROM AppUserRole ur", AppUserSql.SelectRoleIds);
        Assert.Contains("WHERE ur.UserId = @UserId", AppUserSql.SelectRoleIds);
    }

    [Fact]
    public void BuildWhere_WithEmptyQuery_ReturnsNoClauseAndNoParameters()
    {
        var (where, parameters) = AppUserSql.BuildWhere(new AppUserQuery());

        Assert.Equal(string.Empty, where);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildWhere_WithNullQuery_ReturnsNoClause()
    {
        var (where, parameters) = AppUserSql.BuildWhere(null);

        Assert.Equal(string.Empty, where);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildWhere_WithKeyword_SearchesUserIdAndUserName()
    {
        var (where, parameters) = AppUserSql.BuildWhere(new AppUserQuery { Keyword = "helen" });

        Assert.Contains("u.UserId LIKE @Keyword", where);
        Assert.Contains("u.UserName LIKE @Keyword", where);
        Assert.Equal("%helen%", parameters["Keyword"]);
    }

    [Fact]
    public void BuildWhere_NeverFiltersOnPasswordHash()
    {
        var (where, _) = AppUserSql.BuildWhere(new AppUserQuery { Keyword = "helen" });

        Assert.DoesNotContain("PasswordHash", where);
    }

    [Theory]
    [InlineData("  helen  ", "%helen%")]
    [InlineData("50%", "%50[%]%")]
    [InlineData("a_b", "%a[_]b%")]
    [InlineData("x[y", "%x[[]y%")]
    public void BuildWhere_TrimsAndEscapesKeyword(string keyword, string expected)
    {
        var (_, parameters) = AppUserSql.BuildWhere(new AppUserQuery { Keyword = keyword });

        Assert.Equal(expected, parameters["Keyword"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void BuildWhere_IgnoresBlankKeyword(string? keyword)
    {
        var (where, parameters) = AppUserSql.BuildWhere(new AppUserQuery { Keyword = keyword });

        Assert.Equal(string.Empty, where);
        Assert.DoesNotContain("Keyword", parameters.Keys);
    }

    [Fact]
    public void BuildWhere_WithIsActiveTrue_AddsExactMatch()
    {
        var (where, parameters) = AppUserSql.BuildWhere(new AppUserQuery { IsActive = true });

        Assert.Equal("WHERE u.IsActive = @IsActive", where);
        Assert.Equal(true, parameters["IsActive"]);
    }

    /// <summary>A falsy check here would silently drop the 停用 filter.</summary>
    [Fact]
    public void BuildWhere_WithIsActiveFalse_StillAddsTheClause()
    {
        var (where, parameters) = AppUserSql.BuildWhere(new AppUserQuery { IsActive = false });

        Assert.Equal("WHERE u.IsActive = @IsActive", where);
        Assert.Equal(false, parameters["IsActive"]);
    }

    [Fact]
    public void BuildWhere_WithoutIsActive_AddsNoClause()
    {
        var (where, parameters) = AppUserSql.BuildWhere(new AppUserQuery { IsActive = null });

        Assert.Equal(string.Empty, where);
        Assert.DoesNotContain("IsActive", parameters.Keys);
    }

    /// <summary>EXISTS rather than a JOIN, so a user cannot come back twice.</summary>
    [Fact]
    public void BuildWhere_WithRoleId_UsesExistsOnTheJunction()
    {
        var (where, parameters) = AppUserSql.BuildWhere(new AppUserQuery { RoleId = "Admin" });

        Assert.Contains("EXISTS (SELECT 1 FROM AppUserRole ur", where);
        Assert.Contains("ur.UserId = u.UserId AND ur.RoleId = @RoleId", where);
        Assert.DoesNotContain("JOIN", where);
        Assert.Equal("Admin", parameters["RoleId"]);
    }

    [Fact]
    public void BuildWhere_TrimsRoleId()
    {
        var (_, parameters) = AppUserSql.BuildWhere(new AppUserQuery { RoleId = "  Admin  " });

        Assert.Equal("Admin", parameters["RoleId"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void BuildWhere_IgnoresBlankRoleId(string? roleId)
    {
        var (where, parameters) = AppUserSql.BuildWhere(new AppUserQuery { RoleId = roleId });

        Assert.Equal(string.Empty, where);
        Assert.DoesNotContain("RoleId", parameters.Keys);
    }

    [Fact]
    public void BuildWhere_WithPasswordUpdatedFrom_AddsInclusiveLowerBound()
    {
        var from = new DateOnly(2026, 1, 1);
        var (where, parameters) = AppUserSql.BuildWhere(new AppUserQuery { PasswordUpdatedFrom = from });

        Assert.Equal("WHERE u.PasswordUpdatedTime >= @PasswordUpdatedFrom", where);
        Assert.Equal(from, parameters["PasswordUpdatedFrom"]);
    }

    /// <summary>
    /// The column is datetime and the bound is a date: &lt;= would drop everything after midnight
    /// on the closing day, so the upper bound is an exclusive DATEADD instead.
    /// </summary>
    [Fact]
    public void BuildWhere_WithPasswordUpdatedTo_AddsExclusiveNextDayBound()
    {
        var to = new DateOnly(2026, 1, 31);
        var (where, parameters) = AppUserSql.BuildWhere(new AppUserQuery { PasswordUpdatedTo = to });

        Assert.Equal("WHERE u.PasswordUpdatedTime < DATEADD(day, 1, @PasswordUpdatedTo)", where);
        Assert.DoesNotContain("<=", where);
        Assert.Equal(to, parameters["PasswordUpdatedTo"]);
    }

    [Fact]
    public void BuildWhere_WithOnlyOneDateBound_LeavesTheRangeOpenEnded()
    {
        var (whereFrom, _) = AppUserSql.BuildWhere(
            new AppUserQuery { PasswordUpdatedFrom = new DateOnly(2026, 1, 1) });
        var (whereTo, _) = AppUserSql.BuildWhere(
            new AppUserQuery { PasswordUpdatedTo = new DateOnly(2026, 1, 31) });

        Assert.DoesNotContain("PasswordUpdatedTo", whereFrom);
        Assert.DoesNotContain("PasswordUpdatedFrom", whereTo);
    }

    [Fact]
    public void BuildWhere_WithAllFilters_JoinsClausesWithAnd()
    {
        var (where, parameters) = AppUserSql.BuildWhere(new AppUserQuery
        {
            Keyword = "helen",
            IsActive = false,
            RoleId = "Admin",
            PasswordUpdatedFrom = new DateOnly(2026, 1, 1),
            PasswordUpdatedTo = new DateOnly(2026, 1, 31),
        });

        Assert.StartsWith("WHERE ", where);
        Assert.Contains(" AND ", where);
        Assert.Equal(5, parameters.Count);
    }
}
