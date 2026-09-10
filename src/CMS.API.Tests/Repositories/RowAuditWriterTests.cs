using System.Security.Claims;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using static CMS.API.Tests.Fakes.TestPrincipal;

namespace CMS.API.Tests.Repositories;

/// <summary>
/// What the writer composes for each of the three actions, and where the operator's name comes
/// from. The INSERT itself is captured by <see cref="RecordingRowAuditWriter"/>; nothing here
/// opens a connection.
/// </summary>
public class RowAuditWriterTests
{
    private class Course
    {
        public int Pkid { get; set; }

        public string CourseId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public int Seats { get; set; }
    }

    [Fact]
    public async Task LogInsert_DescribesTheRowByItsFirstStringProperty()
    {
        var writer = new RecordingRowAuditWriter(SignedIn("admin", "系統管理員"));

        await writer.LogInsertAsync("Course", new Course { Pkid = 12, CourseId = "C-001", Title = "資料庫入門" });

        var entry = writer.Single();
        Assert.Equal("Course", entry.TableName);
        Assert.Equal(RowAuditWriterDefaults.Insert, entry.ActionType);
        Assert.Equal("12", entry.PrimaryKeyValues);
        Assert.Equal("C-001", entry.ActionDesc);
        Assert.Equal("系統管理員", entry.UserName);
    }

    [Fact]
    public async Task LogDelete_DescribesTheRowByItsFirstStringProperty()
    {
        var writer = new RecordingRowAuditWriter(SignedIn("admin", "系統管理員"));

        await writer.LogDeleteAsync("Course", new Course { Pkid = 12, CourseId = "C-001", Title = "資料庫入門" });

        var entry = writer.Single();
        Assert.Equal(RowAuditWriterDefaults.Delete, entry.ActionType);
        Assert.Equal("12", entry.PrimaryKeyValues);
        Assert.Equal("C-001", entry.ActionDesc);
    }

    [Fact]
    public async Task LogUpdate_ListsExactlyTheChangedPropertyNames()
    {
        var writer = new RecordingRowAuditWriter(SignedIn("admin", "系統管理員"));

        await writer.LogUpdateAsync(
            "Course",
            new Course { Pkid = 12, CourseId = "C-001", Title = "舊標題", Seats = 20 },
            new Course { Pkid = 12, CourseId = "C-001", Title = "新標題", Seats = 30 });

        var entry = writer.Single();
        Assert.Equal(RowAuditWriterDefaults.Update, entry.ActionType);
        Assert.Equal("12", entry.PrimaryKeyValues);
        Assert.Equal("Title,Seats", entry.ActionDesc);
    }

    [Fact]
    public async Task LogUpdate_WithNothingChanged_WritesNoRow()
    {
        var writer = new RecordingRowAuditWriter(SignedIn("admin", "系統管理員"));
        var before = new Course { Pkid = 12, CourseId = "C-001", Title = "標題", Seats = 20 };
        var after = new Course { Pkid = 12, CourseId = "C-001", Title = "標題", Seats = 20 };

        await writer.LogUpdateAsync("Course", before, after);

        // An empty ActionDesc would say only that somebody pressed save.
        Assert.Empty(writer.Entries);
    }

    [Fact]
    public async Task LogUpdate_TakesThePrimaryKeyFromTheAfterImage()
    {
        var writer = new RecordingRowAuditWriter(SignedIn("admin", "系統管理員"));

        await writer.LogUpdateAsync(
            "Course",
            new Course { Pkid = 12, Title = "舊標題" },
            new Course { Pkid = 12, Title = "新標題" });

        Assert.Equal("12", writer.Single().PrimaryKeyValues);
    }

    [Fact]
    public async Task LogInsert_WithNoHttpContext_FallsBackToSystem()
    {
        var writer = new RecordingRowAuditWriter(Anonymous());

        await writer.LogInsertAsync("Course", new Course { Pkid = 1, CourseId = "C-001" });

        Assert.Equal("system", writer.Single().UserName);
    }

    [Fact]
    public async Task LogInsert_WithAnUnauthenticatedPrincipal_FallsBackToSystem()
    {
        // A ClaimsIdentity with no authentication type is not authenticated, whatever it carries.
        var identity = new ClaimsIdentity([new Claim(JwtTokenService.UserNameClaimType, "冒名者")]);
        var writer = new RecordingRowAuditWriter(
            Anonymous(new DefaultHttpContext { User = new ClaimsPrincipal(identity) }));

        await writer.LogInsertAsync("Course", new Course { Pkid = 1, CourseId = "C-001" });

        Assert.Equal("system", writer.Single().UserName);
    }

    [Fact]
    public async Task LogInsert_ReadsTheUserNameClaimNotIdentityName()
    {
        // NameClaimType is bound to userId, so Identity.Name is the login id. Writing that into
        // the UserName column would look right and be wrong.
        var accessor = SignedIn("admin", "系統管理員");
        Assert.Equal("admin", accessor.HttpContext!.User.Identity!.Name);

        var writer = new RecordingRowAuditWriter(accessor);
        await writer.LogInsertAsync("Course", new Course { Pkid = 1, CourseId = "C-001" });

        Assert.Equal("系統管理員", writer.Single().UserName);
    }

    [Fact]
    public async Task LogInsert_ActionDescTruncatesAtTheColumnWidth()
    {
        var writer = new RecordingRowAuditWriter(SignedIn("admin", "系統管理員"));
        var overlong = new string('x', RowAuditEntry.ActionDescLength + 200);

        await writer.LogInsertAsync("Course", new Course { Pkid = 1, CourseId = overlong });

        var entry = writer.Single();
        Assert.Equal(RowAuditEntry.ActionDescLength, entry.ActionDesc!.Length);
        Assert.Equal(overlong[..RowAuditEntry.ActionDescLength], entry.ActionDesc);
    }

    [Fact]
    public void BuildEntry_TruncatesAnyActionDescAtTheColumnWidth()
    {
        // The cut is in the composition, so it covers the update arm too — a changed-column list
        // long enough to overflow needs an entity with hundreds of properties, not a long value.
        var writer = new RecordingRowAuditWriter(SignedIn("admin", "系統管理員"));
        var overlong = string.Join(",", Enumerable.Range(0, 200).Select(i => $"VeryLongColumnName{i}"));

        var entry = writer.BuildEntry("Course", RowAuditWriterDefaults.Update, "1", overlong);

        Assert.Equal(RowAuditEntry.ActionDescLength, entry.ActionDesc!.Length);
        Assert.Equal(overlong[..RowAuditEntry.ActionDescLength], entry.ActionDesc);
    }

    [Fact]
    public async Task LogInsert_TruncatesTheNarrowerColumnsToo()
    {
        // An audit row must never be the thing that fails the caller's write with error 8152.
        var writer = new RecordingRowAuditWriter(
            SignedIn("admin", new string('名', RowAuditEntry.UserNameLength + 40)));

        await writer.LogInsertAsync(new string('T', RowAuditEntry.TableNameLength + 20), new Course { Pkid = 1 });

        var entry = writer.Single();
        Assert.Equal(RowAuditEntry.TableNameLength, entry.TableName.Length);
        Assert.Equal(RowAuditEntry.UserNameLength, entry.UserName.Length);
    }

    [Fact]
    public async Task LogInsert_StampsTheCurrentTime()
    {
        var now = new DateTimeOffset(2026, 9, 8, 14, 30, 15, TimeSpan.Zero);
        var writer = new RecordingRowAuditWriter(SignedIn("admin", "系統管理員"), new FixedTimeProvider(now));

        await writer.LogInsertAsync("Course", new Course { Pkid = 1, CourseId = "C-001" });

        Assert.Equal(now.ToLocalTime().DateTime, writer.Single().LoggedAt);
    }

    [Fact]
    public void Insert_StatementOmitsTheIdentityKeyAndBracketsTheDateTimeColumn()
    {
        Assert.DoesNotContain("pkid", RowAuditSql.Insert, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[DateTime]", RowAuditSql.Insert);
        Assert.Contains("@LoggedAt", RowAuditSql.Insert);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
