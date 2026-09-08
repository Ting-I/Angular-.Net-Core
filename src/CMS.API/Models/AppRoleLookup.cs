namespace CMS.API.Models;

/// <summary>Slim AppRole row used for select / multiselect options.</summary>
public class AppRoleLookup
{
    /// <summary>角色代碼</summary>
    public string RoleId { get; set; } = string.Empty;

    /// <summary>角色名稱</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>權限等級</summary>
    public int PermissionLevel { get; set; }
}
