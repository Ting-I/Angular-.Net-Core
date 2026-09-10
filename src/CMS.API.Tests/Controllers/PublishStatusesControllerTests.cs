using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Tests.Controllers;

public class PublishStatusesControllerTests
{
    private static PublishStatus Status(
        byte pkid,
        string description,
        bool isDraft = false,
        bool isPublished = false,
        bool isDiscontinued = false,
        int courseCount = 0,
        int promotionCount = 0)
        => new()
        {
            Pkid = pkid,
            Description = description,
            IsDraft = isDraft,
            IsPublished = isPublished,
            IsDiscontinued = isDiscontinued,
            CourseCount = courseCount,
            PromotionCount = promotionCount,
        };

    private static (PublishStatusesController Controller, FakePublishStatusRepository Repository) CreateController(
        params PublishStatus[] seed)
    {
        var repository = new FakePublishStatusRepository().Seed(seed);
        return (new PublishStatusesController(repository), repository);
    }

    private static T AssertOk<T>(ActionResult<T> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<T>(ok.Value);
    }

    // ---------- List ----------

    [Fact]
    public async Task GetAll_ReturnsAllStatusesOrderedByPkid()
    {
        var (controller, _) = CreateController(
            Status(3, "已停用", isDiscontinued: true),
            Status(1, "草稿", isDraft: true),
            Status(2, "已發布", isPublished: true));

        var statuses = AssertOk(await controller.GetAll(CancellationToken.None)).ToList();

        Assert.Equal<byte[]>([1, 2, 3], statuses.Select(s => s.Pkid).ToArray());
    }

    [Fact]
    public async Task GetAll_IncludesReferenceCounts()
    {
        var (controller, _) = CreateController(
            Status(2, "已發布", isPublished: true, courseCount: 12, promotionCount: 4));

        var status = Assert.Single(AssertOk(await controller.GetAll(CancellationToken.None)));

        Assert.Equal(12, status.CourseCount);
        Assert.Equal(4, status.PromotionCount);
    }

    // ---------- List (query filter) ----------

    [Fact]
    public async Task Query_WithoutFilters_ReturnsEverything()
    {
        var (controller, _) = CreateController(
            Status(1, "草稿", isDraft: true),
            Status(2, "已發布", isPublished: true));

        var statuses = AssertOk(await controller.Query(new PublishStatusQuery(), CancellationToken.None));

        Assert.Equal(2, statuses.Count());
    }

    [Fact]
    public async Task Query_WithKeyword_MatchesDescription()
    {
        var (controller, _) = CreateController(
            Status(1, "草稿", isDraft: true),
            Status(2, "已發布", isPublished: true));

        var statuses = AssertOk(await controller.Query(
            new PublishStatusQuery { Keyword = "已發布" }, CancellationToken.None));

        Assert.Equal(2, Assert.Single(statuses).Pkid);
    }

    [Fact]
    public async Task Query_WithIsDraftTrue_ReturnsOnlyDrafts()
    {
        var (controller, _) = CreateController(
            Status(1, "草稿", isDraft: true),
            Status(2, "已發布", isPublished: true));

        var statuses = AssertOk(await controller.Query(
            new PublishStatusQuery { IsDraft = true }, CancellationToken.None));

        Assert.Equal(1, Assert.Single(statuses).Pkid);
    }

    [Fact]
    public async Task Query_WithIsDraftFalse_ExcludesDrafts()
    {
        var (controller, _) = CreateController(
            Status(1, "草稿", isDraft: true),
            Status(2, "已發布", isPublished: true));

        var statuses = AssertOk(await controller.Query(
            new PublishStatusQuery { IsDraft = false }, CancellationToken.None));

        Assert.Equal(2, Assert.Single(statuses).Pkid);
    }

    [Fact]
    public async Task Query_WithIsPublished_FiltersExactly()
    {
        var (controller, _) = CreateController(
            Status(1, "草稿", isDraft: true),
            Status(2, "已發布", isPublished: true));

        var statuses = AssertOk(await controller.Query(
            new PublishStatusQuery { IsPublished = true }, CancellationToken.None));

        Assert.Equal(2, Assert.Single(statuses).Pkid);
    }

    [Fact]
    public async Task Query_WithIsDiscontinued_FiltersExactly()
    {
        var (controller, _) = CreateController(
            Status(2, "已發布", isPublished: true),
            Status(3, "已停用", isDiscontinued: true));

        var statuses = AssertOk(await controller.Query(
            new PublishStatusQuery { IsDiscontinued = true }, CancellationToken.None));

        Assert.Equal(3, Assert.Single(statuses).Pkid);
    }

    [Fact]
    public async Task Query_WithCombinedFilters_AppliesAll()
    {
        var (controller, _) = CreateController(
            Status(1, "已發布草稿", isDraft: true, isPublished: true),
            Status(2, "已發布", isPublished: true),
            Status(3, "草稿", isDraft: true));

        var statuses = AssertOk(await controller.Query(
            new PublishStatusQuery { Keyword = "已發布", IsDraft = true }, CancellationToken.None));

        Assert.Equal(1, Assert.Single(statuses).Pkid);
    }

    [Fact]
    public async Task Query_WithNoMatches_ReturnsEmptyList()
    {
        var (controller, _) = CreateController(Status(1, "草稿", isDraft: true));

        var statuses = AssertOk(await controller.Query(
            new PublishStatusQuery { Keyword = "nope" }, CancellationToken.None));

        Assert.Empty(statuses);
    }

    [Fact]
    public async Task Query_WithNullBody_ReturnsEverything()
    {
        var (controller, _) = CreateController(Status(1, "草稿", isDraft: true));

        var statuses = AssertOk(await controller.Query(null!, CancellationToken.None));

        Assert.Single(statuses);
    }

    // ---------- View ----------

    [Fact]
    public async Task GetById_ReturnsStatus()
    {
        var (controller, _) = CreateController(
            Status(2, "已發布", isPublished: true, courseCount: 7));

        var status = AssertOk(await controller.GetById(2, CancellationToken.None));

        Assert.Equal("已發布", status.Description);
        Assert.True(status.IsPublished);
        Assert.Equal(7, status.CourseCount);
    }

    [Fact]
    public async Task GetById_WhenMissing_Returns404()
    {
        var (controller, _) = CreateController();

        var result = await controller.GetById(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---------- Add ----------

    [Fact]
    public async Task Create_ReturnsCreatedAtActionWithPkidRouteValue()
    {
        var (controller, repository) = CreateController();
        var request = new PublishStatusRequest
        {
            Pkid = 4,
            Description = "審核中",
            IsDraft = true,
        };

        var result = await controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(PublishStatusesController.GetById), created.ActionName);
        Assert.Equal(4, created.RouteValues!["id"]);
        Assert.Equal<byte[]>([4], repository.CreatedPkids.ToArray());

        var status = Assert.IsType<PublishStatus>(created.Value);
        Assert.Equal(4, status.Pkid);
        Assert.Equal("審核中", status.Description);
        Assert.True(status.IsDraft);
    }

    [Fact]
    public async Task Create_PersistsTheClientSuppliedKey()
    {
        var (controller, repository) = CreateController();

        await controller.Create(
            new PublishStatusRequest { Pkid = 42, Description = "自訂" },
            CancellationToken.None);

        var stored = await repository.GetByIdAsync(42);
        Assert.NotNull(stored);
        Assert.Equal("自訂", stored.Description);
    }

    [Fact]
    public async Task Create_WithDuplicatePkid_Returns409()
    {
        var (controller, repository) = CreateController(Status(1, "草稿", isDraft: true));

        var result = await controller.Create(
            new PublishStatusRequest { Pkid = 1, Description = "重複" },
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Empty(repository.CreatedPkids);
    }

    // ---------- Edit ----------

    [Fact]
    public async Task Update_TakesKeyFromBodyAndReturnsUpdatedStatus()
    {
        var (controller, repository) = CreateController(Status(2, "已發布", isPublished: true));

        var result = await controller.Update(
            new PublishStatusRequest
            {
                Pkid = 2,
                Description = "已上架",
                IsPublished = true,
                IsDiscontinued = true,
            },
            CancellationToken.None);

        var status = AssertOk(result);
        Assert.Equal("已上架", status.Description);
        Assert.True(status.IsDiscontinued);
        Assert.Equal<byte[]>([2], repository.UpdatedPkids.ToArray());
    }

    [Fact]
    public async Task Update_LeavesReferenceCountsUntouched()
    {
        var (controller, _) = CreateController(
            Status(2, "已發布", isPublished: true, courseCount: 5, promotionCount: 2));

        var status = AssertOk(await controller.Update(
            new PublishStatusRequest { Pkid = 2, Description = "已上架", IsPublished = true },
            CancellationToken.None));

        Assert.Equal(5, status.CourseCount);
        Assert.Equal(2, status.PromotionCount);
    }

    [Fact]
    public async Task Update_WhenMissing_Returns404()
    {
        var (controller, _) = CreateController();

        var result = await controller.Update(
            new PublishStatusRequest { Pkid = 99, Description = "x" },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_RemovesStatusAndReturns204()
    {
        var (controller, repository) = CreateController(Status(1, "草稿", isDraft: true));

        var result = await controller.Delete(1, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await repository.GetByIdAsync(1));
    }

    [Fact]
    public async Task Delete_WhenMissing_Returns404()
    {
        var (controller, _) = CreateController();

        Assert.IsType<NotFoundResult>(await controller.Delete(99, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_WhenReferencedByCourse_Returns409AndKeepsTheRow()
    {
        var (controller, repository) = CreateController(
            Status(2, "已發布", isPublished: true, courseCount: 3));

        var result = await controller.Delete(2, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.NotNull(await repository.GetByIdAsync(2));
        Assert.Empty(repository.DeletedPkids);
    }

    [Fact]
    public async Task Delete_WhenReferencedByPromotionOnly_Returns409()
    {
        var (controller, _) = CreateController(
            Status(2, "已發布", isPublished: true, promotionCount: 1));

        Assert.IsType<ConflictObjectResult>(await controller.Delete(2, CancellationToken.None));
    }
}
