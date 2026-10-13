using System;
using System.Collections.Generic;
using EFT;
using EFT.Interactive;
using HarmonyLib;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.GlobalSettings.Categories.General;
using SAIN.SAINComponent.Classes.Tactics;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Mover;

/// <summary>
/// zzap fork (2026-10-13, user: "port the code in - doordash too if it isn't installed, bots only; the doordash config is the
/// player's, bots get SAIN's own values; must not break for people who have doordash / DualSideDoorBreach installed").
///
/// SAIN's own sprint ram for bots. Ported from two mods, credited here and in the README:
///   - doordash (bmpq / tarkin, https://github.com/bmpq/spt-doordash, RaycastBreacher): sprint into a shut door -> breach it
///     (LockForInteraction, SetUser, Interact(Breach)), the rammer takes arm damage and burns stamina.
///   - DualSideDoorBreach 2.0.2 (maschine34675, https://github.com/maschine34675/DualSideDoorBreach, MIT,
///     Copyright (c) 2026 maschine34675): a door rammed from the side it would swing into is breached the other way round
///     (the Breaching angle handed to InteractionState.InitSmoothOpen is negated), so both sides of a door work.
///
/// Differences from doordash: only bots, only while a SAIN door tactic / DoorRammer has asked for it (a job with an end time,
/// never a component left on the bot); cast along the bot's horizontal velocity (bots aim at corners while they run, so the
/// body's forward is often not where they go); speed threshold, arm damage and both-sides switch from F6 (Door Tactics), never
/// from doordash's config. No door dislodge physics (doordash's PhysicalDoor works off the main player).
///
/// Installed alongside:
///   - doordash: untouched - its RaycastBreacher stays on the main player only, with its own config.
///   - DualSideDoorBreach: its KickOpen prefix turns every kick (bots included) by its own side test. On SAIN's own breaches
///     BotBreachSwingFlipPatch runs after it and sets the angle outright (10/14: its test reads doors rammed from beside the
///     hinge, or left a little off shut, the wrong way round - see WillSwingToward). Everyone else's kicks are left to it.
/// </summary>
public static class BotDoorBreacher
{
    private struct Job
    {
        public Player Player;
        public float Until;
    }

    private const int MAX_JOBS = 16;
    private const float CAST_HEIGHT = 1.2f;
    // 10/14 (user: "bots aren't precise - too many limits"): a wider, longer cast than doordash's player one (0.3 / 1.0)
    private const float CAST_RADIUS = 0.45f;
    private const float CAST_DISTANCE = 1.4f;

    private static readonly List<Job> _jobs = new();
    private static int _doorColliderMask = -1;

    private static DoorTacticSettings Settings => GlobalSettingsClass.Instance?.General?.DoorTactics;

    /// <summary>F6 Door Tactics > "Bot Sprint Ram" on (no other mod needed).</summary>
    public static bool Active => Settings?.BotRamEnabled == true;

    public static bool BothSides => Settings?.BotRamBothSides != false;

    /// <summary>Bot speed (m/s) needed to burst a door - SAIN's own, not doordash's "Velocity Threshold".</summary>
    public static float MinSpeed => Mathf.Max(0.5f, Settings?.BotRamMinSpeed ?? 1.6f);

    /// <summary>A door this bot could ram from where it stands: shut, operatable, and - unless both sides are allowed - not
    /// swinging into it.</summary>
    public static bool CanRam(Door door, Vector3 position)
    {
        if (door == null || door.DoorState != EDoorState.Shut || !door.Operatable || !door.enabled || IsSliding(door))
        {
            return false;
        }
        // CanBeBreached: BSG's own "this door can be kicked" (Labs glass doors etc. are not) - Interact(Breach) doesn't check it
        if (!door.CanBeBreached)
        {
            return false;
        }
        if (!WillSwingToward(door, position))
        {
            return true;
        }
        // turned away from the bot (ours or DualSideDoorBreach's): only when the reversed leaf has room to land - 10/14 sim, a
        // turned leaf stuck across a passage for the rest of the raid (ReversedLeafUnjam clears the ones this misses)
        return BothSides && BotDoorPushOpen.SwingSpaceClear(door);
    }

    /// <summary>
    /// zzap (2026-10-14, user screenshots: Labs cafeteria glass doors, a bot running on the spot into them): EFT's SlidingDoor is
    /// a Door whose "angle" is a slide - position = Lerp(shut, open, angle / OpenAngle), clamped. A reversed angle (a ram / kick
    /// turned away from the bot, DualSideDoorBreach's or ours, or a push-open) slides it nowhere: the glass stays shut while
    /// the door reads Open and the path runs through it. Never rammed, kicked, turned or pushed open by bots.
    /// </summary>
    public static bool IsSliding(Door door)
    {
        return door is SlidingDoor;
    }

    /// <summary>
    /// The position is on the side of the shut door the leaf swings into.
    /// zzap (2026-10-14 sim, F8 mark #2 + screenshots: a bot kicked a Factory door from right in front of it and the leaf came
    /// at it; all 11 rams that raid read "swings away", none turned): doordash's / vanilla BreachSuccessRoll's test (the same one
    /// DualSideDoorBreach uses) took the leaf's facing from door.transform - which already carries the leaf's current angle, so
    /// a door that sits a little off its shut angle (seen: "Shut angle=-17" after a player went through it) gets every direction
    /// turned by that much - and split the sides along the bisector of the shut and open facings, a line tilted ~55 deg off the
    /// door for a 111 deg door, so a bot standing at the door beside the hinge read "away". Now: the side of the shut leaf's
    /// own plane, from the leaf's geometry with the current angle taken out. The old test only when the leaf can't be measured.
    /// </summary>
    public static bool WillSwingToward(Door door, Vector3 position)
    {
        if (TryLeafSides(door, out Vector3 hinge, out _, out Vector3 swingNormal, out _))
        {
            Vector3 d = position - hinge;
            d.y = 0f;
            return Vector3.Dot(d, swingNormal) > 0f;
        }
        return LegacySwingToward(door, position);
    }

    /// <summary>
    /// The door's leaf at its shut angle, measured from its collider with the current angle taken out (BSG: transform.rotation =
    /// GetDoorRotation(CurrentAngle) * parent.rotation, turning about the transform's position = the hinge). leafDir runs along the
    /// shut leaf from the hinge, swingNormal (flat) points to the side the leaf opens into, halfLeaf = hinge to mid-leaf.
    /// </summary>
    public static bool TryLeafSides(Door door, out Vector3 hinge, out Vector3 leafDir, out Vector3 swingNormal, out float halfLeaf)
    {
        hinge = default;
        leafDir = default;
        swingNormal = default;
        halfLeaf = 0f;
        try
        {
            Transform t = door != null ? door.transform : null;
            Collider leaf = t != null ? door.GetComponentInChildren<Collider>() : null;
            if (t == null || t.parent == null || leaf == null)
            {
                return false;
            }
            hinge = t.position;
            Vector3 local = Quaternion.Inverse(t.rotation) * (leaf.bounds.center - hinge);
            Vector3 shut = door.GetDoorRotation(door.GetAngle(EDoorState.Shut)) * t.parent.rotation * local;
            Vector3 open = door.GetDoorRotation(door.GetAngle(EDoorState.Open)) * t.parent.rotation * local;
            shut.y = 0f;
            open.y = 0f;
            halfLeaf = shut.magnitude;
            if (halfLeaf < 0.15f)
            {
                return false;
            }
            leafDir = shut / halfLeaf;
            swingNormal = new Vector3(-leafDir.z, 0f, leafDir.x);
            float side = Vector3.Dot(open, swingNormal);
            if (Mathf.Abs(side) < 0.05f)
            {
                return false;
            }
            if (side < 0f)
            {
                swingNormal = -swingNormal;
            }
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>doordash's RaycastBreacher.WillDoorSwingTowardsPlayer (vanilla Door.BreachSuccessRoll mirrored) - see WillSwingToward.</summary>
    public static bool LegacySwingToward(Door door, Vector3 position)
    {
        try
        {
            Vector3 doorPoint = door.transform.TransformPoint(door.viewTarget1);
            Vector2 toTarget = new Vector2(position.x - doorPoint.x, position.z - doorPoint.z);
            Vector3 axis = WorldInteractiveObject.GetRotationAxis(door.DoorForward, door.transform);
            Vector3 shutForward = door.GetDoorRotation(door.GetAngle(EDoorState.Shut)) * axis;
            Vector3 openForward = door.GetDoorRotation(door.GetAngle(EDoorState.Open)) * axis;
            Vector3 bisector = shutForward + openForward;
            return Vector2.Dot(toTarget.normalized, new Vector2(bisector.x, bisector.z).normalized) > 0f;
        }
        catch (Exception)
        {
            return true;
        }
    }

    /// <summary>Arms the ram on this bot for <paramref name="seconds"/>: every frame until then, a shut door right in front of
    /// its run while it moves fast enough is breached. Always ends - by Stop, the time running out, death or raid end.</summary>
    public static bool Start(Player player, float seconds)
    {
        if (player == null || player.IsYourPlayer || !Active)
        {
            return false;
        }
        float until = Time.time + seconds;
        for (int i = 0; i < _jobs.Count; i++)
        {
            if (ReferenceEquals(_jobs[i].Player, player))
            {
                _jobs[i] = new Job { Player = player, Until = until };
                return true;
            }
        }
        if (_jobs.Count >= MAX_JOBS)
        {
            return false;
        }
        _jobs.Add(new Job { Player = player, Until = until });
        return true;
    }

    /// <summary>
    /// zzap (2026-10-14, user: "bots coming at the door at an angle don't burst it, then open it by hand - it swings into them and
    /// shoves them aside into the gap"): the ram's fallback at the door. The bot is right at it but too slow / off-line for the
    /// burst: kick it open now instead of opening it by hand (a breach always goes away from the bot - turned if needed).
    /// </summary>
    public static bool Kick(Player player, Door door)
    {
        if (player == null || player.IsYourPlayer || !Active || !CanRam(door, player.Position))
        {
            return false;
        }
        if (SomeoneInSwing(door, player))
        {
            TacticDiagnostics.Count("doorram.skip.someoneBehind");
            return false;
        }
        Stop(player);
        Breach(player, door);
        return true;
    }

    public static void Stop(Player player)
    {
        for (int i = _jobs.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_jobs[i].Player, player))
            {
                _jobs.RemoveAt(i);
            }
        }
    }

    /// <summary>Every frame from DoorHandler.ManualUpdate. At most 16 jobs, one sphere cast each; the loop only removes.</summary>
    public static void Tick(float time)
    {
        TickAfterBreach(time);
        for (int i = _jobs.Count - 1; i >= 0; i--)
        {
            Job job = _jobs[i];
            Player player = job.Player;
            if (player == null || time > job.Until || player.HealthController?.IsAlive != true)
            {
                _jobs.RemoveAt(i);
                continue;
            }
            Door door = DoorInFrontOfRun(player);
            if (door == null)
            {
                continue;
            }
            _jobs.RemoveAt(i);
            Breach(player, door);
        }
    }

    public static void OnNewRaid()
    {
        _jobs.Clear();
        _afterBreach.Clear();
        _breachTimes.Clear();
    }

    // 10/14 sim (user: "one ram, the door burst, the bot stood there, then opened it by hand" - the journal had only the ram):
    // where the leaf ended 2 s after each SAIN ram, and when it was last rammed (DoorOpener logs hand use of such doors).
    private struct AfterBreach
    {
        public Door Door;
        public float Time;
        public string Who;
        public bool Turned;
        public Vector3 From;
    }

    private static readonly List<AfterBreach> _afterBreach = new();
    private static readonly Dictionary<int, float> _breachTimes = new();

    public static bool RecentlyBreached(Door door, float within)
    {
        return door != null && _breachTimes.TryGetValue(door.GetInstanceID(), out float t) && Time.time - t < within;
    }

    /// <summary>"state angle (own open X, reversed)" - for journal lines about a door.</summary>
    public static string DescribeLeaf(Door door)
    {
        if (door == null)
        {
            return "door gone";
        }
        return $"{door.DoorState} angle={door.CurrentAngle:0} (own open {door.GetAngle(EDoorState.Open):0}, shut {door.GetAngle(EDoorState.Shut):0}{(ReversedLeafUnjam.IsReversed(door) ? ", REVERSED" : "")})";
    }

    private static void TickAfterBreach(float time)
    {
        for (int i = _afterBreach.Count - 1; i >= 0; i--)
        {
            AfterBreach a = _afterBreach[i];
            if (time - a.Time < 2f)
            {
                continue;
            }
            _afterBreach.RemoveAt(i);
            if (TacticDiagnostics.LogOn)
            {
                // leafSideOfRammer: where the leaf ended relative to where the rammer stood - "far" is right, "rammer's side" is the bug
                string side = "?";
                if (a.Door != null && TryLeafSides(a.Door, out Vector3 hinge, out _, out Vector3 normal, out _))
                {
                    Collider leaf = a.Door.GetComponentInChildren<Collider>();
                    if (leaf != null)
                    {
                        float leafSide = Vector3.Dot(leaf.bounds.center - hinge, normal);
                        float rammerSide = Vector3.Dot(a.From - hinge, normal);
                        side = leafSide * rammerSide > 0f ? "RAMMER'S SIDE" : "far side";
                    }
                }
                TacticDiagnostics.LogCloseCombat($"[DoorRam] door {(a.Door != null ? a.Door.GetInstanceID() : 0)} 2s after {a.Who}'s ram: {DescribeLeaf(a.Door)} | turned={a.Turned} leaf ended on {side}");
            }
        }
    }

    private static Door DoorInFrontOfRun(Player player)
    {
        if (Time.timeScale <= 0f)
        {
            return null;
        }
        Vector3 velocity = player.Velocity;
        velocity.y = 0f;
        float min = MinSpeed * Time.timeScale;
        if (velocity.sqrMagnitude < min * min)
        {
            return null;
        }
        if (_doorColliderMask == -1)
        {
            // doordash: the player capsule collides with both, and BSG puts door colliders on either layer.
            _doorColliderMask = LayerMask.GetMask("LowPolyCollider", "DoorLowPolyCollider");
        }
        Vector3 origin = player.Position + Vector3.up * CAST_HEIGHT;
        if (!Physics.SphereCast(origin, CAST_RADIUS, velocity.normalized, out RaycastHit hit, CAST_DISTANCE, _doorColliderMask))
        {
            return null;
        }
        Door door = hit.collider != null ? hit.collider.GetComponentInParent<Door>() : null;
        if (!CanRam(door, player.Position))
        {
            return null;
        }
        if (SomeoneInSwing(door, player))
        {
            TacticDiagnostics.Count("doorram.skip.someoneBehind");
            return null; // the job ends by time / DoorRammer falls back to opening it
        }
        return door;
    }

    /// <summary>
    /// zzap (2026-10-14, user screenshots, Labs kitchen double doors): a ram from inside burst the leaf into a bot standing right
    /// next to the door outside and pinned him between the leaf and the wall. Anyone (other than the rammer) on the far side
    /// within the leaf's swing - 1.6 m along the door, 1.5 m out from it - means no ram. One pass over the alive players.
    /// </summary>
    public static bool SomeoneInSwing(Door door, Player rammer)
    {
        var world = Comfort.Common.Singleton<GameWorld>.Instance;
        if (door == null || rammer == null || world?.AllAlivePlayersList == null)
        {
            return false;
        }
        Vector3 center;
        Vector3 normal;
        // 10/14: the shut leaf's own plane (TryLeafSides) - the old normal also carried the leaf's current angle
        if (TryLeafSides(door, out Vector3 hinge, out Vector3 leafDir, out Vector3 swingNormal, out float halfLeaf))
        {
            center = hinge + leafDir * halfLeaf;
            normal = swingNormal;
        }
        else
        {
            try
            {
                center = door.transform.TransformPoint(door.viewTarget1);
                normal = door.GetDoorRotation(door.GetAngle(EDoorState.Shut)) * WorldInteractiveObject.GetRotationAxis(door.DoorForward, door.transform);
            }
            catch (Exception)
            {
                return false;
            }
        }
        normal.y = 0f;
        if (normal.sqrMagnitude < 0.0001f)
        {
            return false;
        }
        normal.Normalize();
        Vector3 along = new Vector3(-normal.z, 0f, normal.x);
        float rammerSide = Vector3.Dot(rammer.Position - center, normal);
        var players = world.AllAlivePlayersList;
        for (int i = 0; i < players.Count; i++)
        {
            Player other = players[i];
            if (other == null || other == rammer || other.HealthController?.IsAlive != true)
            {
                continue;
            }
            Vector3 d = other.Position - center;
            if (Mathf.Abs(d.y) > 2f)
            {
                continue;
            }
            float depth = Vector3.Dot(d, normal);
            if (depth * rammerSide >= 0f || Mathf.Abs(depth) > 1.5f)
            {
                continue; // on the rammer's side, or well clear of the swing
            }
            if (Mathf.Abs(Vector3.Dot(d, along)) < 1.6f)
            {
                return true;
            }
        }
        return false;
    }

    private static void Breach(Player player, Door door)
    {
        bool towardBot = WillSwingToward(door, player.Position);
        // 10/14: SAIN sets the direction itself even with DualSideDoorBreach installed - that mod decides with the old test
        // (LegacySwingToward), which got doors rammed from beside the hinge / doors left a little off shut the wrong way round.
        // BotBreachSwingFlipPatch runs after its prefix and sets the final angle outright, so nothing is turned twice.
        if (towardBot != LegacySwingToward(door, player.Position))
        {
            TacticDiagnostics.Count("doorram.sideFixed");
        }
        try
        {
            SwingFlip.Begin(door, towardBot);
            door.LockForInteraction();
            door.SetUser(player);
            door.Interact(new InteractionResult(EInteractionType.Breach));
        }
        catch (Exception ex)
        {
            TacticDiagnostics.Count("doorram.breachError");
            Logger.LogWarning($"SAIN: bot door ram failed on {door?.name}: {ex.GetType().Name}: {ex.Message}");
            return;
        }
        finally
        {
            SwingFlip.End();
        }
        TacticDiagnostics.Count(towardBot ? "doorram.breach.flipped" : "doorram.breach");
        _breachTimes[door.GetInstanceID()] = Time.time;
        if (_afterBreach.Count < 32)
        {
            _afterBreach.Add(new AfterBreach { Door = door, Time = Time.time, Who = player.Profile?.Nickname, Turned = towardBot, From = player.Position });
        }
        HurtRammer(player, door);
    }

    /// <summary>doordash's AffectPlayer for a bot: stamina hit and arm damage (Fall type, so EFT may fracture it; metal doors
    /// x2). The camera / hands kick and the stun are the player's screen effects and are left out.</summary>
    private static void HurtRammer(Player player, Door door)
    {
        try
        {
            player.Physical?.OnBreach();
            float damage = Settings?.BotRamDamage ?? 0f;
            if (damage <= 0f || player.ActiveHealthController == null)
            {
                return;
            }
            string sound = door.BreachSound != null ? door.BreachSound.name : string.Empty;
            if (sound.Contains("grate") || sound.Contains("metal"))
            {
                damage *= 2f;
            }
            var info = new EFT.Ballistics.DamageInfo { DamageType = EDamageType.Fall };
            player.ActiveHealthController.ApplyDamage(EBodyPart.LeftArm, damage, info);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"SAIN: bot door ram damage failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>DualSideDoorBreach's BreachKickContext, for SAIN's own breaches only.</summary>
    internal static class SwingFlip
    {
        public static Door Door { get; private set; }
        public static bool Flip { get; private set; }

        public static void Begin(Door door, bool flip)
        {
            Door = door;
            Flip = flip;
        }

        public static void End()
        {
            Door = null;
            Flip = false;
        }
    }
}

/// <summary>
/// Ported from DualSideDoorBreach (DoorInitSmoothOpenPatch, MIT, (c) 2026 maschine34675): while SAIN breaches a door from the
/// side it swings into, the Breaching target angle is negated - the door bursts away from the bot. Only inside
/// BotDoorBreacher.Breach (synchronous: Interact(Breach) -> KickOpen -> InitSmoothOpen), never for anyone else's kick.
/// </summary>
public class BotBreachSwingFlipPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(WorldInteractiveObject.InteractionState), nameof(WorldInteractiveObject.InteractionState.InitSmoothOpen),
            [typeof(EDoorState), typeof(float), typeof(float), typeof(AnimationCurve), typeof(bool)]);
    }

    // after DualSideDoorBreach's own InitSmoothOpen prefix: on SAIN's breaches our direction is the final word (10/14).
    // SPT's ModulePatch registers each patch under its class name (new Harmony(GetType().Name)), not the plugin GUID - DSDB's
    // is "DoorInitSmoothOpenPatch". Priority.Last alone already puts us after its default (Normal) prefix.
    [PatchPrefix]
    [HarmonyAfter(DoorModInterop.DualSideGUID, "DoorInitSmoothOpenPatch")]
    [HarmonyPriority(Priority.Last)]
    public static void Prefix(EDoorState state, ref float resultAngle)
    {
        Door door = BotDoorBreacher.SwingFlip.Door;
        if (door != null && state == EDoorState.Breaching)
        {
            // KickOpen passes the door's OpenAngle; DualSideDoorBreach may have negated it already - set it outright
            resultAngle = BotDoorBreacher.SwingFlip.Flip ? -door.OpenAngle : door.OpenAngle;
        }
        // a bot's hand-open pushed away from it (BotDoorPushOpen, F6 Always Push Open Doors)
        else if (BotDoorPushOpen.Active && state == EDoorState.Open)
        {
            resultAngle = -resultAngle;
        }
    }
}
