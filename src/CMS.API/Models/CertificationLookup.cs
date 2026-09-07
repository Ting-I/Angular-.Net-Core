namespace CMS.API.Models;

/// <summary>Slim 認證 Certification row used for multiselect options.</summary>
public class CertificationLookup
{
    public int Pkid { get; set; }

    /// <summary>nchar(100) NULL in the schema — RTRIMmed and coalesced to '' by the lookup query.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Carried so the options can be grouped and labelled by their vendor.</summary>
    public short PartnerPkid { get; set; }

    public string PartnerName { get; set; } = string.Empty;
}
