using System.Runtime.Serialization;
using SAIN.Preset.Shared.Attributes;

namespace SAIN.Preset.Shared.GlobalSettings.Categories.General;

[DataContract]
public class ExtractSettings : SAINSettingsBase<ExtractSettings>, ISAINSettings
{
    [DataMember]
    [Name("SAIN Extract Behavior")]
    [Description("REQUIRES GAME RESTART. Disable vanilla bot extract behavior and use SAIN decision making instead.")]
    public bool SAIN_EXTRACT_TOGGLE = false;

    // zzap (user 2026-10-02: "ORBIT's extraction is removed, yet bots extract around 20 min"): with SAIN's extract off,
    // SAIN kept BSG's own "Exfiltration" layer, and ORBIT only bypasses it for bots ORBIT is driving at that moment - so a
    // bot just out of a SAIN fight (before ORBIT takes it back) fell into BSG's exfil walk (21:37 raid: 3 bots in that layer).
    [DataMember]
    [Name("Disable Vanilla Bot Extract")]
    [Description("REQUIRES GAME RESTART. Remove BSG's own bot extract behavior even when SAIN Extract Behavior is off - for setups where another mod (ORBIT) does or disables extraction. With both off, SAIN bots never walk to an exit on their own.")]
    public bool DisableVanillaExtract = true;
}
