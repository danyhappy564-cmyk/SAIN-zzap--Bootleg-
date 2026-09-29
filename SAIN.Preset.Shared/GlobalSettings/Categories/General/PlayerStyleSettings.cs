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
    public bool RecordKeys = false;

    [DataMember]
    [Name("Key Timeline File")]
    [Description("Write every key press/release with its time to <profile>_<date>.keys.csv next to the style record (appended every 2s). Off = only the key statistics.")]
    public bool KeyTimeline = false;

    [DataMember]
    [Name("Adapt Bots To Player Style")]
    [Description("Stage 2: at raid start, read this profile's recorded raids and adapt bots against the player's style (aggressive runner -> hold corners and fight close, bunny hopper -> hold the approach corner, lean peeker -> more lean rocking back, grenade user -> spread out). How much each personality adapts differs (Rats/Turtles hold, Chads fight, Cowards barely). Only against the human player.")]
    public bool AdaptEnabled = true;

    [DataMember]
    [Name("Learn From Outcomes")]
    [Description("Every response a bot makes to the player (push, hold, grenade, flank... in sight or not, by range and own health) is scored by what happened: hit the player = win, killed by the player = loss. Saved per profile across raids (<profile>.learned.json). With adaptation on, responses that worked better than average against this player get more likely, the ones that failed less. Always recorded; only applied with Adapt on.")]
    public bool LearnFromOutcomes = true;

    [DataMember]
    [Name("Raid Journal")]
    [Description("One time-stamped file per raid with only SAIN-zzap content (BepInEx/config/SAIN-zzap/Journal/<date>_<map>.log): every bot decision change with its reason, all tactic logs, the player's position/hits/kills every 2s, the counters at the end. Made for reviewing a raid afterwards.")]
    public bool RaidJournal = false;

    [DataMember]
    [Name("Ignore Player Gear")]
    [Description("Bots don't judge the human player as weak or strong from his armor/helmet/gun (it may be dev-tool or stacked gear). His state (hurt, healing/reloading, alone) and the learned outcomes still count.")]
    public bool IgnorePlayerGear = true;

    [DataMember]
    [Name("Use Test Raids For Adaptation")]
    [Description("Raids detected as test sessions (god mode / infinite ammo, e.g. DevTools) are tagged in the record. On: still used for the style scores (movement and fighting style are the same). Off: skipped.")]
    public bool AdaptUseTestRaids = true;

    [DataMember]
    [Name("Adapt Strength")]
    [Description("0-1. Overall strength of the adaptation (1 = full, 0.5 = half).")]
    [MinMax(0f, 1f, 100f)]
    public float AdaptStrength = 1f;

    [DataMember]
    [Name("Adapt Full Confidence (min)")]
    [Description("Minutes of recorded play needed for the full effect. Less data = proportionally weaker adaptation (min 25% once 10 min are recorded).")]
    [MinMax(10f, 300f, 1f)]
    public float AdaptFullConfidenceMinutes = 60f;

    [DataMember]
    [Name("Adapt Raids Used")]
    [Description("How many of the most recent recorded raids the style is computed from.")]
    [MinMax(1f, 50f, 1f)]
    public float AdaptRaids = 10f;

    [DataMember]
    [Name("Log Summary Every (min)")]
    [Description("Minutes. Also print the running [PlayerStyle] summary during the raid this often. 0 = only at raid end.")]
    [MinMax(0f, 30f, 1f)]
    public float LogEveryMinutes = 5f;
}
