using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>
/// SQL for <see cref="RowAuditEntry"/>. One statement: the trail is written, never read back by
/// the API.
/// </summary>
public static class RowAuditSql
{
    /// <summary>
    /// pkid is IDENTITY, so it is not in the column list. [DateTime] is bracketed — it is a type
    /// name as well as a column name — and takes @LoggedAt, since the model cannot name a property
    /// after the type of its own value.
    /// </summary>
    public const string Insert = """
        INSERT INTO RowAudit (TableName, UserName, PrimaryKeyValues, ActionType, ActionDesc, [DateTime])
        VALUES (@TableName, @UserName, @PrimaryKeyValues, @ActionType, @ActionDesc, @LoggedAt);
        """;
}
