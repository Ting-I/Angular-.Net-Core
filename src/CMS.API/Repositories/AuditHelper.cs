using System.Collections;
using System.Reflection;

namespace CMS.API.Repositories;

/// <summary>
/// The reflection behind <see cref="RowAuditWriter"/>: what a RowAudit row says about an entity of
/// any type. Static and free of both a database and an HttpContext, so it is unit tested directly
/// the way <c>BuildWhere</c> is — the audit trail's shape is the part most easily got wrong
/// silently, and it is the part that needs no fake to pin down.
/// </summary>
public static class AuditHelper
{
    /// <summary>Separator for the changed-column list. No space, so more names fit the 1000 chars.</summary>
    public const string ColumnSeparator = ",";

    /// <summary>
    /// The entity's public readable instance properties in declaration order.
    ///
    /// <c>Type.GetProperties</c> makes no ordering guarantee, so the order is taken from
    /// <see cref="MemberInfo.MetadataToken"/> — the order the compiler emitted the members in,
    /// which for a hand-written model is the order they are written in. That matters: "the first
    /// string property" is only a stable rule if "first" is.
    /// </summary>
    public static IReadOnlyList<PropertyInfo> AuditableProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
            .OrderBy(property => property.MetadataToken)
            .ToArray();

    /// <summary>
    /// The entity's pkid as text, or an empty string when it has no pkid or the value is null.
    ///
    /// Matched case-insensitively because the column is lowercase <c>pkid</c> and the models spell
    /// the property <c>Pkid</c>. An entity whose real key is a string — <c>AppRole</c>, keyed on
    /// RoleId — still audits on pkid: the column it is going into is called PrimaryKeyValues, but
    /// this writer records the surrogate key every table here carries.
    /// </summary>
    public static string PrimaryKeyValue(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var pkid = AuditableProperties(entity.GetType())
            .FirstOrDefault(property => property.Name.Equals("pkid", StringComparison.OrdinalIgnoreCase));

        return pkid?.GetValue(entity)?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// The value of the entity's first string-typed property in declaration order — the Name /
    /// Title / Code field that makes an Insert or Delete row readable at a glance.
    ///
    /// Null when the entity has no string property at all, or when that property is itself null;
    /// ActionDesc is the one nullable column, so there is nothing to invent.
    /// </summary>
    public static string? FirstStringValue(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var first = AuditableProperties(entity.GetType())
            .FirstOrDefault(property => property.PropertyType == typeof(string));

        return (string?)first?.GetValue(entity);
    }

    /// <summary>
    /// The names of the properties whose value differs between <paramref name="before"/> and
    /// <paramref name="after"/>, in declaration order. Empty when nothing changed.
    /// </summary>
    public static IReadOnlyList<string> ChangedColumns<T>(T before, T after)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        return AuditableProperties(before.GetType())
            .Where(property => !AreEqual(property.GetValue(before), property.GetValue(after)))
            .Select(property => property.Name)
            .ToArray();
    }

    /// <summary>
    /// <see cref="ChangedColumns{T}"/> rendered for the ActionDesc column; empty when nothing
    /// changed.
    /// </summary>
    public static string ChangedColumnList<T>(T before, T after)
        where T : notnull
        => string.Join(ColumnSeparator, ChangedColumns(before, after));

    /// <summary>
    /// The value cut to <paramref name="maxLength"/> characters, so a long one is recorded rather
    /// than costing the caller its write with SQL error 8152. Null and short values pass through.
    /// </summary>
    public static string? Truncate(string? value, int maxLength) =>
        value is not null && value.Length > maxLength ? value[..maxLength] : value;

    /// <summary>
    /// Value comparison for two readings of the same property.
    ///
    /// Sequences compare element by element, because an n-n key list such as
    /// <c>CertificationPkids</c> is a new <c>List&lt;int&gt;</c> on every read and would otherwise
    /// report as changed on every save. Everything else defers to the type's own
    /// <see cref="object.Equals(object)"/> — so a nav object that does not define value equality
    /// compares by reference. Pass the row model, not a response model carrying nav objects, and
    /// that never comes up.
    /// </summary>
    private static bool AreEqual(object? before, object? after)
    {
        if (before is null || after is null)
        {
            return before is null && after is null;
        }

        if (before is not string && before is IEnumerable left && after is IEnumerable right)
        {
            return left.Cast<object?>().SequenceEqual(right.Cast<object?>());
        }

        return before.Equals(after);
    }
}
