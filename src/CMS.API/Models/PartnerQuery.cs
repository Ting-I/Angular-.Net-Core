namespace CMS.API.Models;

/// <summary>原廠 Partner — search DTO.</summary>
public class PartnerQuery
{
    /// <summary>LIKE match against Name, AppKey, NameOnPartnerMenu and NameOnCourseDetailPage.</summary>
    public string? Keyword { get; set; }

    /// <summary>Tri-state match on ImageFilename being present — null means no filter.</summary>
    public bool? HasImage { get; set; }
}
