namespace CMS.API.Models;

/// <summary>原廠 Partner — response model.</summary>
public class Partner
{
    /// <summary>主代碼 — smallint IDENTITY primary key.</summary>
    public short Pkid { get; set; }

    /// <summary>原廠名稱</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>應用代碼</summary>
    public string AppKey { get; set; } = string.Empty;

    /// <summary>選單顯示名稱</summary>
    public string NameOnPartnerMenu { get; set; } = string.Empty;

    /// <summary>課程頁顯示名稱</summary>
    public string NameOnCourseDetailPage { get; set; } = string.Empty;

    /// <summary>顯示順序</summary>
    public int DisplayOrder { get; set; }

    /// <summary>圖片檔名 — bare filename, the only nullable column.</summary>
    public string? ImageFilename { get; set; }

    /// <summary>認證數 — count of Certification rows referencing this partner.</summary>
    public int CertificationCount { get; set; }

    /// <summary>課程數 — count of Course rows referencing this partner.</summary>
    public int CourseCount { get; set; }

    /// <summary>課程群組數 — count of PartnerCourseGroup rows referencing this partner.</summary>
    public int CourseGroupCount { get; set; }

    /// <summary>活動數 — count of Promotion2 rows referencing this partner.</summary>
    public int PromotionCount { get; set; }

    /// <summary>說明會數 — count of Seminar rows referencing this partner (unenforced FK).</summary>
    public int SeminarCount { get; set; }
}
