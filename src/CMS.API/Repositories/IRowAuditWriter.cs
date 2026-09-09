using System.Data;

namespace CMS.API.Repositories;

/// <summary>
/// 異動紀錄 — writes one RowAudit row describing a change to any business table.
///
/// Repositories call it after the write they are auditing has succeeded, so a failed write leaves
/// no trail claiming otherwise. Each method takes the transaction the change is running in: the
/// audit row is then part of that transaction, and a rollback takes it with it.
/// </summary>
public interface IRowAuditWriter
{
    /// <summary>
    /// Records an insert. ActionDesc is the entity's first string property — the Name / Title /
    /// Code that identifies the new row to a human.
    /// </summary>
    Task LogInsertAsync<T>(
        string tableName,
        T entity,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where T : notnull;

    /// <summary>
    /// Records an update. ActionDesc is the comma-separated names of the properties that differ
    /// between <paramref name="before"/> and <paramref name="after"/>. A save that changed nothing
    /// writes no row.
    /// </summary>
    Task LogUpdateAsync<T>(
        string tableName,
        T before,
        T after,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where T : notnull;

    /// <summary>
    /// Records a delete. ActionDesc is the deleted entity's first string property — the row is
    /// gone, so the trail is the only thing that still says what it was.
    /// </summary>
    Task LogDeleteAsync<T>(
        string tableName,
        T entity,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where T : notnull;
}
