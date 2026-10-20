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
///   - Scene mark (F8, F6 settings "SAIN Editor > Mark Scene Shortcut"): the bots in front of the camera (within 20 deg of its
///     centre, 50 m, in its line of sight - 2026-10-20, was 30 deg / 80 m through walls) get one full state line each in the
///     journal ([Mark] #n) and in the sim summary md, at the raid time of the key press - press it when something looks wrong,
///     then the screenshot and the log line match. Bots that died there in the last 60 s come with their state 1-3 s before.
///   - Describe(): the same state line, used by SimLabOddity for its automatic episodes.
/// </summary>
public static class SimLabSceneMark
{
    private const int MAX_BOTS = 4;
    private const int MAX_MARKS = 40;
    // a mark with 4 living and 4 dead bots writes up to 1 + 4 + 4 x 4 lines
    private const int LINES_PER_MARK = 1 + MAX_BOTS + MAX_BOTS * (1 + SNAPSHOTS_KEPT);

    private static readonly List<string> _marks = new();
    private static int _count;

    public static IReadOnlyList<string> Marks => _marks;

    public static void Reset()
    {
        _marks.Clear();
        _count = 0;
        _deaths.Clear();
        _recent.Clear();
        _nextSnapshot = 0f;
    }

    // zzap (user 2026-10-20: "the scene mark takes in far too wide an area, and the dead aren't in it - so many times I press it
    // after the bot already died"): living bots now within 20 deg / 50 m of the camera centre and in its line of sight (or
    // within 5 m), dead ones within 25 deg / 50 m. A bot in front of the camera has its state line kept every second (last 3),
    // so one that dies is listed with how it looked 1-3 s before the [Death] line.
    private const float VIEW_ANGLE = 20f;
    private const float VIEW_DIST = 50f;
    private const float DEAD_ANGLE = 25f;
    private const float SNAPSHOT_ANGLE = 45f;
    private const float SNAPSHOT_DIST = 60f;
    private const int SNAPSHOTS_KEPT = 3;

    // 10/14 (user: "this one wasn't marked - dead, so it didn't show up"): F8 only looked at the living. Bots that died within
    // the last 60 s in view are listed too, with their [Death] line. At most 64 kept (oldest dropped).
    private struct Death
    {
        public Vector3 Position;
        public float Time;
        public string Line;
        public List<(float time, string line)> Before;
    }

    private static readonly List<Death> _deaths = new();
    private static readonly Dictionary<string, List<(float time, string line)>> _recent = new();
    private static readonly List<string> _stale = new();
    private static float _nextSnapshot;

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
        _recent.TryGetValue(p.ProfileId, out var before);
        _recent.Remove(p.ProfileId);
        _deaths.Add(new Death { Position = p.Position, Time = Time.time, Line = line, Before = before });
    }

    /// <summary>Once a second while a journal is open: the state line of each bot in front of the camera (45 deg, 60 m, in
    /// sight or within 8 m), last 3 kept - what a bot that dies did right before.</summary>
    public static void Tick()
    {
        float now = Time.time;
        if (now < _nextSnapshot || !RaidJournal.IsOpen)
        {
            return;
        }
        _nextSnapshot = now + 1f;
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
            var players = world.AllAlivePlayersList;
            for (int i = 0; i < players.Count; i++)
            {
                Player p = players[i];
                if (p == null || !p.IsAI || !InView(p, eye, fwd, SNAPSHOT_ANGLE, SNAPSHOT_DIST, 8f, out _, out _))
                {
                    continue;
                }
                if (!_recent.TryGetValue(p.ProfileId, out var list))
                {
                    list = new List<(float time, string line)>(SNAPSHOTS_KEPT);
                    _recent[p.ProfileId] = list;
                }
                if (list.Count >= SNAPSHOTS_KEPT)
                {
                    list.RemoveAt(0);
                }
                list.Add((now, Describe(p)));
            }
            // drop bots out of view for 10 s+ (their lines would be stale by the time they die)
            _stale.Clear();
            foreach (var kv in _recent)
            {
                if (kv.Value.Count == 0 || now - kv.Value[kv.Value.Count - 1].time > 10f)
                {
                    _stale.Add(kv.Key);
                }
            }
            for (int i = 0; i < _stale.Count; i++)
            {
                _recent.Remove(_stale[i]);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[Mark] snapshot failed: {ex.Message}");
        }
    }

    private static bool InView(Player p, Vector3 eye, Vector3 fwd, float maxAngle, float maxDist, float nearAnyway, out float angle, out float dist)
    {
        Vector3 chest = p.Position + Vector3.up * 1.2f;
        Vector3 to = chest - eye;
        dist = to.magnitude;
        angle = dist > 0.1f ? Vector3.Angle(fwd, to) : 0f;
        if (dist > maxDist || dist < 0.1f || angle >= maxAngle)
        {
            return false;
        }
        if (dist <= nearAnyway)
        {
            return true;
        }
        int mask = LayersMaskController.HighPolyWithTerrainMask;
        return !Physics.Linecast(eye, chest, mask) || !Physics.Linecast(eye, p.Position + Vector3.up * 1.6f, mask);
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
                if (p == null || !p.IsAI || !InView(p, eye, fwd, VIEW_ANGLE, VIEW_DIST, 5f, out float angle, out float dist))
                {
                    continue;
                }
                picked.Add((p, angle, dist));
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
                if (dist <= VIEW_DIST && angle < DEAD_ANGLE)
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
                if (x.d.Before == null || x.d.Before.Count == 0)
                {
                    Write($"  #{_count}    (not in front of the camera in the seconds before it died - no state kept)");
                    continue;
                }
                // newest first: 1 s, 2 s, 3 s before the death
                for (int i = x.d.Before.Count - 1; i >= 0; i--)
                {
                    Write($"  #{_count}    {x.d.Time - x.d.Before[i].time:0.0}s before death: {x.d.Before[i].line}");
                }
            }
            Notify(SAIN.Editor.EditorText.Korean
            ? $"장면 표시 #{_count} — 봇 {Math.Min(picked.Count, MAX_BOTS)}명{(dead.Count > 0 ? $", 방금 죽은 봇 {Math.Min(dead.Count, MAX_BOTS)}명" : "")} 기록 (일지 [Mark])"
            : $"Scene mark #{_count} - {Math.Min(picked.Count, MAX_BOTS)} bots{(dead.Count > 0 ? $", {Math.Min(dead.Count, MAX_BOTS)} just died" : "")} recorded (journal [Mark])");
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
        if (_marks.Count < MAX_MARKS * LINES_PER_MARK)
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
            + $"shooting={owner?.ShootData?.Shooting == true} aim={bot.Aim?.AimStatus} lookPitch={pitchUp:0}deg light={bot.BotLight?.DevicesText ?? "?"}";
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
