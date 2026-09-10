using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// 登入 — body of POST /api/auth/login.
/// The password is plaintext on the way in and is hashed before it is compared; it is never stored,
/// logged or echoed back.
/// </summary>
public class LoginRequest
{
    /// <summary>使用者代碼 — matched against AppUser.UserId exactly.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>密碼 — plaintext; hashed and compared against AppUser.PasswordHash, never stored.</summary>
    [Required(AllowEmptyStrings = false)]
    public string Password { get; set; } = string.Empty;
}
