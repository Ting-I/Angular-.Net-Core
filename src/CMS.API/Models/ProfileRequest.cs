using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// 個人資料 — body of PUT /api/auth/profile.
///
/// UserName is the only property, and that is the point: the row this endpoint writes is chosen by
/// the caller's token, so there is deliberately nowhere in the body to name a UserId. A request
/// carrying one is not "rejected" — it has no property to bind to and is discarded by the
/// deserializer. Roles are absent for the same reason.
/// </summary>
public class ProfileRequest
{
    /// <summary>使用者名稱 — required; the server trims it before writing.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string UserName { get; set; } = string.Empty;
}
