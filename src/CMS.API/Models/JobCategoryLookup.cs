namespace CMS.API.Models;

/// <summary>Slim 職務類別 JobCategory row used for multiselect options.</summary>
public class JobCategoryLookup
{
    public short Pkid { get; set; }

    public string Description { get; set; } = string.Empty;
}
