using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// 使用者 AppUser — write DTO for create and update.
/// There is no password field: the create path takes the default password from SysConfig and the
/// update path never touches PasswordHash. Resetting it is a separate endpoint.
/// </summary>
public class AppUserRequest
{
    /// <summary>使用者代碼 — the primary key; immutable once created.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>使用者名稱</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>啟用 — matches DF_AppUser_IsActive.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>角色 — AppRole.RoleId values linked via AppUserRole (n-n).</summary>
    public List<string> RoleIds { get; set; } = [];
}
