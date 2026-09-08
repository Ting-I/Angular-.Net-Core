namespace CMS.API.Models;

/// <summary>使用者 AppUser — search DTO.</summary>
public class AppUserQuery
{
    /// <summary>LIKE match against UserId and UserName.</summary>
    public string? Keyword { get; set; }

    /// <summary>Tri-state: null keeps the filter off, false (停用) is a filter in its own right.</summary>
    public bool? IsActive { get; set; }

    /// <summary>Holders of this role, matched through the AppUserRole junction.</summary>
    public string? RoleId { get; set; }

    /// <summary>密碼更新時間 lower bound (inclusive).</summary>
    public DateOnly? PasswordUpdatedFrom { get; set; }

    /// <summary>密碼更新時間 upper bound (inclusive date, exclusive midnight).</summary>
    public DateOnly? PasswordUpdatedTo { get; set; }
}
