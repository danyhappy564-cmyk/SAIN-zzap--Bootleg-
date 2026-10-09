namespace SAINServerMod.Models;

public sealed class SAINServerConfig
{
    public List<string> DisabledPresets { get; set; } = [];
    public string? ForcedPresetName { get; set; }
    public bool AllowClientEditing { get; set; } = true;
    public List<string> EditAllowlist { get; set; } = [];

    /// <summary>zzap: language of the SAIN web pages, "ko" (default) or "en".</summary>
    public string WebLanguage { get; set; } = "ko";
}
