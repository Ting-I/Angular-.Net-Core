using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// An in-memory ADO.NET connection, scripted per statement, so a Dapper repository can be tested
/// without SQL Server — the same rule every other test here follows, reached the only way a
/// repository can be: by handing it a provider instead of a database.
///
/// It derives from <see cref="DbConnection"/> rather than implementing <see cref="IDbConnection"/>
/// because Dapper's async methods require one; an <c>IDbConnection</c> that is not a
/// <c>DbConnection</c> fails at the first <c>QueryAsync</c>.
///
/// Scripts are matched by substring against the statement text and consumed in registration order,
/// so the two reads either side of an UPDATE are set up as two entries with the same match and
/// answer in turn. An unscripted statement throws rather than returning an empty result — a test
/// that has drifted from the repository should say so, not quietly pass.
/// </summary>
public sealed class FakeDbConnection : DbConnection
{
    private readonly List<Script> _scripts = [];

    /// <summary>Every statement the repository ran, in order, with the values it bound.</summary>
    public List<ExecutedCommand> Executed { get; } = [];

    /// <summary>Every transaction opened on this connection.</summary>
    public List<FakeDbTransaction> Transactions { get; } = [];

    /// <summary>The statements matching <paramref name="match"/>, in the order they ran.</summary>
    public IReadOnlyList<ExecutedCommand> ExecutedMatching(string match) =>
        Executed.Where(command => command.Sql.Contains(match, StringComparison.Ordinal)).ToList();

    /// <summary>The one statement matching <paramref name="match"/>; fails unless there is exactly one.</summary>
    public ExecutedCommand SingleExecuted(string match) => Assert.Single(ExecutedMatching(match));

    /// <summary>Scripts a query, one row per object; property names become column names.</summary>
    public FakeDbConnection Returns(string match, params object[] rows)
    {
        _scripts.Add(Script.ForRows(match, rows.Select(ToColumns).ToList()));
        return this;
    }

    /// <summary>Scripts a query that matches nothing — a missing key.</summary>
    public FakeDbConnection ReturnsNoRows(string match)
    {
        _scripts.Add(Script.ForRows(match, []));
        return this;
    }

    /// <summary>Scripts a SELECT / INSERT ... SELECT SCOPE_IDENTITY() single value.</summary>
    public FakeDbConnection ReturnsScalar(string match, object? value)
    {
        _scripts.Add(Script.ForScalar(match, value));
        return this;
    }

    /// <summary>Scripts an INSERT / UPDATE / DELETE row count.</summary>
    public FakeDbConnection ReturnsAffected(string match, int affected)
    {
        _scripts.Add(Script.ForAffected(match, affected));
        return this;
    }

    /// <summary>Scripts a statement that fails, the way a constraint violation would.</summary>
    public FakeDbConnection Throws(string match, Exception error)
    {
        _scripts.Add(Script.ForError(match, error));
        return this;
    }

    internal object? Execute(FakeDbCommand command, ScriptKind expected)
    {
        Executed.Add(ExecutedCommand.From(command));

        var script = Take(command.CommandText);
        if (script.Error is not null)
        {
            throw script.Error;
        }

        if (script.Kind != expected)
        {
            throw new InvalidOperationException(
                $"The script matching '{script.Match}' was set up as {script.Kind} but the statement ran as {expected}.");
        }

        return script.Value;
    }

    private Script Take(string sql)
    {
        var index = _scripts.FindIndex(script => sql.Contains(script.Match, StringComparison.Ordinal));
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"No script matches this statement. Add one, or fix the repository.\n---\n{sql}\n---");
        }

        var script = _scripts[index];
        _scripts.RemoveAt(index);
        return script;
    }

    /// <summary>An anonymous object's properties as a column set, nulls stored as DBNull.</summary>
    private static Dictionary<string, object?> ToColumns(object row) =>
        row.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
            .OrderBy(property => property.MetadataToken)
            .ToDictionary(
                property => property.Name,
                property => (object?)(property.GetValue(row) ?? DBNull.Value),
                StringComparer.OrdinalIgnoreCase);

    [AllowNull]
    public override string ConnectionString { get; set; } = "fake";

    public override string Database => "fake";

    public override string DataSource => "fake";

    public override string ServerVersion => "0.0";

    /// <summary>Open from the start: the factory contract is an already-open connection.</summary>
    public override ConnectionState State => ConnectionState.Open;

    public override void ChangeDatabase(string databaseName)
    {
    }

    public override void Open()
    {
    }

    public override void Close()
    {
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        var transaction = new FakeDbTransaction(this, isolationLevel);
        Transactions.Add(transaction);
        return transaction;
    }

    protected override DbCommand CreateDbCommand() => new FakeDbCommand(this);

    internal enum ScriptKind
    {
        Rows,
        Scalar,
        Affected,
    }

    private sealed class Script
    {
        private Script(string match, ScriptKind kind, object? value, Exception? error)
        {
            Match = match;
            Kind = kind;
            Value = value;
            Error = error;
        }

        public string Match { get; }

        public ScriptKind Kind { get; }

        public object? Value { get; }

        public Exception? Error { get; }

        public static Script ForRows(string match, List<Dictionary<string, object?>> rows) =>
            new(match, ScriptKind.Rows, rows, null);

        public static Script ForScalar(string match, object? value) =>
            new(match, ScriptKind.Scalar, value, null);

        public static Script ForAffected(string match, int affected) =>
            new(match, ScriptKind.Affected, affected, null);

        public static Script ForError(string match, Exception error) =>
            new(match, ScriptKind.Rows, null, error);
    }
}

/// <summary>
/// One statement as the repository ran it: the text, the values it bound, and the transaction it
/// was enlisted in. The last of those is the point — an audit row on a different transaction from
/// the change it describes is the failure this whole fake exists to catch.
/// </summary>
public sealed record ExecutedCommand(
    string Sql,
    IReadOnlyDictionary<string, object?> Parameters,
    DbTransaction? Transaction)
{
    internal static ExecutedCommand From(FakeDbCommand command) =>
        new(command.CommandText, command.BoundValues, command.Enlisted);

    /// <summary>One bound value by name; fails the test when the statement did not bind it.</summary>
    public object? Param(string name)
    {
        Assert.True(Parameters.ContainsKey(name), $"The statement bound no parameter named '{name}'.");
        return Parameters[name];
    }
}
