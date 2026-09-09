using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>
/// SQL for <see cref="RowAuditEntry"/> and <see cref="RowAuditHistoryEntry"/>: one statement that
/// writes the trail, and one that reads a single record's back. Nothing updates or deletes it —
/// an audit row that can be edited is not an audit row.
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

    /// <summary>
    /// One record's trail, newest first. Both halves of the filter are needed: PrimaryKeyValues is
    /// only unique within a TableName, so pkid 7 alone would mix 原廠 7 with 課程 7.
    ///
    /// The tie-break on pkid DESC is not decoration. A change that writes more than one audit row
    /// — <see cref="FeaturedPromoItemRepository.MoveToSlotAsync"/> writes one per row the swap
    /// moved — stamps them from the same clock reading, so ordering on [DateTime] alone leaves
    /// their order up to the server. IDENTITY is the only thing here that still increases inside
    /// one transaction.
    /// </summary>
    public const string SelectForRecord = """
        SELECT a.[DateTime] AS LoggedAt,
               a.UserName,
               a.ActionType,
               a.ActionDesc
        FROM RowAudit a
        WHERE a.TableName = @TableName
          AND a.PrimaryKeyValues = @PrimaryKeyValues
        ORDER BY a.[DateTime] DESC, a.pkid DESC
        """;
}
