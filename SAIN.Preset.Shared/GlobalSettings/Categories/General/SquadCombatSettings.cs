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
    [Name("Crossfire: Hold The Other Entrance Indoors")]
    [Description("Indoors, instead of a spot in the open around the enemy (usually behind walls), the crossfire bot first looks for a second way into the enemy's room and holds it, watching the doorway - one mate fights at the front door, this one blocks the back.")]
    public bool CrossfireBackDoor = true;

    [DataMember]
    [Name("Crossfire Back Door Hold Time")]
    [Description("Seconds the other entrance is held before deciding: push in or hold on.")]
    [MinMax(5f, 90f, 1f)]
    public float CrossfireBackDoorHoldTime = 15f;

    [DataMember]
    [Name("Crossfire Back Door Push")]
    [Description("When the enemy never came out and is still believed inside: push in through the back door (pincer with the mate at the front) - GigaChad/Chad/Wreckless always, Normal 60%, only with 50%+ magazine and not badly hurt. Others (or off) hold 15s longer once, then leave.")]
    public bool CrossfireBackDoorPush = true;

    [DataMember]
    [Name("Crossfire Indoor Min Angle")]
    [Description("Degrees. Indoors the angle to the engaging teammate can be smaller (rooms are small); points 4-14m from the enemy, crouch-height sight and 'one step out' corner spots count too.")]
    [MinMax(5f, 90f, 1f)]
    public float CrossfireIndoorMinAngle = 20f;

    [DataMember]
    [Name("Post-Combat Tidy-Up")]
    [Description("Right after a real fight (combat layer 3s+) ends: top the magazine up, rejoin the squad leader if 25m+ away, otherwise hold half-crouched with the gun on the last threat. Runs in the SAIN squad layer, so ORBIT (which waits 15s after the combat layer) takes over on time - it fills the gap where the bot used to fall to BSG's vanilla layers.")]
    public bool PostCombat = true;

    [DataMember]
    [Name("Post-Combat Heal")]
    [Description("During the post-combat tidy-up SAIN heals the bot itself (first aid for bleeding/damage, then surgery for a blacked limb), crouched in place and still watching the last threat. The tidy-up lasts until the meds are done (max 60s) - ORBIT waits for healing anyway.")]
    public bool PostCombatHeal = true;

    [DataMember]
    [Name("Post-Combat Time")]
    [Description("Seconds after the combat layer ended. Keep it under 15 so ORBIT takes over right when it wants to.")]
    [MinMax(3f, 30f, 1f)]
    public float PostCombatTime = 14f;

    [DataMember]
    [Name("Squad Storm Weak Enemy")]
    [Description("A squad (2+ alive within 35m) sizes up its enemy every 20s from his gear and state - no/low armor, no helmet, pistol or no gun out, hurt, busy healing/reloading, alone. Weak enough: the squad storms him together (Chad/GigaChad/Wreckless always join, Normal 80%, Timmy 60%, Rat/Turtle 35%, Coward 10%).")]
    public bool SquadStorm = true;

    [DataMember]
    [Name("Squad Storm Weakness Needed")]
    [Description("0-1. Roughly: no armor 0.3, armor class 1-2 0.2, no helmet 0.15, pistol 0.3, no gun out 0.35, badly hurt 0.2, dying 0.35, healing/reloading 0.15, alone 0.1.")]
    [MinMax(0.1f, 1f, 100f)]
    public float SquadStormWeakness = 0.5f;

    [DataMember]
    [Name("Squad Storm Max Distance")]
    [Description("Meters of path to the enemy.")]
    [MinMax(5f, 80f, 1f)]
    public float SquadStormMaxDistance = 40f;

    [DataMember]
    [Name("Fire Lane Guard")]
    [Description("Bots never walk deeper into a squadmate's line of fire (held just outside, or step out sideways if already in it; let through after 1.5s so nobody gets stuck), and a shooter holds fire when a squadmate is about to cross the line (position 0.3s ahead). Field report: bots hanging back ran up to the enemy through the teammates' fire and got team-killed.")]
    public bool FireLaneGuard = true;

    [DataMember]
    [Name("Fire Lane Width")]
    [Description("Meters either side of a squadmate's line of fire that count as 'in the lane'.")]
    [MinMax(0.3f, 2f, 10f)]
    public float FireLaneWidth = 0.9f;

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
    [Name("PMC Squad Voice Callouts")]
    [Description("Off (default): PMC squadmates don't shout squad callouts at each other (contact!, retreat!, reloading/out of ammo, grenade!, sniper!, enemy down, need help...). Real squads use Discord. Taunts/begging aimed at the enemy and pain sounds stay. Scavs keep vanilla talk.")]
    public bool PmcSquadVoiceCallouts = false;

    [DataMember]
    [Name("Diagnostic Logs")]
    [Description("Writes [SquadCombat] lines to BepInEx LogOutput.log for every start and result, plus SUMMARY counters.")]
    public bool DiagnosticLogs = false;
}
