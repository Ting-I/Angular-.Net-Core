namespace CMS.API.Models;

/// <summary>Slim CourseGroup row used for select options.</summary>
public class CourseGroupLookup
{
    public short Pkid { get; set; }

    public string Description { get; set; } = string.Empty;
}
