using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Repositories;

/// <summary>Covers the list/filter WHERE-clause construction used by the query endpoint.</summary>
public class AppRoleSqlTests
{
    [Fact]
    public void SelectBase_ProjectsUserCountFromJunctionTable()
    {
        Assert.Contains("SELECT COUNT(*) FROM AppUserRole ur WHERE ur.RoleId = r.RoleId", AppRoleSql.SelectBase);
        Assert.Contains("r.pkid AS Pkid", AppRoleSql.SelectBase);
    }

    [Fact]
    public void BuildWhere_WithEmptyQuery_ReturnsNoClauseAndNoParameters()
    {
        var (where, parameters) = AppRoleSql.BuildWhere(new AppRoleQuery());

        Assert.Equal(string.Empty, where);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildWhere_WithNullQuery_ReturnsNoClause()
    {
        var (where, parameters) = AppRoleSql.BuildWhere(null);

        Assert.Equal(string.Empty, where);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildWhere_WithKeyword_SearchesRoleIdRoleNameAndDescription()
    {
        var (where, parameters) = AppRoleSql.BuildWhere(new AppRoleQuery { Keyword = "admin" });

        Assert.Contains("r.RoleId LIKE @Keyword", where);
        Assert.Contains("r.RoleName LIKE @Keyword", where);
        Assert.Contains("r.Description LIKE @Keyword", where);
        Assert.Equal("%admin%", parameters["Keyword"]);
    }

    [Theory]
    [InlineData("  admin  ", "%admin%")]
    [InlineData("50%", "%50[%]%")]
    [InlineData("a_b", "%a[_]b%")]
    [InlineData("x[y", "%x[[]y%")]
    public void BuildWhere_TrimsAndEscapesKeyword(string keyword, string expected)
    {
        var (_, parameters) = AppRoleSql.BuildWhere(new AppRoleQuery { Keyword = keyword });

        Assert.Equal(expected, parameters["Keyword"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void BuildWhere_IgnoresBlankKeyword(string? keyword)
    {
        var (where, parameters) = AppRoleSql.BuildWhere(new AppRoleQuery { Keyword = keyword });

        Assert.Equal(string.Empty, where);
        Assert.DoesNotContain("Keyword", parameters.Keys);
    }

    [Fact]
    public void BuildWhere_WithPermissionLevel_AddsExactMatch()
    {
        var (where, parameters) = AppRoleSql.BuildWhere(new AppRoleQuery { PermissionLevel = 1 });

        Assert.Equal("WHERE r.PermissionLevel = @PermissionLevel", where);
        Assert.Equal(1, parameters["PermissionLevel"]);
    }

    [Fact]
    public void BuildWhere_WithBothFilters_JoinsClausesWithAnd()
    {
        var (where, parameters) = AppRoleSql.BuildWhere(new AppRoleQuery
        {
            Keyword = "admin",
            PermissionLevel = 1,
        });

        Assert.StartsWith("WHERE ", where);
        Assert.Contains(" AND ", where);
        Assert.Equal(2, parameters.Count);
    }
}
