namespace CMS.API.Models;

/// <summary>發布狀態 PublishStatus — response model.</summary>
public class PublishStatus
{
    /// <summary>主代碼 — tinyint primary key. Not an IDENTITY column; the value is supplied on create.</summary>
    public byte Pkid { get; set; }

    /// <summary>狀態說明</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>草稿</summary>
    public bool IsDraft { get; set; }

    /// <summary>已發布</summary>
    public bool IsPublished { get; set; }

    /// <summary>已停用</summary>
    public bool IsDiscontinued { get; set; }

    /// <summary>課程數 — count of Course rows referencing this status.</summary>
    public int CourseCount { get; set; }

    /// <summary>活動數 — count of Promotion2 rows referencing this status.</summary>
    public int PromotionCount { get; set; }
}
