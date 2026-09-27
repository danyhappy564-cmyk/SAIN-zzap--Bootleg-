using System.Runtime.Serialization;
using SAIN.Preset.Shared.Attributes;

namespace SAIN.Preset.Shared.GlobalSettings.Categories.General;

/// <summary>
/// Door tactics (zzap fork): jump peek, fake grenade/heal, room trap, door grenade, silent ambush.
/// Shown in the F6 editor under General. Presets saved before this existed get these defaults.
/// </summary>
[DataContract]
public class DoorTacticSettings : SAINSettingsBase<DoorTacticSettings>, ISAINSettings
{
    [DataMember]
    [Name("Door Tactics Enabled")]
    [Description("Master switch. When the enemy was last known inside a room behind a nearby door, GigaChad/Chad/SnappingTurtle/Rat bots use door tactics.")]
    public bool Enabled = true;

    [DataMember]
    [Name("Jump Peek")]
    [Description("GigaChad/Chad: from the corridor, bunny hop out in front of the doorway, snap a look into the room and bunny hop straight back. A shut door is opened first from beside the frame.")]
    public bool JumpPeek = true;

    [DataMember]
    [Name("Fake Grenade")]
    [Description("GigaChad/Chad: after a peek, draw a grenade (audible) and put it away again. If the enemy rushes out before the gun is back up, the bot retreats to cover.")]
    public bool FakeGrenade = true;

    [DataMember]
    [Name("Fake Heal / Stim")]
    [Description("GigaChad/Chad: start a heal (only when actually hurt, cancelled after 1.5s) or a stim injection (cancelled after 0.7s, before it goes in) next to the door to bait a push. Cancelled at once if the enemy is heard coming out.")]
    public bool FakeHeal = true;

    [DataMember]
    [Name("Room Trap")]
    [Description("GigaChad/SnappingTurtle close the door on the room and hold it (taunting only if the personality taunts). Rat holds beside the door silently.")]
    public bool RoomTrap = true;

    [DataMember]
    [Name("Door Grenade")]
    [Description("GigaChad: after closing the door, crouch beside the frame, listen, then gently toss a long fuse grenade right at the door and back off. Skipped if the enemy is heard coming out (sprint/jump/door sound).")]
    public bool DoorGrenade = true;

    [DataMember]
    [Name("Door Grenade Min Fuse")]
    [Description("Seconds. Only frag grenades with at least this fuse are used for the door grenade (M67 type). Impact grenades are never used.")]
    [MinMax(3f, 8f, 10f)]
    public float DoorGrenadeMinFuse = 4.5f;

    [DataMember]
    [Name("Emergency Retreat Distance")]
    [Description("Meters. Right after a fake grenade/heal, if the enemy comes closer than this before the gun is back up, the bot retreats instead of fighting.")]
    [MinMax(3f, 20f, 1f)]
    public float EmergencyRetreatDistance = 10f;

    [DataMember]
    [Name("Squad And Flank Checks")]
    [Description("One bot per door; abort if a teammate is inside the room or pushing through the door, or if another enemy shows up on the bot's side (flank). Door grenades are never thrown within 6m of a teammate.")]
    public bool SquadChecks = true;

    [DataMember]
    [Name("Squad Roles")]
    [Description("When a bot starts a door tactic, the nearest teammate who knows the same enemy takes Overwatch (holds a cross angle on the door from the other side, back far enough to stay clear of a door grenade) and the next one takes Rear Guard (watches behind the group).")]
    public bool SquadRoles = true;

    [DataMember]
    [Name("Squad Role Max Distance")]
    [Description("Meters. Teammates farther than this from the door are not given a role.")]
    [MinMax(5f, 40f, 1f)]
    public float SquadRoleMaxDistance = 20f;

    [DataMember]
    [Name("GigaChad Chance")]
    [Description("Percent. Chance a GigaChad (skilled player) uses a door tactic when one is possible.")]
    [MinMax(0f, 100f, 1f)]
    public float GigaChadChance = 60f;

    [DataMember]
    [Name("Chad Chance")]
    [Description("Percent. Chance a Chad (aggressive) uses a jump peek when one is possible.")]
    [MinMax(0f, 100f, 1f)]
    public float ChadChance = 35f;

    [DataMember]
    [Name("SnappingTurtle Chance")]
    [Description("Percent. Chance a SnappingTurtle (camper) traps the room: closes the door and holds it.")]
    [MinMax(0f, 100f, 1f)]
    public float SnappingTurtleChance = 60f;

    [DataMember]
    [Name("Rat Chance")]
    [Description("Percent. Chance a Rat waits silently beside the door for the enemy to come out.")]
    [MinMax(0f, 100f, 1f)]
    public float RatChance = 50f;

    [DataMember]
    [Name("GigaChad Peek vs Trap")]
    [Description("Percent. When a GigaChad could either jump peek or trap the room, chance it picks the jump peek.")]
    [MinMax(0f, 100f, 1f)]
    public float GigaChadPeekChance = 55f;

    [DataMember]
    [Name("GigaChad Fake Trick Chance")]
    [Description("Percent. After a GigaChad jump peek: chance of a fake grenade (or a fake heal if hurt and no grenade trick).")]
    [MinMax(0f, 100f, 1f)]
    public float GigaChadFakeTrickChance = 40f;

    [DataMember]
    [Name("Chad Fake Trick Chance")]
    [Description("Percent. After a Chad jump peek: chance of a fake grenade (or a fake heal if hurt and no grenade trick).")]
    [MinMax(0f, 100f, 1f)]
    public float ChadFakeTrickChance = 20f;

    [DataMember]
    [Name("GigaChad Trap Fake Heal Chance")]
    [Description("Percent. While a GigaChad holds a trapped room, chance of a fake heal (only when actually hurt).")]
    [MinMax(0f, 100f, 1f)]
    public float GigaChadTrapFakeHealChance = 30f;

    [DataMember]
    [Name("GigaChad Door Grenade Chance")]
    [Description("Percent. After a GigaChad traps a room, chance it backs off and throws a long fuse grenade at the door.")]
    [MinMax(0f, 100f, 1f)]
    public float DoorGrenadeChance = 50f;

    [DataMember]
    [Name("Chance Multiplier")]
    [Description("Multiplies every personality's chance to start a door tactic. Raise it to see tactics more often while testing.")]
    [MinMax(0f, 3f, 10f)]
    public float ChanceMultiplier = 1f;

    [DataMember]
    [Name("Diagnostic Logs")]
    [Description("Writes [DoorTactic] lines to BepInEx LogOutput.log: every tactic start, step change and result.")]
    public bool DiagnosticLogs = true;

    [DataMember]
    [Name("Verbose Logs")]
    [Description("Also logs why a nearby door was NOT used (rate limited). Noisy, for debugging only.")]
    public bool VerboseLogs = false;
}
