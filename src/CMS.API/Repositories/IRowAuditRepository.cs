using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>
/// 異動紀錄 — reads one record's audit trail back.
///
/// Separate from <see cref="IRowAuditWriter"/> on purpose. The writer is called by repositories in
/// the middle of somebody else's transaction and is write-only by design; this is an ordinary
/// read-only repository a controller injects. Keeping them apart means no repository can acquire
/// a way to read the trail while it writes, and the reader can never be handed a transaction.
/// </summary>
public interface IRowAuditRepository
{
    /// <summary>
    /// Every RowAudit row for one record — matched on the business table name and the record's
    /// pkid — newest first.
    /// </summary>
    Task<IEnumerable<RowAuditHistoryEntry>> GetForRecordAsync(
        string tableName,
        int pkid,
        CancellationToken cancellationToken = default);
}
