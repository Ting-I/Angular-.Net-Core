using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Tests.Controllers;

public class CourseGroupsControllerTests
{
    private static CourseGroup MakeCourseGroup(
        short pkid,
        string description,
        int courseCount = 0,
        int partnerCourseGroupCount = 0)
        => new()
        {
            Pkid = pkid,
            Description = description,
            CourseCount = courseCount,
            PartnerCourseGroupCount = partnerCourseGroupCount,
        };

    private static CourseGroupRequest Request(short pkid = 0, string description = "雲端技術")
        => new() { Pkid = pkid, Description = description };

    private static (CourseGroupsController Controller, FakeCourseGroupRepository Repository) CreateController(
        params CourseGroup[] seed)
    {
        var repository = new FakeCourseGroupRepository().Seed(seed);
        return (new CourseGroupsController(repository), repository);
    }

    private static T AssertOk<T>(ActionResult<T> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<T>(ok.Value);
    }

    // ---------- List ----------

    [Fact]
    public async Task GetAll_ReturnsCourseGroupsOrderedByPkid()
    {
        var (controller, _) = CreateController(
            MakeCourseGroup(3, "資料庫"),
            MakeCourseGroup(1, "雲端技術"),
            MakeCourseGroup(2, "網路安全"));

        var courseGroups = AssertOk(await controller.GetAll(CancellationToken.None)).ToList();

        Assert.Equal<short[]>([1, 2, 3], courseGroups.Select(cg => cg.Pkid).ToArray());
    }

    [Fact]
    public async Task GetAll_IncludesBothReferenceCounts()
    {
        var (controller, _) = CreateController(
            MakeCourseGroup(1, "雲端技術", courseCount: 12, partnerCourseGroupCount: 3));

        var courseGroup = Assert.Single(AssertOk(await controller.GetAll(CancellationToken.None)));

        Assert.Equal(12, courseGroup.CourseCount);
        Assert.Equal(3, courseGroup.PartnerCourseGroupCount);
    }

    // ---------- List (query filter) ----------

    [Fact]
    public async Task Query_WithoutFilters_ReturnsEverything()
    {
        var (controller, _) = CreateController(
            MakeCourseGroup(1, "雲端技術"),
            MakeCourseGroup(2, "網路安全"));

        var courseGroups = AssertOk(await controller.Query(new CourseGroupQuery(), CancellationToken.None));

        Assert.Equal(2, courseGroups.Count());
    }

    [Fact]
    public async Task Query_WithNullBody_ReturnsEverything()
    {
        var (controller, _) = CreateController(
            MakeCourseGroup(1, "雲端技術"),
            MakeCourseGroup(2, "網路安全"));

        var courseGroups = AssertOk(await controller.Query(null!, CancellationToken.None));

        Assert.Equal(2, courseGroups.Count());
    }

    [Fact]
    public async Task Query_WithKeyword_MatchesDescription()
    {
        var (controller, _) = CreateController(
            MakeCourseGroup(1, "雲端技術"),
            MakeCourseGroup(2, "網路安全"));

        var courseGroups = AssertOk(
            await controller.Query(new CourseGroupQuery { Keyword = "雲端" }, CancellationToken.None));

        Assert.Equal(1, Assert.Single(courseGroups).Pkid);
    }

    [Fact]
    public async Task Query_WithKeyword_ThatMatchesNothing_ReturnsEmpty()
    {
        var (controller, _) = CreateController(MakeCourseGroup(1, "雲端技術"));

        var courseGroups = AssertOk(
            await controller.Query(new CourseGroupQuery { Keyword = "沒有這個" }, CancellationToken.None));

        Assert.Empty(courseGroups);
    }

    [Fact]
    public async Task Query_WithInUseTrue_ReturnsOnlyReferencedGroups()
    {
        var (controller, _) = CreateController(
            MakeCourseGroup(1, "雲端技術", courseCount: 4),
            MakeCourseGroup(2, "網路安全"),
            MakeCourseGroup(3, "資料庫", partnerCourseGroupCount: 1));

        var courseGroups = AssertOk(
            await controller.Query(new CourseGroupQuery { InUse = true }, CancellationToken.None));

        Assert.Equal<short[]>([1, 3], courseGroups.Select(cg => cg.Pkid).ToArray());
    }

    /// <summary>false is a real filter — it is how the operator finds orphan rows to clean up.</summary>
    [Fact]
    public async Task Query_WithInUseFalse_ReturnsOnlyOrphanGroups()
    {
        var (controller, _) = CreateController(
            MakeCourseGroup(1, "雲端技術", courseCount: 4),
            MakeCourseGroup(2, "網路安全"),
            MakeCourseGroup(3, "資料庫", partnerCourseGroupCount: 1));

        var courseGroups = AssertOk(
            await controller.Query(new CourseGroupQuery { InUse = false }, CancellationToken.None));

        Assert.Equal(2, Assert.Single(courseGroups).Pkid);
    }

    [Fact]
    public async Task Query_WithKeywordAndInUse_AppliesBoth()
    {
        var (controller, _) = CreateController(
            MakeCourseGroup(1, "雲端技術", courseCount: 4),
            MakeCourseGroup(2, "雲端安全"),
            MakeCourseGroup(3, "資料庫", courseCount: 2));

        var courseGroups = AssertOk(await controller.Query(
            new CourseGroupQuery { Keyword = "雲端", InUse = false },
            CancellationToken.None));

        Assert.Equal(2, Assert.Single(courseGroups).Pkid);
    }

    // ---------- Get by id ----------

    [Fact]
    public async Task GetById_WhenFound_ReturnsTheCourseGroup()
    {
        var (controller, _) = CreateController(MakeCourseGroup(1, "雲端技術"));

        var courseGroup = AssertOk(await controller.GetById(1, CancellationToken.None));

        Assert.Equal("雲端技術", courseGroup.Description);
    }

    [Fact]
    public async Task GetById_WhenMissing_Returns404()
    {
        var (controller, _) = CreateController();

        var result = await controller.GetById(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---------- Create ----------

    [Fact]
    public async Task Create_Returns201WithTheGeneratedKey()
    {
        var (controller, repository) = CreateController();

        var result = await controller.Create(Request(description: "雲端技術"), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(CourseGroupsController.GetById), created.ActionName);
        Assert.Equal(1, created.RouteValues!["id"]);
        Assert.Equal<short[]>([1], repository.CreatedPkids.ToArray());

        var body = Assert.IsType<CourseGroup>(created.Value);
        Assert.Equal(1, body.Pkid);
        Assert.Equal("雲端技術", body.Description);
    }

    /// <summary>pkid is IDENTITY, so a key supplied by the caller is ignored.</summary>
    [Fact]
    public async Task Create_IgnoresAnyPkidInTheRequestBody()
    {
        var (controller, _) = CreateController(MakeCourseGroup(1, "雲端技術"));

        var result = await controller.Create(Request(pkid: 999, description: "資料庫"), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(2, Assert.IsType<CourseGroup>(created.Value).Pkid);
    }

    // ---------- Update ----------

    [Fact]
    public async Task Update_TakesTheKeyFromTheBodyAndReturnsTheUpdatedRow()
    {
        var (controller, repository) = CreateController(MakeCourseGroup(1, "雲端技術"));

        var courseGroup = AssertOk(
            await controller.Update(Request(pkid: 1, description: "雲端運算"), CancellationToken.None));

        Assert.Equal("雲端運算", courseGroup.Description);
        Assert.Equal<short[]>([1], repository.UpdatedPkids.ToArray());
    }

    [Fact]
    public async Task Update_PreservesTheProjectedReferenceCounts()
    {
        var (controller, _) = CreateController(
            MakeCourseGroup(1, "雲端技術", courseCount: 7, partnerCourseGroupCount: 2));

        var courseGroup = AssertOk(
            await controller.Update(Request(pkid: 1, description: "雲端運算"), CancellationToken.None));

        Assert.Equal(7, courseGroup.CourseCount);
        Assert.Equal(2, courseGroup.PartnerCourseGroupCount);
    }

    [Fact]
    public async Task Update_WhenMissing_Returns404()
    {
        var (controller, _) = CreateController();

        var result = await controller.Update(Request(pkid: 99), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_WhenUnreferenced_Returns204()
    {
        var (controller, repository) = CreateController(MakeCourseGroup(1, "雲端技術"));

        var result = await controller.Delete(1, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal<short[]>([1], repository.DeletedPkids.ToArray());
    }

    [Fact]
    public async Task Delete_WhenMissing_Returns404()
    {
        var (controller, _) = CreateController();

        var result = await controller.Delete(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    /// <summary>
    /// The cascade arm. FK_Course_CourseGroup is ON DELETE CASCADE, so the database would accept
    /// this delete and destroy the referencing Course rows — the 409 is the only thing stopping it.
    /// </summary>
    [Fact]
    public async Task Delete_WhenReferencedByCourses_Returns409AndDeletesNothing()
    {
        var (controller, repository) = CreateController(
            MakeCourseGroup(1, "雲端技術", courseCount: 12));

        var result = await controller.Delete(1, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Equal("課程群組仍被使用", problem.Title);
        Assert.Contains("12 Course row(s)", problem.Detail);
        Assert.Empty(repository.DeletedPkids);
    }

    /// <summary>The ordinary enforced-FK arm: PartnerCourseGroup would raise SQL error 547.</summary>
    [Fact]
    public async Task Delete_WhenReferencedByPartnerCourseGroups_Returns409AndDeletesNothing()
    {
        var (controller, repository) = CreateController(
            MakeCourseGroup(1, "雲端技術", partnerCourseGroupCount: 3));

        var result = await controller.Delete(1, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Contains("3 PartnerCourseGroup row(s)", problem.Detail);
        Assert.Empty(repository.DeletedPkids);
    }
}
