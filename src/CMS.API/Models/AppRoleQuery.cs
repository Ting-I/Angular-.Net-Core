namespace CMS.API.Models;

/// <summary>角色 AppRole — search DTO.</summary>
public class AppRoleQuery
{
    /// <summary>LIKE match against RoleId, RoleName and Description.</summary>
    public string? Keyword { get; set; }

    /// <summary>Exact match against PermissionLevel.</summary>
    public int? PermissionLevel { get; set; }
}
