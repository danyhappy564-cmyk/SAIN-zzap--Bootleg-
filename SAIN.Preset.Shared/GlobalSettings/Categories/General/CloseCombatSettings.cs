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
    [Description("Meters. Enemy (last known position) closer than this counts as a close standoff. 2026-09-28: 30 -> 12 - walking to cover facing the enemy at 9-30m kept bots slow and in the open (33 of 71 deaths); further out a sprint to cover is safer.")]
    [MinMax(5f, 60f, 1f)]
    public float Distance = 12f;

    [DataMember]
    [Name("Seen Within")]
    [Description("Seconds. Only while the enemy is visible or was seen this recently. Longer ago = the bot may sprint as usual (it's not in the enemy's sights).")]
    [MinMax(0.5f, 10f, 10f)]
    public float SeenWithin = 4f;

    [DataMember]
    [Name("Sprint If Cover Within")]
    [Description("Meters. Exception: if the cover the bot is heading to is this close, it dashes there (a short sprint into cover is what players do too; 2026-09-28: 3 -> 8). 0 = never.")]
    [MinMax(0f, 15f, 10f)]
    public float SprintIfCoverWithin = 8f;

    [DataMember]
    [Name("Suppression Discipline")]
    [Description("Limits blind suppressive fire at an enemy the bot can't see: only right after contact, short bursts with a pause, and never below a healthy magazine - so it isn't caught reloading with nothing left.")]
    public bool SuppressionDiscipline = true;

    [DataMember]
    [Name("Suppress Only Within")]
    [Description("Seconds. Suppress an unseen enemy only if it was seen / shot at the bot / hit the bot this recently. Caps the personality values (TwitchPlayers had 12-30s for being shot at).")]
    [MinMax(0.5f, 15f, 10f)]
    public float SuppressMaxTimeSinceContact = 3f;

    [DataMember]
    [Name("Suppress Keep Magazine")]
    [Description("0-1. Stop suppressing once the magazine is below this fraction (vanilla 0.33). Keeps rounds for the enemy who actually peeks.")]
    [MinMax(0.2f, 0.9f, 100f)]
    public float SuppressMinAmmoRatio = 0.6f;

    [DataMember]
    [Name("Suppress Burst Rounds")]
    [Description("Rounds per suppression burst before a pause.")]
    [MinMax(2f, 15f, 1f)]
    public float SuppressBurstRounds = 5f;

    [DataMember]
    [Name("Suppress Burst Pause")]
    [Description("Seconds of pause between suppression bursts (randomized 0.7x-1.3x).")]
    [MinMax(0.5f, 8f, 10f)]
    public float SuppressBurstPause = 3f;

    [DataMember]
    [Name("Diamond Step While Shooting")]
    [Description("GigaChad/Chad/Wreckless/Normal: while shooting at a visible enemy, tap-dance around the spot - quick W/A/S/D taps (A/D 60%, mostly alternating; W/S 40%) with a rare tiny stop - instead of standing still. The gun stays on the enemy the whole time.")]
    public bool DiamondStep = true;

    [DataMember]
    [Name("Diamond Step Max Distance")]
    [Description("Meters. Only when the enemy is closer than this (and at least 3m away). Further out the bot stands and shoots as usual.")]
    [MinMax(5f, 80f, 1f)]
    public float DiamondStepMaxDistance = 40f;

    [DataMember]
    [Name("Diamond Step Leash")]
    [Description("Meters. How far the bot may drift from the spot where it started shooting; beyond this the next tap goes back toward it.")]
    [MinMax(0.5f, 3f, 10f)]
    public float DiamondStepSize = 1.5f;

    [DataMember]
    [Name("Diamond Step Tap Time")]
    [Description("Seconds one direction is held (randomized 0.65x-1.35x), like tapping A/D. Lower = faster tap-dance. Below ~0.1s EFT's movement inertia barely moves the body; above ~0.35s it looks like plain strafing.")]
    [MinMax(0.08f, 0.5f, 100f)]
    public float DiamondStepTapTime = 0.13f;

    [DataMember]
    [Name("Diamond Step Tap Time (No-Inertia Bots)")]
    [Description("Seconds per tap used instead of the one above when Classic Movement gives bots inertia-less movement (BotsUseOldMovement on, detected automatically). Without inertia every tap turns instantly, so 0.13s looks like jittering - closer to a player's rhythm (the recorded player: ~0.5s between A/D switches) reads right.")]
    [MinMax(0.08f, 0.8f, 100f)]
    public float DiamondStepTapTimeNoInertia = 0.3f;

    [DataMember]
    [Name("Diamond Step: Plant To Fire")]
    [Description("While the bot is firing a burst at an enemy at least the distance below away, it stops for a split second (like a player letting go of A/D to shoot), then goes back to tapping. EFT gives a moving bot 1.5x wider spread and 1.5x longer aim time; this keeps the shots accurate without standing still - at most one stop every two taps. Closer than the distance it never stops.")]
    public bool DiamondStepPlant = true;

    [DataMember]
    [Name("Diamond Step: Plant Min Distance")]
    [Description("Meters. No plant closer than this - up close moving matters more than spread (and under 3m EFT adds no aim offset at all).")]
    [MinMax(3f, 30f, 1f)]
    public float DiamondStepPlantMinDistance = 8f;

    [DataMember]
    [Name("Diamond Step: Plant Time")]
    [Description("Seconds one stop lasts (randomized 0.8x-1.25x). About 0.15s is needed for the body to stop under EFT's inertia, so below that the shot is still a moving shot.")]
    [MinMax(0.1f, 0.4f, 100f)]
    public float DiamondStepPlantTime = 0.22f;

    [DataMember]
    [Name("TEST MODE: Diamond Step For Everyone")]
    [Description("For testing only. Every bot (every personality, scavs too) diamond-steps while shooting.")]
    public bool DiamondStepTestMode = false;

    [DataMember]
    [Name("Corner Hold Aim Offset")]
    [Description("Holding a corner/doorway: how far off the wall edge the gun is held, toward the open side the enemy steps out to, as a fraction of the distance to the corner (clamped 0.6-1.5m). 0 = right along the edge (hugs the wall), 0.3 = default (~17 degrees off the edge).")]
    [MinMax(0f, 0.6f, 100f)]
    public float CornerHoldAimOffset = 0.3f;

    [DataMember]
    [Name("Corner Jiggle Peek")]
    [Description("Shooting from a corner or door frame (one side step puts the body behind cover): now and then the diamond step turns into a jiggle peek - quick A/D taps out of cover and back, the body showing and hiding while it shoots, to throw off the enemy's aim.")]
    public bool CornerJiggle = true;

    [DataMember]
    [Name("Corner Jiggle Peek Chance")]
    [Description("Percent, rolled once per engagement when the bot is at such a corner.")]
    [MinMax(0f, 100f, 1f)]
    public float CornerJiggleChance = 50f;

    [DataMember]
    [Name("Door/Squad Hold: Step To See The Angle")]
    [Description("Door tactics holding a door (trap, ambush, overwatch, rear guard) and squad angles (crossfire, covering a mate): if the wall is between the gun and what the bot watches, it steps to the first spot (toward the door / the angle, else sideways) where the line is open, up to the distance below. Not used while in cover - cover is meant to hide the bot.")]
    public bool HoldStepToOpenAngle = true;

    [DataMember]
    [Name("Door/Squad Hold: Max Step (m)")]
    [Description("How far a door or squad hold may move to open its angle.")]
    [MinMax(0.5f, 3f, 10f)]
    public float HoldStepToOpenAngleMax = 1.5f;

    [DataMember]
    [Name("Lean While Shooting")]
    [Description("Diamond-stepping bots also lean while shooting a visible enemy, like the player does: hold the lean toward the side the enemy is moving (0.35-1s), switch when he reverses, and mix in short Q/E bursts (more often up close).")]
    public bool LeanSpam = true;

    [DataMember]
    [Name("Lean While Shooting Chance")]
    [Description("Percent, rolled once per engagement (each time the diamond step starts).")]
    [MinMax(0f, 100f, 1f)]
    public float LeanSpamChance = 60f;

    [DataMember]
    [Name("Lean Q/E Burst Interval")]
    [Description("Seconds between left and right inside a Q/E burst (randomized 0.7x-1.3x). The player's own measured rhythm was ~0.13s.")]
    [MinMax(0.06f, 0.5f, 100f)]
    public float LeanSpamInterval = 0.13f;

    [DataMember]
    [Name("Lean Hold Min")]
    [Description("Seconds. Shortest lean hold between Q/E bursts.")]
    [MinMax(0.1f, 3f, 100f)]
    public float LeanHoldMin = 0.35f;

    [DataMember]
    [Name("Lean Hold Max")]
    [Description("Seconds. Longest lean hold between Q/E bursts.")]
    [MinMax(0.1f, 3f, 100f)]
    public float LeanHoldMax = 1f;

    [DataMember]
    [Name("Lean Follows Enemy Above Speed")]
    [Description("m/s. When the enemy moves sideways (from the bot's view) faster than this, the lean is held toward that side. Slower = keep the side, sometimes swap.")]
    [MinMax(0.1f, 5f, 10f)]
    public float LeanFollowEnemySpeed = 0.8f;

    [DataMember]
    [Name("Lean Q/E Burst Chance Close")]
    [Description("Percent, each time a hold ends while the enemy is within the close distance below: do a Q/E burst (2-4 switches) instead of another hold.")]
    [MinMax(0f, 100f, 1f)]
    public float LeanRockChanceClose = 45f;

    [DataMember]
    [Name("Lean Q/E Burst Chance Far")]
    [Description("Percent, same beyond the close distance.")]
    [MinMax(0f, 100f, 1f)]
    public float LeanRockChanceFar = 15f;

    [DataMember]
    [Name("Lean Q/E Burst Close Distance")]
    [Description("Meters.")]
    [MinMax(1f, 30f, 1f)]
    public float LeanRockCloseDistance = 7f;

    [DataMember]
    [Name("Fight Instead Of Running Across The Open")]
    [Description("Being shot at by a visible enemy within the distance below and no cover point close: GigaChad/Chad/Wreckless (Normal only when cover is twice as far) keep fighting with the diamond step instead of sprinting across open ground to far cover. Raid logs: that run was the biggest single cause of death (SeekCover, enemy visible 10-20m).")]
    public bool ExposedCommit = true;

    [DataMember]
    [Name("Exposed Commit Distance")]
    [Description("Meters to the enemy.")]
    [MinMax(8f, 40f, 1f)]
    public float ExposedCommitDistance = 22f;

    [DataMember]
    [Name("Exposed Commit Cover Distance")]
    [Description("Meters. Nearest known cover point further than this = too far to run to under fire.")]
    [MinMax(2f, 20f, 1f)]
    public float ExposedCommitCoverDistance = 6f;

    [DataMember]
    [Name("Utility Decision For Hidden Enemy")]
    [Description("Enemy known but out of sight: score push / grenade / hold / flank / search / fall back by expected gain from the situation (info freshness, is he holding an angle or coming, health, magazine, grenade, numbers, his gear and whether he's healing/reloading, how long he's been quiet, distance, personality) and do the best one that can run. [Utility] logs the scores and reasons. Off = SAIN's fixed decision order.")]
    public bool UtilityHiddenEnemy = true;

    [DataMember]
    [Name("Utility Decision For Enemy In Sight")]
    [Description("Enemy in sight: keep shooting / break line of sight to cover / push, by expected gain (is he looking at me, exposure time, just got hit, health, magazine, how far the nearest cover is, his gear and whether he's reloading/healing, my gun vs range, mates on him, personality). Replaces SAIN's fixed 'hold ground N seconds then cover'. [UtilityV] logs the scores and reasons.")]
    public bool UtilityVisibleEnemy = true;

    [DataMember]
    [Name("Utility Reload Timing")]
    [Description("In a fight with the magazine under 80%: reload now / keep shooting / switch to pistol by expected gain (mag left, is he looking at me, in sight, in cover, distance, he's reloading/healing, just got hit, loaded pistol). And the quick reload (drop the mag) only when urgent - with time and 30%+ left, a normal reload keeps the rounds. [Reload] logs why.")]
    public bool UtilityReload = true;

    [DataMember]
    [Name("Utility Heal Timing")]
    [Description("With enemies around: first aid / stim / surgery / wait by expected gain (health, bleeding, in cover, enemy in sight, someone coming, just hit, how old and far the nearest threat is). [Heal] logs why.")]
    public bool UtilityHeal = true;

    [DataMember]
    [Name("Utility Mistakes")]
    [Description("Human mistakes: now and then a bot takes its 2nd/3rd best option instead of the best - Timmy 12%, Wreckless 8%, Coward 6%, Normal 5%, Chad 4%, Rat/SnappingTurtle 3%, GigaChad 2%. Off in the zzap TEST preset.")]
    public bool UtilityMistakes = true;

    [DataMember]
    [Name("Utility Mistake Multiplier")]
    [Description("Multiplies every personality's mistake chance.")]
    [MinMax(0f, 3f, 10f)]
    public float UtilityMistakeMultiplier = 1f;

    [DataMember]
    [Name("Fear")]
    [Description("Per-personality fear in the utility decisions: base (Coward 0.35, Timmy/Rat 0.2, Normal/Turtle 0.1, Chad 0.05, GigaChad/Wreckless 0) plus a squadmate dying nearby, the player's kill streak, being hurt, outnumbered or pinned down - scaled by bravery (Wreckless barely, Coward a lot). Fear favors falling back / holding over pushing; above 0.75 it's panic.")]
    public bool Fear = true;

    [DataMember]
    [Name("Fear Multiplier")]
    [Description("Multiplies every bot's fear (0 = fearless).")]
    [MinMax(0f, 3f, 10f)]
    public float FearMultiplier = 1f;

    [DataMember]
    [Name("Utility Mix Margin")]
    [Description("How much chance mixes into the hidden-enemy choice (weighted dice: the higher a stance scores, the likelier - per personality: Wreckless loosest ... Rat/Coward tightest). 0 = always the best score, 1 = default, 2+ = very unpredictable.")]
    [MinMax(0f, 3f, 10f)]
    public float UtilityMixMargin = 1f;

    [DataMember]
    [Name("Threat Targeting")]
    [Description("Pick the target by threat, not by who was seen first: an enemy hitting / shooting at the bot right now (even unseen, from behind) beats the one it was aiming at, weighted by distance, being in sight and looking at it; the current target keeps a bonus so it doesn't flip. [Threat] logs switches.")]
    public bool ThreatTargeting = true;

    [DataMember]
    [Name("Threat Target Keep Time")]
    [Description("Seconds. Once threat targeting switched to an enemy, keep him at least this long (and as long as he keeps shooting at the bot) - without it the bot flipped between the unseen shooter and the visible enemy every 0.1s.")]
    [MinMax(0f, 6f, 0.5f)]
    public float ThreatTargetKeepTime = 2f;

    [DataMember]
    [Name("Grenade Judgment")]
    [Description("A grenade only when the enemy is out of sight, his position is recent (info age below), and the thrower is safe: not being shot, the enemy's path to it is long enough that he can't rush it during the throw, not badly hurt, no other enemy in sight. Then weighed by situation: +30% he's been camping in one spot 6s+, +10% indoors, +15% healing/reloading, -20% info over 10s old (base 50%). [Nade] logs why.")]
    public bool GrenadeDiscipline = true;

    [DataMember]
    [Name("Grenade Max Info Age")]
    [Description("Seconds since the enemy's position was last updated (seen/heard).")]
    [MinMax(3f, 120f, 1f)]
    public float GrenadeMaxInfoAge = 20f;

    [DataMember]
    [Name("Grenade Max Throw Distance")]
    [Description("Meters. With grenade judgment on, no grenade at an enemy known further away than this (a hand throw doesn't reach 60-70m; those attempts only searched for an arc that never existed).")]
    [MinMax(15f, 75f, 1f)]
    public float GrenadeMaxThrowDistance = 45f;

    [DataMember]
    [Name("Grenade Min Rush Distance")]
    [Description("Meters of path from the enemy to the thrower - closer and he could push it during the throw.")]
    [MinMax(0f, 40f, 1f)]
    public float GrenadeMinRushDistance = 10f;

    [DataMember]
    [Name("Light Discipline")]
    [Description("Flashlight and laser off while holding an angle / ambushing with the enemy out of sight (standing or creeping, enemy known within 60m in the last 60s) and whenever a tactic holds a spot. Turned on only when the enemy shows up, to blind him. Field report: bots holding inside a room lit up the doorway.")]
    public bool LightDiscipline = true;

    [DataMember]
    [Name("Corner Pre-Aim")]
    [Description("Moving toward a known enemy who is out of sight: within 6m of the corner/doorway he'd appear from, stop sprinting, aim at that corner before rounding it and hold the lean to that side - no more stepping out half the body first and only then turning to look.")]
    public bool CornerPreAim = true;

    [DataMember]
    [Name("Corner Chase")]
    [Description("The enemy breaks sight around a corner close by (within the window below): pushy bots (GigaChad 90%, Wreckless 85%, Chad 75%, Normal 40%) follow and take the corner one of four ways - prefire (only when sure he is right there and the magazine has enough left), jump shot (headroom needed), lean in (lean held toward the corner side) or a slow wide pie.")]
    public bool CornerChase = true;

    [DataMember]
    [Name("Corner Chase Window")]
    [Description("Seconds since the enemy was last seen in which a chase can start.")]
    [MinMax(1f, 20f, 10f)]
    public float CornerChaseWindow = 6f;

    [DataMember]
    [Name("Corner Chase Max Distance")]
    [Description("Meters of path to the enemy.")]
    [MinMax(3f, 40f, 1f)]
    public float CornerChaseMaxDistance = 15f;

    [DataMember]
    [Name("Corner Chase Prefire Chance")]
    [Description("Percent, when the bot is sure the enemy is at the corner and has the rounds.")]
    [MinMax(0f, 100f, 1f)]
    public float CornerChasePrefireChance = 60f;

    [DataMember]
    [Name("Corner Chase Prefire Min Rounds")]
    [Description("Rounds in the magazine needed to prefire (also at least 40% of the magazine). Prefire stops at 30% left - there is no reloading once inside.")]
    [MinMax(1f, 60f, 1f)]
    public float CornerChasePrefireMinRounds = 10f;

    [DataMember]
    [Name("Corner Chase Jump Shot Chance")]
    [Description("Percent, GigaChad/Chad/Wreckless with headroom.")]
    [MinMax(0f, 100f, 1f)]
    public float CornerChaseJumpChance = 30f;

    [DataMember]
    [Name("Corner Chase Pie Speed")]
    [Description("Move speed (0-1) while slicing the corner slowly.")]
    [MinMax(0.1f, 1f, 100f)]
    public float CornerChasePieSpeed = 0.35f;

    [DataMember]
    [Name("Retreat Head Down")]
    [Description("While sprinting away with the back to an enemy that sees the bot or shot at it in the last 3s, look down at the floor like players do - the head drops out of the easy line of fire.")]
    public bool RetreatHeadDown = true;

    [DataMember]
    [Name("Retreat Head Down Angle")]
    [Description("Degrees below the horizon.")]
    [MinMax(10f, 70f, 1f)]
    public float RetreatHeadDownPitch = 55f;

    [DataMember]
    [Name("Retreat Weave")]
    [Description("Same trigger as Retreat Head Down: the run swings left/right every 0.45-0.8s instead of a straight line, sometimes with a hop.")]
    public bool RetreatWeave = true;

    [DataMember]
    [Name("Retreat Weave Angle")]
    [Description("Degrees off the run line.")]
    [MinMax(5f, 60f, 1f)]
    public float RetreatWeaveAngle = 30f;

    [DataMember]
    [Name("Retreat Weave Jump Chance")]
    [Description("Percent per left/right switch (headroom needed).")]
    [MinMax(0f, 100f, 1f)]
    public float RetreatWeaveJumpChance = 12f;

    [DataMember]
    [Name("Retreat Head Down Max Distance")]
    [Description("Meters. Enemy further than this = run normally.")]
    [MinMax(10f, 200f, 1f)]
    public float RetreatHeadDownMaxDistance = 80f;

    [DataMember]
    [Name("Fight Close Instead Of Cover")]
    [Description("When the enemy is visible and this close, keep shooting (with the diamond step) instead of walking off to cover - at point-blank range the walk just gets the bot shot. Not while healing/reloading or under 20% magazine; Cowards still run.")]
    public bool CloseFight = true;

    [DataMember]
    [Name("Fight Close Distance")]
    [Description("Meters. Enemy closer than this = fight it out.")]
    [MinMax(2f, 25f, 1f)]
    public float CloseFightDistance = 8f;

    [DataMember]
    [Name("Pistol Swap When Empty")]
    [Description("Magazine runs dry mid-fight with the enemy in sight and close: draw the pistol instead of reloading (faster, like a player). Back to the main gun when the pistol is empty, the enemy is gone for 6s or has moved far away - it is reloaded then.")]
    public bool PistolSwap = true;

    [DataMember]
    [Name("Pistol Swap Max Distance")]
    [Description("Meters. Only swap to the pistol when the enemy is closer than this.")]
    [MinMax(5f, 60f, 1f)]
    public float PistolSwapMaxDistance = 30f;

    [DataMember]
    [Name("Quick Reload In Combat")]
    [Description("In a fight (enemy seen within 10s or under fire) reload like double-tapping R: the old magazine is dropped on the ground instead of stowed, which is faster. Out of combat the normal reload is used.")]
    public bool QuickReload = true;

    [DataMember]
    [Name("Long-Range Weapon Swap")]
    [Description("If the second primary slot holds a DMR or sniper rifle and the enemy is seen far away, switch to it; switch back to the main gun when the enemy gets close or is gone.")]
    public bool SniperSwap = true;

    [DataMember]
    [Name("Long-Range Swap Distance")]
    [Description("Meters. Switch to the DMR/sniper when the enemy is at least this far.")]
    [MinMax(30f, 300f, 1f)]
    public float SniperSwapMinDistance = 80f;

    [DataMember]
    [Name("Long-Range Swap Back Distance")]
    [Description("Meters. Switch back to the main gun when the enemy is closer than this.")]
    [MinMax(10f, 150f, 1f)]
    public float SniperSwapBackDistance = 45f;

    [DataMember]
    [Name("PMC Only")]
    [Description("Apply everything above only to PMCs. Scavs keep vanilla behaviour.")]
    public bool PmcOnly = true;
}
