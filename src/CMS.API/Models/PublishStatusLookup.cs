namespace CMS.API.Models;

/// <summary>Slim PublishStatus row used for select options.</summary>
public class PublishStatusLookup
{
    public byte Pkid { get; set; }

    public string Description { get; set; } = string.Empty;
}
