using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>課程群組 CourseGroup — write DTO for create and update.</summary>
public class CourseGroupRequest
{
    /// <summary>
    /// 主代碼 — the primary key. The column is smallint IDENTITY, so this is ignored on create and
    /// carries the key from the body on update.
    /// </summary>
    public short Pkid { get; set; }

    /// <summary>群組說明</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public string Description { get; set; } = string.Empty;
}
