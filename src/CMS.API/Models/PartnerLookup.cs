namespace CMS.API.Models;

/// <summary>Slim Partner row used for select options.</summary>
public class PartnerLookup
{
    public short Pkid { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Carried alongside Name so similarly-named brands can be told apart.</summary>
    public string AppKey { get; set; } = string.Empty;
}
