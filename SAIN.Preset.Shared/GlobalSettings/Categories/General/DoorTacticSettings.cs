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
    [Name("Close Gunfire Abort Distance")]
    [Description("Meters. If anyone other than the enemy in the room is heard shooting closer than this, the bot drops the door and deals with it. 0 = off.")]
    [MinMax(0f, 40f, 1f)]
    public float CloseGunfireDistance = 12f;

    [DataMember]
    [Name("Resume After Third Party")]
    [Description("After a door tactic was dropped because of a third party (shot at, flanked, close gunfire, target switched), the bot goes back to the same door once that's dealt with, without re-rolling the chance.")]
    public bool ResumeAfterThirdParty = true;

    [DataMember]
    [Name("Resume Window")]
    [Description("Seconds. How long after dropping the door the bot may still come back to it (the room enemy may have been last heard up to this long ago).")]
    [MinMax(10f, 180f, 1f)]
    public float ResumeWindow = 90f;

    [DataMember]
    [Name("Squad Breach")]
    [Description("GigaChad/Chad with teammates nearby: open the door, everyone who has a grenade throws one into the room, then they rush in together right after the blast while one teammate watches the door.")]
    public bool SquadBreach = true;

    [DataMember]
    [Name("Squad Breach Chance")]
    [Description("Percent. When a GigaChad/Chad starts a door tactic with a teammate available and a grenade in the rig, chance it picks the squad breach over the solo tactics.")]
    [MinMax(0f, 100f, 1f)]
    public float SquadBreachChance = 50f;

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
    [Name("Door Grenade Follow-Up")]
    [Description("After the door grenade goes off, either rush the door and peek-shoot-pull back, or listen and throw another one if nobody reacts. Off = just hold the door like before.")]
    public bool PostBlastFollowUp = true;

    [DataMember]
    [Name("Follow-Up Peek Chance")]
    [Description("Percent. Right after the blast: chance to rush the door and jump peek (shoot briefly if the enemy shows, then pull back). Otherwise the bot listens first and throws again if there is no reaction; with no grenade left it peeks anyway.")]
    [MinMax(0f, 100f, 1f)]
    public float PostBlastPeekChance = 40f;

    [DataMember]
    [Name("Follow-Up Fake Retreat Chance")]
    [Description("Percent, rolled when the peek roll fails. Fake retreat: sprint away from the door so the enemy hears them leave, sneak back crouched to the covered spot and hold the door until they come out. Otherwise: listen, and throw again if nobody reacts.")]
    [MinMax(0f, 100f, 1f)]
    public float PostBlastFakeRetreatChance = 40f;

    [DataMember]
    [Name("Door Grenade Inside Chance")]
    [Description("Percent. Open the door from the side and throw the grenade into the room (at the enemy's last known spot) instead of in front of the closed door. If the door is already open it always goes inside. No arc into the room = door front.")]
    [MinMax(0f, 100f, 1f)]
    public float DoorGrenadeInsideChance = 40f;

    [DataMember]
    [Name("Max Door Grenades")]
    [Description("How many door grenades one bot may throw at the same door (first one included).")]
    [MinMax(1f, 3f, 1f)]
    public float MaxDoorGrenades = 2f;

    [DataMember]
    [Name("Follow-Up Listen Time")]
    [Description("Seconds the bot listens after the blast. Any sound from the enemy = reaction -> it holds the door instead of throwing again.")]
    [MinMax(1f, 6f, 10f)]
    public float PostBlastListenTime = 2.5f;

    [DataMember]
    [Name("Peek Shot Time")]
    [Description("Seconds the bot shoots after spotting the enemy during the follow-up peek, before pulling back.")]
    [MinMax(0.3f, 2f, 10f)]
    public float PeekShotTime = 0.8f;

    [DataMember]
    [Name("Pull Back Time")]
    [Description("Seconds the bot retreats after the peek shot.")]
    [MinMax(1f, 4f, 10f)]
    public float PullBackTime = 2f;

    [DataMember]
    [Name("Chance Multiplier")]
    [Description("Multiplies every personality's chance to start a door tactic. Raise it to see tactics more often while testing.")]
    [MinMax(0f, 3f, 10f)]
    public float ChanceMultiplier = 1f;

    [DataMember]
    [Name("Bots Close Open Doors In Their Way")]
    [Description("Vanilla SAIN closes any open door its path runs into (6s grace for doors it just opened). With a door that opens toward the bot that means: open, walk into the leaf, jitter/spin, close it, open it again... and bots closing open doors all over the map. Off (default): bots only open shut doors while moving; tactics still close doors on purpose.")]
    public bool AutoCloseDoors = false;

    [DataMember]
    [Name("TEST MODE: All PMCs Use GigaChad Tactics")]
    [Description("For testing only. Every PMC plans door tactics as a GigaChad (peek / trap / door grenade and its follow-ups) and the start chance is 100%. Turn it off for normal play.")]
    public bool TestModeAllPmcGigaChad = false;

    [DataMember]
    [Name("Diagnostic Logs")]
    [Description("Writes [DoorTactic] lines to BepInEx LogOutput.log: every tactic start, step change and result.")]
    public bool DiagnosticLogs = true;

    [DataMember]
    [Name("Verbose Logs")]
    [Description("Also logs why a nearby door was NOT used (rate limited). Noisy, for debugging only.")]
    public bool VerboseLogs = false;
}
