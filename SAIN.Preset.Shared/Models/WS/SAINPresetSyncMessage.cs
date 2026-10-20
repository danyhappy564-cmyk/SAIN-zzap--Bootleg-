using System.Runtime.Serialization;

namespace SAIN.Preset.Shared.Models.WS;

public enum EPresetSyncChange
{
    Saved,
    Deleted,
    ConfigChanged,
}

[DataContract]
public sealed class SAINPresetSyncMessage
{
    [DataMember]
    public string PresetName;

    [DataMember]
    public EPresetSyncChange Change;

    /// <summary>zzap (user 2026-10-20: "saved on the web, the game says pick it in F6 - make the web save apply in game too"):
    /// a Saved preset that is the web page's "[Modified]" copy of this bundled zzap preset. A game using that preset (or an
    /// earlier copy of it) switches to the new copy. Null otherwise.</summary>
    [DataMember]
    public string CopyOf;
}
