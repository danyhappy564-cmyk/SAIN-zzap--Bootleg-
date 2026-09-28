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
    [Name("Log Summary Every (min)")]
    [Description("Minutes. Also print the running [PlayerStyle] summary during the raid this often. 0 = only at raid end.")]
    [MinMax(0f, 30f, 1f)]
    public float LogEveryMinutes = 5f;
}
