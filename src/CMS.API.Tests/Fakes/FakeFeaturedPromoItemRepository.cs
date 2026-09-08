using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IFeaturedPromoItemRepository"/> that mirrors the real repository's contract
/// (int IDENTITY key assignment, the training-centre and Monday..Sunday week filters, the
/// (day, centre, slot) uniqueness check, the slot swap, and day/centre/slot ordering) so
/// controller behaviour can be tested without SQL Server.
/// </summary>
public class FakeFeaturedPromoItemRepository : IFeaturedPromoItemRepository
{
    private readonly Dictionary<int, FeaturedPromoItem> _items = [];
    private int _nextPkid = 1;

    /// <summary>Promotions the fake resolves nav objects from, keyed by pkid.</summary>
    public Dictionary<int, PromotionLookup> Promotions { get; } = [];

    /// <summary>Training centres the fake resolves nav objects from, keyed by pkid.</summary>
    public Dictionary<short, TrainingCenterLookup> TrainingCenters { get; } = [];

    public List<int> CreatedPkids { get; } = [];
    public List<int> UpdatedPkids { get; } = [];
    public List<int> DeletedPkids { get; } = [];
    public List<(int Pkid, byte TargetSlot)> Moves { get; } = [];

    public FakeFeaturedPromoItemRepository Seed(params FeaturedPromoItem[] items)
    {
        foreach (var item in items)
        {
            _items[item.Pkid] = item;
            Promotions.TryAdd(item.PromotionPkid, item.Promotion);
            TrainingCenters.TryAdd(item.TrainingCenterPkid, item.TrainingCenter);
            if (item.Pkid >= _nextPkid)
            {
                _nextPkid = item.Pkid + 1;
            }
        }

        return this;
    }

    public Task<IEnumerable<FeaturedPromoItem>> GetAllAsync(CancellationToken cancellationToken = default)
        => QueryAsync(new FeaturedPromoItemQuery(), cancellationToken);

    public Task<IEnumerable<FeaturedPromoItem>> QueryAsync(
        FeaturedPromoItemQuery query,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<FeaturedPromoItem> results = _items.Values;

        if (query.TrainingCenterPkid is short trainingCenterPkid)
        {
            results = results.Where(i => i.TrainingCenterPkid == trainingCenterPkid);
        }

        if (query.WeekOf is DateOnly weekOf)
        {
            var (start, end) = FeaturedPromoItemSql.WeekOf(weekOf);
            results = results.Where(i => i.ScheduleOn >= start && i.ScheduleOn <= end);
        }

        return Task.FromResult(results
            .OrderBy(i => i.ScheduleOn)
            .ThenBy(i => i.TrainingCenterPkid)
            .ThenBy(i => i.Slot)
            .AsEnumerable());
    }

    public Task<FeaturedPromoItem?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default)
        => Task.FromResult(_items.GetValueOrDefault(pkid));

    public Task<bool> SlotTakenAsync(
        DateOnly scheduleOn,
        short trainingCenterPkid,
        byte slot,
        int? excludePkid,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_items.Values.Any(i =>
            i.ScheduleOn == scheduleOn
            && i.TrainingCenterPkid == trainingCenterPkid
            && i.Slot == slot
            && i.Pkid != excludePkid));

    /// <summary>The key is server-generated, exactly as the IDENTITY column is.</summary>
    public Task<int> CreateAsync(FeaturedPromoItemRequest request, CancellationToken cancellationToken = default)
    {
        var pkid = _nextPkid++;
        CreatedPkids.Add(pkid);
        _items[pkid] = FromRequest(pkid, request);
        return Task.FromResult(pkid);
    }

    public Task<bool> UpdateAsync(FeaturedPromoItemRequest request, CancellationToken cancellationToken = default)
    {
        if (!_items.ContainsKey(request.Pkid))
        {
            return Task.FromResult(false);
        }

        UpdatedPkids.Add(request.Pkid);
        _items[request.Pkid] = FromRequest(request.Pkid, request);
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default)
    {
        var removed = _items.Remove(pkid);
        if (removed)
        {
            DeletedPkids.Add(pkid);
        }

        return Task.FromResult(removed);
    }

    /// <summary>Swaps with the occupant of the target slot when there is one, as the SQL does.</summary>
    public Task<bool> MoveToSlotAsync(int pkid, byte targetSlot, CancellationToken cancellationToken = default)
    {
        if (!_items.TryGetValue(pkid, out var current))
        {
            return Task.FromResult(false);
        }

        Moves.Add((pkid, targetSlot));

        var neighbour = _items.Values.FirstOrDefault(i =>
            i.Pkid != pkid
            && i.ScheduleOn == current.ScheduleOn
            && i.TrainingCenterPkid == current.TrainingCenterPkid
            && i.Slot == targetSlot);

        if (neighbour is not null)
        {
            neighbour.Slot = current.Slot;
        }

        current.Slot = targetSlot;
        return Task.FromResult(true);
    }

    private FeaturedPromoItem FromRequest(int pkid, FeaturedPromoItemRequest request) => new()
    {
        Pkid = pkid,
        ScheduleOn = request.ScheduleOn,
        TrainingCenterPkid = request.TrainingCenterPkid,
        Slot = request.Slot,
        PromotionPkid = request.PromotionPkid,
        Topic = request.Topic.Trim(),
        Description = request.Description.Trim(),
        TrainingCenter = TrainingCenters.GetValueOrDefault(request.TrainingCenterPkid)
            ?? new TrainingCenterLookup { Pkid = request.TrainingCenterPkid },
        Promotion = Promotions.GetValueOrDefault(request.PromotionPkid)
            ?? new PromotionLookup { Pkid = request.PromotionPkid },
    };
}
