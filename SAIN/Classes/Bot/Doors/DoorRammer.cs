using System.Collections.Generic;
using EFT;
using EFT.Interactive;
using SAIN.Components;
using SAIN.Models.Enums;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.GlobalSettings.Categories.General;
using SAIN.SAINComponent.Classes.Tactics;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Mover;

/// <summary>
/// zzap fork (2026-10-13, user: "I never saw them ram a door - running away, going in not sure if someone's there, searching;
/// the mod has more uses than the door tactic"): sprint ram on an ordinary move. While the bot runs its path (BotPathData)
/// away from an enemy, at an enemy, searching for one, or any other fight move (10/13), a shut door 1.5-7.5m ahead on a straight leg
/// of the path gets one roll (chance per situation, F6 Door Tactics). On a hit the bot keeps (or starts) sprinting at the door
/// with SAIN's ram armed (BotDoorBreacher - ported from doordash / DualSideDoorBreach, needs neither) and the door opener / door
/// slowdown held off - the ram bursts the door open. No burst (too slow, door opened by someone else) -> everything back to
/// normal, the bot opens it as usual. Never inside a door tactic (that one has its own ram).
/// Counters doormod.move.*, journal [DoorRam].
/// </summary>
public class DoorRammer(BotComponent bot)
{
    private enum ECtx { None, Flee, Rush, Search, Move }

    private const float SCAN_INTERVAL = 0.25f;
    private const float SCAN_LENGTH = 7.5f;
    // 10/14 (user: "too many limits for bots"): was 3 / 1.8 m and a 35 deg straight leg
    private const float MIN_START_DIST = 1.5f;
    private const float MIN_START_DIST_FAST = 0.9f;
    private const float MAX_LEG_TURN = 50f;
    private const float RAM_TIME = 3f;
    private const float ROLL_MEMORY = 10f;
    private const float BURST_FOLLOW_TIME = 0.6f;
    private const int MAX_SEGMENTS = 4;
    private const int MAX_ROLLED = 16;

    private readonly BotComponent _bot = bot;
    private readonly Dictionary<int, float> _rolledUntil = new();
    private float _nextScan;

    private bool _charging;
    private Door _door;
    private Vector3 _doorPoint;
    private ECtx _ctx;
    private float _until;
    private bool _burst;
    private float _burstTime;
    private float _nearSince = -1f;
    private BotPathDataManual _forcedSprintPath;

    private static DoorTacticSettings Settings => GlobalSettingsClass.Instance.General.DoorTactics;

    public bool Charging => _charging;

    /// <summary>From BotPathDataManual.TickPath every frame of a SAIN path move. True while a ram is running: the caller keeps
    /// sprinting and skips its door slowdown.</summary>
    public bool TickPath(BotPathDataManual path, Vector3 botPosition)
    {
        float time = Time.time;
        if (_charging)
        {
            TickCharge(path, botPosition, time);
            return _charging;
        }
        if (time < _nextScan)
        {
            return false;
        }
        _nextScan = time + SCAN_INTERVAL;
        if (!BotDoorBreacher.Active || Settings?.DoorModRamOnMove != true || _bot.Player == null || _bot.Player.IsYourPlayer)
        {
            return false;
        }
        ECtx ctx = Context(time);
        if (ctx == ECtx.None)
        {
            return false;
        }
        float speed = _bot.Player.Velocity.magnitude;
        bool fast = speed >= BotDoorBreacher.MinSpeed;
        if (!FindDoorAhead(path, botPosition, out Door door, out Vector3 hitPoint, out float dist, out bool offPath))
        {
            if (offPath)
            {
                Count($"doormod.move.skip.notOnPath.{CtxName(ctx)}");
            }
            return false;
        }
        if (dist < (fast ? MIN_START_DIST_FAST : MIN_START_DIST))
        {
            return false;
        }
        int id = door.GetInstanceID();
        if (_rolledUntil.TryGetValue(id, out float until) && until > time)
        {
            return false;
        }
        if (_rolledUntil.Count >= MAX_ROLLED)
        {
            _rolledUntil.Clear();
        }
        _rolledUntil[id] = time + ROLL_MEMORY;

        string ctxName = CtxName(ctx);
        if (!door.CanBeBreached)
        {
            Count($"doormod.move.skip.unbreakable.{ctxName}");
            return false;
        }
        if (BotDoorBreacher.WillSwingToward(door, botPosition))
        {
            if (!BotDoorBreacher.BothSides)
            {
                Count($"doormod.move.skip.swingsToward.{ctxName}");
                return false;
            }
            // zzap (10/14 21:07 sim, Customs: 7 rams started, none burst, noBurst at 2.7 m/s): the leaf would have to be turned,
            // and BotDoorBreacher.CanRam refuses a turned leaf with no room to land (dorm corridors) - so the ram and the kick
            // at the door could never happen and the bot only sprinted at a door it then opened by hand. Same test up front.
            if (!BotDoorPushOpen.SwingSpaceClear(door))
            {
                Count($"doormod.move.skip.noSwingRoom.{ctxName}");
                return false;
            }
        }
        if (BotDoorBreacher.SomeoneInSwing(door, _bot.Player))
        {
            Count($"doormod.move.skip.someoneBehind.{ctxName}");
            return false;
        }
        if (TeammateNear(hitPoint, 2f))
        {
            Count($"doormod.move.skip.teammateAtDoor.{ctxName}");
            return false;
        }
        float chance = ctx switch
        {
            ECtx.Flee => Settings.DoorModRamFleeChance,
            ECtx.Rush => Settings.DoorModRamRushChance,
            ECtx.Search => Settings.DoorModRamSearchChance,
            _ => Settings.DoorModRamMoveChance,
        };
        if (!Settings.TestModeDoorMods && Random.value >= Mathf.Clamp01(chance / 100f))
        {
            Count($"doormod.move.skip.rollFailed.{ctxName}");
            return false;
        }
        if (!BotDoorBreacher.Start(_bot.Player, RAM_TIME))
        {
            Count($"doormod.move.skip.noBreacher.{ctxName}");
            return false;
        }
        _charging = true;
        _door = door;
        _doorPoint = hitPoint;
        _ctx = ctx;
        _until = time + RAM_TIME;
        _burst = false;
        _nearSince = -1f;
        _forcedSprintPath = null;
        _bot.DoorOpener.NoInteractUntil = _until;
        Count($"doormod.move.start.{ctxName}");
        Log($"[DoorRam] [{_bot.name}] [{_bot.Info.Personality}] {ctxName}: sprint ram at door {id} {dist:0.0}m ahead ({speed:0.0} m/s)");
        EnsureSprint(path);
        return true;
    }

    private void TickCharge(BotPathDataManual path, Vector3 botPosition, float time)
    {
        string ctxName = CtxName(_ctx);
        if (_door == null || _bot.IsDead)
        {
            Stop("doorGone");
            return;
        }
        if (!_burst && _door.DoorState != EDoorState.Shut)
        {
            _burst = true;
            _burstTime = time;
            // The ram breached it (Breaching -> Open), or someone else opened it: either way the way is open.
            Count($"doormod.move.burst.{ctxName}");
            Log($"[DoorRam] [{_bot.name}] {ctxName}: rammed door {_door.GetInstanceID()} ({_door.DoorState}) at {_bot.Player.Velocity.magnitude:0.0} m/s");
        }
        if (_burst)
        {
            if (time - _burstTime > BURST_FOLLOW_TIME)
            {
                Stop(null);
            }
            else
            {
                EnsureSprint(path);
            }
            return;
        }
        if (Context(time) != _ctx)
        {
            Fallback("situationChanged");
            return;
        }
        Vector3 d = _doorPoint - botPosition;
        d.y = 0f;
        if (d.sqrMagnitude < 1.1f * 1.1f)
        {
            if (_nearSince < 0f)
            {
                _nearSince = time;
            }
            else if (time - _nearSince > 0.8f)
            {
                if (KickAtDoor($"slow {_bot.Player.Velocity.magnitude:0.0}m/s"))
                {
                    return;
                }
                Fallback($"noBurst(at the door 0.8s, {_bot.Player.Velocity.magnitude:0.0}m/s, ram speed {BotDoorBreacher.MinSpeed:0.0}, ram allowed now {BotDoorBreacher.CanRam(_door, botPosition)})");
                return;
            }
        }
        if (time > _until - 0.3f)
        {
            if (d.sqrMagnitude < 2f * 2f && KickAtDoor("timeout"))
            {
                return;
            }
            Fallback("timeout");
            return;
        }
        EnsureSprint(path);
    }

    private void EnsureSprint(BotPathDataManual path)
    {
        if (!path.WantToSprint)
        {
            path.RequestStartSprint(ESprintUrgency.High, "doorRam");
            _forcedSprintPath = path;
        }
    }

    /// <summary>
    /// 10/14 (user: "at an angle the ram doesn't burst it, then they open it by hand, it swings into them and shoves them aside"):
    /// at the door without a burst -> kick it (always away from the bot) instead of opening it by hand. True = kicked.
    /// </summary>
    private bool KickAtDoor(string why)
    {
        if (_door == null || _door.DoorState != EDoorState.Shut || !BotDoorBreacher.Kick(_bot.Player, _door))
        {
            return false;
        }
        string ctxName = CtxName(_ctx);
        Count($"doormod.move.kick.{ctxName}");
        Log($"[DoorRam] [{_bot.name}] {ctxName}: no burst ({why}) - kicked door {_door.GetInstanceID()} open at the door");
        _burst = true;
        _burstTime = Time.time;
        return true;
    }

    private void Fallback(string why)
    {
        Count($"doormod.move.fallback.{why.Split('(')[0]}.{CtxName(_ctx)}");
        Log($"[DoorRam] [{_bot.name}] {CtxName(_ctx)}: ram didn't happen ({why}) - opening the door normally");
        Stop(why);
    }

    /// <summary>Ends the ram: ram disarmed, door opener back on, a sprint we started ourselves ended again.</summary>
    public void Stop(string why)
    {
        if (!_charging)
        {
            return;
        }
        _charging = false;
        BotDoorBreacher.Stop(_bot.Player);
        if (_bot.DoorOpener != null)
        {
            _bot.DoorOpener.NoInteractUntil = 0f;
        }
        if (_forcedSprintPath != null && !_burst)
        {
            _forcedSprintPath.RequestEndSprint(ESprintUrgency.None, "doorRamEnd");
        }
        _forcedSprintPath = null;
        _door = null;
    }

    private ECtx Context(float time)
    {
        var decision = _bot.Decision;
        if (decision == null)
        {
            return ECtx.None;
        }
        var enemy = _bot.GoalEnemy;
        switch (decision.CurrentCombatDecision)
        {
            case ECombatDecision.Retreat:
            case ECombatDecision.RunAway:
            case ECombatDecision.SeekCover:
                var memory = _bot.BotOwner?.Memory;
                bool pressed = (memory != null && time - memory.UnderFireTime < 4f)
                    || (enemy != null && (enemy.IsVisible || (enemy.Seen && enemy.TimeSinceSeen < 4f)));
                return pressed ? ECtx.Flee : enemy != null ? ECtx.Move : ECtx.None;

            case ECombatDecision.RushEnemy:
            case ECombatDecision.MoveToEngage:
                return enemy != null ? ECtx.Rush : ECtx.None;

            case ECombatDecision.Search:
                return enemy != null && !enemy.IsVisible ? ECtx.Search : ECtx.None;

            // 10/13 (user: "1-2 rams in 15 minutes is as good as none"): any other SAIN combat move through a shut door -
            // cover runs without pressure, flanks, squad moves, repositioning (this only runs while a SAIN path is followed).
            // sneaking / holding still on purpose, or another tactic that runs its own door handling
            case ECombatDecision.None:
            case ECombatDecision.CreepOnEnemy:
            case ECombatDecision.Freeze:
            case ECombatDecision.DoorTactic:
            case ECombatDecision.DebugNoDecision:
                return ECtx.None;

            default:
                return enemy != null ? ECtx.Move : ECtx.None;
        }
    }

    private static string CtxName(ECtx ctx)
    {
        return ctx switch
        {
            ECtx.Flee => "flee",
            ECtx.Rush => "rush",
            ECtx.Search => "search",
            ECtx.Move => "move",
            _ => "none",
        };
    }

    /// <summary>
    /// Casts along the next legs of the path (chest height) up to 7.5m: the first thing hit must be a shut, operatable,
    /// unlocked door. Stops at a turn sharper than MAX_LEG_TURN (50 deg) - the ram casts along the run (velocity), so the door has to sit on the
    /// leg the bot is running straight down.
    /// </summary>
    private static bool FindDoorAhead(BotPathDataManual path, Vector3 botPosition, out Door door, out Vector3 hitPoint, out float dist, out bool offPath)
    {
        door = null;
        hitPoint = default;
        dist = 0f;
        offPath = false;
        var corners = path.PathCorners;
        int index = path.CurrentIndex;
        if (corners == null || index < 0 || index >= corners.Count)
        {
            return false;
        }
        Vector3 from = botPosition;
        Vector3 lastDir = Vector3.zero;
        float travelled = 0f;
        int mask = LayersMaskController.PlayerStaticDoorMask | (1 << LayersMaskController.DoorLayer) | LayerMask.GetMask("DoorLowPolyCollider");
        for (int i = index, n = 0; i < corners.Count && n < MAX_SEGMENTS && travelled < SCAN_LENGTH; i++, n++)
        {
            Vector3 to = corners[i].Position;
            Vector3 leg = to - from;
            leg.y = 0f;
            float legLength = leg.magnitude;
            if (legLength < 0.05f)
            {
                continue;
            }
            Vector3 dir = leg / legLength;
            if (n > 0 && Vector3.Angle(lastDir, dir) > MAX_LEG_TURN)
            {
                return false;
            }
            float cast = Mathf.Min(legLength + 0.4f, SCAN_LENGTH - travelled);
            if (Physics.SphereCast(from + Vector3.up * 1.2f, 0.3f, dir, out RaycastHit hit, cast, mask))
            {
                Door d = hit.collider != null ? hit.collider.GetComponentInParent<Door>() : null;
                if (d == null || d.DoorState != EDoorState.Shut || !d.Operatable || !d.enabled || BotDoorBreacher.IsSliding(d))
                {
                    return false;
                }
                // 2026-10-20 (user, 2nd sim: "rams the door, then goes back"; 1st stage: 14 of 377 bursts ended 2-8 m back where they
                // rammed, same decision): the cast runs 0.4 m past a corner and its 0.3 m sphere grazes doors beside the path - only
                // ram a door the path goes through, i.e. a later corner lies past the leaf.
                if (!PathCrosses(corners, i, hit.point, hit.normal, dir))
                {
                    offPath = true;
                    return false;
                }
                door = d;
                hitPoint = hit.point;
                dist = travelled + hit.distance;
                return true;
            }
            travelled += legLength;
            from = to;
            lastDir = dir;
        }
        return false;
    }

    /// <summary>A path corner from 'first' on (up to three) more than 0.3 m past the leaf face the cast hit.</summary>
    private static bool PathCrosses(List<BotPathCorner> corners, int first, Vector3 hitPoint, Vector3 hitNormal, Vector3 dir)
    {
        Vector3 n = hitNormal;
        n.y = 0f;
        if (n.sqrMagnitude < 1e-4f)
        {
            n = -dir;
        }
        n.Normalize();
        if (Vector3.Dot(n, dir) > 0f)
        {
            n = -n; // the face toward the bot
        }
        for (int k = first; k < corners.Count && k < first + 3; k++)
        {
            Vector3 c = corners[k].Position - hitPoint;
            c.y = 0f;
            if (Vector3.Dot(c, -n) > 0.3f)
            {
                return true;
            }
        }
        return false;
    }

    private bool TeammateNear(Vector3 point, float radius)
    {
        var members = _bot.Squad?.Members;
        if (members == null)
        {
            return false;
        }
        foreach (var member in members.Values)
        {
            if (member == null || ReferenceEquals(member, _bot) || member.IsDead)
            {
                continue;
            }
            if ((member.Position - point).sqrMagnitude < radius * radius)
            {
                return true;
            }
        }
        return false;
    }

    private static void Count(string key)
    {
        if (TacticDiagnostics.CountOn) TacticDiagnostics.Count(key);
    }

    private static void Log(string line)
    {
        if (RaidJournal.IsOpen) RaidJournal.Line(line);
        if (Settings?.DiagnosticLogs == true) Logger.LogWarning(line);
    }
}
