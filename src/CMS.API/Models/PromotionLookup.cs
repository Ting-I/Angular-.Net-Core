namespace CMS.API.Models;

/// <summary>
/// Slim Promotion2 row. PromoCode is what the operator types; Topic and Description are carried
/// so a fresh 上稿作業 item can be pre-filled from the promotion it points at.
/// </summary>
public class PromotionLookup
{
    public int Pkid { get; set; }

    /// <summary>活動代碼 — unique (IX_Promotion2_UniquePromoCode).</summary>
    public string PromoCode { get; set; } = string.Empty;

    public string Topic { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}
