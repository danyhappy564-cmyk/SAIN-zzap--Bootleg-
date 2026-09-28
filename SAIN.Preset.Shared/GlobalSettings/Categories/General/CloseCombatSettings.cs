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
    [Name("Sprint If Cover Within")]
    [Description("Meters. Exception: if the cover the bot is heading to is this close, it may dash there (a short sprint into cover is what players do too). 0 = never.")]
    [MinMax(0f, 8f, 10f)]
    public float SprintIfCoverWithin = 3f;

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
    [Name("TEST MODE: Diamond Step For Everyone")]
    [Description("For testing only. Every bot (every personality, scavs too) diamond-steps while shooting.")]
    public bool DiamondStepTestMode = false;

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
