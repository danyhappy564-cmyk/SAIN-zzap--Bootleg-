using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using SAIN.Components;
using SAIN.Models.Enums;
using SAIN.Plugin;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.Classes.Mover;
using UnityEngine;
using UnityEngine.AI;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// Door tactics used by skilled players, driven from the SAIN combat layer (ECombatDecision.DoorTactic)
/// so ORBIT, which only watches for the "SAIN : Combat Layer" name, sees an ordinary SAIN fight.
///
/// Plans (picked by personality when the enemy was last known inside a room behind a nearby door):
///   Peek   - GigaChad/Chad: stack beside an open doorway, jump out in front of it to look inside,
///            back-step to the stack point, optionally fake a grenade (audible draw, then put away)
///            and hold the doorway for a few seconds.
///   Trap   - GigaChad/SnappingTurtle: close the door on the room, taunt if the personality taunts,
///            then hold the door. GigaChad may back off and throw a grenade at the door instead.
///   Ambush - Rat: stack beside the door crouched and silent, wait for the enemy to come out.
///
/// Any time the enemy becomes visible SAIN's normal decisions (StandAndShoot first) take over and
/// the tactic ends. Every start, step change and result is logged as [DoorTactic] when
/// DoorTacticConfig.DiagnosticLogs is on.
/// </summary>
public class DoorTacticClass : BotComponentClassBase
{
    public enum EPlan
    {
        None,
        Peek,
        Trap,
        Ambush,
    }

    public enum EStep
    {
        None,
        MoveToStack,
        PeekOut,
        PeekBack,
        FakeNadeDraw,
        FakeNadeHolster,
        MoveToClose,
        CloseDoor,
        MoveToFarHold,
        ThrowNade,
        Hold,
    }

    private const float MAX_BOT_DOOR_DIST = 8f;
    private const float MAX_ENEMY_DOOR_DIST = 12f;
    private const float MIN_ENEMY_DEPTH = 0.6f;
    private const float MIN_BOT_DEPTH = 0.3f;
    private const float MAX_TIME_SINCE_KNOWN = 25f;
    private const float STACK_DEPTH = 0.9f;
    private const float STACK_SIDE_GAP = 0.7f;
    private const float PEEK_DEPTH = 0.8f;
    private const float CLOSE_DEPTH = 0.9f;
    private const float ARRIVE_DIST = 0.6f;
    private const float MOVE_STEP_TIMEOUT = 7f;
    private const float SESSION_MAX_TIME = 150f;
    private const float FAR_HOLD_MIN_DIST = 4.5f;
    private const float DOOR_COOLDOWN_AFTER_SESSION = 60f;
    private const float DOOR_COOLDOWN_AFTER_ROLL_FAIL = 25f;
    private const float GLOBAL_COOLDOWN = 8f;
    private const float VERBOSE_LOG_INTERVAL = 10f;

    public DoorTacticClass(BotComponent bot)
        : base(bot)
    {
        CanEverTick = false;
    }

    public bool Active
    {
        get { return _session != null; }
    }

    public EPlan Plan
    {
        get { return _session?.Plan ?? EPlan.None; }
    }

    public EStep Step
    {
        get { return _session?.Step ?? EStep.None; }
    }

    /// <summary>
    /// Where the action should point the bot's view this tick, or null to use SAIN's normal steering.
    /// </summary>
    public Vector3? LookTarget
    {
        get { return _session?.LookTarget; }
    }

    // ---------------------------------------------------------------- decision

    /// <summary>
    /// Called from EnemyDecisionClass once aggressive actions are allowed. Returns true while a
    /// tactic is running, or when a new one was just started.
    /// </summary>
    public bool ShallUse(Enemy enemy, out string reason)
    {
        if (_session != null)
        {
            if (_session.Enemy != enemy)
            {
                End("goalEnemyChanged");
                reason = "goalEnemyChanged";
                return false;
            }
            if (Time.time - _session.StartTime > SESSION_MAX_TIME)
            {
                End("sessionTimeout");
                reason = "sessionTimeout";
                return false;
            }
            reason = $"active:{_session.Plan}/{_session.Step}";
            return true;
        }

        if (!TryStart(enemy, out reason))
        {
            LogVerbose(reason);
            return false;
        }
        return true;
    }

    private bool TryStart(Enemy enemy, out string reason)
    {
        if (DoorTacticConfig.Enabled == null || !DoorTacticConfig.Enabled.Value)
        {
            reason = "disabled";
            return false;
        }
        if (_nextAllowedTime > Time.time)
        {
            reason = "globalCooldown";
            return false;
        }
        EPersonality personality = Bot.Info.Personality;
        float baseChance = GetBaseChance(personality);
        if (baseChance <= 0f)
        {
            reason = "personalityNoTactics";
            return false;
        }
        if (enemy == null || enemy.IsVisible)
        {
            reason = "enemyVisibleOrNull";
            return false;
        }
        Vector3? lastKnown = enemy.KnownPlaces.LastKnownPosition;
        if (lastKnown == null)
        {
            reason = "noLastKnown";
            return false;
        }
        if (enemy.TimeSinceLastKnownUpdated > MAX_TIME_SINCE_KNOWN)
        {
            reason = "lastKnownTooOld";
            return false;
        }
        if (!FindDoor(lastKnown.Value, out DoorGeometry geo, out reason))
        {
            return false;
        }

        float time = Time.time;
        if (_doorCooldowns.TryGetValue(geo.Data.Id, out float until) && until > time)
        {
            reason = "doorCooldown";
            return false;
        }

        float chance = Mathf.Clamp01(baseChance * DoorTacticConfig.ChanceMultiplier.Value);
        if (Random.value > chance)
        {
            _doorCooldowns[geo.Data.Id] = time + DOOR_COOLDOWN_AFTER_ROLL_FAIL;
            _nextAllowedTime = time + GLOBAL_COOLDOWN;
            reason = $"rollFailed({chance:0.00})";
            Log($"{Who()} skipped door {geo.Data.Id}: chance roll failed ({chance:0.00}), door cooldown {DOOR_COOLDOWN_AFTER_ROLL_FAIL}s");
            return false;
        }

        Session session = BuildSession(personality, enemy, geo, out reason);
        if (session == null)
        {
            _doorCooldowns[geo.Data.Id] = time + DOOR_COOLDOWN_AFTER_ROLL_FAIL;
            _nextAllowedTime = time + GLOBAL_COOLDOWN;
            Log($"{Who()} could not plan at door {geo.Data.Id}: {reason}");
            return false;
        }

        _session = session;
        Log(
            $"{Who()} START plan={session.Plan} door={geo.Data.Id} doorState={geo.Data.Door.DoorState} "
                + $"botDist={geo.BotDistance:0.0}m enemyDepth={geo.EnemyDepth:0.0}m sinceKnown={enemy.TimeSinceLastKnownUpdated:0.0}s "
                + $"fakeNade={session.WantFakeNade} doorNade={session.WantDoorNade} taunt={session.WantTaunt} hold={session.HoldTime:0}s"
        );
        SetStep(session.FirstStep, "start");
        reason = $"start:{session.Plan}";
        return true;
    }

    private static float GetBaseChance(EPersonality personality)
    {
        switch (personality)
        {
            case EPersonality.GigaChad:
                return 0.6f;
            case EPersonality.Chad:
                return 0.35f;
            case EPersonality.SnappingTurtle:
                return 0.6f;
            case EPersonality.Rat:
                return 0.5f;
            default:
                return 0f;
        }
    }

    // ---------------------------------------------------------------- geometry

    private struct DoorGeometry
    {
        public DoorDataStruct Data;
        public Vector3 Center;
        public Vector3 Axis;
        public Vector3 BotSide;
        public float HalfWidth;
        public float BotDistance;
        public float EnemyDepth;
    }

    private bool FindDoor(Vector3 enemyPos, out DoorGeometry best, out string reason)
    {
        best = default;
        var doors = Bot.DoorOpener.AllDoors;
        if (doors == null || doors.Count == 0)
        {
            reason = "noDoorsInVoxel";
            return false;
        }

        Vector3 botPos = Bot.Position;
        float bestDist = float.MaxValue;
        bool found = false;
        reason = "noDoorBetween";

        for (int i = 0; i < doors.Count; i++)
        {
            DoorDataStruct data = doors[i];
            if (data.Door == null || data.Link == null)
            {
                continue;
            }
            EDoorState state = data.Door.DoorState;
            if (state != EDoorState.Open && state != EDoorState.Shut)
            {
                continue;
            }

            Vector3 center = data.Link.MidClose;
            Vector3 axis = data.Link.Close2_Normal - data.Link.Close1;
            axis.y = 0f;
            float width = axis.magnitude;
            if (width < 0.4f)
            {
                continue;
            }
            axis /= width;
            Vector3 normal = Vector3.Cross(Vector3.up, axis);

            Vector3 toBot = botPos - center;
            toBot.y = 0f;
            float botDist = toBot.magnitude;
            if (botDist > MAX_BOT_DOOR_DIST || botDist >= bestDist)
            {
                continue;
            }

            Vector3 toEnemy = enemyPos - center;
            toEnemy.y = 0f;
            if (toEnemy.magnitude > MAX_ENEMY_DOOR_DIST)
            {
                continue;
            }

            float botDot = Vector3.Dot(toBot, normal);
            float enemyDot = Vector3.Dot(toEnemy, normal);
            if (Mathf.Abs(botDot) < MIN_BOT_DEPTH || Mathf.Abs(enemyDot) < MIN_ENEMY_DEPTH)
            {
                continue;
            }
            if (Mathf.Sign(botDot) == Mathf.Sign(enemyDot))
            {
                continue;
            }

            best = new DoorGeometry
            {
                Data = data,
                Center = center,
                Axis = axis,
                BotSide = normal * Mathf.Sign(botDot),
                HalfWidth = width * 0.5f,
                BotDistance = botDist,
                EnemyDepth = Mathf.Abs(enemyDot),
            };
            bestDist = botDist;
            found = true;
        }
        return found;
    }

    /// <summary>
    /// Samples the navmesh near a planned point and rejects results that snapped through the wall
    /// onto the enemy's side of the door.
    /// </summary>
    private static bool SampleOnBotSide(Vector3 point, DoorGeometry geo, out Vector3 result)
    {
        if (!SampleNav(point, out result))
        {
            return false;
        }
        return Vector3.Dot(result - geo.Center, geo.BotSide) > 0.3f;
    }

    private static bool SampleNav(Vector3 point, out Vector3 result)
    {
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, 0.7f, -1))
        {
            result = hit.position;
            return true;
        }
        result = point;
        return false;
    }

    // ---------------------------------------------------------------- planning

    private sealed class Session
    {
        public EPlan Plan;
        public Enemy Enemy;
        public DoorDataStruct Door;
        public Vector3 Center;
        public Vector3 InsidePoint;
        public Vector3 Stack;
        public Vector3 PeekPoint;
        public Vector3 ClosePoint;
        public Vector3 FarHold;
        public bool HasFarHold;
        public bool WantFakeNade;
        public bool WantDoorNade;
        public bool WantTaunt;
        public bool NeedsClose;
        public float HoldTime;
        public float HoldPose;
        public EStep FirstStep;
        public EStep Step;
        public float StartTime;
        public float StepStartTime;
        public float NextMoveOrderTime;
        public bool Jumped;
        public bool FakeNadeDrawn;
        public bool CloseAttempted;
        public bool NadeThrown;
        public EDoorState DoorStateAtHoldStart;
        public Vector3? LookTarget;
    }

    private Session BuildSession(EPersonality personality, Enemy enemy, DoorGeometry geo, out string reason)
    {
        bool doorOpen = geo.Data.Door.DoorState == EDoorState.Open;
        var grenades = BotOwner.WeaponManager?.Grenades;
        bool haveNade = grenades != null && grenades.HaveGrenade;
        var talk = Bot.Info.PersonalitySettings.Talk;
        bool canTaunt = talk.CanTaunt && Bot.Info.FileSettings.Mind.BotTaunts;

        var s = new Session
        {
            Enemy = enemy,
            Door = geo.Data,
            Center = geo.Center,
            InsidePoint = geo.Center - geo.BotSide * 1.5f + Vector3.up * 1.2f,
            StartTime = Time.time,
        };

        // Stack beside the frame on the bot's side, on whichever edge is closer to the bot.
        float side = Mathf.Sign(Vector3.Dot(Bot.Position - geo.Center, geo.Axis));
        if (side == 0f)
        {
            side = 1f;
        }
        Vector3 stackRaw = geo.Center + geo.BotSide * STACK_DEPTH + geo.Axis * side * (geo.HalfWidth + STACK_SIDE_GAP);
        if (!SampleOnBotSide(stackRaw, geo, out s.Stack))
        {
            stackRaw = geo.Center + geo.BotSide * STACK_DEPTH - geo.Axis * side * (geo.HalfWidth + STACK_SIDE_GAP);
            if (!SampleOnBotSide(stackRaw, geo, out s.Stack))
            {
                reason = "noStackPointOnNavmesh";
                return null;
            }
        }
        bool peekPointOk = SampleOnBotSide(geo.Center + geo.BotSide * PEEK_DEPTH, geo, out s.PeekPoint);
        bool closePointOk = SampleOnBotSide(geo.Center + geo.BotSide * CLOSE_DEPTH, geo, out s.ClosePoint);
        if (!closePointOk)
        {
            // Can't stand in front of the door on our side: treat it as if it can't be closed.
            doorOpen = false;
        }

        switch (personality)
        {
            case EPersonality.GigaChad:
            case EPersonality.Chad:
                bool peekAllowed = DoorTacticConfig.JumpPeek.Value && doorOpen && peekPointOk;
                bool trapAllowed = personality == EPersonality.GigaChad && DoorTacticConfig.RoomTrap.Value;
                if (peekAllowed && (!trapAllowed || Random.value < 0.55f))
                {
                    s.Plan = EPlan.Peek;
                    s.FirstStep = EStep.MoveToStack;
                    float fakeChance = personality == EPersonality.GigaChad ? 0.4f : 0.2f;
                    s.WantFakeNade = DoorTacticConfig.FakeGrenade.Value && haveNade && Random.value < fakeChance;
                    s.HoldTime = Random.Range(3f, 6f);
                    s.HoldPose = 0.8f;
                }
                else if (trapAllowed)
                {
                    BuildTrap(s, geo, doorOpen, canTaunt);
                    s.HoldTime = Random.Range(20f, 40f);
                    s.HoldPose = 0.7f;
                    if (DoorTacticConfig.DoorGrenade.Value && haveNade && Random.value < 0.5f && FindFarHold(geo, out s.FarHold))
                    {
                        s.HasFarHold = true;
                        s.WantDoorNade = true;
                        if (!doorOpen)
                        {
                            // Door already shut: nothing to close, go straight to backing off and throwing.
                            s.FirstStep = EStep.MoveToFarHold;
                        }
                    }
                }
                else
                {
                    reason = doorOpen ? "tacticsDisabledForPersonality" : "doorShutNoPeek";
                    return null;
                }
                break;

            case EPersonality.SnappingTurtle:
                if (!DoorTacticConfig.RoomTrap.Value)
                {
                    reason = "roomTrapDisabled";
                    return null;
                }
                BuildTrap(s, geo, doorOpen, canTaunt);
                s.HoldTime = Random.Range(45f, 90f);
                s.HoldPose = 0.4f;
                break;

            case EPersonality.Rat:
                if (!DoorTacticConfig.RoomTrap.Value)
                {
                    reason = "roomTrapDisabled";
                    return null;
                }
                s.Plan = EPlan.Ambush;
                s.FirstStep = EStep.MoveToStack;
                s.HoldTime = Random.Range(60f, 120f);
                s.HoldPose = 0.2f;
                break;

            default:
                reason = "personalityNoTactics";
                return null;
        }

        reason = string.Empty;
        return s;
    }

    private static void BuildTrap(Session s, DoorGeometry geo, bool doorOpen, bool canTaunt)
    {
        s.Plan = EPlan.Trap;
        s.NeedsClose = doorOpen;
        s.WantTaunt = canTaunt;
        s.FirstStep = doorOpen ? EStep.MoveToClose : EStep.MoveToStack;
    }

    private bool FindFarHold(DoorGeometry geo, out Vector3 result)
    {
        float[] depths = [6f, 5.5f, 5f];
        float[] laterals = [0f, 1.5f, -1.5f];
        foreach (float depth in depths)
        {
            foreach (float lateral in laterals)
            {
                Vector3 raw = geo.Center + geo.BotSide * depth + geo.Axis * lateral;
                if (!SampleOnBotSide(raw, geo, out Vector3 point))
                {
                    continue;
                }
                Vector3 flat = point - geo.Center;
                flat.y = 0f;
                if (flat.magnitude < FAR_HOLD_MIN_DIST)
                {
                    continue;
                }
                if (Bot.Mover.CanGoToPoint(point, out _, true))
                {
                    result = point;
                    return true;
                }
            }
        }
        result = default;
        return false;
    }

    // ---------------------------------------------------------------- execution

    /// <summary>
    /// Advances the running tactic. Called from DoorTacticAction.Update.
    /// </summary>
    public void Tick()
    {
        Session s = _session;
        if (s == null)
        {
            return;
        }
        float time = Time.time;
        float stepTime = time - s.StepStartTime;

        switch (s.Step)
        {
            case EStep.MoveToStack:
                s.LookTarget = s.InsidePoint;
                if (MoveStep(s, s.Stack, false, stepTime))
                {
                    if (s.Plan == EPlan.Peek)
                    {
                        SetStep(EStep.PeekOut, "atStack");
                    }
                    else
                    {
                        SetStep(EStep.Hold, "atStack");
                    }
                }
                break;

            case EStep.PeekOut:
                Bot.Mover.IgnoreDoorSlow = true;
                s.LookTarget = s.InsidePoint;
                if (!s.Jumped && HorizontalDistance(Bot.Position, s.PeekPoint) < 1.3f)
                {
                    s.Jumped = Bot.Mover.TryJump();
                    if (s.Jumped)
                    {
                        Log($"{Who()} jump peek at door {s.Door.Id}");
                    }
                }
                if (MoveStep(s, s.PeekPoint, true, stepTime))
                {
                    SetStep(EStep.PeekBack, "peekedInside");
                }
                break;

            case EStep.PeekBack:
                Bot.Mover.IgnoreDoorSlow = true;
                s.LookTarget = s.InsidePoint;
                if (stepTime < 0.35f)
                {
                    Bot.Mover.Stop();
                    break;
                }
                if (MoveStep(s, s.Stack, false, stepTime))
                {
                    Bot.Mover.IgnoreDoorSlow = false;
                    SetStep(s.WantFakeNade ? EStep.FakeNadeDraw : EStep.Hold, "backAtStack");
                }
                break;

            case EStep.FakeNadeDraw:
                s.LookTarget = s.InsidePoint;
                if (!s.FakeNadeDrawn)
                {
                    s.FakeNadeDrawn = true;
                    if (!DrawGrenadeForFake())
                    {
                        SetStep(EStep.Hold, "fakeNadeDrawFailed");
                    }
                    break;
                }
                if (stepTime > 1.3f)
                {
                    SetStep(EStep.FakeNadeHolster, "fakeNadeShown");
                }
                break;

            case EStep.FakeNadeHolster:
                s.LookTarget = s.InsidePoint;
                if (RestoreWeaponIfHoldingGrenade())
                {
                    SetStep(EStep.Hold, "fakeNadeHolstered");
                }
                else if (stepTime > 3f)
                {
                    Log($"{Who()} WARNING could not put the fake grenade away within 3s, continuing");
                    SetStep(EStep.Hold, "fakeNadeHolsterTimeout");
                }
                break;

            case EStep.MoveToClose:
                s.LookTarget = s.InsidePoint;
                if (MoveStep(s, s.ClosePoint, false, stepTime))
                {
                    SetStep(EStep.CloseDoor, "atDoor");
                }
                break;

            case EStep.CloseDoor:
                s.LookTarget = s.Center + Vector3.up * 1.2f;
                if (!s.CloseAttempted)
                {
                    s.CloseAttempted = true;
                    Bot.Mover.Stop();
                    bool ok = Bot.DoorOpener.TryCloseDoorForTactic(s.Door);
                    Log($"{Who()} close door {s.Door.Id}: interact={(ok ? "ok" : "FAILED")}");
                    break;
                }
                if (stepTime > 1.2f)
                {
                    Log($"{Who()} door {s.Door.Id} state after close attempt: {s.Door.Door?.DoorState}");
                    if (s.WantTaunt)
                    {
                        bool said = Bot.Talk.Say(EPhraseTrigger.OnFight, ETagStatus.Combat, false)
                            || Bot.Talk.Say(EPhraseTrigger.BadWork, ETagStatus.Combat, false);
                        Log($"{Who()} taunt after closing door: {(said ? "said" : "blocked")}");
                    }
                    if (s.WantDoorNade && s.HasFarHold)
                    {
                        SetStep(EStep.MoveToFarHold, "doorClosed");
                    }
                    else
                    {
                        SetStep(EStep.MoveToStack, "doorClosed");
                    }
                }
                break;

            case EStep.MoveToFarHold:
                s.LookTarget = null;
                if (MoveStep(s, s.FarHold, true, stepTime))
                {
                    SetStep(EStep.ThrowNade, "atFarHold");
                }
                break;

            case EStep.ThrowNade:
                s.LookTarget = s.Center + Vector3.up * 0.5f;
                if (!s.NadeThrown)
                {
                    Bot.Mover.Stop();
                    s.NadeThrown = true;
                    bool thrown = TryThrowAtDoor(s);
                    Log($"{Who()} door grenade at door {s.Door.Id}: {(thrown ? "THROWN" : "no valid arc")}");
                    if (!thrown)
                    {
                        SetStep(EStep.Hold, "nadeNoArc");
                    }
                    break;
                }
                if (stepTime > 1.5f)
                {
                    SetStep(EStep.Hold, "nadeThrown");
                }
                break;

            case EStep.Hold:
                s.LookTarget = s.Center + Vector3.up * 1.2f;
                Bot.Mover.Stop();
                Bot.Mover.SetTargetPose(s.HoldPose);
                EDoorState now = s.Door.Door != null ? s.Door.Door.DoorState : EDoorState.None;
                if (now != s.DoorStateAtHoldStart)
                {
                    Log($"{Who()} door {s.Door.Id} changed {s.DoorStateAtHoldStart} -> {now} while holding");
                    s.DoorStateAtHoldStart = now;
                }
                if (stepTime > s.HoldTime)
                {
                    End("holdTimeout");
                }
                break;
        }
    }

    private bool MoveStep(Session s, Vector3 target, bool sprint, float stepTime)
    {
        if (HorizontalDistance(Bot.Position, target) < ARRIVE_DIST)
        {
            return true;
        }
        if (stepTime > MOVE_STEP_TIMEOUT)
        {
            End($"moveTimeout:{s.Step}");
            return false;
        }
        if (s.NextMoveOrderTime < Time.time)
        {
            s.NextMoveOrderTime = Time.time + 0.75f;
            bool ok = sprint
                ? Bot.Mover.RunToPoint(target, true, ARRIVE_DIST * 0.7f, ESprintUrgency.High)
                : Bot.Mover.WalkToPoint(target, true, ARRIVE_DIST * 0.7f);
            if (!ok)
            {
                End($"noPath:{s.Step}");
                return false;
            }
        }
        if (!sprint)
        {
            Bot.Mover.SetTargetMoveSpeed(s.Plan == EPlan.Peek ? 0.7f : 0.45f);
        }
        return false;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return (a - b).magnitude;
    }

    private bool DrawGrenadeForFake()
    {
        var grenades = BotOwner.WeaponManager?.Grenades;
        if (grenades == null || grenades.ThrowindNow || !grenades.HaveGrenade)
        {
            Log($"{Who()} fake grenade skipped: no grenade or already throwing");
            return false;
        }
        grenades.CheckGrenade();
        var nade = grenades.grenade;
        if (nade == null)
        {
            Log($"{Who()} fake grenade skipped: CheckGrenade found none");
            return false;
        }
        Callback<IGrenadeController> callback = result =>
        {
            Log($"{Who()} fake grenade drawn: {(result.Value != null ? "in hands" : "FAILED")}");
        };
        Player.SetInHands(nade, callback);
        return true;
    }

    /// <summary>
    /// Puts the weapon back if a grenade is in hands and no real throw is running. Returns true
    /// once the hands no longer hold a grenade.
    /// </summary>
    private bool RestoreWeaponIfHoldingGrenade()
    {
        if (Player.HandsController is not IGrenadeController)
        {
            return true;
        }
        var weaponManager = BotOwner.WeaponManager;
        if (weaponManager == null || weaponManager.Grenades.ThrowindNow || weaponManager.Selector.IsChanging)
        {
            return false;
        }
        if (_nextHolsterAttempt < Time.time)
        {
            _nextHolsterAttempt = Time.time + 0.5f;
            bool ok = weaponManager.Selector.TakePrevWeapon();
            Log($"{Who()} fake grenade put away: TakePrevWeapon={ok}");
        }
        return false;
    }

    private float _nextHolsterAttempt;

    /// <summary>
    /// Used when the tactic is interrupted (usually because the enemy just appeared) so the bot
    /// never starts a firefight with a fake grenade still in its hands. Mirrors what
    /// BotGrenadeController.EndAll does after a real throw: TakePrevWeapon, and if that is refused
    /// right now, retry once shortly after.
    /// </summary>
    private void ForceRestoreWeapon()
    {
        if (Player.HandsController is not IGrenadeController)
        {
            return;
        }
        var weaponManager = BotOwner.WeaponManager;
        if (weaponManager == null || weaponManager.Grenades.ThrowindNow)
        {
            return;
        }
        bool ok = weaponManager.Selector.TakePrevWeapon();
        Log($"{Who()} interrupted with a grenade in hands, TakePrevWeapon={ok}");
        if (!ok)
        {
            BotOwner.AITaskManager.RegisterDelayedTask(
                BotOwner,
                1f,
                () =>
                {
                    if (Player != null && Player.HandsController is IGrenadeController && !weaponManager.Grenades.ThrowindNow)
                    {
                        Log($"{Who()} retry put away grenade: TakePrevWeapon={weaponManager.Selector.TakePrevWeapon()}");
                    }
                }
            );
        }
    }

    private bool TryThrowAtDoor(Session s)
    {
        var grenades = BotOwner.WeaponManager?.Grenades;
        if (grenades == null || grenades.ThrowindNow || !grenades.HaveGrenade || !grenades.ReadyToThrow)
        {
            return false;
        }
        Vector3 target = s.Center + (Bot.Position - s.Center).normalized * 0.6f;
        target.y = s.Center.y + 0.25f;
        Vector3 from = Bot.Transform.WeaponData.WeaponRoot;
        AIGreandeAng[] angles = [AIGreandeAng.ang15, AIGreandeAng.ang25, AIGreandeAng.ang5, AIGreandeAng.ang35];
        foreach (AIGreandeAng angle in angles)
        {
            AIGreanageThrowData data = AIGrenadeHelper.CanThrowGrenade2(from, target, grenades.MaxPower * 0.9f, angle, -1f, 0.66f);
            if (data.CanThrow && grenades.SetThrowData(data))
            {
                return grenades.DoThrow();
            }
        }
        return false;
    }

    // ---------------------------------------------------------------- lifecycle

    private void SetStep(EStep step, string why)
    {
        Session s = _session;
        if (s == null)
        {
            return;
        }
        Log($"{Who()} step {s.Step} -> {step} ({why}) t={Time.time - s.StartTime:0.0}s");
        s.Step = step;
        s.StepStartTime = Time.time;
        s.NextMoveOrderTime = 0f;
        if (step == EStep.Hold)
        {
            s.DoorStateAtHoldStart = s.Door.Door != null ? s.Door.Door.DoorState : EDoorState.None;
        }
    }

    /// <summary>
    /// Called by DoorTacticAction.Stop: the combat layer switched to another decision (usually
    /// StandAndShoot because the enemy showed up) or the layer went inactive.
    /// </summary>
    public void OnActionStopped()
    {
        Bot.Mover.IgnoreDoorSlow = false;
        if (_session == null)
        {
            return;
        }
        ForceRestoreWeapon();
        ECombatDecision next = Bot.Decision.CurrentCombatDecision;
        string why = next == ECombatDecision.StandAndShoot ? "enemySpotted(StandAndShoot)" : $"interrupted({next})";
        End(why);
    }

    public void End(string result)
    {
        Session s = _session;
        if (s == null)
        {
            return;
        }
        _session = null;
        Bot.Mover.IgnoreDoorSlow = false;
        float time = Time.time;
        _doorCooldowns[s.Door.Id] = time + DOOR_COOLDOWN_AFTER_SESSION;
        _nextAllowedTime = time + GLOBAL_COOLDOWN;
        Log(
            $"{Who()} END plan={s.Plan} lastStep={s.Step} result={result} duration={time - s.StartTime:0.0}s "
                + $"jumped={s.Jumped} fakeNade={s.FakeNadeDrawn} nadeThrown={s.NadeThrown} enemyVisibleNow={s.Enemy?.IsVisible}"
        );
    }

    public override void Dispose()
    {
        if (_session != null)
        {
            End("disposed");
        }
        base.Dispose();
    }

    // ---------------------------------------------------------------- logging

    private string Who()
    {
        return $"[DoorTactic] [{Bot.name}] [{Bot.Info.Personality}]";
    }

    private static void Log(string message)
    {
        if (DoorTacticConfig.DiagnosticLogs != null && DoorTacticConfig.DiagnosticLogs.Value)
        {
            Logger.LogWarning(message);
        }
    }

    private void LogVerbose(string reason)
    {
        if (DoorTacticConfig.VerboseLogs == null || !DoorTacticConfig.VerboseLogs.Value)
        {
            return;
        }
        if (reason == "personalityNoTactics" || reason == "disabled" || _nextVerboseLogTime > Time.time)
        {
            return;
        }
        _nextVerboseLogTime = Time.time + VERBOSE_LOG_INTERVAL;
        Logger.LogWarning($"{Who()} not used: {reason}");
    }

    private Session _session;
    private float _nextAllowedTime;
    private float _nextVerboseLogTime;
    private readonly Dictionary<int, float> _doorCooldowns = new();
}
