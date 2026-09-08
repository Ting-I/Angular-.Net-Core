using System.Data;
using System.Security.Claims;
using CMS.API.Data;
using CMS.API.Models;
using CMS.API.Security;
using Dapper;

namespace CMS.API.Repositories;

/// <summary>
/// The one place a RowAudit row is written. Generic over the entity type: every table's audit row
/// is built by reflection in <see cref="AuditHelper"/> rather than by a per-entity mapper, so
/// adding an entity adds no audit code.
///
/// **The operator's name comes from the request's token, not from the caller.** A repository is in
/// no position to say who is signed in, and a parameter for it would be a parameter someone can
/// get wrong. <see cref="IHttpContextAccessor"/> reads it from the validated principal instead,
/// and falls back to <see cref="RowAuditWriterDefaults.SystemUserName"/> when there is nobody —
/// a startup task or a test has no HttpContext, and the column is NOT NULL.
///
/// **It reads the userName claim, never <c>User.Identity.Name</c>.** ConfigureJwtBearerOptions
/// points NameClaimType at the *userId* claim, so Identity.Name here is the login id — writing it
/// into a column called UserName would look right and be wrong.
///
/// Every value is cut to its column width before it leaves: an audit row is a side effect of the
/// caller's write, and it must not be the thing that fails it — SQL error 8152 over a long Title
/// would otherwise take the caller's transaction down with it.
///
/// **The audit row rides the caller's transaction.** Every Log* method takes the
/// <see cref="IDbTransaction"/> the change is running in and inserts on its connection, inside it,
/// so a rolled-back change leaves no row claiming it happened. A null transaction is still
/// accepted and opens a connection of its own — that is the path for a caller with no transaction
/// to lend, and it is nobody's default.
/// </summary>
public class RowAuditWriter : IRowAuditWriter
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TimeProvider _timeProvider;

    public RowAuditWriter(
        IDbConnectionFactory connectionFactory,
        IHttpContextAccessor httpContextAccessor,
        TimeProvider? timeProvider = null)
    {
        _connectionFactory = connectionFactory;
        _httpContextAccessor = httpContextAccessor;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task LogInsertAsync<T>(
        string tableName,
        T entity,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where T : notnull
        => WriteAsync(
            BuildEntry(
                tableName,
                RowAuditWriterDefaults.Insert,
                AuditHelper.PrimaryKeyValue(entity),
                AuditHelper.FirstStringValue(entity)),
            transaction,
            cancellationToken);

    public Task LogDeleteAsync<T>(
        string tableName,
        T entity,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where T : notnull
        => WriteAsync(
            BuildEntry(
                tableName,
                RowAuditWriterDefaults.Delete,
                AuditHelper.PrimaryKeyValue(entity),
                AuditHelper.FirstStringValue(entity)),
            transaction,
            cancellationToken);

    /// <summary>
    /// A save that changed nothing writes no row. An empty ActionDesc would be a row saying only
    /// that somebody pressed 儲存, and the trail is read to answer "what changed" — the entries
    /// that answer nothing are the ones that make it unreadable.
    /// </summary>
    public Task LogUpdateAsync<T>(
        string tableName,
        T before,
        T after,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where T : notnull
    {
        var changed = AuditHelper.ChangedColumnList(before, after);
        if (changed.Length == 0)
        {
            return Task.CompletedTask;
        }

        return WriteAsync(
            BuildEntry(
                tableName,
                RowAuditWriterDefaults.Update,
                AuditHelper.PrimaryKeyValue(after),
                changed),
            transaction,
            cancellationToken);
    }

    /// <summary>
    /// Composes the row, cutting every value to its column width. Exposed so a test can assert the
    /// shape of what would be written without a database.
    /// </summary>
    public RowAuditEntry BuildEntry(string tableName, string actionType, string primaryKeyValues, string? actionDesc)
        => new()
        {
            TableName = AuditHelper.Truncate(tableName, RowAuditEntry.TableNameLength) ?? string.Empty,
            UserName = AuditHelper.Truncate(CurrentUserName(), RowAuditEntry.UserNameLength)!,
            PrimaryKeyValues = AuditHelper.Truncate(primaryKeyValues, RowAuditEntry.PrimaryKeyValuesLength) ?? string.Empty,
            ActionType = AuditHelper.Truncate(actionType, RowAuditEntry.ActionTypeLength) ?? string.Empty,
            ActionDesc = AuditHelper.Truncate(actionDesc, RowAuditEntry.ActionDescLength),
            LoggedAt = _timeProvider.GetLocalNow().DateTime,
        };

    /// <summary>
    /// The single INSERT, on the caller's transaction when there is one. <c>virtual</c> so a test
    /// can capture the composed row instead of reaching SQL Server — the reflection above it is
    /// what those tests are about, and the house rule is that they never open a connection.
    /// </summary>
    protected virtual async Task WriteAsync(
        RowAuditEntry entry,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction?.Connection is IDbConnection enlisted)
        {
            await enlisted.ExecuteAsync(new CommandDefinition(
                RowAuditSql.Insert,
                entry,
                transaction,
                cancellationToken: cancellationToken));
            return;
        }

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            RowAuditSql.Insert,
            entry,
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// The signed-in operator's 使用者名稱, or "system" when the request carries no authenticated
    /// user. A principal that is authenticated but carries no userName claim is treated the same
    /// way: the column is NOT NULL and a blank name is worse than an honest one.
    /// </summary>
    private string CurrentUserName()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return RowAuditWriterDefaults.SystemUserName;
        }

        var userName = user.FindFirstValue(JwtTokenService.UserNameClaimType);
        return string.IsNullOrWhiteSpace(userName)
            ? RowAuditWriterDefaults.SystemUserName
            : userName;
    }
}
