namespace CMS.API.Models;

/// <summary>
/// 異動紀錄 RowAudit — one row of the cross-cutting audit trail, on its way to the table.
///
/// A write-only shape, not a response model: nothing serializes it and no endpoint returns it, so
/// it carries the column widths rather than nav objects or child counts. The properties are named
/// for the columns except <see cref="LoggedAt"/>, which maps to the <c>[DateTime]</c> column —
/// a property called <c>DateTime</c> would shadow the type of its own value.
/// </summary>
public sealed class RowAuditEntry
{
    /// <summary>varchar(50) — the business table the change was made to, e.g. "Course".</summary>
    public const int TableNameLength = 50;

    /// <summary>nvarchar(100) — the signed-in operator's 使用者名稱.</summary>
    public const int UserNameLength = 100;

    /// <summary>nvarchar(100) — the changed row's pkid, as text.</summary>
    public const int PrimaryKeyValuesLength = 100;

    /// <summary>varchar(20) — Insert / Update / Delete.</summary>
    public const int ActionTypeLength = 20;

    /// <summary>varchar(1000) — the only nullable column.</summary>
    public const int ActionDescLength = 1000;

    /// <summary>資料表名稱</summary>
    public string TableName { get; init; } = string.Empty;

    /// <summary>使用者名稱 — <see cref="RowAuditWriterDefaults.SystemUserName"/> when unauthenticated.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>主鍵值 — the row's pkid.</summary>
    public string PrimaryKeyValues { get; init; } = string.Empty;

    /// <summary>異動類型</summary>
    public string ActionType { get; init; } = string.Empty;

    /// <summary>異動說明 — the row's first string value on insert/delete, the changed column names on update.</summary>
    public string? ActionDesc { get; init; }

    /// <summary>異動時間 — maps to the [DateTime] column.</summary>
    public DateTime LoggedAt { get; init; }
}

/// <summary>Constants shared by the writer and the tests that pin its behaviour.</summary>
public static class RowAuditWriterDefaults
{
    /// <summary>Written to UserName when the request carries no authenticated user.</summary>
    public const string SystemUserName = "system";

    /// <summary>ActionType values. varchar(20), so all three fit with room to spare.</summary>
    public const string Insert = "Insert";

    public const string Update = "Update";

    public const string Delete = "Delete";
}
