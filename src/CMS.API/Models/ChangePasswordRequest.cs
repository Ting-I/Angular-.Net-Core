using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// 變更密碼 — body of POST /api/auth/change-password.
///
/// Three plaintext passwords and no key, for the same reason <see cref="ProfileRequest"/> carries
/// no UserId: the account is the one the caller's token names, so there is nowhere in the body to
/// point the endpoint at somebody else's row. A UserId sent anyway has no property to bind to and
/// is discarded by the deserializer.
///
/// Nothing here is ever hashed by the client. The server is the only place that knows what
/// AppUser.PasswordHash holds, and a hash neither enters nor leaves through this model.
/// </summary>
public class ChangePasswordRequest
{
    /// <summary>目前密碼 — plaintext; the server hashes it to compare against the stored value.</summary>
    [Required(AllowEmptyStrings = false)]
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>新密碼 — plaintext; must clear <see cref="Security.PasswordPolicy"/>.</summary>
    [Required(AllowEmptyStrings = false)]
    public string NewPassword { get; set; } = string.Empty;

    /// <summary>確認新密碼 — must equal <see cref="NewPassword"/> exactly.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
