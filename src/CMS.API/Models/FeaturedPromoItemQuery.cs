namespace CMS.API.Models;

/// <summary>上稿作業 FeaturedPromoItem — search DTO.</summary>
public class FeaturedPromoItemQuery
{
    /// <summary>Exact match on TrainingCenter_pkid — the active tab. null means every centre.</summary>
    public short? TrainingCenterPkid { get; set; }

    /// <summary>
    /// Any date inside the week to show. The server widens it to Monday..Sunday and filters
    /// ScheduleOn to that range, so the client never has to compute week bounds itself.
    /// </summary>
    public DateOnly? WeekOf { get; set; }
}
