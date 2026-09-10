namespace CMS.API.Models;

/// <summary>課程群組 CourseGroup — search DTO.</summary>
public class CourseGroupQuery
{
    /// <summary>LIKE match against Description, the table's only string column.</summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// Tri-state match on the group being referenced by any Course or PartnerCourseGroup row —
    /// null means no filter.
    /// </summary>
    public bool? InUse { get; set; }
}
