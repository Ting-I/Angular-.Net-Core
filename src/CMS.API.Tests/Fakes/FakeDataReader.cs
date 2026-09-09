using System.Collections;
using System.Data.Common;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// A reader over scripted rows. Every typed getter casts what <see cref="GetValue"/> returns, so a
/// row is written with the CLR type the real column would come back as and nothing converts
/// silently — a smallint column seeded as <c>int</c> fails here rather than in production.
/// </summary>
public sealed class FakeDataReader : DbDataReader
{
    private readonly List<Dictionary<string, object?>> _rows;
    private readonly List<string> _columns;
    private int _index = -1;

    public FakeDataReader(List<Dictionary<string, object?>> rows)
    {
        _rows = rows;
        _columns = rows.Count == 0 ? [] : [.. rows[0].Keys];
    }

    private Dictionary<string, object?> Current => _rows[_index];

    public override int Depth => 0;

    public override int FieldCount => _columns.Count;

    public override bool HasRows => _rows.Count > 0;

    public override bool IsClosed { get; }

    public override int RecordsAffected => 0;

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => GetValue(GetOrdinal(name));

    public override bool Read() => ++_index < _rows.Count;

    public override bool NextResult() => false;

    public override string GetName(int ordinal) => _columns[ordinal];

    public override int GetOrdinal(string name) =>
        _columns.FindIndex(column => string.Equals(column, name, StringComparison.OrdinalIgnoreCase));

    public override object GetValue(int ordinal) => Current[_columns[ordinal]] ?? DBNull.Value;

    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }

        return count;
    }

    public override bool IsDBNull(int ordinal) => GetValue(ordinal) is DBNull;

    /// <summary>
    /// The type of the value in the first row, which is what Dapper asks for when it builds the
    /// deserializer. An all-empty result never reaches this — Dapper does not build one.
    /// </summary>
    public override Type GetFieldType(int ordinal) =>
        _rows.Count == 0 ? typeof(object) : (_rows[0][_columns[ordinal]] ?? DBNull.Value).GetType();

    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;

    public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);

    public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);

    public override char GetChar(int ordinal) => (char)GetValue(ordinal);

    public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);

    public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);

    public override double GetDouble(int ordinal) => (double)GetValue(ordinal);

    public override float GetFloat(int ordinal) => (float)GetValue(ordinal);

    public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);

    public override short GetInt16(int ordinal) => (short)GetValue(ordinal);

    public override int GetInt32(int ordinal) => (int)GetValue(ordinal);

    public override long GetInt64(int ordinal) => (long)GetValue(ordinal);

    public override string GetString(int ordinal) => (string)GetValue(ordinal);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) =>
        throw new NotSupportedException();

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) =>
        throw new NotSupportedException();

    public override IEnumerator GetEnumerator() => _rows.GetEnumerator();
}
