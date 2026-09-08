using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>
/// SQL fragments for <see cref="AppUser"/>. The WHERE builder is kept separate from the
/// repository so the filter logic can be unit tested without a database.
/// </summary>
public static class AppUserSql
{
    /// <summary>
    /// Base projection. RoleCount is a correlated subquery over the n-n table.
    /// PasswordHash is deliberately absent — this projection is the single place that keeps the
    /// hash off the wire, so it must stay absent from GetAll, Query and GetById alike.
    /// </summary>
    public const string SelectBase = """
        SELECT u.pkid AS Pkid,
               u.UserId,
               u.UserName,
               u.IsActive,
               u.PasswordUpdatedTime,
               (SELECT COUNT(*) FROM AppUserRole ur WHERE ur.UserId = u.UserId) AS RoleCount
        FROM AppUser u
        """;

    public const string DefaultOrderBy = "ORDER BY u.UserId ASC";

    /// <summary>角色 — read on GetById only, on the same connection as the record.</summary>
    public const string SelectRoleIds = """
        SELECT ur.RoleId
        FROM AppUserRole ur
        WHERE ur.UserId = @UserId
        ORDER BY ur.RoleId ASC
        """;

    /// <summary>
    /// Builds the WHERE clause (empty string when the query is unfiltered) plus the
    /// matching Dapper parameter set.
    /// </summary>
    public static (string Where, Dictionary<string, object?> Parameters) BuildWhere(AppUserQuery? query)
    {
        var clauses = new List<string>();
        var parameters = new Dictionary<string, object?>();

        var keyword = query?.Keyword?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            // UserId and UserName are the only searchable string columns; PasswordHash is not
            // projected at all, so it is not searchable either.
            clauses.Add("(u.UserId LIKE @Keyword OR u.UserName LIKE @Keyword)");
            parameters["Keyword"] = $"%{EscapeLike(keyword)}%";
        }

        // Tri-state: false (停用) is a filter, not an absence of one.
        if (query?.IsActive is bool isActive)
        {
            clauses.Add("u.IsActive = @IsActive");
            parameters["IsActive"] = isActive;
        }

        var roleId = query?.RoleId?.Trim();
        if (!string.IsNullOrEmpty(roleId))
        {
            // EXISTS rather than a JOIN so a user cannot appear twice.
            clauses.Add(
                "EXISTS (SELECT 1 FROM AppUserRole ur WHERE ur.UserId = u.UserId AND ur.RoleId = @RoleId)");
            parameters["RoleId"] = roleId;
        }

        // Each bound is emitted independently so an open-ended range works.
        if (query?.PasswordUpdatedFrom is DateOnly passwordUpdatedFrom)
        {
            clauses.Add("u.PasswordUpdatedTime >= @PasswordUpdatedFrom");
            parameters["PasswordUpdatedFrom"] = passwordUpdatedFrom;
        }

        if (query?.PasswordUpdatedTo is DateOnly passwordUpdatedTo)
        {
            // The column is datetime while the bound is a date: <= would drop everything after
            // midnight on the closing day.
            clauses.Add("u.PasswordUpdatedTime < DATEADD(day, 1, @PasswordUpdatedTo)");
            parameters["PasswordUpdatedTo"] = passwordUpdatedTo;
        }

        var where = clauses.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", clauses);
        return (where, parameters);
    }

    /// <summary>Escapes LIKE wildcards so a keyword such as "50%" matches literally.</summary>
    private static string EscapeLike(string value) =>
        value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
}
