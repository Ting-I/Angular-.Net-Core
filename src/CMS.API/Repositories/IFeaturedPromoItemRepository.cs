using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IFeaturedPromoItemRepository
{
    Task<IEnumerable<FeaturedPromoItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<FeaturedPromoItem>> QueryAsync(
        FeaturedPromoItemQuery query,
        CancellationToken cancellationToken = default);

    Task<FeaturedPromoItem?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when another row already occupies (scheduleOn, trainingCenterPkid, slot) — the
    /// IX_FeaturedPromoItem_UniqueDateLocSlot key. <paramref name="excludePkid"/> lets an update
    /// ignore the row being edited.
    /// </summary>
    Task<bool> SlotTakenAsync(
        DateOnly scheduleOn,
        short trainingCenterPkid,
        byte slot,
        int? excludePkid,
        CancellationToken cancellationToken = default);

    Task<int> CreateAsync(FeaturedPromoItemRequest request, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(FeaturedPromoItemRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the row into <paramref name="targetSlot"/> on the same day and centre, swapping with
    /// whatever already sits there. Returns false when the row does not exist.
    /// </summary>
    Task<bool> MoveToSlotAsync(int pkid, byte targetSlot, CancellationToken cancellationToken = default);
}
