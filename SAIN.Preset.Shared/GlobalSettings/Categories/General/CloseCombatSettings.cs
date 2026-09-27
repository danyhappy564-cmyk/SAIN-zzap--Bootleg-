using System.Runtime.Serialization;
using SAIN.Preset.Shared.Attributes;

namespace SAIN.Preset.Shared.GlobalSettings.Categories.General;

/// <summary>
/// zzap fork: close-range standoff movement. Vanilla SAIN sprints to cover whenever the bot is reloading/healing
/// or after a short timer, and a sprinting bot faces where it runs - so in a close fight it turned its back on
/// an enemy it hadn't dealt with and was trivial to kill. Real players back-pedal / strafe to cover facing the threat.
/// </summary>
[DataContract]
public class CloseCombatSettings : SAINSettingsBase<CloseCombatSettings>, ISAINSettings
{
    [DataMember]
    [Name("No Back Turning Near Enemy")]
    [Description("While an enemy is close and was seen very recently, the bot does not sprint to cover (which turns its back). It walks there instead - back-pedalling / strafing with its gun on the enemy - even while reloading or healing.")]
    public bool NoBackTurning = true;

    [DataMember]
    [Name("Close Distance")]
    [Description("Meters. Enemy (last known position) closer than this counts as a close standoff.")]
    [MinMax(5f, 60f, 1f)]
    public float Distance = 30f;

    [DataMember]
    [Name("Seen Within")]
    [Description("Seconds. Only while the enemy is visible or was seen this recently. Longer ago = the bot may sprint as usual (it's not in the enemy's sights).")]
    [MinMax(0.5f, 10f, 10f)]
    public float SeenWithin = 4f;

    [DataMember]
    [Name("PMC Only")]
    [Description("Apply only to PMCs. Scavs keep vanilla behaviour.")]
    public bool PmcOnly = true;
}
