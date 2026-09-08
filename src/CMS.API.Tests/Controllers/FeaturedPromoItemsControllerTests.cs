using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Tests.Controllers;

public class FeaturedPromoItemsControllerTests
{
    /// <summary>2026-03-16 is a Monday; the week runs to Sunday 2026-03-22.</summary>
    private static readonly DateOnly Monday = new(2026, 3, 16);
    private static readonly DateOnly Sunday = new(2026, 3, 22);

    private static readonly TrainingCenterLookup Taipei = new() { Pkid = 1, Name = "台北", AppKey = "TPE", IsDefault = true };
    private static readonly TrainingCenterLookup Hsinchu = new() { Pkid = 2, Name = "新竹", AppKey = "HSC" };

    private static readonly PromotionLookup SkillTrainAi = new()
    {
        Pkid = 10,
        PromoCode = "20251204_SkillTrainAI",
        Topic = "成為能AI協作的程式設計師",
        Description = "轉職就業養成班",
    };

    private static readonly PromotionLookup GoogleAi = new()
    {
        Pkid = 11,
        PromoCode = "251211_GoogleAI",
        Topic = "Google AI工具一次掌握",
        Description = "不需技術基礎",
    };

    private static FeaturedPromoItem MakeItem(
        int pkid,
        DateOnly? scheduleOn = null,
        TrainingCenterLookup? trainingCenter = null,
        byte slot = 1,
        PromotionLookup? promotion = null)
    {
        trainingCenter ??= Taipei;
        promotion ??= SkillTrainAi;
        return new FeaturedPromoItem
        {
            Pkid = pkid,
            ScheduleOn = scheduleOn ?? Monday,
            TrainingCenterPkid = trainingCenter.Pkid,
            Slot = slot,
            PromotionPkid = promotion.Pkid,
            Topic = promotion.Topic,
            Description = promotion.Description,
            TrainingCenter = trainingCenter,
            Promotion = promotion,
        };
    }

    private static FeaturedPromoItemRequest Request(
        int pkid = 0,
        DateOnly? scheduleOn = null,
        short trainingCenterPkid = 1,
        byte slot = 1,
        int promotionPkid = 10,
        string topic = "成為能AI協作的程式設計師",
        string description = "轉職就業養成班")
        => new()
        {
            Pkid = pkid,
            ScheduleOn = scheduleOn ?? Monday,
            TrainingCenterPkid = trainingCenterPkid,
            Slot = slot,
            PromotionPkid = promotionPkid,
            Topic = topic,
            Description = description,
        };

    private static (FeaturedPromoItemsController Controller, FakeFeaturedPromoItemRepository Repository) CreateController(
        params FeaturedPromoItem[] seed)
    {
        var repository = new FakeFeaturedPromoItemRepository().Seed(seed);
        repository.Promotions.TryAdd(SkillTrainAi.Pkid, SkillTrainAi);
        repository.Promotions.TryAdd(GoogleAi.Pkid, GoogleAi);
        repository.TrainingCenters.TryAdd(Taipei.Pkid, Taipei);
        repository.TrainingCenters.TryAdd(Hsinchu.Pkid, Hsinchu);
        return (new FeaturedPromoItemsController(repository), repository);
    }

    private static T AssertOk<T>(ActionResult<T> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<T>(ok.Value);
    }

    // ---------- List ----------

    [Fact]
    public async Task GetAll_ReturnsItemsOrderedByDayCentreAndSlot()
    {
        var (controller, _) = CreateController(
            MakeItem(1, Monday.AddDays(1), Taipei, slot: 1),
            MakeItem(2, Monday, Hsinchu, slot: 1),
            MakeItem(3, Monday, Taipei, slot: 2),
            MakeItem(4, Monday, Taipei, slot: 1));

        var items = AssertOk(await controller.GetAll(CancellationToken.None)).ToList();

        Assert.Equal([4, 3, 2, 1], items.Select(i => i.Pkid).ToArray());
    }

    [Fact]
    public async Task GetAll_CarriesBothNavObjects()
    {
        var (controller, _) = CreateController(MakeItem(1, promotion: GoogleAi));

        var item = Assert.Single(AssertOk(await controller.GetAll(CancellationToken.None)));

        Assert.Equal("台北", item.TrainingCenter.Name);
        Assert.Equal("251211_GoogleAI", item.Promotion.PromoCode);
        Assert.Equal(11, item.PromotionPkid);
    }

    // ---------- List (query filter) ----------

    [Fact]
    public async Task Query_WithoutFilters_ReturnsEverything()
    {
        var (controller, _) = CreateController(
            MakeItem(1, Monday, Taipei),
            MakeItem(2, Monday.AddDays(30), Hsinchu));

        var items = AssertOk(await controller.Query(new FeaturedPromoItemQuery(), CancellationToken.None));

        Assert.Equal(2, items.Count());
    }

    [Fact]
    public async Task Query_WithNullBody_ReturnsEverything()
    {
        var (controller, _) = CreateController(MakeItem(1), MakeItem(2, slot: 2));

        var items = AssertOk(await controller.Query(null!, CancellationToken.None));

        Assert.Equal(2, items.Count());
    }

    [Fact]
    public async Task Query_WithTrainingCenter_ReturnsOnlyThatCentresItems()
    {
        var (controller, _) = CreateController(
            MakeItem(1, Monday, Taipei),
            MakeItem(2, Monday, Hsinchu),
            MakeItem(3, Monday.AddDays(2), Hsinchu));

        var items = AssertOk(await controller.Query(
            new FeaturedPromoItemQuery { TrainingCenterPkid = 2 },
            CancellationToken.None));

        Assert.Equal([2, 3], items.Select(i => i.Pkid).ToArray());
        Assert.All(items, i => Assert.Equal("新竹", i.TrainingCenter.Name));
    }

    /// <summary>
    /// The one-week filter: any date in the week selects Monday..Sunday inclusive. Items on the
    /// Sunday before and the Monday after fall outside it.
    /// </summary>
    [Fact]
    public async Task Query_WithWeekOf_ReturnsMondayThroughSundayInclusive()
    {
        var (controller, _) = CreateController(
            MakeItem(1, Monday.AddDays(-1)),
            MakeItem(2, Monday),
            MakeItem(3, Monday.AddDays(3)),
            MakeItem(4, Sunday),
            MakeItem(5, Sunday.AddDays(1)));

        var items = AssertOk(await controller.Query(
            new FeaturedPromoItemQuery { WeekOf = Monday.AddDays(3) },
            CancellationToken.None));

        Assert.Equal([2, 3, 4], items.Select(i => i.Pkid).ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(6)]
    public async Task Query_WithAnyDayOfTheWeek_SelectsTheSameWeek(int dayOffset)
    {
        var (controller, _) = CreateController(
            MakeItem(1, Monday),
            MakeItem(2, Sunday),
            MakeItem(3, Sunday.AddDays(1)));

        var items = AssertOk(await controller.Query(
            new FeaturedPromoItemQuery { WeekOf = Monday.AddDays(dayOffset) },
            CancellationToken.None));

        Assert.Equal([1, 2], items.Select(i => i.Pkid).ToArray());
    }

    [Fact]
    public async Task Query_WithWeekOfAndTrainingCenter_AppliesBoth()
    {
        var (controller, _) = CreateController(
            MakeItem(1, Monday, Taipei),
            MakeItem(2, Monday, Hsinchu),
            MakeItem(3, Sunday.AddDays(1), Hsinchu));

        var items = AssertOk(await controller.Query(
            new FeaturedPromoItemQuery { WeekOf = Monday, TrainingCenterPkid = 2 },
            CancellationToken.None));

        Assert.Equal(2, Assert.Single(items).Pkid);
    }

    // ---------- Get by id ----------

    [Fact]
    public async Task GetById_WhenFound_ReturnsTheItem()
    {
        var (controller, _) = CreateController(MakeItem(1, slot: 2));

        var item = AssertOk(await controller.GetById(1, CancellationToken.None));

        Assert.Equal(2, item.Slot);
        Assert.Equal("20251204_SkillTrainAI", item.Promotion.PromoCode);
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
    public async Task Create_Returns201WithTheGeneratedKeyAndResolvedNavObjects()
    {
        var (controller, repository) = CreateController();

        var result = await controller.Create(Request(promotionPkid: 11), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(FeaturedPromoItemsController.GetById), created.ActionName);
        Assert.Equal(1, created.RouteValues!["id"]);
        Assert.Equal([1], repository.CreatedPkids.ToArray());

        var body = Assert.IsType<FeaturedPromoItem>(created.Value);
        Assert.Equal(1, body.Pkid);
        Assert.Equal(Monday, body.ScheduleOn);
        Assert.Equal("251211_GoogleAI", body.Promotion.PromoCode);
        Assert.Equal("台北", body.TrainingCenter.Name);
    }

    /// <summary>pkid is IDENTITY, so a key supplied by the caller is ignored.</summary>
    [Fact]
    public async Task Create_IgnoresAnyPkidInTheRequestBody()
    {
        var (controller, _) = CreateController(MakeItem(1));

        var result = await controller.Create(Request(pkid: 999, slot: 2), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(2, Assert.IsType<FeaturedPromoItem>(created.Value).Pkid);
    }

    /// <summary>IX_FeaturedPromoItem_UniqueDateLocSlot — the cell is already occupied.</summary>
    [Fact]
    public async Task Create_WhenSlotIsTaken_Returns409AndCreatesNothing()
    {
        var (controller, repository) = CreateController(MakeItem(1, Monday, Taipei, slot: 1));

        var result = await controller.Create(Request(scheduleOn: Monday, trainingCenterPkid: 1, slot: 1), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Equal("版位已被使用", problem.Title);
        Assert.Empty(repository.CreatedPkids);
    }

    [Fact]
    public async Task Create_WhenSameSlotOnAnotherCentre_Succeeds()
    {
        var (controller, repository) = CreateController(MakeItem(1, Monday, Taipei, slot: 1));

        var result = await controller.Create(Request(scheduleOn: Monday, trainingCenterPkid: 2, slot: 1), CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal([2], repository.CreatedPkids.ToArray());
    }

    // ---------- Update ----------

    [Fact]
    public async Task Update_TakesTheKeyFromTheBodyAndReturnsTheUpdatedRow()
    {
        var (controller, repository) = CreateController(MakeItem(1));

        var item = AssertOk(await controller.Update(
            Request(pkid: 1, promotionPkid: 11, topic: "  Google AI工具一次掌握  "),
            CancellationToken.None));

        Assert.Equal("Google AI工具一次掌握", item.Topic);
        Assert.Equal("251211_GoogleAI", item.Promotion.PromoCode);
        Assert.Equal([1], repository.UpdatedPkids.ToArray());
    }

    /// <summary>Re-saving a row into its own slot is not a conflict.</summary>
    [Fact]
    public async Task Update_KeepingTheSameSlot_Succeeds()
    {
        var (controller, _) = CreateController(MakeItem(1, Monday, Taipei, slot: 2));

        var item = AssertOk(await controller.Update(Request(pkid: 1, slot: 2), CancellationToken.None));

        Assert.Equal(2, item.Slot);
    }

    [Fact]
    public async Task Update_IntoAnotherRowsSlot_Returns409AndUpdatesNothing()
    {
        var (controller, repository) = CreateController(
            MakeItem(1, Monday, Taipei, slot: 1),
            MakeItem(2, Monday, Taipei, slot: 2));

        var result = await controller.Update(Request(pkid: 2, slot: 1), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal("版位已被使用", Assert.IsType<ProblemDetails>(conflict.Value).Title);
        Assert.Empty(repository.UpdatedPkids);
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
    public async Task Delete_WhenFound_Returns204()
    {
        var (controller, repository) = CreateController(MakeItem(1));

        var result = await controller.Delete(1, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal([1], repository.DeletedPkids.ToArray());
    }

    [Fact]
    public async Task Delete_WhenMissing_Returns404()
    {
        var (controller, _) = CreateController();

        var result = await controller.Delete(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    // ---------- Move slot ----------

    /// <summary>The "+" action: 1 → 2, swapping with whatever sat on 2.</summary>
    [Fact]
    public async Task MoveDown_SwapsWithTheOccupantOfTheNextSlot()
    {
        var (controller, repository) = CreateController(
            MakeItem(1, Monday, Taipei, slot: 1, promotion: SkillTrainAi),
            MakeItem(2, Monday, Taipei, slot: 2, promotion: GoogleAi));

        var moved = AssertOk(await controller.MoveDown(1, CancellationToken.None));

        Assert.Equal(2, moved.Slot);
        Assert.Equal([(1, (byte)2)], repository.Moves.ToArray());
        var neighbour = await repository.GetByIdAsync(2);
        Assert.Equal(1, neighbour!.Slot);
    }

    /// <summary>The "-" action: 2 → 1.</summary>
    [Fact]
    public async Task MoveUp_MovesIntoAnEmptySlotWithoutASwap()
    {
        var (controller, repository) = CreateController(MakeItem(1, Monday, Taipei, slot: 2));

        var moved = AssertOk(await controller.MoveUp(1, CancellationToken.None));

        Assert.Equal(1, moved.Slot);
        Assert.Equal([(1, (byte)1)], repository.Moves.ToArray());
    }

    [Fact]
    public async Task MoveUp_FromTheFirstSlot_Returns400AndMovesNothing()
    {
        var (controller, repository) = CreateController(MakeItem(1, slot: 1));

        var result = await controller.MoveUp(1, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("版位無法再移動", Assert.IsType<ProblemDetails>(bad.Value).Title);
        Assert.Empty(repository.Moves);
    }

    [Fact]
    public async Task MoveDown_FromTheLastSlot_Returns400AndMovesNothing()
    {
        var (controller, repository) = CreateController(MakeItem(1, slot: 3));

        var result = await controller.MoveDown(1, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(repository.Moves);
    }

    [Fact]
    public async Task Move_OnlySwapsWithinTheSameDayAndCentre()
    {
        var (controller, repository) = CreateController(
            MakeItem(1, Monday, Taipei, slot: 1),
            MakeItem(2, Monday, Hsinchu, slot: 2),
            MakeItem(3, Monday.AddDays(1), Taipei, slot: 2));

        AssertOk(await controller.MoveDown(1, CancellationToken.None));

        Assert.Equal(2, (await repository.GetByIdAsync(2))!.Slot);
        Assert.Equal(2, (await repository.GetByIdAsync(3))!.Slot);
    }

    [Fact]
    public async Task Move_WhenMissing_Returns404()
    {
        var (controller, _) = CreateController();

        var result = await controller.MoveDown(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }
}
