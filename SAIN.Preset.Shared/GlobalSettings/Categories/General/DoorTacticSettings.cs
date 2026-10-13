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
    [Description("GigaChad/Chad: from the corridor, bunny hop out in front of the doorway, snap a look into the room and bunny hop straight back. A shut door is opened first from beside the frame. PMCs only - bosses and followers use the run-by or a step peek.")]
    public bool JumpPeek = true;

    [DataMember]
    [Name("Fake Grenade")]
    [Description("GigaChad/Chad at the door frame: draw a grenade (the sound is the bait) and put it away the moment it is in hand. Only when the room enemy was heard/located within 60s and is within 14m of the door, and only from real cover (no known enemy has line of sight, indoors or boxed in, not under fire); running footsteps cancel it at any point. If the enemy rushes out before the gun is back up, the bot retreats to cover.")]
    public bool FakeGrenade = true;

    [DataMember]
    [Name("Fake Heal / Stim")]
    [Description("GigaChad/Chad at the door frame: start a heal (only when actually hurt) or a stim injection and cancel it the moment the item is in hand (0.3-0.4s cap), so it never actually goes in. Same conditions as the fake grenade; running footsteps cancel it.")]
    public bool FakeHeal = true;

    [DataMember]
    [Name("Room Trap")]
    [Description("GigaChad/SnappingTurtle stack beside the door and hold it (the door is left as it is). Rat holds beside the door silently.")]
    public bool RoomTrap = true;

    [DataMember]
    [Name("Hold Stance Holds The Door")]
    [Description("When the hidden-enemy decision picks 'hold' and the enemy is in a room behind a door close by (bot within 8m of it), any bot holds that door from beside the frame (room trap; Rat low and silent) instead of freezing in the open. Needs Room Trap on.")]
    public bool HoldStanceDoorTrap = true;

    [DataMember]
    [Name("Emergency Retreat Distance")]
    [Description("Meters. Right after a fake grenade/heal, if the enemy comes closer than this before the gun is back up, the bot retreats instead of fighting.")]
    [MinMax(3f, 20f, 1f)]
    public float EmergencyRetreatDistance = 10f;

    [DataMember]
    [Name("Squad And Flank Checks")]
    [Description("One bot per door; abort if a teammate is inside the room or pushing through the door, or if another enemy shows up on the bot's side (flank).")]
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
    [Name("Squad Roles")]
    [Description("When a bot starts a door tactic, the nearest teammate who knows the same enemy takes Overwatch (holds a cross angle on the door from the other side) and the next one takes Rear Guard (watches behind the group).")]
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
    [Description("Percent. Chance a SnappingTurtle (camper) traps the room: holds the door from beside the frame.")]
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
    [Name("Chance Multiplier")]
    [Description("Multiplies every personality's chance to start a door tactic. Raise it to see tactics more often while testing.")]
    [MinMax(0f, 3f, 10f)]
    public float ChanceMultiplier = 1f;

    [DataMember]
    [Name("Room Clear")]
    [Description("Going into the enemy's room instead of creeping through the doorway muzzle first (players wait in the corner for that): stack beside the frame, open it from the side, 0-2 fake grenade draws mixed in (someone running out cancels), then a real grenade and the dash right after its blast, or straight a short hard dash inside. The bot never throws where its own blast could reach it and waits for the real explosion.")]
    public bool RoomClear = true;

    [DataMember]
    [Name("Room Clear Chance (GigaChad/Chad)")]
    [Description("Percent. When a GigaChad/Chad starts a door tactic, chance it clears the room instead of peek/trap.")]
    [MinMax(0f, 100f, 1f)]
    public float RoomClearChance = 45f;

    [DataMember]
    [Name("Room Clear Chance Wreckless")]
    [Description("Percent. Chance a Wreckless clears a room with an enemy behind a nearby door (its only door tactic).")]
    [MinMax(0f, 100f, 1f)]
    public float RoomClearWrecklessChance = 60f;

    [DataMember]
    [Name("Room Clear Chance Normal")]
    [Description("Percent. Chance a Normal bot clears a room with an enemy behind a nearby door.")]
    [MinMax(0f, 100f, 1f)]
    public float RoomClearNormalChance = 25f;

    [DataMember]
    [Name("Search: Clear The Room First")]
    [Description("A bot that goes searching for an enemy who has gone quiet in a room behind a door (camping) clears that room on the way in - fakes, a real grenade or a dash, picked as in a room clear - instead of opening the door and walking in. Any personality that searches. The normal door tactics stop once the enemy has been quiet for 25s, which is exactly when a camper is in there.")]
    public bool SearchRoomClear = true;

    [DataMember]
    [Name("Search: Clear The Room Chance")]
    [Description("Percent, rolled once per door (times the chance multiplier above). A failed roll walks in as before (door cooldown 25s).")]
    [MinMax(0f, 100f, 1f)]
    public float SearchRoomClearChance = 70f;

    [DataMember]
    [Name("Search: Max Info Age (s)")]
    [Description("Seconds since the enemy was last seen/heard. Older than this the search no longer assumes he is still in that room.")]
    [MinMax(25f, 300f, 1f)]
    public float SearchRoomClearMaxInfoAge = 120f;

    [DataMember]
    [Name("Room Clear Real Grenade Chance")]
    [Description("Percent, when the bot has a grenade. Otherwise (or when unsafe) it dashes in without one - possibly after a fake.")]
    [MinMax(0f, 100f, 1f)]
    public float RoomClearGrenadeChance = 55f;

    [DataMember]
    [Name("Run-By Peek Chance")]
    [Description("Percent. Instead of the jump peek: sprint along the corridor straight past the open doorway glancing in, turn around, run past it once more back to the frame, then settle in. Always used when a jump isn't safe there (low ceiling, step/stairs).")]
    [MinMax(0f, 100f, 1f)]
    public float RunByChance = 40f;

    [DataMember]
    [Name("Step Peeks After Jump Peek")]
    [Description("After the jump peek, 2-3 quick short steps into the doorway line and back alternating stance and the corner of the room being checked, before holding. Only when the door is open.")]
    public bool StepPeek = true;

    [DataMember]
    [Name("Bots Close Open Doors In Their Way")]
    [Description("Vanilla SAIN closes any open door its path runs into (6s grace for doors it just opened). With a door that opens toward the bot that means: open, walk into the leaf, jitter/spin, close it, open it again... and bots closing open doors all over the map. Off (default): bots only open shut doors while moving.")]
    public bool AutoCloseDoors = false;

    // zzap (2026-10-13): bot sprint ram - SAIN's own (ported from doordash + DualSideDoorBreach), no other mod needed. doordash's
    // config only affects the player; installed or not, bots use the values below.

    [DataMember]
    [Name("Bot Sprint Ram")]
    [Description("Bots can sprint into a shut door and burst it open (ported from the doordash and DualSideDoorBreach mods - neither needs to be installed; if they are, they keep working for the player with their own settings). Used by the room clear and by bots running away / rushing / searching through a door (chances below). The rammer burns stamina and takes arm damage. Off: bots always open doors by hand.")]
    public bool BotRamEnabled = true;

    [DataMember]
    [Name("Bot Sprint Ram: Speed Needed (m/s)")]
    [Description("How fast the bot must be moving when it reaches the door for it to burst. doordash's player value is 4.5 m/s - bots slow down while aiming at corners and rarely keep that, so theirs is lower. Too slow at the door -> the bot opens it normally.")]
    [MinMax(1.5f, 6f, 0.1f)]
    public float BotRamMinSpeed = 2.8f;

    [DataMember]
    [Name("Bot Sprint Ram: Arm Damage")]
    [Description("Damage to the rammer's left arm per burst door (metal doors x2; EFT may fracture the arm like a fall). doordash's player default is 10. 0 = none.")]
    [MinMax(0f, 30f, 1f)]
    public float BotRamDamage = 5f;

    [DataMember]
    [Name("Bot Sprint Ram: Both Sides Of A Door")]
    [Description("NOT USED since 10/14 (always off): a door burst the other way round ended with its leaf where EFT's pathing doesn't expect it and bots got stuck behind it. Bots only ram doors that swing away from them and that BSG marks as breachable, and SAIN keeps BSG's own direction on a bot ram even with DualSideDoorBreach installed.")]
    public bool BotRamBothSides = true;

    [DataMember]
    [Name("Sprint Ram Into The Room")]
    [Description("A room clear that dashes in without a grenade takes a run-up and sprints through the shut door instead of opening it from the side. Only shut (not locked) doors with a straight run-up in front of them. If the ram doesn't happen (too slow, blocked), the bot opens the door normally. Needs Bot Sprint Ram.")]
    public bool DoorModRam = true;

    [DataMember]
    [Name("Sprint Ram Into The Room: Chance")]
    [Description("Percent, rolled when a grenade-less room clear meets a shut door.")]
    [MinMax(0f, 100f, 1f)]
    public float DoorModRamChance = 50f;

    [DataMember]
    [Name("Sprint Ram While Running Through Doors")]
    [Description("Outside door tactics too: a bot in a fight (running away, rushing, searching or any other move) whose path goes through a shut door 3-7m ahead (straight lane) may sprint into it instead of stopping to open it. The chance depends on what the bot is doing (below). If it doesn't burst (too slow, wrong angle), the bot opens the door normally. Needs Bot Sprint Ram.")]
    public bool DoorModRamOnMove = true;

    [DataMember]
    [Name("Ram Chance - Running Away")]
    [Description("Percent, rolled once per door: retreating / running for cover with the enemy on it (shot at or seen in the last few seconds).")]
    [MinMax(0f, 100f, 1f)]
    public float DoorModRamFleeChance = 75f;

    [DataMember]
    [Name("Ram Chance - Rushing")]
    [Description("Percent, rolled once per door: rushing / closing in on an enemy.")]
    [MinMax(0f, 100f, 1f)]
    public float DoorModRamRushChance = 65f;

    [DataMember]
    [Name("Ram Chance - Searching")]
    [Description("Percent, rolled once per door: searching for an enemy that might be behind it (not sure where he is). The bot speeds up for the last meters to hit the door hard.")]
    [MinMax(0f, 100f, 1f)]
    public float DoorModRamSearchChance = 50f;

    // zzap (2026-10-13, user: "1-2 rams in 15 minutes - I barely saw it, is the ram tactic even worth having?"): the 21:47 sim
    // had only 13 rolls in 60 minutes over 4 maps - the chance wasn't the limit, the three situations were. Every other
    // combat move through a shut door (cover runs without pressure, flanks, squad moves, repositioning) gets this one.
    [DataMember]
    [Name("Ram Chance - Other Combat Moves")]
    [Description("Percent, rolled once per door: any other move in a fight (running to cover without being shot at, flanking, squad moves, repositioning) whose path goes through a shut door.")]
    [MinMax(0f, 100f, 1f)]
    public float DoorModRamMoveChance = 35f;

    [DataMember]
    [Name("TEST MODE: Bot Sprint Ram Always")]
    [Description("For testing only (sim). Every bot sprint ram whose conditions are met is used (100% chance) - room clear and running through doors. Turn it off for normal play.")]
    public bool TestModeDoorMods = false;

    [DataMember]
    [Name("TEST MODE: All PMCs Use GigaChad Tactics")]
    [Description("For testing only. Every PMC plans door tactics as a GigaChad (peek / trap / fakes) and the start chance is 100%. Turn it off for normal play.")]
    public bool TestModeAllPmcGigaChad = false;

    [DataMember]
    [Name("Diagnostic Logs")]
    [Description("Writes [DoorTactic] lines to BepInEx LogOutput.log: every tactic start, step change and result.")]
    public bool DiagnosticLogs = false;

    [DataMember]
    [Name("Verbose Logs")]
    [Description("Also logs why a nearby door was NOT used (rate limited). Noisy, for debugging only.")]
    public bool VerboseLogs = false;
}
