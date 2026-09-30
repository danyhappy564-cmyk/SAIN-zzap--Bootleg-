using System.Runtime.Serialization;
using SAIN.Preset.Shared.Attributes;

namespace SAIN.Preset.Shared.GlobalSettings.Categories.General;

/// <summary>
/// SAIN "Freeze" (hold still and wait when an enemy is heard close by before any fight started),
/// used by personalities whose Heard From Peace Behavior is Freeze (Rat, SnappingTurtle, Chad, and
/// GigaChad in the zzap presets). These values were hardcoded in EnemyDecisionClass before (zzap fork).
/// </summary>
[DataContract]
public class FreezeAmbushSettings : SAINSettingsBase<FreezeAmbushSettings>, ISAINSettings
{
    [DataMember]
    [Name("Max Enemy Distance")]
    [Description("Meters. Only freeze if the heard enemy is closer than this. SAIN original: 70.")]
    [MinMax(10f, 100f, 1f)]
    public float MaxDistance = 45f;

    [DataMember]
    [Name("Freeze Outdoors")]
    [Description("SAIN original only freezes indoors. On: also hold still outside (the bot freezes where it stands, so it can end up in the open).")]
    public bool AllowOutdoors = false;

    [DataMember]
    [Name("Min Freeze Time")]
    [Description("Seconds, before the personality's aggression is applied (freeze time = random(min, max) / aggression). SAIN original: 10.")]
    [MinMax(1f, 120f, 1f)]
    public float MinDuration = 10f;

    [DataMember]
    [Name("Max Freeze Time")]
    [Description("Seconds, before the personality's aggression is applied. SAIN original: 120.")]
    [MinMax(5f, 300f, 1f)]
    public float MaxDuration = 120f;

    [DataMember]
    [Name("Not Seen For")]
    [Description("Seconds. Only freeze if the enemy hasn't been seen for at least this long. SAIN original: 240.")]
    [MinMax(0f, 600f, 1f)]
    public float MinTimeSinceSeen = 240f;

    [DataMember]
    [Name("Heard Within")]
    [Description("Seconds. Only freeze if the enemy was heard within this time. SAIN original: 80.")]
    [MinMax(5f, 300f, 1f)]
    public float MaxTimeSinceHeard = 80f;

    [DataMember]
    [Name("Watch Approach Corner")]
    [Description("While frozen, aim at the corner the enemy has to come around (the last visible point on the path to them) instead of the default look.")]
    public bool WatchApproachCorner = true;

    [DataMember]
    [Name("Step To A Spot That Sees Past The Corner")]
    [Description(
        "zzap: while holding a corner, if the wall is between the bot and the spot it aims at (it would stare into the wall), it steps toward the "
            + "corner edge - the nearest spot where the aim line is open, up to the distance below - and holds there. Off = hold where it stopped."
    )]
    public bool StepToPeekSpot = true;

    [DataMember]
    [Name("Peek Spot Max Step (m)")]
    [Description("How far a holding bot may step toward the corner edge to get a clear angle.")]
    [MinMax(0.5f, 4f, 10f)]
    public float PeekSpotMaxStep = 2.5f;

    [DataMember]
    [Name("Diagnostic Logs")]
    [Description("Writes [Freeze] lines to BepInEx LogOutput.log when a bot starts/stops a freeze ambush.")]
    public bool DiagnosticLogs = false;
}
