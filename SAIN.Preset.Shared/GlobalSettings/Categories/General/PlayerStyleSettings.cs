using System.Runtime.Serialization;
using SAIN.Preset.Shared.Attributes;

namespace SAIN.Preset.Shared.GlobalSettings.Categories.General;

/// <summary>
/// zzap fork: records how the human player plays (movement, peeking, shooting, reloads, kills, deaths) so a later
/// version can adapt bots to it. Stage 1 only records - bot behaviour is not changed by anything here.
/// </summary>
[DataContract]
public class PlayerStyleSettings : SAINSettingsBase<PlayerStyleSettings>, ISAINSettings
{
    [DataMember]
    [Name("Record Player Style")]
    [Description("Records the player's play style every raid (sprint/crouch/lean/ADS/jump ratios, camping, shots, reloads, grenades, kill and death distances) to BepInEx/config/SAIN-zzap/PlayerStyle/<profile>.jsonl and prints a [PlayerStyle] summary to the log. Does not change bot behaviour.")]
    public bool Enabled = true;

    [DataMember]
    [Name("Record Keys")]
    [Description("Also record the player's game keys (press/release and hold time) - A/D switching, jump, crouch, lean, fire taps vs holds. Binds are read from SPT's Control.ini. Only while in a raid, not while the cursor is visible (inventory/menus).")]
    public bool RecordKeys = true;

    [DataMember]
    [Name("Key Timeline File")]
    [Description("Write every key press/release with its time to <profile>_<date>.keys.csv next to the style record (appended every 2s). Off = only the key statistics.")]
    public bool KeyTimeline = true;

    [DataMember]
    [Name("Log Summary Every (min)")]
    [Description("Minutes. Also print the running [PlayerStyle] summary during the raid this often. 0 = only at raid end.")]
    [MinMax(0f, 30f, 1f)]
    public float LogEveryMinutes = 5f;
}
