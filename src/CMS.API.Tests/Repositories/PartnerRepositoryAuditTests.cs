using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Tests.Fakes;
using static CMS.API.Tests.Fakes.TestPrincipal;

namespace CMS.API.Tests.Repositories;

/// <summary>
/// The 異動紀錄 retrofit, proved on one repository end to end: the real
/// <see cref="PartnerRepository"/>, the real <see cref="RowAuditWriter"/>, and a scripted
/// <see cref="FakeDbConnection"/> in place of SQL Server. Every other repository follows the same
/// three shapes, so what is pinned here is the pattern, not just Partner.
///
/// The writer is handed a <see cref="ThrowingDbConnectionFactory"/> on purpose: the audit INSERT
/// must ride the repository's own transaction, so if it ever opens a connection of its own instead,
/// these tests fail loudly rather than passing on a second connection nothing rolls back.
/// </summary>
public class PartnerRepositoryAuditTests
{
    private const string AuditInsert = "INSERT INTO RowAudit";
    private const string SnapshotRead = "FROM Partner p";
    private const string OperatorRead = "FROM AppUser u";

    /// <summary>The columns PartnerSql.SelectRow projects, as the reader would hand them back.</summary>
    private static object Row(
        short pkid = 7,
        string name = "原廠甲",
        int displayOrder = 10) => new
        {
            Pkid = pkid,
            Name = name,
            AppKey = "alpha",
            NameOnPartnerMenu = "選單名稱",
            NameOnCourseDetailPage = "課程頁名稱",
            DisplayOrder = displayOrder,
            ImageFilename = "alpha.png",
        };

    private static PartnerRequest Request(
        short pkid = 7,
        string name = "原廠甲",
        int displayOrder = 10) => new()
        {
            Pkid = pkid,
            Name = name,
            AppKey = "alpha",
            NameOnPartnerMenu = "選單名稱",
            NameOnCourseDetailPage = "課程頁名稱",
            DisplayOrder = displayOrder,
            ImageFilename = "alpha.png",
        };

    /// <summary>
    /// The repository and the real writer over one scripted connection.
    ///
    /// The operator's 使用者名稱 is read from AppUser on the caller's transaction, so every test
    /// that writes an audit row consumes this script. <paramref name="storedUserName"/> is what
    /// that read answers; the token claim says 系統管理員, so passing something else is how a test
    /// asks which of the two the trail records.
    /// </summary>
    private static (PartnerRepository Repository, FakeDbConnection Connection) Build(
        string? storedUserName = "系統管理員")
    {
        var connection = new FakeDbConnection();
        if (storedUserName is null)
        {
            connection.ReturnsNoRows(OperatorRead);
        }
        else
        {
            connection.Returns(OperatorRead, new { UserName = storedUserName });
        }

        var writer = new RowAuditWriter(
            new ThrowingDbConnectionFactory(),
            SignedIn("admin", "系統管理員"));

        return (new PartnerRepository(new FakeDbConnectionFactory(connection), writer), connection);
    }

    [Fact]
    public async Task Create_WritesAnInsertRowDescribedByTheFirstStringColumn()
    {
        var (repository, connection) = Build();
        connection
            .ReturnsScalar("INSERT INTO Partner", (short)7)
            .Returns(SnapshotRead, Row(name: "原廠甲"))
            .ReturnsAffected(AuditInsert, 1);

        var pkid = await repository.CreateAsync(Request());

        Assert.Equal((short)7, pkid);

        var audit = connection.SingleExecuted(AuditInsert);
        Assert.Equal("Partner", audit.Param("TableName"));
        Assert.Equal(RowAuditWriterDefaults.Insert, audit.Param("ActionType"));
        Assert.Equal("7", audit.Param("PrimaryKeyValues"));
        Assert.Equal("原廠甲", audit.Param("ActionDesc"));
        Assert.Equal("系統管理員", audit.Param("UserName"));
    }

    [Fact]
    public async Task Update_WritesAnUpdateRowListingExactlyTheChangedColumns()
    {
        var (repository, connection) = Build();
        connection
            .Returns(SnapshotRead, Row(name: "原廠甲", displayOrder: 10))
            .ReturnsAffected("UPDATE Partner", 1)
            .Returns(SnapshotRead, Row(name: "原廠乙", displayOrder: 20))
            .ReturnsAffected(AuditInsert, 1);

        Assert.True(await repository.UpdateAsync(Request(name: "原廠乙", displayOrder: 20)));

        var audit = connection.SingleExecuted(AuditInsert);
        Assert.Equal(RowAuditWriterDefaults.Update, audit.Param("ActionType"));
        Assert.Equal("7", audit.Param("PrimaryKeyValues"));

        // Declaration order, and nothing else: AppKey and ImageFilename were rewritten with the
        // values they already held, which is not a change.
        Assert.Equal("Name,DisplayOrder", audit.Param("ActionDesc"));
    }

    [Fact]
    public async Task Update_ThatChangedNothing_WritesNoAuditRow()
    {
        var (repository, connection) = Build();
        connection
            .Returns(SnapshotRead, Row())
            .ReturnsAffected("UPDATE Partner", 1)
            .Returns(SnapshotRead, Row());

        Assert.True(await repository.UpdateAsync(Request()));

        // A row saying only that somebody pressed 儲存 is what makes the trail unreadable.
        Assert.Empty(connection.ExecutedMatching(AuditInsert));
    }

    [Fact]
    public async Task Delete_WritesADeleteRowFromTheSnapshotTakenBeforeTheRowWent()
    {
        var (repository, connection) = Build();
        connection
            .Returns(SnapshotRead, Row(name: "原廠甲"))
            .ReturnsAffected("DELETE FROM Partner", 1)
            .ReturnsAffected(AuditInsert, 1);

        Assert.True(await repository.DeleteAsync(7));

        var audit = connection.SingleExecuted(AuditInsert);
        Assert.Equal(RowAuditWriterDefaults.Delete, audit.Param("ActionType"));
        Assert.Equal("7", audit.Param("PrimaryKeyValues"));
        Assert.Equal("原廠甲", audit.Param("ActionDesc"));

        // The snapshot has to precede the DELETE, or there is nothing left to describe.
        var order = connection.Executed.Select(command => command.Sql).ToList();
        Assert.True(
            order.FindIndex(sql => sql.Contains(SnapshotRead)) <
            order.FindIndex(sql => sql.Contains("DELETE FROM Partner")));
    }

    [Fact]
    public async Task AuditRow_RidesTheSameTransactionAsTheChange()
    {
        var (repository, connection) = Build();
        connection
            .Returns(SnapshotRead, Row())
            .ReturnsAffected("DELETE FROM Partner", 1)
            .ReturnsAffected(AuditInsert, 1);

        await repository.DeleteAsync(7);

        var transaction = Assert.Single(connection.Transactions);
        Assert.Same(transaction, connection.SingleExecuted("DELETE FROM Partner").Transaction);
        Assert.Same(transaction, connection.SingleExecuted(AuditInsert).Transaction);
        Assert.Equal(1, transaction.CommitCount);
    }

    [Fact]
    public async Task Update_ThatFails_LeavesNoAuditRowAndNeverCommits()
    {
        var (repository, connection) = Build();
        connection
            .Returns(SnapshotRead, Row())
            .Throws("UPDATE Partner", new InvalidOperationException("simulated constraint violation"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.UpdateAsync(Request(name: "原廠乙")));

        Assert.Empty(connection.ExecutedMatching(AuditInsert));
        Assert.Equal(0, Assert.Single(connection.Transactions).CommitCount);
    }

    [Fact]
    public async Task Update_OfAMissingRow_WritesNothingAtAll()
    {
        var (repository, connection) = Build();
        connection.ReturnsNoRows(SnapshotRead);

        Assert.False(await repository.UpdateAsync(Request(pkid: 999)));

        Assert.Empty(connection.ExecutedMatching("UPDATE Partner"));
        Assert.Empty(connection.ExecutedMatching(AuditInsert));
        Assert.Equal(0, Assert.Single(connection.Transactions).CommitCount);
    }

    [Fact]
    public async Task Delete_OfAMissingRow_WritesNothingAtAll()
    {
        var (repository, connection) = Build();
        connection.ReturnsNoRows(SnapshotRead);

        Assert.False(await repository.DeleteAsync(999));

        Assert.Empty(connection.ExecutedMatching("DELETE FROM Partner"));
        Assert.Empty(connection.ExecutedMatching(AuditInsert));
        Assert.Equal(0, Assert.Single(connection.Transactions).CommitCount);
    }

    [Fact]
    public async Task AuditRow_NamesTheOperatorAsAppUserHoldsThemNow()
    {
        // The token was issued at login and still carries 系統管理員. A rename since then —
        // their own, or an administrator's — is what the trail has to show, or every row
        // written in the token's remaining 24 hours is filed under a name nobody has.
        var (repository, connection) = Build(storedUserName: "改名後的管理員");
        connection
            .Returns(SnapshotRead, Row())
            .ReturnsAffected("DELETE FROM Partner", 1)
            .ReturnsAffected(AuditInsert, 1);

        await repository.DeleteAsync(7);

        Assert.Equal("改名後的管理員", connection.SingleExecuted(AuditInsert).Param("UserName"));
    }

    [Fact]
    public async Task AuditRow_FallsBackToTheTokenWhenTheAccountIsGone()
    {
        // An operator deleting their own row, or a token outliving the account it names.
        // The claim is stale by definition here, and it is still better than nothing:
        // UserName is NOT NULL and the change did happen.
        var (repository, connection) = Build(storedUserName: null);
        connection
            .Returns(SnapshotRead, Row())
            .ReturnsAffected("DELETE FROM Partner", 1)
            .ReturnsAffected(AuditInsert, 1);

        await repository.DeleteAsync(7);

        Assert.Equal("系統管理員", connection.SingleExecuted(AuditInsert).Param("UserName"));
    }

    [Fact]
    public async Task Update_ThatChangedNothing_ReadsNoOperatorNameEither()
    {
        var (repository, connection) = Build();
        connection
            .Returns(SnapshotRead, Row())
            .ReturnsAffected("UPDATE Partner", 1)
            .Returns(SnapshotRead, Row());

        Assert.True(await repository.UpdateAsync(Request()));

        // LogUpdateAsync returns before resolving the name, so a no-op save costs no query.
        Assert.Empty(connection.ExecutedMatching(OperatorRead));
    }
}
