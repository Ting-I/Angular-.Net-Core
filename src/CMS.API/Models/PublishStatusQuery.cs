namespace CMS.API.Models;

/// <summary>發布狀態 PublishStatus — search DTO.</summary>
public class PublishStatusQuery
{
    /// <summary>LIKE match against Description.</summary>
    public string? Keyword { get; set; }

    /// <summary>Tri-state match against IsDraft — null means no filter.</summary>
    public bool? IsDraft { get; set; }

    /// <summary>Tri-state match against IsPublished — null means no filter.</summary>
    public bool? IsPublished { get; set; }

    /// <summary>Tri-state match against IsDiscontinued — null means no filter.</summary>
    public bool? IsDiscontinued { get; set; }
}
