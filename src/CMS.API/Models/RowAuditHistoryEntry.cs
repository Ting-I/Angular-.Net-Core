using System.Text.Json.Serialization;

namespace CMS.API.Models;

/// <summary>
/// 異動紀錄 — one row of a record's audit trail on its way out to the client.
///
/// A read model, and deliberately not <see cref="RowAuditEntry"/> reversed: the write model
/// carries the column widths and the key columns the writer fills in, while a reader has already
/// said which table and which key it is asking about, so repeating them in every row would be
/// noise. The four properties here are the four the badge and its dialog render.
/// </summary>
public sealed class RowAuditHistoryEntry
{
    /// <summary>
    /// 異動時間 — the <c>[DateTime]</c> column, aliased to <c>LoggedAt</c> on the way in for the
    /// same reason <see cref="RowAuditEntry.LoggedAt"/> is: a property called <c>DateTime</c> would
    /// shadow the type of its own value. The wire name stays <c>dateTime</c>, because that is the
    /// column the client is showing and the API is the only place the two names have to meet.
    /// </summary>
    [JsonPropertyName("dateTime")]
    public DateTime LoggedAt { get; init; }

    /// <summary>使用者名稱 — who made the change, as it stood when the row was written.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>異動類型 — Insert / Update / Delete.</summary>
    public string ActionType { get; init; } = string.Empty;

    /// <summary>
    /// 異動說明 — the record's first string value on an insert or delete, the changed-column list
    /// on an update. The one nullable column, so it stays nullable here.
    /// </summary>
    public string? ActionDesc { get; init; }
}
