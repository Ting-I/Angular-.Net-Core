using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Tests.Controllers;

public class LookupsControllerTests
{
    [Fact]
    public async Task GetAppUsers_ReturnsLookupList()
    {
        var controller = new LookupsController(new FakeLookupRepository
        {
            AppUsers =
            [
                new AppUserLookup { UserId = "helen", UserName = "helen", IsActive = true },
                new AppUserLookup { UserId = "miles", UserName = "Miles Sun", IsActive = true },
            ],
        });

        var result = await controller.GetAppUsers(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var users = Assert.IsAssignableFrom<IEnumerable<AppUserLookup>>(ok.Value).ToList();
        Assert.Equal(2, users.Count);
        Assert.Equal("helen", users[0].UserId);
    }

    [Fact]
    public async Task GetAppRoles_ReturnsLookupListOrderedByPermissionLevel()
    {
        var controller = new LookupsController(new FakeLookupRepository
        {
            AppRoles =
            [
                new AppRoleLookup { RoleId = "Admin", RoleName = "Administrator", PermissionLevel = 1 },
                new AppRoleLookup { RoleId = "User", RoleName = "User", PermissionLevel = 100 },
            ],
        });

        var result = await controller.GetAppRoles(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var roles = Assert.IsAssignableFrom<IEnumerable<AppRoleLookup>>(ok.Value).ToList();
        Assert.Equal(2, roles.Count);
        Assert.Equal("Admin", roles[0].RoleId);
        Assert.Equal(1, roles[0].PermissionLevel);
        Assert.Equal("User", roles[1].RoleName);
    }

    [Fact]
    public async Task GetPublishStatuses_ReturnsLookupList()
    {
        var controller = new LookupsController(new FakeLookupRepository
        {
            PublishStatuses =
            [
                new PublishStatusLookup { Pkid = 1, Description = "草稿" },
                new PublishStatusLookup { Pkid = 2, Description = "已發布" },
            ],
        });

        var result = await controller.GetPublishStatuses(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var statuses = Assert.IsAssignableFrom<IEnumerable<PublishStatusLookup>>(ok.Value).ToList();
        Assert.Equal(2, statuses.Count);
        Assert.Equal(1, statuses[0].Pkid);
        Assert.Equal("已發布", statuses[1].Description);
    }

    [Fact]
    public async Task GetPartners_ReturnsLookupList()
    {
        var controller = new LookupsController(new FakeLookupRepository
        {
            Partners =
            [
                new PartnerLookup { Pkid = 1, Name = "Microsoft", AppKey = "MS" },
                new PartnerLookup { Pkid = 2, Name = "Cisco", AppKey = "CSCO" },
            ],
        });

        var result = await controller.GetPartners(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var partners = Assert.IsAssignableFrom<IEnumerable<PartnerLookup>>(ok.Value).ToList();
        Assert.Equal(2, partners.Count);
        Assert.Equal(1, partners[0].Pkid);
        Assert.Equal("Cisco", partners[1].Name);
        Assert.Equal("CSCO", partners[1].AppKey);
    }

    [Fact]
    public async Task GetCourseGroups_ReturnsLookupList()
    {
        var controller = new LookupsController(new FakeLookupRepository
        {
            CourseGroups =
            [
                new CourseGroupLookup { Pkid = 2, Description = "網路安全" },
                new CourseGroupLookup { Pkid = 1, Description = "雲端技術" },
            ],
        });

        var result = await controller.GetCourseGroups(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var courseGroups = Assert.IsAssignableFrom<IEnumerable<CourseGroupLookup>>(ok.Value).ToList();
        Assert.Equal(2, courseGroups.Count);
        Assert.Equal(2, courseGroups[0].Pkid);
        Assert.Equal("雲端技術", courseGroups[1].Description);
    }

    [Fact]
    public async Task GetCertifications_ReturnsLookupListCarryingThePartner()
    {
        var controller = new LookupsController(new FakeLookupRepository
        {
            Certifications =
            [
                new CertificationLookup
                {
                    Pkid = 7,
                    Title = "Azure Administrator",
                    PartnerPkid = 1,
                    PartnerName = "Microsoft",
                },
                new CertificationLookup { Pkid = 9, Title = "CCNA", PartnerPkid = 2, PartnerName = "Cisco" },
            ],
        });

        var result = await controller.GetCertifications(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var certifications = Assert.IsAssignableFrom<IEnumerable<CertificationLookup>>(ok.Value).ToList();
        Assert.Equal(2, certifications.Count);
        Assert.Equal(7, certifications[0].Pkid);
        Assert.Equal("Microsoft", certifications[0].PartnerName);
        Assert.Equal("CCNA", certifications[1].Title);
    }

    [Fact]
    public async Task GetJobCategories_ReturnsLookupList()
    {
        var controller = new LookupsController(new FakeLookupRepository
        {
            JobCategories =
            [
                new JobCategoryLookup { Pkid = 3, Description = "系統管理" },
                new JobCategoryLookup { Pkid = 5, Description = "軟體開發" },
            ],
        });

        var result = await controller.GetJobCategories(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var jobCategories = Assert.IsAssignableFrom<IEnumerable<JobCategoryLookup>>(ok.Value).ToList();
        Assert.Equal(2, jobCategories.Count);
        Assert.Equal(3, jobCategories[0].Pkid);
        Assert.Equal("軟體開發", jobCategories[1].Description);
    }

    [Fact]
    public async Task GetTrainingCenters_ReturnsLookupListCarryingTheDefaultFlag()
    {
        var controller = new LookupsController(new FakeLookupRepository
        {
            TrainingCenters =
            [
                new TrainingCenterLookup { Pkid = 1, Name = "台北", AppKey = "TPE", IsDefault = true },
                new TrainingCenterLookup { Pkid = 2, Name = "新竹", AppKey = "HSC" },
            ],
        });

        var result = await controller.GetTrainingCenters(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var centres = Assert.IsAssignableFrom<IEnumerable<TrainingCenterLookup>>(ok.Value).ToList();
        Assert.Equal(2, centres.Count);
        Assert.Equal("台北", centres[0].Name);
        Assert.True(centres[0].IsDefault);
        Assert.False(centres[1].IsDefault);
    }

    private static FakeLookupRepository PromotionRepository() => new()
    {
        Promotions =
        [
            new PromotionLookup
            {
                Pkid = 10,
                PromoCode = "20251204_SkillTrainAI",
                Topic = "成為能AI協作的程式設計師",
                Description = "轉職就業養成班",
            },
            new PromotionLookup
            {
                Pkid = 11,
                PromoCode = "251211_GoogleAI",
                Topic = "Google AI工具一次掌握",
                Description = "不需技術基礎",
            },
            new PromotionLookup
            {
                Pkid = 12,
                PromoCode = "20251215_n8n",
                Topic = "n8n自動化三部曲",
                Description = "從自動化新手到企業級AI架構師",
            },
        ],
    };

    [Fact]
    public async Task SearchPromotions_ReturnsCodesContainingTheKeywordNewestFirst()
    {
        var controller = new LookupsController(PromotionRepository());

        var result = await controller.SearchPromotions("2025", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var promotions = Assert.IsAssignableFrom<IEnumerable<PromotionLookup>>(ok.Value).ToList();
        Assert.Equal(["20251215_n8n", "20251204_SkillTrainAI"], promotions.Select(p => p.PromoCode).ToArray());
    }

    [Fact]
    public async Task SearchPromotions_WithoutKeyword_ReturnsEverything()
    {
        var controller = new LookupsController(PromotionRepository());

        var result = await controller.SearchPromotions(null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(3, Assert.IsAssignableFrom<IEnumerable<PromotionLookup>>(ok.Value).Count());
    }

    /// <summary>The PromoCode → Promotion_pkid resolution the 上稿作業 form relies on.</summary>
    [Fact]
    public async Task GetPromotionByCode_WhenFound_ReturnsThePkidAndPrefillText()
    {
        var controller = new LookupsController(PromotionRepository());

        var result = await controller.GetPromotionByCode("251211_GoogleAI", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var promotion = Assert.IsType<PromotionLookup>(ok.Value);
        Assert.Equal(11, promotion.Pkid);
        Assert.Equal("Google AI工具一次掌握", promotion.Topic);
        Assert.Equal("不需技術基礎", promotion.Description);
    }

    [Fact]
    public async Task GetPromotionByCode_TrimsTheCode()
    {
        var controller = new LookupsController(PromotionRepository());

        var result = await controller.GetPromotionByCode("  20251215_n8n  ", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(12, Assert.IsType<PromotionLookup>(ok.Value).Pkid);
    }

    [Fact]
    public async Task GetPromotionByCode_WhenMissing_Returns404()
    {
        var controller = new LookupsController(PromotionRepository());

        var result = await controller.GetPromotionByCode("NOPE", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }
}
