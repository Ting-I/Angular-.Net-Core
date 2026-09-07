using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>原廠 Partner — write DTO for create and update.</summary>
public class PartnerRequest
{
    /// <summary>
    /// 主代碼 — the primary key. The column is smallint IDENTITY, so this is ignored on create and
    /// carries the key from the body on update.
    /// </summary>
    public short Pkid { get; set; }

    /// <summary>原廠名稱</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>應用代碼</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(10)]
    public string AppKey { get; set; } = string.Empty;

    /// <summary>選單顯示名稱</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string NameOnPartnerMenu { get; set; } = string.Empty;

    /// <summary>課程頁顯示名稱</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    public string NameOnCourseDetailPage { get; set; } = string.Empty;

    /// <summary>顯示順序</summary>
    public int DisplayOrder { get; set; }

    /// <summary>圖片檔名</summary>
    [StringLength(50)]
    public string? ImageFilename { get; set; }
}
