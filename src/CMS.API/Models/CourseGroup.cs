namespace CMS.API.Models;

/// <summary>課程群組 CourseGroup — response model.</summary>
public class CourseGroup
{
    /// <summary>主代碼 — smallint IDENTITY primary key.</summary>
    public short Pkid { get; set; }

    /// <summary>群組說明 — the table's only data column.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 課程數 — count of Course rows filed under this group. FK_Course_CourseGroup is
    /// ON DELETE CASCADE, so this count is what stops a delete from destroying those rows.
    /// </summary>
    public int CourseCount { get; set; }

    /// <summary>原廠群組數 — count of PartnerCourseGroup rows referencing this group.</summary>
    public int PartnerCourseGroupCount { get; set; }
}
