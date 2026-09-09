using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Tests.Controllers;

/// <summary>
/// GET /api/rowaudit — one record's 異動紀錄, filtered by table and key, newest first.
/// </summary>
public class RowAuditControllerTests
{
    private static DateTime At(int day, int hour, int minute)
        => new(2026, 6, day, hour, minute, 0);

    private static (RowAuditController Controller, FakeRowAuditRepository Repository) CreateController()
    {
        var repository = new FakeRowAuditRepository();
        return (new RowAuditController(repository), repository);
    }

    private static List<RowAuditHistoryEntry> AssertOk(
        ActionResult<IEnumerable<RowAuditHistoryEntry>> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<IEnumerable<RowAuditHistoryEntry>>(ok.Value).ToList();
    }

    // ---------- Filtering ----------

    [Fact]
    public async Task GetForRecord_ReturnsOnlyTheRowsForThatTableAndKey()
    {
        var (controller, repository) = CreateController();
        repository
            .Seed("Course", 7, At(1, 9, 0), "Insert", actionDesc: "C-001")
            .Seed("Partner", 7, At(2, 9, 0), "Insert", actionDesc: "原廠甲")
            .Seed("Course", 8, At(3, 9, 0), "Insert", actionDesc: "C-002")
            .Seed("Course", 7, At(4, 9, 0), "Update", actionDesc: "Title");

        var history = AssertOk(await controller.GetForRecord("Course", 7, CancellationToken.None));

        // 原廠 7 and 課程 8 are both excluded: the key alone does not identify a record.
        Assert.Equal(["Title", "C-001"], history.Select(entry => entry.ActionDesc));
        Assert.Equal(("Course", 7), Assert.Single(repository.Requests));
    }

    [Fact]
    public async Task GetForRecord_WithNoHistory_ReturnsAnEmptyListNotA404()
    {
        var (controller, _) = CreateController();

        var history = AssertOk(await controller.GetForRecord("Course", 99, CancellationToken.None));

        // "No history yet" is what the badge renders. It is not an error.
        Assert.Empty(history);
    }

    // ---------- Ordering ----------

    [Fact]
    public async Task GetForRecord_ReturnsRowsNewestFirst()
    {
        var (controller, repository) = CreateController();
        repository
            .Seed("Course", 7, At(2, 14, 30), "Update", actionDesc: "Title")
            .Seed("Course", 7, At(1, 9, 0), "Insert", actionDesc: "C-001")
            .Seed("Course", 7, At(3, 8, 15), "Update", actionDesc: "Seats");

        var history = AssertOk(await controller.GetForRecord("Course", 7, CancellationToken.None));

        Assert.Equal(
            [At(3, 8, 15), At(2, 14, 30), At(1, 9, 0)],
            history.Select(entry => entry.LoggedAt));
    }

    [Fact]
    public async Task GetForRecord_BreaksATieOnTheAuditRowsOwnKey()
    {
        // A slot swap writes two rows from one clock reading; the later one is still the later one.
        var (controller, repository) = CreateController();
        repository
            .Seed("FeaturedPromoItem", 3, At(4, 11, 0), "Update", actionDesc: "Slot")
            .Seed("FeaturedPromoItem", 3, At(4, 11, 0), "Update", actionDesc: "Slot,Topic");

        var history = AssertOk(
            await controller.GetForRecord("FeaturedPromoItem", 3, CancellationToken.None));

        Assert.Equal(["Slot,Topic", "Slot"], history.Select(entry => entry.ActionDesc));
    }

    [Fact]
    public async Task GetForRecord_CarriesEveryColumnTheClientRenders()
    {
        var (controller, repository) = CreateController();
        repository.Seed("Course", 7, At(4, 14, 30), "Update", "alice", "Title,Seats");

        var entry = Assert.Single(
            AssertOk(await controller.GetForRecord("Course", 7, CancellationToken.None)));

        Assert.Equal(At(4, 14, 30), entry.LoggedAt);
        Assert.Equal("alice", entry.UserName);
        Assert.Equal("Update", entry.ActionType);
        Assert.Equal("Title,Seats", entry.ActionDesc);
    }

    // ---------- Bad requests ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetForRecord_WithoutATableName_Returns400(string? tableName)
    {
        var (controller, repository) = CreateController();

        var result = await controller.GetForRecord(tableName, 7, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(
            StatusCodes.Status400BadRequest,
            Assert.IsType<ProblemDetails>(bad.Value).Status);
        Assert.Empty(repository.Requests);
    }

    [Fact]
    public async Task GetForRecord_WithoutAKey_Returns400RatherThanQueryingForZero()
    {
        // pkid 0 is a real key for a table whose pkid is not IDENTITY, so a missing one must not
        // quietly become a query.
        var (controller, repository) = CreateController();

        var result = await controller.GetForRecord("Course", null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(repository.Requests);
    }

    [Fact]
    public async Task GetForRecord_TrimsTheTableName()
    {
        var (controller, repository) = CreateController();
        repository.Seed("Course", 7, At(1, 9, 0));

        var history = AssertOk(await controller.GetForRecord("  Course  ", 7, CancellationToken.None));

        Assert.Single(history);
    }
}
