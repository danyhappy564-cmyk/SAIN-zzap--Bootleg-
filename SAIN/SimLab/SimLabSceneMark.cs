using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Communications;
using EFT.Interactive;
using EFT.UI;
using SAIN.Components;
using SAIN.SAINComponent.Classes.Mover;
using SAIN.SAINComponent.Classes.Tactics;
using UnityEngine;

namespace SAIN.SimLab;

/// <summary>
/// zzap (2026-10-14, user: "you can't even find what I sent screenshots of in the logs? The journal should record enough to see
/// that"): the journal logs decisions when they change, not what a bot looks like on screen - position, speed, where it walks vs
/// where it looks, door next to it, wall in its face. Two tools:
///   - Scene mark (F8, F6 settings "SAIN Editor > Mark Scene Shortcut"): the bots in front of the camera (within 30 deg of its
///     centre, 80 m) get one full state line each in the journal ([Mark] #n) and in the sim summary md, at the raid time of the
///     key press - press it when something looks wrong, then the screenshot and the log line match.
///   - Describe(): the same state line, used by SimLabOddity for its automatic episodes.
/// </summary>
public static class SimLabSceneMark
{
    private const int MAX_BOTS = 4;
    private const int MAX_MARKS = 40;

    private static readonly List<string> _marks = new();
    private static int _count;

    public static IReadOnlyList<string> Marks => _marks;

    public static void Reset()
    {
        _marks.Clear();
        _count = 0;
        _deaths.Clear();
    }

    // 10/14 (user: "this one wasn't marked - dead, so it didn't show up"): F8 only looked at the living. Bots that died within
    // the last 60 s in view are listed too, with their [Death] line. At most 64 kept (oldest dropped).
    private struct Death
    {
        public Vector3 Position;
        public float Time;
        public string Line;
    }

    private static readonly List<Death> _deaths = new();

    public static void NoteDeath(Player p, string line)
    {
        if (p == null)
        {
            return;
        }
        if (_deaths.Count >= 64)
        {
            _deaths.RemoveAt(0);
        }
        _deaths.Add(new Death { Position = p.Position, Time = Time.time, Line = line });
    }

    public static void Mark()
    {
        try
        {
            var world = Singleton<GameWorld>.Instance;
            Camera cam = Camera.main;
            if (world?.AllAlivePlayersList == null || cam == null)
            {
                return;
            }
            Vector3 eye = cam.transform.position;
            Vector3 fwd = cam.transform.forward;
            var picked = new List<(Player p, float angle, float dist)>();
            var players = world.AllAlivePlayersList;
            for (int i = 0; i < players.Count; i++)
            {
                Player p = players[i];
                if (p == null || !p.IsAI)
                {
                    continue;
                }
                Vector3 to = p.Position + Vector3.up * 1.2f - eye;
                float dist = to.magnitude;
                if (dist > 80f || dist < 0.1f)
                {
                    continue;
                }
                float angle = Vector3.Angle(fwd, to);
                if (angle < 30f)
                {
                    picked.Add((p, angle, dist));
                }
            }
            var dead = new List<(Death d, float angle, float dist)>();
            for (int i = 0; i < _deaths.Count; i++)
            {
                Death d = _deaths[i];
                if (Time.time - d.Time > 60f)
                {
                    continue;
                }
                Vector3 to = d.Position + Vector3.up * 0.5f - eye;
                float dist = to.magnitude;
                float angle = Vector3.Angle(fwd, to);
                if (dist <= 80f && angle < 35f)
                {
                    dead.Add((d, angle, dist));
                }
            }
            _count++;
            // the journal's own clock (its lines start t=), not the sim's raid clock (they differ by the loading time)
            string head = $"[Mark] #{_count} camera at {RaidJournal.Pos(eye)} looking {Compass(fwd)} - {picked.Count} bot(s) in view, {dead.Count} died here in the last 60s";
            Write(head);
            foreach (var x in picked.OrderBy(x => x.angle * 2f + x.dist).Take(MAX_BOTS))
            {
                Write($"  #{_count} {x.dist:0}m from camera: {Describe(x.p)}");
            }
            foreach (var x in dead.OrderBy(x => x.angle * 2f + x.dist).Take(MAX_BOTS))
            {
                Write($"  #{_count} {x.dist:0}m from camera: DEAD {Time.time - x.d.Time:0}s ago: {x.d.Line}");
            }
            Notify($"장면 표시 #{_count} — 봇 {Math.Min(picked.Count, MAX_BOTS)}명{(dead.Count > 0 ? $", 방금 죽은 봇 {Math.Min(dead.Count, MAX_BOTS)}명" : "")} 기록 (일지 [Mark])");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[Mark] failed: {ex.Message}");
        }
    }

    private static void Write(string line)
    {
        if (RaidJournal.IsOpen)
        {
            RaidJournal.Line(line);
        }
        Logger.LogWarning(line);
        if (_marks.Count < MAX_MARKS * (MAX_BOTS + 1))
        {
            _marks.Add(line);
        }
    }

    /// <summary>One line with everything that shows on screen: who, layer, decisions, movement vs look, gun, hits, legs, enemy,
    /// door / leaf next to it, wall in its face.</summary>
    public static string Describe(Player p)
    {
        if (p == null)
        {
            return "?";
        }
        var owner = p.AIData?.BotOwner;
        string layer = owner?.Brain?.ActiveLayerName() ?? "none";
        Vector3 pos = p.Position;
        Vector3 vel = p.Velocity;
        vel.y = 0f;
        float speed = vel.magnitude;
        string head = $"[{p.Profile?.Nickname}] {p.Profile?.Info?.Settings?.Role}";
        if (!SAINEnableClass.GetSAIN(p.ProfileId, out BotComponent bot) || bot == null)
        {
            return $"{head} (no SAIN) layer={layer} speed={speed:0.0} at {RaidJournal.Pos(pos)}";
        }
        Vector3 look = bot.Transform.LookDirection;
        look.y = 0f;
        string moveVsLook = speed > 0.3f && look.sqrMagnitude > 0.01f ? $"{Vector3.Angle(vel, look):0}deg" : "-";
        var d = bot.Decision;
        string decisions = $"combat={d.CurrentCombatDecision} squad={d.CurrentSquadDecision} self={d.CurrentSelfDecision} why={d.EnemyDecisions?.LastReason}";
        float underFireAgo = owner?.Memory != null && owner.Memory.UnderFireTime > 0f ? Time.time - owner.Memory.UnderFireTime : -1f;
        // 10/14 marks #7/#11/#12: what holds the hands (gun / meds / grenade), whether EFT lets it sprint, where the muzzle points
        string hands = p.HandsController == null ? "none" : p.HandsController is EFT.Player.FirearmController ? "gun"
            : p.HandsController is IGrenadeController ? "GRENADE" : p.HandsController.GetType().Name.Replace("Controller", "");
        float pitchUp = Mathf.Asin(Mathf.Clamp(bot.Transform.LookDirection.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        string gun = $"hands={hands} reloading={owner?.WeaponManager?.Reload?.Reloading == true} meds={owner?.Medecine?.Using == true} "
            + $"shooting={owner?.ShootData?.Shooting == true} aim={bot.Aim?.AimStatus} lookPitch={pitchUp:0}deg light={owner?.BotLight?.IsEnable == true}";
        var cover = bot.Cover?.CoverInUse;
        string coverText = cover == null ? "none" : cover.BotInThisCover ? "in" : $"{(cover.Position - pos).magnitude:0}m away";
        string legs = Legs(p);
        var enemy = bot.GoalEnemy;
        string enemyText = enemy == null ? "none"
            : $"{enemy.EnemyPlayer?.Profile?.Nickname} {enemy.RealDistance:0}m {(enemy.IsVisible ? "visible" : enemy.Seen ? $"seen {enemy.TimeSinceSeen:0}s ago" : "never seen")}";
        return $"{head} [{bot.Info.Personality}] layer={layer} {decisions} | speed={speed:0.0}m/s sprint={p.IsSprintEnabled} canSprint={p.MovementContext?.CanSprint == true} cover={coverText} pose={p.PoseLevel:0.0} "
            + $"moveVsLook={moveVsLook} doorSlow={bot.Mover.DoorSlowActive} | {gun} underFire={(underFireAgo >= 0f && underFireAgo < 30f ? $"{underFireAgo:0.0}s ago" : "no")} "
            + $"hp={bot.Memory.Health.HealthStatus} legs={legs} | enemy={enemyText} | {Door(bot, pos)} | wallAhead={WallAhead(bot)} | at {RaidJournal.Pos(pos)}";
    }

    private static string Legs(Player p)
    {
        try
        {
            var hc = p.HealthController;
            if (hc == null)
            {
                return "?";
            }
            var l = hc.GetBodyPartHealth(EBodyPart.LeftLeg, true);
            var r = hc.GetBodyPartHealth(EBodyPart.RightLeg, true);
            return $"{l.Current:0}/{r.Current:0}";
        }
        catch (Exception)
        {
            return "?";
        }
    }

    private static string Door(BotComponent bot, Vector3 pos)
    {
        Door nearest = null;
        float best = 3f * 3f;
        var doors = bot.DoorOpener?.AllDoors;
        if (doors != null)
        {
            for (int i = 0; i < doors.Count; i++)
            {
                Door door = doors[i].Door;
                if (door == null)
                {
                    continue;
                }
                float sq = (door.transform.position - pos).sqrMagnitude;
                if (sq < best)
                {
                    best = sq;
                    nearest = door;
                }
            }
        }
        bool leaf = DoorLeafCheck.OpenLeafNear(pos, 1f);
        if (nearest == null)
        {
            return $"door=none leafWithin1m={leaf}";
        }
        string kind = BotDoorBreacher.IsSliding(nearest) ? "sliding" : BotDoorBreacher.WillSwingToward(nearest, pos) ? "swingsTowardBot" : "swingsAway";
        return $"door={nearest.DoorState} {Mathf.Sqrt(best):0.0}m {kind} angle={nearest.CurrentAngle:0} leafWithin1m={leaf}";
    }

    private static string WallAhead(BotComponent bot)
    {
        Vector3 eye = bot.Transform.EyePosition;
        Vector3 look = bot.Transform.LookDirection;
        if (look.sqrMagnitude < 0.01f)
        {
            return "?";
        }
        return Physics.Raycast(eye, look.normalized, out RaycastHit hit, 5f, LayersMaskController.HighPolyWithTerrainMask)
            ? $"{hit.distance:0.0}m"
            : "clear5m";
    }

    private static string Compass(Vector3 fwd)
    {
        float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        return $"yaw {((yaw + 360f) % 360f):0}";
    }

    private static void Notify(string message)
    {
        try
        {
            NotificationManager.DisplayMessageNotification(message, ENotificationDurationType.Default, ENotificationIconType.Default, null);
        }
        catch (Exception)
        {
        }
    }
}
