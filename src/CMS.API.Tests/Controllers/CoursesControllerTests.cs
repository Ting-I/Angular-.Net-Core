using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Tests.Controllers;

public class CoursesControllerTests
{
    private static readonly DateOnly DefaultOn = new(2026, 1, 1);
    private static readonly DateOnly DefaultOff = new(2036, 1, 1);

    internal static Course MakeCourse(
        int pkid,
        string courseId = "AZ-104",
        string title = "Azure 系統管理",
        int displayOrder = 1,
        short partnerPkid = 1,
        short? courseGroupPkid = 1,
        byte publishStatusPkid = 2,
        DateOnly? scheduleOn = null,
        DateOnly? scheduleOff = null,
        bool canRepeat = false,
        int courseFaqCount = 0,
        int courseRelatedLinkCount = 0,
        int hotCourseCount = 0,
        int courseRecommCount = 0,
        List<int>? certificationPkids = null,
        List<short>? jobCategoryPkids = null)
        => new()
        {
            Pkid = pkid,
            Title = title,
            OfficialTitle = $"{title} (Official)",
            CourseId = courseId,
            ProdCourseId = $"P-{courseId}",
            FriendlyUrl = courseId.ToLowerInvariant(),
            DisplayOrder = displayOrder,
            PartnerPkid = partnerPkid,
            CourseGroupPkid = courseGroupPkid,
            PublishStatusPkid = publishStatusPkid,
            ScheduleOn = scheduleOn ?? DefaultOn,
            ScheduleOff = scheduleOff ?? DefaultOff,
            Hour = 21,
            ListPrice = 24000m,
            LearningCredit = 3.5m,
            CanRepeat = canRepeat,
            Partner = new PartnerLookup { Pkid = partnerPkid, Name = "Microsoft", AppKey = "MS" },
            CourseGroup = courseGroupPkid is null
                ? null
                : new CourseGroupLookup { Pkid = courseGroupPkid.Value, Description = "雲端技術" },
            PublishStatus = new PublishStatusLookup { Pkid = publishStatusPkid, Description = "已上架" },
            CourseFaqCount = courseFaqCount,
            CourseRelatedLinkCount = courseRelatedLinkCount,
            HotCourseCount = hotCourseCount,
            CourseRecommCount = courseRecommCount,
            CertificationPkids = certificationPkids ?? [],
            JobCategoryPkids = jobCategoryPkids ?? [],
        };

    internal static CourseRequest Request(
        int pkid = 0,
        string courseId = "AZ-104",
        string title = "Azure 系統管理",
        short? courseGroupPkid = 1,
        List<int>? certificationPkids = null,
        List<short>? jobCategoryPkids = null)
        => new()
        {
            Pkid = pkid,
            Title = title,
            OfficialTitle = null,
            CourseId = courseId,
            ProdCourseId = $"P-{courseId}",
            FriendlyUrl = courseId.ToLowerInvariant(),
            DisplayOrder = 1,
            PartnerPkid = 1,
            CourseGroupPkid = courseGroupPkid,
            PublishStatusPkid = 2,
            ScheduleOn = DefaultOn,
            ScheduleOff = DefaultOff,
            Hour = 21,
            ListPrice = 24000m,
            LearningCredit = 3.5m,
            CanRepeat = false,
            CertificationPkids = certificationPkids ?? [],
            JobCategoryPkids = jobCategoryPkids ?? [],
        };

    /// <summary>
    /// A controller over seeded courses, for the CRUD arms. The logger and the AppUser store are the
    /// 課程簡介 export record's business only (see <c>CoursesControllerSheetTests</c>), so this
    /// overload hands them throwaways and keeps the two-value shape every CRUD test destructures.
    /// </summary>
    internal static (CoursesController Controller, FakeCourseRepository Repository) CreateController(
        params Course[] seed)
    {
        var repository = new FakeCourseRepository().Seed(seed);
        return (
            new CoursesController(
                repository,
                new FakeAppUserRepository(),
                new CapturingLogger<CoursesController>()),
            repository);
    }

    private static T AssertOk<T>(ActionResult<T> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<T>(ok.Value);
    }

    // ---------- List ----------

    [Fact]
    public async Task GetAll_ReturnsCoursesOrderedByDisplayOrderThenPkid()
    {
        var (controller, _) = CreateController(
            MakeCourse(3, "C", displayOrder: 2),
            MakeCourse(1, "A", displayOrder: 2),
            MakeCourse(2, "B", displayOrder: 1));

        var courses = AssertOk(await controller.GetAll(CancellationToken.None)).ToList();

        Assert.Equal([2, 1, 3], courses.Select(c => c.Pkid).ToArray());
    }

    [Fact]
    public async Task GetAll_IncludesTheThreeNavObjects()
    {
        var (controller, _) = CreateController(MakeCourse(1));

        var course = Assert.Single(AssertOk(await controller.GetAll(CancellationToken.None)));

        Assert.Equal("Microsoft", course.Partner?.Name);
        Assert.Equal("雲端技術", course.CourseGroup?.Description);
        Assert.Equal("已上架", course.PublishStatus?.Description);
    }

    /// <summary>CourseGroup_pkid is the one nullable FK, so its nav object may legitimately be null.</summary>
    [Fact]
    public async Task GetAll_LeavesTheCourseGroupNavObjectNullWhenTheForeignKeyIsNull()
    {
        var (controller, _) = CreateController(MakeCourse(1, courseGroupPkid: null));

        var course = Assert.Single(AssertOk(await controller.GetAll(CancellationToken.None)));

        Assert.Null(course.CourseGroupPkid);
        Assert.Null(course.CourseGroup);
        Assert.NotNull(course.Partner);
    }

    [Fact]
    public async Task GetAll_IncludesEveryChildReferenceCount()
    {
        var (controller, _) = CreateController(MakeCourse(
            1,
            courseFaqCount: 4,
            courseRelatedLinkCount: 3,
            hotCourseCount: 2,
            courseRecommCount: 1));

        var course = Assert.Single(AssertOk(await controller.GetAll(CancellationToken.None)));

        Assert.Equal(4, course.CourseFaqCount);
        Assert.Equal(3, course.CourseRelatedLinkCount);
        Assert.Equal(2, course.HotCourseCount);
        Assert.Equal(1, course.CourseRecommCount);
    }

    /// <summary>
    /// The n-n lists are a GetById concern only — the list endpoints would pay two round trips per
    /// row for data the table does not show.
    /// </summary>
    [Fact]
    public async Task GetAll_LeavesBothNnListsEmpty()
    {
        var (controller, _) = CreateController(
            MakeCourse(1, certificationPkids: [7, 9], jobCategoryPkids: [3]));

        var course = Assert.Single(AssertOk(await controller.GetAll(CancellationToken.None)));

        Assert.Empty(course.CertificationPkids);
        Assert.Empty(course.JobCategoryPkids);
    }

    // ---------- Query ----------

    [Fact]
    public async Task Query_WithoutFilters_ReturnsEverything()
    {
        var (controller, _) = CreateController(MakeCourse(1, "A"), MakeCourse(2, "B"));

        var courses = AssertOk(await controller.Query(new CourseQuery(), CancellationToken.None));

        Assert.Equal(2, courses.Count());
    }

    [Fact]
    public async Task Query_WithNullBody_ReturnsEverything()
    {
        var (controller, _) = CreateController(MakeCourse(1, "A"), MakeCourse(2, "B"));

        var courses = AssertOk(await controller.Query(null!, CancellationToken.None));

        Assert.Equal(2, courses.Count());
    }

    [Fact]
    public async Task Query_WithKeyword_SearchesTitleAndTheCodeColumns()
    {
        var (controller, _) = CreateController(
            MakeCourse(1, "AZ-104", "Azure 系統管理"),
            MakeCourse(2, "CCNA", "思科網路"));

        var byTitle = AssertOk(await controller.Query(
            new CourseQuery { Keyword = "Azure" }, CancellationToken.None));
        var byCode = AssertOk(await controller.Query(
            new CourseQuery { Keyword = "CCNA" }, CancellationToken.None));

        Assert.Equal(1, Assert.Single(byTitle).Pkid);
        Assert.Equal(2, Assert.Single(byCode).Pkid);
    }

    [Fact]
    public async Task Query_WithNoMatch_ReturnsEmpty()
    {
        var (controller, _) = CreateController(MakeCourse(1));

        var courses = AssertOk(await controller.Query(
            new CourseQuery { Keyword = "沒有這門課" }, CancellationToken.None));

        Assert.Empty(courses);
    }

    [Fact]
    public async Task Query_WithPartnerPkid_FiltersOnTheForeignKey()
    {
        var (controller, _) = CreateController(
            MakeCourse(1, "A", partnerPkid: 1),
            MakeCourse(2, "B", partnerPkid: 2));

        var courses = AssertOk(await controller.Query(
            new CourseQuery { PartnerPkid = 2 }, CancellationToken.None));

        Assert.Equal(2, Assert.Single(courses).Pkid);
    }

    [Fact]
    public async Task Query_WithCourseGroupPkid_FiltersOnTheForeignKey()
    {
        var (controller, _) = CreateController(
            MakeCourse(1, "A", courseGroupPkid: 1),
            MakeCourse(2, "B", courseGroupPkid: 5),
            MakeCourse(3, "C", courseGroupPkid: null));

        var courses = AssertOk(await controller.Query(
            new CourseQuery { CourseGroupPkid = 5 }, CancellationToken.None));

        Assert.Equal(2, Assert.Single(courses).Pkid);
    }

    [Fact]
    public async Task Query_WithPublishStatusPkid_FiltersOnTheForeignKey()
    {
        var (controller, _) = CreateController(
            MakeCourse(1, "A", publishStatusPkid: 1),
            MakeCourse(2, "B", publishStatusPkid: 2));

        var courses = AssertOk(await controller.Query(
            new CourseQuery { PublishStatusPkid = 1 }, CancellationToken.None));

        Assert.Equal(1, Assert.Single(courses).Pkid);
    }

    [Fact]
    public async Task Query_WithScheduleOnRange_IsInclusiveOnBothBounds()
    {
        var (controller, _) = CreateController(
            MakeCourse(1, "A", scheduleOn: new DateOnly(2026, 1, 1)),
            MakeCourse(2, "B", scheduleOn: new DateOnly(2026, 6, 15)),
            MakeCourse(3, "C", scheduleOn: new DateOnly(2026, 12, 31)));

        var courses = AssertOk(await controller.Query(
            new CourseQuery
            {
                ScheduleOnFrom = new DateOnly(2026, 1, 1),
                ScheduleOnTo = new DateOnly(2026, 12, 31),
            },
            CancellationToken.None));

        Assert.Equal(3, courses.Count());
    }

    [Fact]
    public async Task Query_WithOnlyTheLowerScheduleOnBound_LeavesTheRangeOpenEnded()
    {
        var (controller, _) = CreateController(
            MakeCourse(1, "A", scheduleOn: new DateOnly(2025, 12, 31)),
            MakeCourse(2, "B", scheduleOn: new DateOnly(2026, 6, 15)));

        var courses = AssertOk(await controller.Query(
            new CourseQuery { ScheduleOnFrom = new DateOnly(2026, 1, 1) }, CancellationToken.None));

        Assert.Equal(2, Assert.Single(courses).Pkid);
    }

    [Fact]
    public async Task Query_WithScheduleOffRange_FiltersOnTheOtherDateColumn()
    {
        var (controller, _) = CreateController(
            MakeCourse(1, "A", scheduleOff: new DateOnly(2026, 6, 30)),
            MakeCourse(2, "B", scheduleOff: new DateOnly(2030, 1, 1)));

        var courses = AssertOk(await controller.Query(
            new CourseQuery { ScheduleOffTo = new DateOnly(2027, 1, 1) }, CancellationToken.None));

        Assert.Equal(1, Assert.Single(courses).Pkid);
    }

    [Fact]
    public async Task Query_WithCanRepeatTrue_ReturnsOnlyRepeatableCourses()
    {
        var (controller, _) = CreateController(
            MakeCourse(1, "A", canRepeat: true),
            MakeCourse(2, "B", canRepeat: false));

        var courses = AssertOk(await controller.Query(
            new CourseQuery { CanRepeat = true }, CancellationToken.None));

        Assert.Equal(1, Assert.Single(courses).Pkid);
    }

    /// <summary>false (不允許) is a real filter, not an absence of one.</summary>
    [Fact]
    public async Task Query_WithCanRepeatFalse_ReturnsOnlyNonRepeatableCourses()
    {
        var (controller, _) = CreateController(
            MakeCourse(1, "A", canRepeat: true),
            MakeCourse(2, "B", canRepeat: false));

        var courses = AssertOk(await controller.Query(
            new CourseQuery { CanRepeat = false }, CancellationToken.None));

        Assert.Equal(2, Assert.Single(courses).Pkid);
    }

    [Fact]
    public async Task Query_WithSeveralFilters_AppliesThemTogether()
    {
        var (controller, _) = CreateController(
            MakeCourse(1, "AZ-104", "Azure 系統管理", partnerPkid: 1, canRepeat: true),
            MakeCourse(2, "AZ-204", "Azure 開發", partnerPkid: 1, canRepeat: false),
            MakeCourse(3, "CCNA", "思科網路", partnerPkid: 2, canRepeat: true));

        var courses = AssertOk(await controller.Query(
            new CourseQuery { Keyword = "Azure", PartnerPkid = 1, CanRepeat = true },
            CancellationToken.None));

        Assert.Equal(1, Assert.Single(courses).Pkid);
    }

    // ---------- Get by id ----------

    [Fact]
    public async Task GetById_WhenFound_ReturnsTheCourseWithBothNnLists()
    {
        var (controller, _) = CreateController(
            MakeCourse(1, certificationPkids: [9, 7], jobCategoryPkids: [3, 1]));

        var course = AssertOk(await controller.GetById(1, CancellationToken.None));

        Assert.Equal(1, course.Pkid);
        Assert.Equal([7, 9], course.CertificationPkids);
        Assert.Equal<short[]>([1, 3], [.. course.JobCategoryPkids]);
    }

    [Fact]
    public async Task GetById_WhenMissing_Returns404()
    {
        var (controller, _) = CreateController(MakeCourse(1));

        var result = await controller.GetById(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---------- Create ----------

    [Fact]
    public async Task Create_Returns201WithTheIdentityKeyAndRouteValue()
    {
        var (controller, repository) = CreateController();

        var result = await controller.Create(Request(courseId: "AZ-104"), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(CoursesController.GetById), created.ActionName);
        var pkid = Assert.Single(repository.CreatedPkids);
        Assert.Equal(pkid, created.RouteValues!["id"]);
        Assert.Equal("AZ-104", Assert.IsType<Course>(created.Value).CourseId);
    }

    /// <summary>pkid is IDENTITY, so a value supplied in the body is ignored.</summary>
    [Fact]
    public async Task Create_IgnoresAnyPkidInTheRequestBody()
    {
        var (controller, repository) = CreateController(MakeCourse(1));

        await controller.Create(Request(pkid: 999, courseId: "NEW"), CancellationToken.None);

        Assert.Equal(2, Assert.Single(repository.CreatedPkids));
    }

    [Fact]
    public async Task Create_WritesBothJunctionTables()
    {
        var (controller, repository) = CreateController();

        await controller.Create(
            Request(certificationPkids: [7, 9], jobCategoryPkids: [3]),
            CancellationToken.None);

        var pkid = Assert.Single(repository.CreatedPkids);
        Assert.Equal([7, 9], repository.CertificationLinks[pkid]);
        Assert.Equal<short[]>([3], [.. repository.JobCategoryLinks[pkid]]);
    }

    [Fact]
    public async Task Create_AcceptsANullCourseGroup()
    {
        var (controller, repository) = CreateController();

        await controller.Create(Request(courseGroupPkid: null), CancellationToken.None);

        var pkid = Assert.Single(repository.CreatedPkids);
        var created = await repository.GetByIdAsync(pkid);
        Assert.Null(created!.CourseGroupPkid);
    }

    // ---------- Update ----------

    [Fact]
    public async Task Update_TakesTheKeyFromTheBodyAndReturnsTheUpdatedCourse()
    {
        var (controller, repository) = CreateController(MakeCourse(1, "AZ-104", "舊名稱"));

        var course = AssertOk(await controller.Update(
            Request(pkid: 1, courseId: "AZ-104", title: "新名稱"), CancellationToken.None));

        Assert.Equal("新名稱", course.Title);
        Assert.Equal(1, Assert.Single(repository.UpdatedPkids));
    }

    [Fact]
    public async Task Update_RewritesBothJunctionTables()
    {
        var (controller, repository) = CreateController(
            MakeCourse(1, certificationPkids: [7, 9], jobCategoryPkids: [1, 3]));

        await controller.Update(
            Request(pkid: 1, certificationPkids: [9], jobCategoryPkids: []),
            CancellationToken.None);

        Assert.Equal([9], repository.CertificationLinks[1]);
        Assert.Empty(repository.JobCategoryLinks[1]);
    }

    /// <summary>The composite junction PKs reject a duplicate pair, so the list is deduplicated.</summary>
    [Fact]
    public async Task Update_DeduplicatesTheJunctionLists()
    {
        var (controller, repository) = CreateController(MakeCourse(1));

        await controller.Update(
            Request(pkid: 1, certificationPkids: [7, 7, 9]),
            CancellationToken.None);

        Assert.Equal([7, 9], repository.CertificationLinks[1]);
    }

    [Fact]
    public async Task Update_WhenMissing_Returns404()
    {
        var (controller, repository) = CreateController(MakeCourse(1));

        var result = await controller.Update(Request(pkid: 99), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        Assert.Empty(repository.UpdatedPkids);
    }

    [Fact]
    public async Task Update_LeavesTheReferenceCountsAlone()
    {
        var (controller, _) = CreateController(MakeCourse(1, courseFaqCount: 4));

        var course = AssertOk(await controller.Update(Request(pkid: 1), CancellationToken.None));

        Assert.Equal(4, course.CourseFaqCount);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_WhenUnreferenced_Returns204()
    {
        var (controller, repository) = CreateController(MakeCourse(1));

        var result = await controller.Delete(1, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(1, Assert.Single(repository.DeletedPkids));
    }

    [Fact]
    public async Task Delete_AlsoClearsBothJunctionTables()
    {
        var (controller, repository) = CreateController(
            MakeCourse(1, certificationPkids: [7], jobCategoryPkids: [3]));

        await controller.Delete(1, CancellationToken.None);

        Assert.False(repository.CertificationLinks.ContainsKey(1));
        Assert.False(repository.JobCategoryLinks.ContainsKey(1));
    }

    [Fact]
    public async Task Delete_WhenMissing_Returns404()
    {
        var (controller, repository) = CreateController(MakeCourse(1));

        var result = await controller.Delete(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(repository.DeletedPkids);
    }

    /// <summary>
    /// One case per referencing table. CourseRecomm is the important one: it has no FK constraint,
    /// so the database would accept the delete and orphan those rows silently.
    /// </summary>
    [Theory]
    [InlineData(1, 0, 0, 0)]
    [InlineData(0, 1, 0, 0)]
    [InlineData(0, 0, 1, 0)]
    [InlineData(0, 0, 0, 1)]
    public async Task Delete_WhenAnyChildTableStillReferencesIt_Returns409(
        int faqCount,
        int relatedLinkCount,
        int hotCourseCount,
        int recommCount)
    {
        var (controller, repository) = CreateController(MakeCourse(
            1,
            courseFaqCount: faqCount,
            courseRelatedLinkCount: relatedLinkCount,
            hotCourseCount: hotCourseCount,
            courseRecommCount: recommCount));

        var result = await controller.Delete(1, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Equal("課程仍被使用", problem.Title);
        Assert.Empty(repository.DeletedPkids);
    }

    /// <summary>
    /// The junction rows are this course's own and are deleted with it, so they must not block the
    /// delete the way the four child tables do.
    /// </summary>
    [Fact]
    public async Task Delete_IsNotBlockedByItsOwnJunctionRows()
    {
        var (controller, repository) = CreateController(
            MakeCourse(1, certificationPkids: [7, 9], jobCategoryPkids: [3]));

        var result = await controller.Delete(1, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(1, Assert.Single(repository.DeletedPkids));
    }
}
