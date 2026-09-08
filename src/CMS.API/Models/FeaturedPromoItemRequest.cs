using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>上稿作業 FeaturedPromoItem — write DTO for create and update.</summary>
public class FeaturedPromoItemRequest
{
    /// <summary>
    /// 主代碼 — the primary key. The column is int IDENTITY, so this is ignored on create and
    /// carries the key from the body on update.
    /// </summary>
    public int Pkid { get; set; }

    /// <summary>上稿日期</summary>
    [Required]
    public DateOnly ScheduleOn { get; set; }

    /// <summary>訓練中心</summary>
    [Range(1, short.MaxValue)]
    public short TrainingCenterPkid { get; set; }

    /// <summary>版位 — 1..3.</summary>
    [Range(FeaturedPromoItem.MinSlot, FeaturedPromoItem.MaxSlot)]
    public byte Slot { get; set; }

    /// <summary>活動 — resolved from Promotion2.PromoCode by the client before saving.</summary>
    [Range(1, int.MaxValue)]
    public int PromotionPkid { get; set; }

    /// <summary>主題</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public string Topic { get; set; } = string.Empty;

    /// <summary>說明</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(300)]
    public string Description { get; set; } = string.Empty;
}
