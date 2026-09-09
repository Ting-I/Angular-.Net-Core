using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// One statement on a <see cref="FakeDbConnection"/>. The three Execute members route to the
/// connection's scripts; the async overloads come free from <see cref="DbCommand"/>, which runs
/// them over these.
/// </summary>
public sealed class FakeDbCommand : DbCommand
{
    private readonly FakeDbConnection _connection;

    public FakeDbCommand(FakeDbConnection connection)
    {
        _connection = connection;
        DbParameterCollection = new FakeDbParameterCollection();
    }

    [AllowNull]
    public override string CommandText { get; set; } = string.Empty;

    public override int CommandTimeout { get; set; }

    public override CommandType CommandType { get; set; } = CommandType.Text;

    public override bool DesignTimeVisible { get; set; }

    public override UpdateRowSource UpdatedRowSource { get; set; }

    protected override DbConnection? DbConnection { get; set; }

    protected override DbParameterCollection DbParameterCollection { get; }

    protected override DbTransaction? DbTransaction { get; set; }

    /// <summary>The bound parameter values by name, for a test to assert on.</summary>
    public IReadOnlyDictionary<string, object?> BoundValues =>
        DbParameterCollection
            .Cast<DbParameter>()
            .ToDictionary(
                parameter => parameter.ParameterName,
                parameter => parameter.Value is DBNull ? null : parameter.Value,
                StringComparer.OrdinalIgnoreCase);

    /// <summary>The transaction this statement ran on, if any.</summary>
    public DbTransaction? Enlisted => DbTransaction;

    public override void Cancel()
    {
    }

    public override void Prepare()
    {
    }

    public override int ExecuteNonQuery() =>
        (int)(_connection.Execute(this, FakeDbConnection.ScriptKind.Affected) ?? 0);

    public override object? ExecuteScalar() =>
        _connection.Execute(this, FakeDbConnection.ScriptKind.Scalar);

    protected override DbParameter CreateDbParameter() => new FakeDbParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
        new FakeDataReader(
            (List<Dictionary<string, object?>>)_connection.Execute(this, FakeDbConnection.ScriptKind.Rows)!);
}

/// <summary>A parameter carrying only what Dapper sets on it.</summary>
public sealed class FakeDbParameter : DbParameter
{
    public override DbType DbType { get; set; }

    public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;

    public override bool IsNullable { get; set; }

    [AllowNull]
    public override string ParameterName { get; set; } = string.Empty;

    public override int Size { get; set; }

    [AllowNull]
    public override string SourceColumn { get; set; } = string.Empty;

    public override bool SourceColumnNullMapping { get; set; }

    public override object? Value { get; set; }

    public override void ResetDbType() => DbType = DbType.Object;
}

/// <summary>A parameter collection over a plain list.</summary>
public sealed class FakeDbParameterCollection : DbParameterCollection
{
    private readonly List<DbParameter> _parameters = [];

    public override int Count => _parameters.Count;

    public override object SyncRoot { get; } = new();

    public override int Add(object value)
    {
        _parameters.Add((DbParameter)value);
        return _parameters.Count - 1;
    }

    public override void AddRange(Array values)
    {
        foreach (var value in values)
        {
            Add(value);
        }
    }

    public override void Clear() => _parameters.Clear();

    public override bool Contains(object value) => _parameters.Contains((DbParameter)value);

    public override bool Contains(string value) => IndexOf(value) >= 0;

    public override void CopyTo(Array array, int index) => ((ICollection)_parameters).CopyTo(array, index);

    public override IEnumerator GetEnumerator() => _parameters.GetEnumerator();

    public override int IndexOf(object value) => _parameters.IndexOf((DbParameter)value);

    public override int IndexOf(string parameterName) => _parameters.FindIndex(
        parameter => string.Equals(parameter.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase));

    public override void Insert(int index, object value) => _parameters.Insert(index, (DbParameter)value);

    public override void Remove(object value) => _parameters.Remove((DbParameter)value);

    public override void RemoveAt(int index) => _parameters.RemoveAt(index);

    public override void RemoveAt(string parameterName) => RemoveAt(IndexOf(parameterName));

    protected override DbParameter GetParameter(int index) => _parameters[index];

    protected override DbParameter GetParameter(string parameterName) => _parameters[IndexOf(parameterName)];

    protected override void SetParameter(int index, DbParameter value) => _parameters[index] = value;

    protected override void SetParameter(string parameterName, DbParameter value) =>
        _parameters[IndexOf(parameterName)] = value;
}

/// <summary>
/// Records Commit / Rollback so a test can prove that a failed change never reached either — the
/// audit row rides this transaction, so what happened to it is what happened to the trail.
/// </summary>
public sealed class FakeDbTransaction : DbTransaction
{
    private readonly FakeDbConnection _connection;

    public FakeDbTransaction(FakeDbConnection connection, IsolationLevel isolationLevel)
    {
        _connection = connection;
        IsolationLevel = isolationLevel;
    }

    public int CommitCount { get; private set; }

    public int RollbackCount { get; private set; }

    public override IsolationLevel IsolationLevel { get; }

    protected override DbConnection DbConnection => _connection;

    public override void Commit() => CommitCount++;

    public override void Rollback() => RollbackCount++;
}
