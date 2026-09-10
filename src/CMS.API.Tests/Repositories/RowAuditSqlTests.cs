using CMS.API.Repositories;

namespace CMS.API.Tests.Repositories;

/// <summary>
/// The read statement's shape. Like every other {Table}Sql test this needs no fake and no
/// database — the projection, the filter and the ordering are the parts a change breaks silently.
/// </summary>
public class RowAuditSqlTests
{
    [Fact]
    public void SelectForRecord_FiltersOnBothTableNameAndTheKey()
    {
        // PrimaryKeyValues is only unique within a TableName: pkid 7 alone mixes 原廠 7 with 課程 7.
        Assert.Contains("a.TableName = @TableName", RowAuditSql.SelectForRecord);
        Assert.Contains("a.PrimaryKeyValues = @PrimaryKeyValues", RowAuditSql.SelectForRecord);
    }

    [Fact]
    public void SelectForRecord_OrdersNewestFirstWithIdentityBreakingTies()
    {
        Assert.Contains("ORDER BY a.[DateTime] DESC, a.pkid DESC", RowAuditSql.SelectForRecord);
    }

    [Fact]
    public void SelectForRecord_ProjectsTheFourColumnsTheClientRenders()
    {
        // [DateTime] is bracketed and aliased: it is a type name as well as a column name.
        Assert.Contains("a.[DateTime] AS LoggedAt", RowAuditSql.SelectForRecord);
        Assert.Contains("a.UserName", RowAuditSql.SelectForRecord);
        Assert.Contains("a.ActionType", RowAuditSql.SelectForRecord);
        Assert.Contains("a.ActionDesc", RowAuditSql.SelectForRecord);
    }

    [Fact]
    public void SelectForRecord_IsAReadAndNothingElse()
    {
        // An audit row that can be edited is not an audit row.
        Assert.DoesNotContain("UPDATE", RowAuditSql.SelectForRecord, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", RowAuditSql.SelectForRecord, StringComparison.OrdinalIgnoreCase);
    }
}
