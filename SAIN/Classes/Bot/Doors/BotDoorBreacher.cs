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
///   - DualSideDoorBreach: its KickOpen prefix already turns every kick (bots included) away from the kicker while its
///     "Enabled" + "AdjustOpenDirection" are on. Then we leave the angle to it - flipping it here too would turn it back.
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
    private const float CAST_RADIUS = 0.3f;
    private const float CAST_DISTANCE = 1.0f;

    private static readonly List<Job> _jobs = new();
    private static int _doorColliderMask = -1;

    private static DoorTacticSettings Settings => GlobalSettingsClass.Instance?.General?.DoorTactics;

    /// <summary>F6 Door Tactics > "Bot Sprint Ram" on (no other mod needed).</summary>
    public static bool Active => Settings?.BotRamEnabled == true;

    /// <summary>
    /// zzap (2026-10-14, user screenshots + "with DualSideDoorBreach the ram happens but the door opens oddly and they can't get
    /// in"): turning a breach the other way round (negated angle, DualSideDoorBreach's way) leaves the leaf on the side EFT's
    /// door NavMesh link and obstacle don't expect - bots got shoved behind the leaf (Factory) or ran on the spot against a
    /// door that opened the wrong way (Labs). Bots now only ram doors that swing away from them, and the F6 switch is ignored.
    /// </summary>
    public static bool BothSides => false;

    /// <summary>Bot speed (m/s) needed to burst a door - SAIN's own, not doordash's "Velocity Threshold".</summary>
    public static float MinSpeed => Mathf.Max(0.5f, Settings?.BotRamMinSpeed ?? 2.8f);

    /// <summary>A door this bot could ram from where it stands: shut, operatable, and - unless both sides are allowed - not
    /// swinging into it.</summary>
    public static bool CanRam(Door door, Vector3 position)
    {
        if (door == null || door.DoorState != EDoorState.Shut || !door.Operatable || !door.enabled)
        {
            return false;
        }
        // CanBeBreached: BSG's own "this door can be kicked" (Labs glass doors etc. are not) - Interact(Breach) doesn't check it
        return door.CanBeBreached && !WillSwingToward(door, position);
    }

    /// <summary>
    /// doordash's RaycastBreacher.WillDoorSwingTowardsPlayer (the same test as vanilla Door.BreachSuccessRoll, mirrored): the
    /// door's swing (halfway between its shut and open facing) points at the position.
    /// </summary>
    public static bool WillSwingToward(Door door, Vector3 position)
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
        try
        {
            center = door.transform.TransformPoint(door.viewTarget1);
            normal = door.GetDoorRotation(door.GetAngle(EDoorState.Shut)) * WorldInteractiveObject.GetRotationAxis(door.DoorForward, door.transform);
        }
        catch (Exception)
        {
            return false;
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
        try
        {
            // the door swings away from the bot (CanRam): keep BSG's own angle even if DualSideDoorBreach would turn it
            SwingFlip.Begin(door, door.OpenAngle);
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
        TacticDiagnostics.Count("doorram.breach");
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

    /// <summary>The angle SAIN's own breach must end at (BSG's OpenAngle), for the InitSmoothOpen patch below.</summary>
    internal static class SwingFlip
    {
        public static Door Door { get; private set; }
        public static bool Active { get; private set; }
        public static float Angle { get; private set; }

        public static void Begin(Door door, float angle)
        {
            Door = door;
            Angle = angle;
            Active = true;
        }

        public static void End()
        {
            Door = null;
            Active = false;
        }
    }
}

/// <summary>
/// Started as a port of DualSideDoorBreach's DoorInitSmoothOpenPatch (MIT, (c) 2026 maschine34675), which negates the Breaching
/// angle. Since 10/14 it does the opposite job: inside BotDoorBreacher.Breach only (synchronous: Interact(Breach) -> KickOpen ->
/// InitSmoothOpen) it runs last and puts BSG's own angle back, so a bot's ram never ends with a leaf turned the wrong way
/// (DualSideDoorBreach's prefix sees Interact(Breach)'s KickOpen(door position) and may turn it). Anyone else's kick: untouched.
/// </summary>
public class BotBreachSwingFlipPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(WorldInteractiveObject.InteractionState), nameof(WorldInteractiveObject.InteractionState.InitSmoothOpen),
            [typeof(EDoorState), typeof(float), typeof(float), typeof(AnimationCurve), typeof(bool)]);
    }

    [PatchPrefix]
    [HarmonyPriority(Priority.Last)]
    public static void Prefix(EDoorState state, ref float resultAngle)
    {
        if (BotDoorBreacher.SwingFlip.Active && state == EDoorState.Breaching)
        {
            if (!Mathf.Approximately(resultAngle, BotDoorBreacher.SwingFlip.Angle))
            {
                TacticDiagnostics.Count("doorram.turnUndone");
            }
            resultAngle = BotDoorBreacher.SwingFlip.Angle;
        }
    }
}
