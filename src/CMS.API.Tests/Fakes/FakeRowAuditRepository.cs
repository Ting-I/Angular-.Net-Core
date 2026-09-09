using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IRowAuditRepository"/>. It mirrors what the real query gets from the
/// database rather than returning whatever it was seeded with: the filter on both TableName and
/// pkid, and the newest-first ordering with IDENTITY breaking a tie. Seeding rows out of order and
/// across two tables is then a real test of the endpoint's contract instead of a test that a list
/// survives a round trip.
/// </summary>
public class FakeRowAuditRepository : IRowAuditRepository
{
    private readonly List<Seeded> _rows = [];
    private int _nextRowPkid = 1;

    /// <summary>Every (tableName, pkid) pair the repository was asked for, in order.</summary>
    public List<(string TableName, int Pkid)> Requests { get; } = [];

    /// <summary>
    /// One audit row. <paramref name="loggedAt"/> is deliberately allowed to repeat: two rows
    /// written by the same change share a clock reading, which is the case the tie-break exists
    /// for.
    /// </summary>
    public FakeRowAuditRepository Seed(
        string tableName,
        int pkid,
        DateTime loggedAt,
        string actionType = "Update",
        string userName = "系統管理員",
        string? actionDesc = null)
    {
        _rows.Add(new Seeded(tableName, pkid, _nextRowPkid++, new RowAuditHistoryEntry
        {
            LoggedAt = loggedAt,
            UserName = userName,
            ActionType = actionType,
            ActionDesc = actionDesc,
        }));

        return this;
    }

    public Task<IEnumerable<RowAuditHistoryEntry>> GetForRecordAsync(
        string tableName,
        int pkid,
        CancellationToken cancellationToken = default)
    {
        Requests.Add((tableName, pkid));

        return Task.FromResult(_rows
            .Where(row => row.TableName == tableName && row.Pkid == pkid)
            .OrderByDescending(row => row.Entry.LoggedAt)
            .ThenByDescending(row => row.RowPkid)
            .Select(row => row.Entry)
            .AsEnumerable());
    }

    private sealed record Seeded(string TableName, int Pkid, int RowPkid, RowAuditHistoryEntry Entry);
}
