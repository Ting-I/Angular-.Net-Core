using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>上稿作業 FeaturedPromoItem CRUD plus the slot-ordering actions.</summary>
[ApiController]
[Route("api/featured-promo-items")]
[Produces("application/json")]
public class FeaturedPromoItemsController : ControllerBase
{
    private readonly IFeaturedPromoItemRepository _repository;

    public FeaturedPromoItemsController(IFeaturedPromoItemRepository repository)
    {
        _repository = repository;
    }

    /// <summary>All featured promo items.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<FeaturedPromoItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<FeaturedPromoItem>>> GetAll(CancellationToken cancellationToken)
        => Ok(await _repository.GetAllAsync(cancellationToken));

    /// <summary>Filtered search — by training centre and by the Monday..Sunday week containing WeekOf.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<FeaturedPromoItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<FeaturedPromoItem>>> Query(
        [FromBody] FeaturedPromoItemQuery query,
        CancellationToken cancellationToken)
        => Ok(await _repository.QueryAsync(query ?? new FeaturedPromoItemQuery(), cancellationToken));

    /// <summary>Single featured promo item by pkid.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(FeaturedPromoItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeaturedPromoItem>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _repository.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>
    /// Create a featured promo item. pkid is IDENTITY — any value in the request body is ignored.
    /// Refuses with 409 when the (day, centre, slot) cell is already occupied, which is what the
    /// unique index would otherwise surface as a 500.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(FeaturedPromoItem), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FeaturedPromoItem>> Create(
        [FromBody] FeaturedPromoItemRequest request,
        CancellationToken cancellationToken)
    {
        if (await _repository.SlotTakenAsync(
                request.ScheduleOn, request.TrainingCenterPkid, request.Slot, excludePkid: null, cancellationToken))
        {
            return SlotTaken(request);
        }

        var pkid = await _repository.CreateAsync(request, cancellationToken);
        var created = await _repository.GetByIdAsync(pkid, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = pkid }, created);
    }

    /// <summary>Update a featured promo item. The key (pkid) comes from the body, not the route.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(FeaturedPromoItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FeaturedPromoItem>> Update(
        [FromBody] FeaturedPromoItemRequest request,
        CancellationToken cancellationToken)
    {
        if (await _repository.GetByIdAsync(request.Pkid, cancellationToken) is null)
        {
            return NotFound();
        }

        if (await _repository.SlotTakenAsync(
                request.ScheduleOn, request.TrainingCenterPkid, request.Slot, request.Pkid, cancellationToken))
        {
            return SlotTaken(request);
        }

        var updated = await _repository.UpdateAsync(request, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        return Ok(await _repository.GetByIdAsync(request.Pkid, cancellationToken));
    }

    /// <summary>Delete a featured promo item. Nothing references the table, so no 409 arm is needed.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        => await _repository.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    /// <summary>Move the item one slot up (e.g. 2 → 1) on its day, swapping with the occupant if any.</summary>
    [HttpPost("{id:int}/move-up")]
    [ProducesResponseType(typeof(FeaturedPromoItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<FeaturedPromoItem>> MoveUp(int id, CancellationToken cancellationToken)
        => MoveAsync(id, -1, cancellationToken);

    /// <summary>Move the item one slot down (e.g. 1 → 2) on its day, swapping with the occupant if any.</summary>
    [HttpPost("{id:int}/move-down")]
    [ProducesResponseType(typeof(FeaturedPromoItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<FeaturedPromoItem>> MoveDown(int id, CancellationToken cancellationToken)
        => MoveAsync(id, +1, cancellationToken);

    private async Task<ActionResult<FeaturedPromoItem>> MoveAsync(int id, int delta, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        var targetSlot = existing.Slot + delta;
        if (targetSlot is < FeaturedPromoItem.MinSlot or > FeaturedPromoItem.MaxSlot)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "版位無法再移動",
                Detail = $"FeaturedPromoItem '{id}' is already on slot {existing.Slot}; " +
                         $"slots run {FeaturedPromoItem.MinSlot}..{FeaturedPromoItem.MaxSlot}.",
            });
        }

        if (!await _repository.MoveToSlotAsync(id, (byte)targetSlot, cancellationToken))
        {
            return NotFound();
        }

        return Ok(await _repository.GetByIdAsync(id, cancellationToken));
    }

    private ConflictObjectResult SlotTaken(FeaturedPromoItemRequest request) => Conflict(new ProblemDetails
    {
        Status = StatusCodes.Status409Conflict,
        Title = "版位已被使用",
        Detail = $"Slot {request.Slot} on {request.ScheduleOn:yyyy-MM-dd} for TrainingCenter " +
                 $"'{request.TrainingCenterPkid}' is already occupied.",
    });
}
