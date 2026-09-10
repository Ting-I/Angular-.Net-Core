using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>發布狀態 PublishStatus — write DTO for create and update.</summary>
public class PublishStatusRequest
{
    /// <summary>
    /// 主代碼 — the primary key. The column is tinyint but not IDENTITY, so the value travels in
    /// the request on create; it is immutable afterwards.
    /// </summary>
    [Range(0, 255)]
    public byte Pkid { get; set; }

    /// <summary>狀態說明</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    public string Description { get; set; } = string.Empty;

    /// <summary>草稿</summary>
    public bool IsDraft { get; set; }

    /// <summary>已發布</summary>
    public bool IsPublished { get; set; }

    /// <summary>已停用</summary>
    public bool IsDiscontinued { get; set; }
}
