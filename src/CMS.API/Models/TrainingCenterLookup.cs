namespace CMS.API.Models;

/// <summary>Slim TrainingCenter row used for the 上稿作業 tabs and select options.</summary>
public class TrainingCenterLookup
{
    public short Pkid { get; set; }

    /// <summary>訓練中心名稱 — the tab label.</summary>
    public string Name { get; set; } = string.Empty;

    public string AppKey { get; set; } = string.Empty;

    /// <summary>The centre the UI opens on when nothing has been chosen yet.</summary>
    public bool IsDefault { get; set; }
}
