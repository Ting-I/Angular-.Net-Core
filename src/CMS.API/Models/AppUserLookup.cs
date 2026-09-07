namespace CMS.API.Models;

/// <summary>Slim AppUser row used for select / multiselect options.</summary>
public class AppUserLookup
{
    /// <summary>使用者代碼</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>使用者名稱</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>啟用</summary>
    public bool IsActive { get; set; }
}
