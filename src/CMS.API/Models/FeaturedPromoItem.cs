namespace CMS.API.Models;

/// <summary>上稿作業 FeaturedPromoItem — response model.</summary>
public class FeaturedPromoItem
{
    /// <summary>First slot on a day. Slot is tinyint; the UI shows exactly three per day.</summary>
    public const int MinSlot = 1;

    /// <summary>Last slot on a day.</summary>
    public const int MaxSlot = 3;

    /// <summary>主代碼 — int IDENTITY primary key.</summary>
    public int Pkid { get; set; }

    /// <summary>上稿日期 — the day this item is featured on.</summary>
    public DateOnly ScheduleOn { get; set; }

    /// <summary>訓練中心 — FK to TrainingCenter.pkid.</summary>
    public short TrainingCenterPkid { get; set; }

    /// <summary>版位 — 1..3, unique per (ScheduleOn, TrainingCenter_pkid).</summary>
    public byte Slot { get; set; }

    /// <summary>活動 — FK to Promotion2.pkid.</summary>
    public int PromotionPkid { get; set; }

    /// <summary>主題</summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>說明</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>訓練中心 nav object — the tab the item belongs to.</summary>
    public TrainingCenterLookup TrainingCenter { get; set; } = new();

    /// <summary>活動 nav object — carries the PromoCode the operator keys the item by.</summary>
    public PromotionLookup Promotion { get; set; } = new();
}
