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
/// **Who did it comes from the token; what they are called comes from the row.** A repository is
/// in no position to say who is signed in, and a parameter for it would be a parameter someone can
/// get wrong, so the operator's *identity* is the userId claim on the validated principal, read
/// through <see cref="IHttpContextAccessor"/>. Their 使用者名稱 is then read from AppUser, because
/// the userName claim is only ever as fresh as the login that issued it: PUT /api/auth/profile
/// renames an account without re-issuing a token, an administrator renaming somebody else could
/// not re-issue theirs at all, and <see cref="TokenFreshness"/> revokes on a password change and
/// nothing else. Trusting the claim files up to 24 hours of audit rows under a name the operator
/// no longer has.
///
/// **That read happens on the caller's transaction, or not at all.** The rename is audited by the
/// very transaction that performed it, so the name has to be read from inside it — on any other
/// connection the new value is invisible, and the read would block on the lock the rename holds
/// until the caller's transaction commits, which it cannot do until this returns. A caller with no
/// transaction to lend is outside the audited-write path entirely and falls back to the claim.
///
/// **It never reads <c>User.Identity.Name</c>.** ConfigureJwtBearerOptions points NameClaimType at
/// the *userId* claim, so Identity.Name here is the login id — writing it into a column called
/// UserName would look right and be wrong.
///
/// Every value is cut to its column width before it leaves: an audit row is a side effect of the
/// caller's write, and it must not be the thing that fails it — SQL error 8152 over a long Title
/// would otherwise take the caller's transaction down with it.
///
/// **The audit row rides the caller's transaction too.** Every Log* method takes the
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

    public async Task LogInsertAsync<T>(
        string tableName,
        T entity,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where T : notnull
        => await WriteAsync(
            BuildEntry(
                tableName,
                RowAuditWriterDefaults.Insert,
                AuditHelper.PrimaryKeyValue(entity),
                AuditHelper.FirstStringValue(entity),
                await ResolveUserNameAsync(transaction, cancellationToken)),
            transaction,
            cancellationToken);

    public async Task LogDeleteAsync<T>(
        string tableName,
        T entity,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where T : notnull
        => await WriteAsync(
            BuildEntry(
                tableName,
                RowAuditWriterDefaults.Delete,
                AuditHelper.PrimaryKeyValue(entity),
                AuditHelper.FirstStringValue(entity),
                await ResolveUserNameAsync(transaction, cancellationToken)),
            transaction,
            cancellationToken);

    /// <summary>
    /// A save that changed nothing writes no row. An empty ActionDesc would be a row saying only
    /// that somebody pressed 儲存, and the trail is read to answer "what changed" — the entries
    /// that answer nothing are the ones that make it unreadable. Returning before the name is
    /// resolved keeps that case free of a query as well as free of a row.
    /// </summary>
    public async Task LogUpdateAsync<T>(
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
            return;
        }

        await WriteAsync(
            BuildEntry(
                tableName,
                RowAuditWriterDefaults.Update,
                AuditHelper.PrimaryKeyValue(after),
                changed,
                await ResolveUserNameAsync(transaction, cancellationToken)),
            transaction,
            cancellationToken);
    }

    /// <summary>
    /// Composes the row, cutting every value to its column width. Exposed so a test can assert the
    /// shape of what would be written without a database.
    ///
    /// <paramref name="userName"/> is whatever <see cref="ResolveUserNameAsync"/> settled on;
    /// passing null falls back to the token's claim, which is all a caller composing a row outside
    /// the audited-write path has.
    /// </summary>
    public RowAuditEntry BuildEntry(
        string tableName,
        string actionType,
        string primaryKeyValues,
        string? actionDesc,
        string? userName = null)
        => new()
        {
            TableName = AuditHelper.Truncate(tableName, RowAuditEntry.TableNameLength) ?? string.Empty,
            UserName = AuditHelper.Truncate(userName ?? ClaimedUserName(), RowAuditEntry.UserNameLength)!,
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
    /// The operator's 使用者名稱 as AppUser holds it right now — read on the caller's transaction,
    /// keyed on the token's userId.
    ///
    /// The query is <see cref="AppUserSql.SelectUserName"/> run here rather than
    /// <c>IAppUserRepository.GetByIdAsync</c> for three reasons: AppUserRepository takes an
    /// <see cref="IRowAuditWriter"/>, so injecting it back would be a DI cycle; it opens a
    /// connection of its own, which cannot see an uncommitted rename and would block on the lock
    /// that rename holds; and it runs SelectBase plus a second query for RoleIds to fetch one
    /// string. The INSERT below is executed directly for the same reason.
    ///
    /// Falls back to the userName claim, then to
    /// <see cref="RowAuditWriterDefaults.SystemUserName"/>: there is no transaction to read on, or
    /// the account is gone — an operator deleting their own row still leaves a trail entry, and
    /// the column is NOT NULL.
    /// </summary>
    private async Task<string> ResolveUserNameAsync(
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return RowAuditWriterDefaults.SystemUserName;
        }

        var userId = user.FindFirstValue(JwtTokenService.UserIdClaimType);
        if (!string.IsNullOrWhiteSpace(userId) && transaction?.Connection is IDbConnection enlisted)
        {
            var current = await enlisted.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
                AppUserSql.SelectUserName,
                new { UserId = userId },
                transaction,
                cancellationToken: cancellationToken));

            if (!string.IsNullOrWhiteSpace(current))
            {
                return current;
            }
        }

        return ClaimedUserName();
    }

    /// <summary>
    /// The 使用者名稱 the token was issued with, or "system" when the request carries no
    /// authenticated user. A principal that is authenticated but carries no userName claim is
    /// treated the same way: the column is NOT NULL and a blank name is worse than an honest one.
    /// </summary>
    private string ClaimedUserName()
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
