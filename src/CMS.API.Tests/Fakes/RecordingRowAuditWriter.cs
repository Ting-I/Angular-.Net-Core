using System.Data;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Http;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// The real <see cref="RowAuditWriter"/> with its one INSERT captured instead of executed, so the
/// reflection and the claim reading are tested through the public Log* methods rather than around
/// them. Hand-written, like every other double here — the fakes are the pattern.
///
/// It hands the base class a <see cref="ThrowingDbConnectionFactory"/>: if a change ever moves the
/// write out from behind the override, the test fails loudly instead of opening a connection to
/// the developer's database.
/// </summary>
public class RecordingRowAuditWriter : RowAuditWriter
{
    public RecordingRowAuditWriter(IHttpContextAccessor httpContextAccessor, TimeProvider? timeProvider = null)
        : base(new ThrowingDbConnectionFactory(), httpContextAccessor, timeProvider)
    {
    }

    /// <summary>Every row the writer decided to write, in order.</summary>
    public List<RowAuditEntry> Entries { get; } = [];

    /// <summary>The transaction each of those rows was handed, in the same order.</summary>
    public List<IDbTransaction?> Transactions { get; } = [];

    /// <summary>The only row written; fails the test when the count is anything but one.</summary>
    public RowAuditEntry Single() => Assert.Single(Entries);

    protected override Task WriteAsync(
        RowAuditEntry entry,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        Entries.Add(entry);
        Transactions.Add(transaction);
        return Task.CompletedTask;
    }
}
