using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Tests.Controllers;

public class PartnersControllerTests
{
    private static Partner MakePartner(
        short pkid,
        string name,
        string appKey = "AK",
        string? nameOnPartnerMenu = null,
        string? nameOnCourseDetailPage = null,
        int displayOrder = 0,
        string? imageFilename = null,
        int certificationCount = 0,
        int courseCount = 0,
        int courseGroupCount = 0,
        int promotionCount = 0,
        int seminarCount = 0)
        => new()
        {
            Pkid = pkid,
            Name = name,
            AppKey = appKey,
            NameOnPartnerMenu = nameOnPartnerMenu ?? name,
            NameOnCourseDetailPage = nameOnCourseDetailPage ?? name,
            DisplayOrder = displayOrder,
            ImageFilename = imageFilename,
            CertificationCount = certificationCount,
            CourseCount = courseCount,
            CourseGroupCount = courseGroupCount,
            PromotionCount = promotionCount,
            SeminarCount = seminarCount,
        };

    private static PartnerRequest Request(
        short pkid = 0,
        string name = "Microsoft",
        string appKey = "MS",
        string nameOnPartnerMenu = "微軟 Microsoft",
        string nameOnCourseDetailPage = "微軟",
        int displayOrder = 0,
        string? imageFilename = null)
        => new()
        {
            Pkid = pkid,
            Name = name,
            AppKey = appKey,
            NameOnPartnerMenu = nameOnPartnerMenu,
            NameOnCourseDetailPage = nameOnCourseDetailPage,
            DisplayOrder = displayOrder,
            ImageFilename = imageFilename,
        };

    private static (PartnersController Controller, FakePartnerRepository Repository) CreateController(
        params Partner[] seed)
    {
        var repository = new FakePartnerRepository().Seed(seed);
        return (new PartnersController(repository), repository);
    }

    private static T AssertOk<T>(ActionResult<T> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<T>(ok.Value);
    }

    // ---------- List ----------

    [Fact]
    public async Task GetAll_ReturnsPartnersOrderedByDisplayOrderThenPkid()
    {
        var (controller, _) = CreateController(
            MakePartner(3, "Cisco", displayOrder: 2),
            MakePartner(1, "Oracle", displayOrder: 5),
            MakePartner(2, "Microsoft", displayOrder: 2));

        var partners = AssertOk(await controller.GetAll(CancellationToken.None)).ToList();

        Assert.Equal<short[]>([2, 3, 1], partners.Select(p => p.Pkid).ToArray());
    }

    [Fact]
    public async Task GetAll_IncludesAllFiveReferenceCounts()
    {
        var (controller, _) = CreateController(MakePartner(
            1,
            "Microsoft",
            certificationCount: 4,
            courseCount: 12,
            courseGroupCount: 3,
            promotionCount: 2,
            seminarCount: 1));

        var partner = Assert.Single(AssertOk(await controller.GetAll(CancellationToken.None)));

        Assert.Equal(4, partner.CertificationCount);
        Assert.Equal(12, partner.CourseCount);
        Assert.Equal(3, partner.CourseGroupCount);
        Assert.Equal(2, partner.PromotionCount);
        Assert.Equal(1, partner.SeminarCount);
    }

    // ---------- List (query filter) ----------

    [Fact]
    public async Task Query_WithoutFilters_ReturnsEverything()
    {
        var (controller, _) = CreateController(MakePartner(1, "Microsoft"), MakePartner(2, "Cisco"));

        var partners = AssertOk(await controller.Query(new PartnerQuery(), CancellationToken.None));

        Assert.Equal(2, partners.Count());
    }

    [Fact]
    public async Task Query_WithKeyword_MatchesName()
    {
        var (controller, _) = CreateController(MakePartner(1, "Microsoft"), MakePartner(2, "Cisco"));

        var partners = AssertOk(await controller.Query(
            new PartnerQuery { Keyword = "cisco" }, CancellationToken.None));

        Assert.Equal(2, Assert.Single(partners).Pkid);
    }

    [Fact]
    public async Task Query_WithKeyword_MatchesAppKey()
    {
        var (controller, _) = CreateController(
            MakePartner(1, "Microsoft", appKey: "MS"),
            MakePartner(2, "Cisco", appKey: "CSCO"));

        var partners = AssertOk(await controller.Query(
            new PartnerQuery { Keyword = "CSCO" }, CancellationToken.None));

        Assert.Equal(2, Assert.Single(partners).Pkid);
    }

    [Fact]
    public async Task Query_WithKeyword_MatchesTheTwoDisplayNameColumns()
    {
        var (controller, _) = CreateController(
            MakePartner(1, "Microsoft", nameOnPartnerMenu: "微軟認證課程"),
            MakePartner(2, "Cisco", nameOnCourseDetailPage: "思科"));

        var byMenu = AssertOk(await controller.Query(
            new PartnerQuery { Keyword = "微軟認證" }, CancellationToken.None));
        var byDetailPage = AssertOk(await controller.Query(
            new PartnerQuery { Keyword = "思科" }, CancellationToken.None));

        Assert.Equal(1, Assert.Single(byMenu).Pkid);
        Assert.Equal(2, Assert.Single(byDetailPage).Pkid);
    }

    [Fact]
    public async Task Query_WithHasImageTrue_ReturnsOnlyPartnersWithAnImage()
    {
        var (controller, _) = CreateController(
            MakePartner(1, "Microsoft", imageFilename: "ms.png"),
            MakePartner(2, "Cisco"));

        var partners = AssertOk(await controller.Query(
            new PartnerQuery { HasImage = true }, CancellationToken.None));

        Assert.Equal(1, Assert.Single(partners).Pkid);
    }

    [Fact]
    public async Task Query_WithHasImageFalse_ReturnsPartnersWithNullOrBlankFilename()
    {
        var (controller, _) = CreateController(
            MakePartner(1, "Microsoft", imageFilename: "ms.png"),
            MakePartner(2, "Cisco"),
            MakePartner(3, "Oracle", imageFilename: string.Empty));

        var partners = AssertOk(await controller.Query(
            new PartnerQuery { HasImage = false }, CancellationToken.None)).ToList();

        Assert.Equal<short[]>([2, 3], partners.Select(p => p.Pkid).ToArray());
    }

    [Fact]
    public async Task Query_WithCombinedFilters_AppliesAll()
    {
        var (controller, _) = CreateController(
            MakePartner(1, "Microsoft", imageFilename: "ms.png"),
            MakePartner(2, "Microsoft Azure"),
            MakePartner(3, "Cisco"));

        var partners = AssertOk(await controller.Query(
            new PartnerQuery { Keyword = "Microsoft", HasImage = false }, CancellationToken.None));

        Assert.Equal(2, Assert.Single(partners).Pkid);
    }

    [Fact]
    public async Task Query_WithNoMatches_ReturnsEmptyList()
    {
        var (controller, _) = CreateController(MakePartner(1, "Microsoft"));

        var partners = AssertOk(await controller.Query(
            new PartnerQuery { Keyword = "nope" }, CancellationToken.None));

        Assert.Empty(partners);
    }

    [Fact]
    public async Task Query_WithNullBody_ReturnsEverything()
    {
        var (controller, _) = CreateController(MakePartner(1, "Microsoft"));

        var partners = AssertOk(await controller.Query(null!, CancellationToken.None));

        Assert.Single(partners);
    }

    // ---------- View ----------

    [Fact]
    public async Task GetById_ReturnsPartner()
    {
        var (controller, _) = CreateController(MakePartner(
            2,
            "Cisco",
            appKey: "CSCO",
            imageFilename: "cisco.png",
            courseCount: 7));

        var partner = AssertOk(await controller.GetById(2, CancellationToken.None));

        Assert.Equal("Cisco", partner.Name);
        Assert.Equal("CSCO", partner.AppKey);
        Assert.Equal("cisco.png", partner.ImageFilename);
        Assert.Equal(7, partner.CourseCount);
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
    public async Task Create_ReturnsCreatedAtActionWithTheGeneratedPkid()
    {
        var (controller, repository) = CreateController(MakePartner(1, "Microsoft"));

        var result = await controller.Create(Request(name: "Cisco", appKey: "CSCO"), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(PartnersController.GetById), created.ActionName);
        Assert.Equal(2, created.RouteValues!["id"]);
        Assert.Equal<short[]>([2], repository.CreatedPkids.ToArray());

        var partner = Assert.IsType<Partner>(created.Value);
        Assert.Equal(2, partner.Pkid);
        Assert.Equal("Cisco", partner.Name);
        Assert.Equal("CSCO", partner.AppKey);
    }

    [Fact]
    public async Task Create_IgnoresAnyPkidSuppliedInTheBody()
    {
        var (controller, repository) = CreateController();

        // pkid is IDENTITY: the column generates the key, whatever the caller sent.
        await controller.Create(Request(pkid: 999, name: "Oracle"), CancellationToken.None);

        Assert.Equal<short[]>([1], repository.CreatedPkids.ToArray());
        Assert.Null(await repository.GetByIdAsync(999));
        Assert.NotNull(await repository.GetByIdAsync(1));
    }

    [Fact]
    public async Task Create_PersistsEveryWritableColumn()
    {
        var (controller, repository) = CreateController();

        await controller.Create(
            Request(
                name: "Oracle",
                appKey: "ORCL",
                nameOnPartnerMenu: "甲骨文 Oracle",
                nameOnCourseDetailPage: "甲骨文",
                displayOrder: 7,
                imageFilename: "oracle.png"),
            CancellationToken.None);

        var stored = await repository.GetByIdAsync(1);
        Assert.NotNull(stored);
        Assert.Equal("甲骨文 Oracle", stored.NameOnPartnerMenu);
        Assert.Equal("甲骨文", stored.NameOnCourseDetailPage);
        Assert.Equal(7, stored.DisplayOrder);
        Assert.Equal("oracle.png", stored.ImageFilename);
    }

    // ---------- Edit ----------

    [Fact]
    public async Task Update_TakesKeyFromBodyAndReturnsUpdatedPartner()
    {
        var (controller, repository) = CreateController(MakePartner(2, "Cisco"));

        var result = await controller.Update(
            Request(pkid: 2, name: "Cisco Systems", appKey: "CSCO", displayOrder: 3),
            CancellationToken.None);

        var partner = AssertOk(result);
        Assert.Equal(2, partner.Pkid);
        Assert.Equal("Cisco Systems", partner.Name);
        Assert.Equal(3, partner.DisplayOrder);
        Assert.Equal<short[]>([2], repository.UpdatedPkids.ToArray());
    }

    [Fact]
    public async Task Update_LeavesReferenceCountsUntouched()
    {
        var (controller, _) = CreateController(
            MakePartner(2, "Cisco", courseCount: 5, certificationCount: 2, seminarCount: 1));

        var partner = AssertOk(await controller.Update(
            Request(pkid: 2, name: "Cisco Systems"), CancellationToken.None));

        Assert.Equal(5, partner.CourseCount);
        Assert.Equal(2, partner.CertificationCount);
        Assert.Equal(1, partner.SeminarCount);
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
    public async Task Delete_RemovesPartnerAndReturns204()
    {
        var (controller, repository) = CreateController(MakePartner(1, "Microsoft"));

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
        var (controller, repository) = CreateController(MakePartner(1, "Microsoft", courseCount: 3));

        var result = await controller.Delete(1, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.NotNull(await repository.GetByIdAsync(1));
        Assert.Empty(repository.DeletedPkids);
    }

    [Theory]
    [InlineData(1, 0, 0, 0, 0)]
    [InlineData(0, 1, 0, 0, 0)]
    [InlineData(0, 0, 1, 0, 0)]
    [InlineData(0, 0, 0, 1, 0)]
    [InlineData(0, 0, 0, 0, 1)]
    public async Task Delete_WhenReferencedByAnyChildTable_Returns409(
        int certificationCount,
        int courseCount,
        int courseGroupCount,
        int promotionCount,
        int seminarCount)
    {
        // Seminar is in the list even though its FK is not enforced — the database would let that
        // delete through and orphan the rows, so the controller has to be the one to stop it.
        var (controller, _) = CreateController(MakePartner(
            1,
            "Microsoft",
            certificationCount: certificationCount,
            courseCount: courseCount,
            courseGroupCount: courseGroupCount,
            promotionCount: promotionCount,
            seminarCount: seminarCount));

        Assert.IsType<ConflictObjectResult>(await controller.Delete(1, CancellationToken.None));
    }
}
