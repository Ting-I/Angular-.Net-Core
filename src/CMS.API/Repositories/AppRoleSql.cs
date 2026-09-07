using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>
/// SQL fragments for <see cref="AppRole"/>. The WHERE builder is kept separate from the
/// repository so the filter logic can be unit tested without a database.
/// </summary>
public static class AppRoleSql
{
    /// <summary>Base projection. UserCount is a correlated subquery over the n-n table.</summary>
    public const string SelectBase = """
        SELECT r.pkid AS Pkid,
               r.RoleId,
               r.RoleName,
               r.PermissionLevel,
               r.Description,
               (SELECT COUNT(*) FROM AppUserRole ur WHERE ur.RoleId = r.RoleId) AS UserCount
        FROM AppRole r
        """;

    public const string DefaultOrderBy = "ORDER BY r.RoleId ASC";

    /// <summary>
    /// Builds the WHERE clause (empty string when the query is unfiltered) plus the
    /// matching Dapper parameter set.
    /// </summary>
    public static (string Where, Dictionary<string, object?> Parameters) BuildWhere(AppRoleQuery? query)
    {
        var clauses = new List<string>();
        var parameters = new Dictionary<string, object?>();

        var keyword = query?.Keyword?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            clauses.Add("(r.RoleId LIKE @Keyword OR r.RoleName LIKE @Keyword OR r.Description LIKE @Keyword)");
            parameters["Keyword"] = $"%{EscapeLike(keyword)}%";
        }

        if (query?.PermissionLevel is int permissionLevel)
        {
            clauses.Add("r.PermissionLevel = @PermissionLevel");
            parameters["PermissionLevel"] = permissionLevel;
        }

        var where = clauses.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", clauses);
        return (where, parameters);
    }

    /// <summary>Escapes LIKE wildcards so a keyword such as "50%" matches literally.</summary>
    private static string EscapeLike(string value) =>
        value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
}
