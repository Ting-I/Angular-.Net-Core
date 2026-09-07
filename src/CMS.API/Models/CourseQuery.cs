namespace CMS.API.Models;

/// <summary>課程 Course — search DTO.</summary>
public class CourseQuery
{
    /// <summary>LIKE match against Title, OfficialTitle, CourseId, ProdCourseId and FriendlyUrl.</summary>
    public string? Keyword { get; set; }

    /// <summary>原廠 — exact match on Partner_pkid.</summary>
    public short? PartnerPkid { get; set; }

    /// <summary>課程群組 — exact match on CourseGroup_pkid.</summary>
    public short? CourseGroupPkid { get; set; }

    /// <summary>上架狀態 — exact match on PublishStatus_pkid.</summary>
    public byte? PublishStatusPkid { get; set; }

    /// <summary>上架日期 lower bound, inclusive.</summary>
    public DateOnly? ScheduleOnFrom { get; set; }

    /// <summary>上架日期 upper bound, inclusive.</summary>
    public DateOnly? ScheduleOnTo { get; set; }

    /// <summary>下架日期 lower bound, inclusive.</summary>
    public DateOnly? ScheduleOffFrom { get; set; }

    /// <summary>下架日期 upper bound, inclusive.</summary>
    public DateOnly? ScheduleOffTo { get; set; }

    /// <summary>Tri-state match on CanRepeat — null means no filter, false is a real filter.</summary>
    public bool? CanRepeat { get; set; }
}
