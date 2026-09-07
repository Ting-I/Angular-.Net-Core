using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>角色 AppRole — write DTO for create and update.</summary>
public class AppRoleRequest
{
    /// <summary>角色代碼 — the primary key; immutable once created.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string RoleId { get; set; } = string.Empty;

    /// <summary>角色名稱</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string RoleName { get; set; } = string.Empty;

    /// <summary>權限等級</summary>
    [Range(0, int.MaxValue)]
    public int PermissionLevel { get; set; } = 100;

    /// <summary>描述</summary>
    [StringLength(400)]
    public string? Description { get; set; }

    /// <summary>使用者 — AppUser.UserId values linked via AppUserRole (n-n).</summary>
    public List<string> UserIds { get; set; } = [];
}
