using System.Runtime.Serialization;
using SAIN.Preset.Shared.Attributes;

namespace SAIN.Preset.Shared.GlobalSettings.Categories.General;

/// <summary>
/// zzap fork: squad engagement framework for the active firefight. Vanilla SAIN's squad layer only
/// acts when a bot hasn't seen its enemy for 10s+, so in a live fight every squad bot fights alone.
/// These behaviours run for the squadmates who know the enemy but can't see it right now.
/// </summary>
[DataContract]
public class SquadCombatSettings : SAINSettingsBase<SquadCombatSettings>, ISAINSettings
{
    [DataMember]
    [Name("Squad Combat Enabled")]
    [Description("Master switch for the squad engagement behaviours below (crossfire, cover a reloading/healing teammate, trade a downed teammate).")]
    public bool Enabled = true;

    [DataMember]
    [Name("Crossfire")]
    [Description("While a teammate is shooting at the enemy, a squadmate who can't see that enemy moves to a different angle on it (spread out) instead of stacking behind the teammate.")]
    public bool Crossfire = true;

    [DataMember]
    [Name("Crossfire Min Angle")]
    [Description("Degrees. The new angle on the enemy must differ at least this much from the engaging teammate's angle.")]
    [MinMax(15f, 90f, 1f)]
    public float CrossfireMinAngle = 35f;

    [DataMember]
    [Name("Min Teammate Spacing")]
    [Description("Meters. Squadmates don't pick a position closer than this to a teammate (no bunching up).")]
    [MinMax(2f, 15f, 1f)]
    public float MinSpacing = 4f;

    [DataMember]
    [Name("Cover Reloading/Healing Teammate")]
    [Description("When a teammate nearby reloads, heals or does surgery mid-fight, a squadmate moves next to them and holds their enemy's angle.")]
    public bool CoverTeammate = true;

    [DataMember]
    [Name("Cover Max Distance")]
    [Description("Meters. Only teammates this close get covered.")]
    [MinMax(5f, 40f, 1f)]
    public float CoverMaxDistance = 20f;

    [DataMember]
    [Name("Trade Downed Teammate")]
    [Description("When a teammate goes down, aggressive personalities push the position of the enemy that teammate was fighting; cautious ones take an angle on it and hold.")]
    public bool Trade = true;

    [DataMember]
    [Name("Trade Window")]
    [Description("Seconds after a teammate goes down during which the trade can start.")]
    [MinMax(3f, 30f, 1f)]
    public float TradeWindow = 10f;

    [DataMember]
    [Name("Max Engage Distance")]
    [Description("Meters. Squad behaviours only for enemies closer than this to the bot.")]
    [MinMax(20f, 200f, 1f)]
    public float MaxEnemyDistance = 80f;

    [DataMember]
    [Name("Diagnostic Logs")]
    [Description("Writes [SquadCombat] lines to BepInEx LogOutput.log for every start and result, plus SUMMARY counters.")]
    public bool DiagnosticLogs = true;
}
