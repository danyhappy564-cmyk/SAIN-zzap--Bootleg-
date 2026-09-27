using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using SAIN.Components;
using SAIN.Models.Enums;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.GlobalSettings.Categories.General;
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
/// the F6 setting General > Door Tactics (zzap) > Diagnostic Logs is on.
/// </summary>
public class DoorTacticClass : BotComponentClassBase
{
    public enum EPlan
    {
        None,
        Peek,
        Trap,
        Ambush,
        Overwatch,
        RearGuard,
        Breach,
        Assault,
    }

    public enum EStep
    {
        None,
        MoveToStack,
        MoveToOverwatch,
        MoveToAssault,
        AssaultReady,
        AssaultThrow,
        BreachReady,
        BreachThrow,
        BreachWaitBlast,
        BreachRush,
        OpenDoorFromSide,
        PeekOut,
        PeekBack,
        FakeNadeDraw,
        FakeNadeHolster,
        FakeHealStart,
        FakeHealCancel,
        MoveToClose,
        CloseDoor,
        MoveToFarHold,
        NadeListen,
        ThrowNade,
        Hold,
        WaitBlast,
        PostBlastListen,
        FakeRetreatRun,
        FakeRetreatSneakBack,
    }

    private const float MAX_BOT_DOOR_DIST = 8f;
    private const float MAX_ENEMY_DOOR_DIST = 12f;
    private const float MIN_ENEMY_DEPTH = 0.6f;
    private const float MIN_BOT_DEPTH = 0.3f;
    private const float MAX_TIME_SINCE_KNOWN = 25f;
    private const float STACK_DEPTH = 0.9f;
    private const float STACK_SIDE_GAP = 0.7f;
    private const float PEEK_DEPTH = 1.0f;
    private const float PEEK_LOOK_TIME = 0.15f;
    private const float EMERGENCY_WINDOW_AFTER_FAKE = 2.5f;
    private const float EMERGENCY_MIN_RETREAT_TIME = 1.5f;
    private const float CLOSE_DEPTH = 0.9f;
    private const float ARRIVE_DIST = 0.6f;
    private const float MOVE_STEP_TIMEOUT = 7f;
    private const float SESSION_MAX_TIME = 150f;
    private const float FAR_HOLD_MIN_DIST = 6f;
    private const float FAR_HOLD_MAX_PATH = 14f;
    private const float DOOR_COOLDOWN_AFTER_SESSION = 60f;
    private const float DOOR_COOLDOWN_AFTER_ROLL_FAIL = 25f;
    private const float GLOBAL_COOLDOWN = 8f;
    private const float VERBOSE_LOG_INTERVAL = 10f;

    public DoorTacticClass(BotComponent bot)
        : base(bot)
    {
        CanEverTick = false;
        TacticDiagnostics.OnBotCreated();
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

        if (TryStartSupportRole(enemy, out reason))
        {
            return true;
        }

        if (!TryStart(enemy, out reason))
        {
            LogVerbose(reason);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Door id of the tactic this bot is leading, or -1. Read by squad support bots every tick.
    /// </summary>
    public int LeadingDoorId
    {
        get { return _session != null && !_session.IsSupport ? _session.Door.Id : -1; }
    }

    /// <summary>Squad breach sync, read by Assault teammates: has the leader thrown, and when does it go off.</summary>
    public bool LeaderBreachThrown
    {
        get { return _session != null && _session.Plan == EPlan.Breach && _session.BreachThrown; }
    }

    public float LeaderBreachBlastTime
    {
        get { return _session != null && _session.Plan == EPlan.Breach ? _session.BreachBlastTime : -1f; }
    }

    private bool TryStart(Enemy enemy, out string reason)
    {
        if (!Settings.Enabled)
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
        bool testMode = Settings.TestModeAllPmcGigaChad && Bot.Info.Profile.IsPMC;
        if (testMode)
        {
            personality = EPersonality.GigaChad;
        }
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
        bool resumeCandidate = Settings.ResumeAfterThirdParty
            && _resumeUntil > Time.time
            && _resumeEnemyId == enemy.EnemyProfileId;
        float maxSinceKnown = resumeCandidate ? Mathf.Max(MAX_TIME_SINCE_KNOWN, Settings.ResumeWindow) : MAX_TIME_SINCE_KNOWN;
        if (enemy.TimeSinceLastKnownUpdated > maxSinceKnown)
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

        if (DoorClaimedByOther(geo.Data.Id, out string holder))
        {
            reason = $"doorClaimedBy({holder})";
            TacticDiagnostics.Count("door.skipClaimedBySomeoneElse");
            return false;
        }
        if (Settings.SquadChecks && SquadOrFlankProblem(geo.Center, geo.BotSide, enemy, out string squadReason, false, null))
        {
            reason = squadReason;
            return false;
        }

        bool resuming = resumeCandidate && _resumeDoorId == geo.Data.Id;
        float chance = testMode ? 1f : Mathf.Clamp01(baseChance * Settings.ChanceMultiplier);
        if (resuming)
        {
            _resumeUntil = 0f;
            TacticDiagnostics.Count("door.resume");
            Log($"{Who()} RESUME door {geo.Data.Id} after third party ({_resumeReason}), no re-roll");
        }
        else if (Random.value > chance)
        {
            _doorCooldowns[geo.Data.Id] = time + DOOR_COOLDOWN_AFTER_ROLL_FAIL;
            _nextAllowedTime = time + GLOBAL_COOLDOWN;
            reason = $"rollFailed({chance:0.00})";
            TacticDiagnostics.Count($"door.rollFailed.{personality}");
            Log($"{Who()} skipped door {geo.Data.Id}: chance roll failed ({chance:0.00}), door cooldown {DOOR_COOLDOWN_AFTER_ROLL_FAIL}s");
            return false;
        }

        Session session = BuildSession(personality, enemy, geo, out reason);
        if (session == null)
        {
            _doorCooldowns[geo.Data.Id] = time + DOOR_COOLDOWN_AFTER_ROLL_FAIL;
            _nextAllowedTime = time + GLOBAL_COOLDOWN;
            Log($"{Who()} could not plan at door {geo.Data.Id}: {reason}");
            TacticDiagnostics.Count($"door.planFailed.{reason}");
            return false;
        }

        _session = session;
        _doorClaims[geo.Data.Id] = new DoorClaim(Bot.ProfileId, Bot.name, Time.time + SESSION_MAX_TIME);
        TacticDiagnostics.Count($"door.start.{personality}.{session.Plan}");
        Log(
            $"{Who()} START plan={session.Plan} door={geo.Data.Id} doorState={geo.Data.Door.DoorState} "
                + $"botDist={geo.BotDistance:0.0}m enemyDepth={geo.EnemyDepth:0.0}m sinceKnown={enemy.TimeSinceLastKnownUpdated:0.0}s "
                + $"fakeNade={session.WantFakeNade} fakeHeal={session.WantFakeHeal} doorNade={session.WantDoorNade} taunt={session.WantTaunt} hold={session.HoldTime:0}s"
        );
        SetStep(session.FirstStep, "start");
        if (Settings.SquadRoles)
        {
            AssignSquadRoles(session, geo);
        }
        reason = $"start:{session.Plan}";
        return true;
    }

    private static float GetBaseChance(EPersonality personality)
    {
        switch (personality)
        {
            case EPersonality.GigaChad:
                return Settings.GigaChadChance / 100f;
            case EPersonality.Chad:
                return Settings.ChadChance / 100f;
            case EPersonality.SnappingTurtle:
                return Settings.SnappingTurtleChance / 100f;
            case EPersonality.Rat:
                return Settings.RatChance / 100f;
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

            // The link's Close1/Close2_Normal/MidClose sit at door height, not on the floor; every stack/peek/
            // close point sampled from there was >0.7m above the navmesh -> "noStackPointOnNavmesh" on every
            // door in the first two raids. Drop the center to the floor on the bot's side.
            center = FloorPoint(data.Link, center, normal * Mathf.Sign(botDot));

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
    private static readonly float[] STACK_DEPTHS = { STACK_DEPTH, 1.3f, 0.6f, 1.8f };
    private static readonly float[] STACK_GAPS = { STACK_SIDE_GAP, 0.4f, 1.1f, 0.15f };

    /// <summary>
    /// Beside the frame on the bot's side. The first test (Customs) failed with the single 0.9m/0.7m spot on
    /// several doors (frame close to a corner or a narrow corridor), so try a few depths/gaps on both edges,
    /// preferring the edge closer to the bot. The spot must stay outside the doorway's width (not visible
    /// straight through the opening).
    /// </summary>
    private static bool FindStackPoint(DoorGeometry geo, float side, out Vector3 result)
    {
        foreach (float edge in new[] { side, -side })
        {
            foreach (float depth in STACK_DEPTHS)
            {
                foreach (float gap in STACK_GAPS)
                {
                    Vector3 raw = geo.Center + geo.BotSide * depth + geo.Axis * edge * (geo.HalfWidth + gap);
                    if (!SampleOnBotSide(raw, geo, out result))
                    {
                        continue;
                    }
                    if (Mathf.Abs(Vector3.Dot(result - geo.Center, geo.Axis)) < geo.HalfWidth * 0.9f)
                    {
                        continue;
                    }
                    return true;
                }
            }
        }
        result = default;
        return false;
    }

    private static Vector3 FloorPoint(NavMeshDoorLink link, Vector3 center, Vector3 botSide)
    {
        // Off the door plane (a shut door would stop the ray), straight down.
        Vector3 start = center + botSide * 0.5f + Vector3.up * 0.3f;
        if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, 3f, LayersMaskController.HighPolyWithTerrainMask))
        {
            center.y = hit.point.y;
            return center;
        }
        if (Mathf.Abs(center.y - link.BottomY) < 3f)
        {
            center.y = link.BottomY;
        }
        return center;
    }

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
        public Vector3 BotSide;
        public Vector3 InsidePoint;
        public Vector3 Stack;
        public Vector3 PeekPoint;
        public Vector3 ClosePoint;
        public Vector3 FarHold;
        public bool HasFarHold;
        public bool WantFakeNade;
        public bool WantFakeHeal;
        public bool FakeHealStarted;
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
        public bool JumpedBack;
        public bool OpenAttempted;
        public bool NeedsOpenForPeek;
        public bool FakeNadeDrawn;
        public bool CloseAttempted;
        public bool NadeThrown;
        public int NadeCount;
        public float NadeThrowTime;
        public float NadeFuse;
        public Grenade OwnNade;
        public bool OwnNadeTracked;
        public float BlastTime = -1f;
        public bool PostBlastPeek;
        public bool NadeInside;
        public Vector3 FakeRetreatPoint;
        public bool PeekPointOk;
        public EDoorState DoorStateAtHoldStart;
        public Vector3? LookTarget;
        public Vector3? HoldLook;
        public bool IsSupport;
        public BotComponent Leader;
        public Vector3 OverwatchPoint;
        public float LeaderGoneTime = -1f;
        public Vector3 RushPoint;
        public bool BreachThrown;
        public float BreachBlastTime = -1f;
        public bool OwnNadeThrown;
        public readonly HashSet<string> SupportIds = new();
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
            BotSide = geo.BotSide,
            InsidePoint = geo.Center - geo.BotSide * 1.5f + Vector3.up * 1.2f,
            StartTime = Time.time,
        };

        // Stack beside the frame on the bot's side, on whichever edge is closer to the bot.
        float side = Mathf.Sign(Vector3.Dot(Bot.Position - geo.Center, geo.Axis));
        if (side == 0f)
        {
            side = 1f;
        }
        if (!FindStackPoint(geo, side, out s.Stack))
        {
            bool nav = NavMesh.SamplePosition(geo.Center + geo.BotSide * STACK_DEPTH, out NavMeshHit near, 3f, -1);
            Log($"{Who()} no stack point at door {geo.Data.Id}: center y={geo.Center.y:0.00} linkMid y={geo.Data.Link.MidClose.y:0.00} "
                + $"bottomY={geo.Data.Link.BottomY:0.00} width={geo.HalfWidth * 2f:0.00} nearestNav={(nav ? $"{(near.position - (geo.Center + geo.BotSide * STACK_DEPTH)).magnitude:0.00}m away" : "none within 3m")}");
            reason = "noStackPointOnNavmesh";
            return null;
        }
        bool peekPointOk = SampleOnBotSide(geo.Center + geo.BotSide * PEEK_DEPTH, geo, out s.PeekPoint);
        s.PeekPointOk = peekPointOk;
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
                // Shut door: opened first from the stack point beside the frame, out of the room's line of sight.
                bool peekAllowed = Settings.JumpPeek && peekPointOk;
                bool trapAllowed = personality == EPersonality.GigaChad && Settings.RoomTrap;
                bool breachAllowed = Settings.SquadBreach
                    && Settings.SquadRoles
                    && haveNade
                    && FindRoleCandidates(geo, enemy.EnemyProfileId).Count > 0;
                if (breachAllowed && Random.value < Settings.SquadBreachChance / 100f)
                {
                    s.Plan = EPlan.Breach;
                    s.FirstStep = EStep.MoveToStack;
                    s.NeedsOpenForPeek = !doorOpen;
                    s.RushPoint = PickRushPoint(geo, enemy, 0f);
                    s.HoldTime = 0f;
                    s.HoldPose = 0.8f;
                }
                else if (peekAllowed && (!trapAllowed || Random.value < Settings.GigaChadPeekChance / 100f))
                {
                    s.Plan = EPlan.Peek;
                    s.FirstStep = EStep.MoveToStack;
                    s.NeedsOpenForPeek = !doorOpen;
                    float fakeChance = (personality == EPersonality.GigaChad ? Settings.GigaChadFakeTrickChance : Settings.ChadFakeTrickChance) / 100f;
                    s.WantFakeNade = Settings.FakeGrenade && haveNade && Random.value < fakeChance;
                    s.WantFakeHeal = !s.WantFakeNade && CanFakeHeal() && Random.value < fakeChance;
                    s.HoldTime = Random.Range(3f, 6f);
                    s.HoldPose = 0.8f;
                }
                else if (trapAllowed)
                {
                    BuildTrap(s, geo, doorOpen, canTaunt);
                    s.HoldTime = Random.Range(20f, 40f);
                    s.HoldPose = 0.7f;
                    s.WantFakeHeal = CanFakeHeal() && Random.value < Settings.GigaChadTrapFakeHealChance / 100f;
                    if (Settings.DoorGrenade && FindLongFuseGrenade() != null && Random.value < Settings.DoorGrenadeChance / 100f && FindFarHold(geo, out s.FarHold))
                    {
                        s.HasFarHold = true;
                        s.WantDoorNade = true;
                        s.NadeInside = RollNadeInside(s);

                    }
                }
                else
                {
                    reason = "tacticsDisabledForPersonality";
                    return null;
                }
                break;

            case EPersonality.SnappingTurtle:
                if (!Settings.RoomTrap)
                {
                    reason = "roomTrapDisabled";
                    return null;
                }
                BuildTrap(s, geo, doorOpen, canTaunt);
                s.HoldTime = Random.Range(45f, 90f);
                s.HoldPose = 0.4f;
                break;

            case EPersonality.Rat:
                if (!Settings.RoomTrap)
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

    /// <summary>
    /// Where to wait out our own door grenade: 6-9m back on our side, preferring a spot the landing point
    /// can't see (around a corner / behind a wall), and a short path so the bot gets there before the fuse.
    /// </summary>
    private bool FindFarHold(DoorGeometry geo, out Vector3 result)
    {
        float[] depths = [9f, 8f, 7f, 6f];
        float[] laterals = [0f, 2f, -2f, 3.5f, -3.5f];
        Vector3 landing = geo.Center + geo.BotSide * 0.4f + Vector3.up * 0.3f;
        bool haveOpen = false;
        Vector3 bestOpen = default;
        float bestOpenDist = 0f;
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
                if (!Bot.Mover.CanGoToPoint(point, out NavMeshPath path, true) || PathLength(path) > FAR_HOLD_MAX_PATH)
                {
                    continue;
                }
                bool covered = Physics.Linecast(landing, point + Vector3.up * 1.2f, LayersMaskController.HighPolyWithTerrainMask);
                if (covered)
                {
                    result = point;
                    return true;
                }
                if (flat.magnitude > bestOpenDist)
                {
                    bestOpenDist = flat.magnitude;
                    bestOpen = point;
                    haveOpen = true;
                }
            }
        }
        result = bestOpen;
        return haveOpen;
    }

    private static float PathLength(NavMeshPath path)
    {
        float length = 0f;
        var corners = path.corners;
        for (int i = 1; i < corners.Length; i++)
        {
            length += (corners[i] - corners[i - 1]).magnitude;
        }
        return length;
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

        if (s.IsSupport && !LeaderStillOnDoor(s, time))
        {
            return;
        }

        if (CloseThirdPartyGunfire(s, out string gunfire))
        {
            Log($"{Who()} abort at step {s.Step}: {gunfire}");
            TacticDiagnostics.Count("door.abort.closeGunfire");
            End($"closeGunfire({gunfire})");
            return;
        }

        // Third party: being shot at by anyone (e.g. a distant shooter the flank check doesn't cover)
        // means the door is no longer the priority - hand the bot back to SAIN's normal combat decisions.
        if (BotOwner.Memory.IsUnderFire)
        {
            Log($"{Who()} abort at step {s.Step}: under fire (third party or unseen shooter)");
            TacticDiagnostics.Count("door.abort.underFire");
            End("underFire");
            return;
        }

        if (Settings.SquadChecks && _nextSquadCheckTime < time)
        {
            _nextSquadCheckTime = time + 0.5f;
            if (SquadOrFlankProblem(s.Center, s.BotSide, s.Enemy, out string squadReason, s.IsSupport, s.SupportIds))
            {
                Log($"{Who()} abort at step {s.Step}: {squadReason}");
                TacticDiagnostics.Count($"door.abort.{squadReason.Split('(')[0]}");
                End(squadReason);
                return;
            }
        }

        switch (s.Step)
        {
            case EStep.MoveToStack:
                s.LookTarget = s.InsidePoint;
                if (MoveStep(s, s.Stack, s.PostBlastPeek, stepTime))
                {
                    if (s.PostBlastPeek)
                    {
                        s.NeedsOpenForPeek = s.Door.Door != null && s.Door.Door.DoorState != EDoorState.Open;
                        SetStep(s.NeedsOpenForPeek ? EStep.OpenDoorFromSide : EStep.PeekOut, "atStackAfterBlast");
                    }
                    else if (s.Plan == EPlan.Peek)
                    {
                        SetStep(s.NeedsOpenForPeek ? EStep.OpenDoorFromSide : EStep.PeekOut, "atStack");
                    }
                    else if (s.Plan == EPlan.Breach)
                    {
                        SetStep(s.NeedsOpenForPeek ? EStep.OpenDoorFromSide : EStep.BreachReady, "atStack");
                    }
                    else if (s.WantDoorNade && !s.NadeThrown)
                    {
                        bool shut = s.Door.Door != null && s.Door.Door.DoorState != EDoorState.Open;
                        if (s.NadeInside && shut)
                        {
                            s.OpenAttempted = false;
                            SetStep(EStep.OpenDoorFromSide, "atStackOpenForInsideNade");
                        }
                        else
                        {
                            SetStep(EStep.NadeListen, "atStack");
                        }
                    }
                    else
                    {
                        SetStep(s.WantFakeHeal ? EStep.FakeHealStart : EStep.Hold, "atStack");
                    }
                }
                break;

            case EStep.OpenDoorFromSide:
                // Opened from beside the frame so whoever is inside can't see the bot through the gap.
                s.LookTarget = s.InsidePoint;
                if (!s.OpenAttempted)
                {
                    s.OpenAttempted = true;
                    Bot.Mover.Stop();
                    bool ok = Bot.DoorOpener.TryOpenDoorForTactic(s.Door);
                    Log($"{Who()} open door {s.Door.Id} from the side: interact={(ok ? "ok" : "FAILED")}");
                    if (!ok)
                    {
                        End("doorOpenFailed");
                    }
                    break;
                }
                if (stepTime > 1.1f)
                {
                    EDoorState state = s.Door.Door != null ? s.Door.Door.DoorState : EDoorState.None;
                    if (state != EDoorState.Open)
                    {
                        Log($"{Who()} door {s.Door.Id} still {state} after opening, giving up");
                        End("doorDidNotOpen");
                        break;
                    }
                    EStep afterOpen = s.Plan == EPlan.Breach ? EStep.BreachReady
                        : s.WantDoorNade && !s.NadeThrown && s.NadeInside ? EStep.NadeListen
                        : EStep.PeekOut;
                    SetStep(afterOpen, "doorOpened");
                }
                break;

            case EStep.PeekOut:
                // Corridor-side bunny hop: sprint-jump out in front of the doorway (never into the room),
                // head snapped sideways into the room the whole time.
                Bot.Mover.IgnoreDoorSlow = true;
                s.LookTarget = s.InsidePoint;
                if (!s.Jumped)
                {
                    s.Jumped = Bot.Mover.TryJump();
                    if (s.Jumped)
                    {
                        TacticDiagnostics.Count("door.jumpOut");
                        Log($"{Who()} jump peek OUT at door {s.Door.Id}");
                    }
                }
                if (MoveStep(s, s.PeekPoint, true, stepTime))
                {
                    SetStep(EStep.PeekBack, "inFrontOfDoor");
                }
                break;

            case EStep.PeekBack:
                // Quick look, then bunny hop straight back to the stack point while still facing the room.
                Bot.Mover.IgnoreDoorSlow = true;
                s.LookTarget = s.InsidePoint;
                if (stepTime < PEEK_LOOK_TIME)
                {
                    break;
                }
                if (!s.JumpedBack)
                {
                    s.JumpedBack = Bot.Mover.TryJump();
                    if (s.JumpedBack)
                    {
                        TacticDiagnostics.Count("door.jumpBack");
                        Log($"{Who()} jump peek BACK at door {s.Door.Id}");
                    }
                }
                if (MoveStep(s, s.Stack, true, stepTime))
                {
                    Bot.Mover.IgnoreDoorSlow = false;
                    SetStep(s.WantFakeNade ? EStep.FakeNadeDraw : s.WantFakeHeal ? EStep.FakeHealStart : EStep.Hold, "backAtStack");
                }
                break;

            case EStep.FakeNadeDraw:
                s.LookTarget = s.InsidePoint;
                if (s.FakeNadeDrawn && EnemyComingOut(s, out string heardNade))
                {
                    Log($"{Who()} heard {heardNade} during fake grenade -> put it away now, gun up on the door");
                    TacticDiagnostics.Count("trick.cancelledOnSound");
                    SetStep(EStep.FakeNadeHolster, "heardComingOut");
                    break;
                }
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

            case EStep.FakeHealStart:
                s.LookTarget = s.InsidePoint;
                if (s.FakeHealStarted && EnemyComingOut(s, out string heardHeal))
                {
                    Log($"{Who()} heard {heardHeal} during fake heal/stim -> cancel now, gun up on the door");
                    TacticDiagnostics.Count("trick.cancelledOnSound");
                    CancelFakeHeal("heardComingOut");
                    SetStep(EStep.Hold, "heardComingOut");
                    break;
                }
                if (!s.FakeHealStarted)
                {
                    s.FakeHealStarted = true;
                    Bot.Mover.Stop();
                    if (!StartFakeHeal())
                    {
                        SetStep(EStep.Hold, "fakeHealStartFailed");
                    }
                    break;
                }
                if (stepTime > (_fakeStimRunning ? 0.7f : 1.5f))
                {
                    SetStep(EStep.FakeHealCancel, _fakeStimRunning ? "fakeStimShown" : "fakeHealShown");
                }
                break;

            case EStep.FakeHealCancel:
                s.LookTarget = s.InsidePoint;
                CancelFakeHeal("done");
                SetStep(EStep.Hold, "fakeHealCancelled");
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
                    TacticDiagnostics.Count(ok ? "door.closeOk" : "door.closeFailed");
                    break;
                }
                if (stepTime > 1.2f)
                {
                    Log($"{Who()} door {s.Door.Id} state after close attempt: {s.Door.Door?.DoorState}");
                    TacticDiagnostics.Count($"door.stateAfterClose.{s.Door.Door?.DoorState}");
                    if (s.WantTaunt)
                    {
                        bool said = Bot.Talk.Say(EPhraseTrigger.OnFight, ETagStatus.Combat, false)
                            || Bot.Talk.Say(EPhraseTrigger.BadWork, ETagStatus.Combat, false);
                        Log($"{Who()} taunt after closing door: {(said ? "said" : "blocked")}");
                    }
                    SetStep(EStep.MoveToStack, "doorClosed");
                }
                break;

            case EStep.MoveToFarHold:
                s.LookTarget = null;
                if (MoveStep(s, s.FarHold, true, stepTime))
                {
                    SetStep(Settings.PostBlastFollowUp ? EStep.WaitBlast : EStep.Hold, "backedOffFromOwnGrenade");
                }
                break;

            case EStep.WaitBlast:
                // Out of the grenade's reach, gun on the door, until it goes off.
                s.LookTarget = s.Center + Vector3.up * 1.2f;
                Bot.Mover.Stop();
                Bot.Mover.SetTargetPose(0.7f);
                {
                    bool gone = s.OwnNadeTracked && s.OwnNade == null;
                    bool late = time > s.NadeThrowTime + s.NadeFuse + 1f;
                    if (gone || late || stepTime > 12f)
                    {
                        s.BlastTime = time;
                        TacticDiagnostics.Count(gone ? "door.postBlast.blast" : "door.postBlast.blastAssumed");
                        bool peek = Random.value * 100f < Settings.PostBlastPeekChance;
                        bool fake = !peek && Random.value * 100f < Settings.PostBlastFakeRetreatChance && FindFakeRetreatPoint(s);
                        Log($"{Who()} door grenade #{s.NadeCount} went off ({(gone ? "tracked" : "by fuse time")}) -> "
                            + (peek ? "rush the door, peek-shoot, pull back"
                                : fake ? "FAKE RETREAT: run off loudly, sneak back, wait for them to come out"
                                : $"listen {Settings.PostBlastListenTime:0.0}s for a reaction"));
                        if (peek)
                        {
                            StartPostBlastPeek(s, "rolledPeek");
                        }
                        else if (fake)
                        {
                            TacticDiagnostics.Count("door.postBlast.fakeRetreat");
                            SetStep(EStep.FakeRetreatRun, "rolledFakeRetreat");
                        }
                        else
                        {
                            SetStep(EStep.PostBlastListen, "rolledListen");
                        }
                    }
                }
                break;

            case EStep.FakeRetreatRun:
                // Sprint away from the door on purpose: the footsteps are the bait ("they ran off").
                s.LookTarget = null;
                if (MoveStep(s, s.FakeRetreatPoint, true, stepTime) || stepTime > 2.5f)
                {
                    Log($"{Who()} fake retreat: ran {HorizontalDistance(Bot.Position, s.FarHold):0.0}m off, now sneaking back");
                    SetStep(EStep.FakeRetreatSneakBack, "ranOff");
                }
                break;

            case EStep.FakeRetreatSneakBack:
                // Crouched, very slow (quiet) back to the covered spot with the gun on the door, then wait long.
                s.LookTarget = s.Center + Vector3.up * 1.2f;
                Bot.Mover.SetTargetPose(0.5f);
                if (MoveStep(s, s.FarHold, false, stepTime))
                {
                    TacticDiagnostics.Count("door.postBlast.fakeRetreatSetUp");
                    Log($"{Who()} fake retreat: back in position, holding the door until they come out");
                    s.HoldTime = Random.Range(25f, 45f);
                    s.HoldPose = 0.4f;
                    SetStep(EStep.Hold, "fakeRetreatSetUp");
                }
                break;

            case EStep.PostBlastListen:
                s.LookTarget = s.Center + Vector3.up * 1.2f;
                Bot.Mover.Stop();
                Bot.Mover.SetTargetPose(0.7f);
                {
                    var hearing = s.Enemy?.Hearing;
                    if (hearing != null && hearing.LastHeardSoundTime > s.BlastTime + 0.3f)
                    {
                        // Someone is alive and moving in there: don't walk into it, hold the door and let them come.
                        TacticDiagnostics.Count("door.postBlast.reactionHeard");
                        Log($"{Who()} reaction after the blast ({hearing.LastHeardSoundType}) -> hold the door");
                        s.HoldTime = Random.Range(8f, 15f);
                        s.HoldPose = 0.7f;
                        SetStep(EStep.Hold, "reactionHeard");
                        break;
                    }
                    if (stepTime < Settings.PostBlastListenTime)
                    {
                        break;
                    }
                    if (s.NadeCount < Settings.MaxDoorGrenades && FindLongFuseGrenade() != null)
                    {
                        TacticDiagnostics.Count("door.postBlast.throwAgain");
                        Log($"{Who()} no reaction after the blast -> another grenade ({s.NadeCount + 1}/{Settings.MaxDoorGrenades:0})");
                        s.NadeThrown = false;
                        s.WantDoorNade = true;
                        s.NadeInside = RollNadeInside(s);
                        s.OpenAttempted = false;
                        SetStep(EStep.MoveToStack, "noReactionThrowAgain");
                        break;
                    }
                    TacticDiagnostics.Count("door.postBlast.noGrenadeLeft");
                    StartPostBlastPeek(s, "noReactionNoGrenade");
                }
                break;

            case EStep.NadeListen:
                // Crouch beside the frame and listen before committing: whoever is inside may have heard
                // the door close and be about to come out.
                s.LookTarget = s.Center + Vector3.up * 1.2f;
                Bot.Mover.Stop();
                Bot.Mover.SetTargetPose(0f);
                if (AbortIfEnemyComingOut(s, "before door grenade"))
                {
                    break;
                }
                if (stepTime > 0.7f)
                {
                    SetStep(EStep.ThrowNade, "quietInside");
                }
                break;

            case EStep.ThrowNade:
                // Crouched, short low toss at the door front from beside the frame (like a right-click
                // underhand throw), so it doesn't bounce back. Long fuse only, then back off.
                s.LookTarget = s.Center + Vector3.up * 0.3f;
                if (!s.NadeThrown)
                {
                    Bot.Mover.Stop();
                    Bot.Mover.SetTargetPose(0f);
                    if (AbortIfEnemyComingOut(s, "at grenade throw"))
                    {
                        break;
                    }
                    s.NadeThrown = true;
                    bool thrown = TryThrowAtDoor(s);
                    Log($"{Who()} door grenade at door {s.Door.Id}: {(thrown ? "THROWN (crouched, short toss)" : "no valid arc")}");
                    TacticDiagnostics.Count(thrown ? "door.nadeThrown" : "door.nadeNoArc");
                    if (!thrown)
                    {
                        SetStep(EStep.Hold, "nadeNoArc");
                    }
                    break;
                }
                // Go as soon as the grenade has left the hand (tracked) - not a fixed wait next to it.
                if ((_expectOwnNadeUntil == 0f && stepTime > 0.2f) || stepTime > 1.6f)
                {
                    SetStep(s.HasFarHold ? EStep.MoveToFarHold : EStep.Hold, "nadeThrown");
                }
                break;

            case EStep.BreachReady:
                // Give the assault teammates a moment to stack up on the frame.
                s.LookTarget = s.InsidePoint;
                Bot.Mover.Stop();
                if (AbortIfEnemyComingOut(s, "before breach"))
                {
                    break;
                }
                if (stepTime > 2f || SupportsReady(s))
                {
                    SetStep(EStep.BreachThrow, stepTime > 2f ? "waitedForSquad" : "squadStacked");
                }
                break;

            case EStep.BreachThrow:
                s.LookTarget = s.InsidePoint;
                if (!s.BreachThrown)
                {
                    Bot.Mover.Stop();
                    s.BreachThrown = true;
                    float fuse = ThrowIntoRoom(s, s.RushPoint, "breach");
                    s.BreachBlastTime = fuse > 0f ? Time.time + fuse + 0.8f : Time.time + 0.5f;
                    Log($"{Who()} BREACH grenade {(fuse > 0f ? $"thrown, blast in ~{s.BreachBlastTime - Time.time:0.0}s" : "NOT thrown, going in anyway")}");
                    break;
                }
                if (stepTime > 1f)
                {
                    SetStep(EStep.BreachWaitBlast, "nadeOut");
                }
                break;

            case EStep.MoveToAssault:
                s.LookTarget = s.InsidePoint;
                if (MoveStep(s, s.OverwatchPoint, true, stepTime))
                {
                    SetStep(EStep.AssaultReady, "stackedOnFrame");
                }
                break;

            case EStep.AssaultReady:
                s.LookTarget = s.InsidePoint;
                Bot.Mover.Stop();
                if (s.Leader != null && s.Leader.DoorTactic.LeaderBreachThrown)
                {
                    SetStep(EStep.AssaultThrow, "leaderThrew");
                }
                break;

            case EStep.AssaultThrow:
                s.LookTarget = s.InsidePoint;
                if (!s.OwnNadeThrown)
                {
                    s.OwnNadeThrown = true;
                    float fuse = ThrowIntoRoom(s, s.RushPoint, "assault");
                    Log($"{Who()} assault grenade {(fuse > 0f ? "thrown" : "not thrown (none/no arc)")}");
                    break;
                }
                if (stepTime > 1f)
                {
                    SetStep(EStep.BreachWaitBlast, "nadeOut");
                }
                break;

            case EStep.BreachWaitBlast:
                s.LookTarget = s.InsidePoint;
                Bot.Mover.Stop();
                float blastAt = s.IsSupport ? s.Leader?.DoorTactic.LeaderBreachBlastTime ?? -1f : s.BreachBlastTime;
                if (blastAt > 0f && Time.time >= blastAt)
                {
                    TacticDiagnostics.Count($"door.breach.rush.{s.Plan}");
                    SetStep(EStep.BreachRush, "blast");
                }
                else if (stepTime > 8f)
                {
                    End("breachBlastTimeout");
                }
                break;

            case EStep.BreachRush:
                s.LookTarget = null;
                if (MoveStep(s, s.RushPoint, true, stepTime) || stepTime > 4f)
                {
                    End("breachEntered");
                }
                break;

            case EStep.MoveToOverwatch:
                s.LookTarget = s.Center + Vector3.up * 1.2f;
                if (MoveStep(s, s.OverwatchPoint, false, stepTime))
                {
                    SetStep(EStep.Hold, "atOverwatch");
                }
                break;

            case EStep.Hold:
                s.LookTarget = s.HoldLook ?? s.Center + Vector3.up * 1.2f;
                Bot.Mover.Stop();
                Bot.Mover.SetTargetPose(s.HoldPose);
                EDoorState now = s.Door.Door != null ? s.Door.Door.DoorState : EDoorState.None;
                if (now != s.DoorStateAtHoldStart)
                {
                    Log($"{Who()} door {s.Door.Id} changed {s.DoorStateAtHoldStart} -> {now} while holding");
                    TacticDiagnostics.Count($"door.changedWhileHolding.{now}");
                    s.DoorStateAtHoldStart = now;
                }
                if (stepTime > s.HoldTime)
                {
                    End("holdTimeout");
                }
                break;
        }
    }

    /// <summary>
    /// Right after our door grenade: sprint back to the frame, open from the side if needed, jump peek,
    /// and if the enemy shows up shoot briefly then pull back (End -> ShallEmergencyRetreat window).
    /// </summary>
    private void StartPostBlastPeek(Session s, string why)
    {
        if (!s.PeekPointOk)
        {
            TacticDiagnostics.Count("door.postBlast.noPeekPoint");
            s.HoldTime = Random.Range(6f, 12f);
            SetStep(EStep.Hold, $"{why}:noPeekPoint");
            return;
        }
        TacticDiagnostics.Count("door.postBlast.peek");
        s.PostBlastPeek = true;
        s.WantFakeNade = false;
        s.WantFakeHeal = false;
        s.WantDoorNade = false;
        s.Jumped = false;
        s.JumpedBack = false;
        s.OpenAttempted = false;
        s.HoldTime = Random.Range(4f, 8f);
        s.HoldPose = 0.8f;
        SetStep(EStep.MoveToStack, why);
    }

    /// <summary>
    /// 4-7m further back from the far hold, away from the door: far enough that the sprint is heard as leaving.
    /// </summary>
    private bool FindFakeRetreatPoint(Session s)
    {
        Vector3 away = s.FarHold - s.Center;
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : s.BotSide;
        foreach (float dist in new[] { 6f, 5f, 4f, 7f })
        {
            if (!SampleNav(s.FarHold + away * dist, out Vector3 point))
            {
                continue;
            }
            if (Bot.Mover.CanGoToPoint(point, out NavMeshPath path, true) && PathLength(path) < 14f)
            {
                s.FakeRetreatPoint = point;
                return true;
            }
        }
        TacticDiagnostics.Count("door.postBlast.fakeRetreatNoPoint");
        return false;
    }

    private float _peekShotUntil;
    private float _pullBackUntil;
    private bool _pullBackLogged;

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
            float speed = s.Step == EStep.FakeRetreatSneakBack ? 0.25f
                : s.Plan == EPlan.Peek || s.Plan == EPlan.Overwatch ? 0.7f : 0.45f;
            Bot.Mover.SetTargetMoveSpeed(speed);
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
            TacticDiagnostics.Count(result.Value != null ? "fakeNade.drawn" : "fakeNade.drawFailed");
        };
        Player.SetInHands(nade, callback);
        _emergencyWindowUntil = Time.time + 1.3f + EMERGENCY_WINDOW_AFTER_FAKE;
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
            TacticDiagnostics.Count(ok ? "fakeNade.putAway" : "fakeNade.putAwayRefused");
        }
        return false;
    }

    private float _nextHolsterAttempt;

    /// <summary>
    /// A heal can only be started when a body part is actually damaged (EFT refuses otherwise),
    /// so the fake heal is only planned for hurt bots - the same situation a player uses it in.
    /// </summary>
    private bool CanFakeHeal()
    {
        if (!Settings.FakeHeal)
        {
            return false;
        }
        var medecine = BotOwner.Medecine;
        if (medecine == null || medecine.Using)
        {
            return false;
        }
        bool hurt = medecine.FirstAid != null && !medecine.FirstAid.Using && medecine.FirstAid.Have2Do;
        bool haveStim = medecine.Stimulators?._stimulator != null && !medecine.Stimulators.Using;
        return hurt || haveStim;
    }

    private bool _fakeStimRunning;
    private float _fakeStimStartTime;

    /// <summary>
    /// Stim version of the fake: start the injector (audible) and switch back before it goes in.
    /// Used when the bot isn't hurt (a heal can't start then) but carries a stim.
    /// </summary>
    private bool StartFakeStim()
    {
        var stims = BotOwner.Medecine?.Stimulators;
        var stim = stims?._stimulator;
        if (stim == null || stims.Using)
        {
            Log($"{Who()} fake stim skipped: no stim or already using one");
            return false;
        }
        Callback<IMedsController> callback = result =>
        {
            Log($"{Who()} fake stim in hands: {(result.Value != null ? "yes" : "FAILED")}");
        };
        Player.SetInHands(stim, EBodyPart.Chest, stim.GetRandomAnimationVariant(), callback);
        _fakeStimRunning = true;
        _fakeStimStartTime = Time.time;
        _emergencyWindowUntil = Time.time + 0.8f + EMERGENCY_WINDOW_AFTER_FAKE;
        Log($"{Who()} fake stim start: {stim.ShortName.Localized()} (will cancel before injecting)");
        TacticDiagnostics.Count("fakeStim.started");
        return true;
    }

    private bool _fakeHealRunning;

    private bool StartFakeHeal()
    {
        var firstAid = BotOwner.Medecine?.FirstAid;
        if (firstAid == null || firstAid.Using || !firstAid.Have2Do)
        {
            if (StartFakeStim())
            {
                return true;
            }
            Log($"{Who()} fake heal skipped: nothing to heal or already healing");
            return false;
        }
        firstAid.TryApplyToCurrentPart();
        _fakeHealRunning = firstAid.Using;
        if (_fakeHealRunning)
        {
            _emergencyWindowUntil = Time.time + 1.5f + EMERGENCY_WINDOW_AFTER_FAKE;
        }
        Log($"{Who()} fake heal start: {(_fakeHealRunning ? "healing (will cancel)" : "FAILED to start")}");
        TacticDiagnostics.Count(_fakeHealRunning ? "fakeHeal.started" : "fakeHeal.failedToStart");
        return _fakeHealRunning;
    }

    private void CancelFakeHeal(string why)
    {
        if (_fakeStimRunning)
        {
            _fakeStimRunning = false;
            bool ok = BotOwner.WeaponManager?.Selector?.TakePrevWeapon() == true;
            Log($"{Who()} fake stim cancelled ({why}) after {Time.time - _fakeStimStartTime:0.0}s: TakePrevWeapon={ok}");
            TacticDiagnostics.Count(ok ? "fakeStim.cancelled" : "fakeStim.cancelRefused");
        }
        if (!_fakeHealRunning)
        {
            return;
        }
        _fakeHealRunning = false;
        var firstAid = BotOwner.Medecine?.FirstAid;
        if (firstAid != null && firstAid.Using)
        {
            // BSG's own cancel: TakePrevWeapon + heal cooldown.
            firstAid.StopUse();
            Log($"{Who()} fake heal cancelled ({why})");
        }
    }

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

    /// <summary>
    /// Door grenades must have a long fuse (M67-type) so the bot is well clear and the enemy has time
    /// to open the door onto it. Impact grenades (VOG etc.) and anything under the F12 minimum fuse
    /// are never used for this.
    /// </summary>
    private ThrowWeap FindLongFuseGrenade()
    {
        var inventory = Player?.InventoryController?.Inventory;
        if (inventory == null)
        {
            return null;
        }
        float minFuse = Settings.DoorGrenadeMinFuse;
        ThrowWeap best = null;
        foreach (var item in inventory.GetPlayerItems(EPlayerItems.Equipment))
        {
            if (item is not ThrowWeap nade || nade.ThrowType != ThrowWeapType.frag_grenade)
            {
                continue;
            }
            if (nade.MinTimeToContactExplode >= 0f || nade.GetExplDelay < minFuse)
            {
                continue;
            }
            if (best == null || nade.GetExplDelay > best.GetExplDelay)
            {
                best = nade;
            }
        }
        return best;
    }

    private bool TryThrowAtDoor(Session s)
    {
        var grenades = BotOwner.WeaponManager?.Grenades;
        if (grenades == null || grenades.ThrowindNow || !grenades.HaveGrenade || !grenades.ReadyToThrow)
        {
            return false;
        }
        ThrowWeap longFuse = FindLongFuseGrenade();
        if (longFuse == null)
        {
            Log($"{Who()} door grenade skipped: no frag with fuse >= {Settings.DoorGrenadeMinFuse:0.0}s");
            return false;
        }
        if (Settings.SquadChecks && TeammateNear(target: s.Center + s.BotSide * 0.4f, radius: 6f, out string mate))
        {
            Log($"{Who()} door grenade cancelled: teammate {mate} within 6m of the landing spot");
            TacticDiagnostics.Count("door.nadeCancelledTeammateNear");
            return false;
        }
        grenades.SetThrowParams(longFuse);
        Log($"{Who()} door grenade picked {longFuse.ShortName.Localized()} fuse={longFuse.GetExplDelay:0.0}s");
        // Default: right at the door on our side, so it lands against the door instead of bouncing back to us.
        // Inside: through the (opened) doorway at the enemy's last known spot, falling back to the door front.
        Vector3 front = s.Center + s.BotSide * 0.4f;
        front.y = s.Center.y + 0.1f;
        Vector3 from = Bot.Transform.WeaponData.WeaponRoot;
        bool doorOpen = s.Door.Door != null && s.Door.Door.DoorState == EDoorState.Open;
        if (s.NadeInside && doorOpen)
        {
            Vector3 inside = s.Center - s.BotSide * 2.5f;
            Vector3? known = s.Enemy?.KnownPlaces.LastKnownPosition;
            if (known != null && Vector3.Dot(known.Value - s.Center, s.BotSide) < -0.5f && HorizontalDistance(known.Value, s.Center) < 10f)
            {
                inside = known.Value;
            }
            inside.y = s.Center.y + 0.1f;
            if (TryThrowArc(grenades, longFuse, from, inside, [AIGreandeAng.ang15, AIGreandeAng.ang25, AIGreandeAng.ang5], s))
            {
                Log($"{Who()} door grenade INTO the room ({HorizontalDistance(inside, s.Center):0.0}m past the door)");
                TacticDiagnostics.Count("door.nadeInside");
                return true;
            }
            Log($"{Who()} no arc into the room -> door front instead");
            TacticDiagnostics.Count("door.nadeInsideNoArc");
        }
        return TryThrowArc(grenades, longFuse, from, front, [AIGreandeAng.ang5, AIGreandeAng.ang15, AIGreandeAng.ang25], s);
    }

    private bool RollNadeInside(Session s)
    {
        bool open = s.Door.Door != null && s.Door.Door.DoorState == EDoorState.Open;
        return open || Random.value * 100f < Settings.DoorGrenadeInsideChance;
    }

    private bool TryThrowArc(BotGrenadeController grenades, ThrowWeap longFuse, Vector3 from, Vector3 target, AIGreandeAng[] angles, Session s)
    {
        foreach (AIGreandeAng angle in angles)
        {
            AIGreanageThrowData data = AIGrenadeHelper.CanThrowGrenade2(from, target, grenades.MaxPower * 0.9f, angle, -1f, 0.66f);
            if (data.CanThrow)
            {
                // No GrenadeType: DoThrow would otherwise swap to the first frag in the rig (CheckGrenadeWithType).
                data.GrenadeType = null;
                grenades.SetThrowParams(longFuse);
                if (grenades.SetThrowData(data) && grenades.DoThrow())
                {
                    _expectOwnNadeUntil = Time.time + 4f;
                    s.NadeCount++;
                    s.NadeThrowTime = Time.time;
                    s.NadeFuse = longFuse.GetExplDelay;
                    s.OwnNade = null;
                    s.OwnNadeTracked = false;
                    return true;
                }
            }
        }
        return false;
    }

    // ---------------------------------------------------------------- squad / multiple enemies

    private readonly struct DoorClaim(string profileId, string name, float until)
    {
        public readonly string ProfileId = profileId;
        public readonly string Name = name;
        public readonly float Until = until;
    }

    /// <summary>
    /// One bot per door at a time, shared by every bot in the raid (static), so two squadmates don't
    /// both close the door, throw grenades at it or peek it together.
    /// </summary>
    private static readonly Dictionary<int, DoorClaim> _doorClaims = new();

    private float _nextSquadCheckTime;

    private bool DoorClaimedByOther(int doorId, out string holder)
    {
        holder = string.Empty;
        if (!_doorClaims.TryGetValue(doorId, out DoorClaim claim))
        {
            return false;
        }
        if (claim.ProfileId == Bot.ProfileId || claim.Until < Time.time)
        {
            _doorClaims.Remove(doorId);
            return false;
        }
        holder = claim.Name;
        return true;
    }

    /// <summary>
    /// Things that make a solo door tactic wrong when more than one player is involved:
    ///  - a teammate is inside the room / on the enemy's side of the door (never close it on them),
    ///  - a teammate is right at the door, i.e. pushing through it anyway,
    ///  - another known enemy is on OUR side of the door (someone flanking while we stare at the door).
    /// </summary>
    private bool SquadOrFlankProblem(
        Vector3 doorCenter,
        Vector3 botSide,
        Enemy goalEnemy,
        out string reason,
        bool flankOnly,
        HashSet<string> ignoreMates
    )
    {
        var members = flankOnly ? null : Bot.Squad.Members;
        if (members != null)
        {
            foreach (var member in members.Values)
            {
                if (member == null || ReferenceEquals(member, Bot) || member.IsDead)
                {
                    continue;
                }
                if (ignoreMates != null && ignoreMates.Contains(member.ProfileId))
                {
                    continue;
                }
                Vector3 toMate = member.Position - doorCenter;
                toMate.y = 0f;
                float dist = toMate.magnitude;
                if (dist < 2.5f)
                {
                    reason = $"teammatePushingDoor({member.name})";
                    return true;
                }
                if (dist < MAX_ENEMY_DOOR_DIST && Vector3.Dot(toMate, botSide) < -0.3f)
                {
                    reason = $"teammateInsideRoom({member.name})";
                    return true;
                }
            }
        }

        var enemies = Bot.EnemyController.KnownEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy other = enemies[i];
            if (other == null || ReferenceEquals(other, goalEnemy))
            {
                continue;
            }
            if (!other.IsVisible && other.TimeSinceLastKnownUpdated > 5f)
            {
                continue;
            }
            Vector3? known = other.KnownPlaces.LastKnownPosition;
            if (known == null)
            {
                continue;
            }
            Vector3 toOther = known.Value - doorCenter;
            toOther.y = 0f;
            if (toOther.magnitude < 15f && Vector3.Dot(toOther, botSide) > 0f)
            {
                reason = $"otherEnemyOnOurSide({other.EnemyName}, {toOther.magnitude:0}m)";
                return true;
            }
        }
        reason = string.Empty;
        return false;
    }

    private bool TeammateNear(Vector3 target, float radius, out string who)
    {
        who = string.Empty;
        var members = Bot.Squad.Members;
        if (members == null)
        {
            return false;
        }
        foreach (var member in members.Values)
        {
            if (member == null || ReferenceEquals(member, Bot) || member.IsDead)
            {
                continue;
            }
            if (HorizontalDistance(member.Position, target) < radius)
            {
                who = member.name;
                return true;
            }
        }
        return false;
    }

    // ---------------------------------------------------------------- squad roles

    private sealed class RoleAssignment
    {
        public EPlan Role;
        public BotComponent Leader;
        public string EnemyProfileId;
        public DoorDataStruct Door;
        public Vector3 Center;
        public Vector3 BotSide;
        public Vector3 Point;
        public Vector3 Look;
        public float Until;
    }

    private RoleAssignment _pendingRole;

    /// <summary>
    /// Leader side: the nearest teammate who knows the same enemy gets Overwatch (cross angle on the
    /// door from the other edge, 5.5-7m back so a door grenade doesn't get cancelled), the next one
    /// gets Rear Guard (holds where it is and watches away from the door). Assignments expire in 3s if
    /// the teammate's own decision loop doesn't pick them up.
    /// </summary>
    private void AssignSquadRoles(Session leader, DoorGeometry geo)
    {
        var members = Bot.Squad.Members;
        if (members == null || members.Count <= 1)
        {
            TacticDiagnostics.Count("door.role.solo");
            return;
        }
        string enemyId = leader.Enemy.EnemyProfileId;
        List<BotComponent> candidates = FindRoleCandidates(geo, enemyId);
        if (candidates.Count == 0)
        {
            Log($"{Who()} squad roles: no teammate near the door who knows this enemy");
            TacticDiagnostics.Count("door.role.noMateAvailable");
            return;
        }
        candidates.Sort((a, b) => HorizontalDistance(a.Position, geo.Center).CompareTo(HorizontalDistance(b.Position, geo.Center)));
        if (leader.Plan == EPlan.Breach)
        {
            AssignBreachRoles(leader, geo, candidates, enemyId);
            return;
        }

        string overwatchName = "none";
        string rearName = "none";
        int index = 0;
        if (FindOverwatchPoint(geo, out Vector3 overwatch))
        {
            BotComponent mate = candidates[index++];
            mate.DoorTactic.ReceiveRole(
                new RoleAssignment
                {
                    Role = EPlan.Overwatch,
                    Leader = Bot,
                    EnemyProfileId = enemyId,
                    Door = geo.Data,
                    Center = geo.Center,
                    BotSide = geo.BotSide,
                    Point = overwatch,
                    Look = geo.Center + Vector3.up * 1.2f,
                    Until = Time.time + 3f,
                }
            );
            leader.SupportIds.Add(mate.ProfileId);
            overwatchName = mate.name;
            TacticDiagnostics.Count("door.role.overwatchAssigned");
        }
        else
        {
            TacticDiagnostics.Count("door.role.noOverwatchPoint");
        }
        if (index < candidates.Count)
        {
            BotComponent mate = candidates[index];
            Vector3 away = mate.Position - geo.Center;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : geo.BotSide;
            mate.DoorTactic.ReceiveRole(
                new RoleAssignment
                {
                    Role = EPlan.RearGuard,
                    Leader = Bot,
                    EnemyProfileId = enemyId,
                    Door = geo.Data,
                    Center = geo.Center,
                    BotSide = geo.BotSide,
                    Point = mate.Position,
                    Look = mate.Position + away * 10f + Vector3.up * 1.3f,
                    Until = Time.time + 3f,
                }
            );
            leader.SupportIds.Add(mate.ProfileId);
            rearName = mate.name;
            TacticDiagnostics.Count("door.role.rearGuardAssigned");
        }
        Log($"{Who()} squad roles for door {geo.Data.Id}: overwatch={overwatchName} rearGuard={rearName} (candidates={candidates.Count})");
    }

    private List<BotComponent> FindRoleCandidates(DoorGeometry geo, string enemyId)
    {
        var result = new List<BotComponent>();
        var members = Bot.Squad.Members;
        if (members == null)
        {
            return result;
        }
        foreach (var member in members.Values)
        {
            if (member == null || ReferenceEquals(member, Bot) || member.IsDead || member.DoorTactic == null || member.DoorTactic.Active)
            {
                continue;
            }
            Enemy theirs = member.GoalEnemy;
            if (theirs == null || theirs.EnemyProfileId != enemyId || theirs.IsVisible)
            {
                continue;
            }
            Vector3 toMate = member.Position - geo.Center;
            toMate.y = 0f;
            if (toMate.magnitude > Settings.SquadRoleMaxDistance || Vector3.Dot(toMate, geo.BotSide) < 0f)
            {
                continue;
            }
            result.Add(member);
        }
        return result;
    }

    /// <summary>
    /// Squad breach: nearest teammate watches the door (Overwatch), up to two more stack on the frame
    /// (Assault), throw their own grenade when the leader throws, and rush in with the leader after the blast.
    /// </summary>
    private void AssignBreachRoles(Session leader, DoorGeometry geo, List<BotComponent> candidates, string enemyId)
    {
        int index = 0;
        string overwatchName = "none";
        var assaultNames = new List<string>();
        if (FindOverwatchPoint(geo, out Vector3 overwatch))
        {
            BotComponent mate = candidates[index++];
            mate.DoorTactic.ReceiveRole(
                new RoleAssignment
                {
                    Role = EPlan.Overwatch,
                    Leader = Bot,
                    EnemyProfileId = enemyId,
                    Door = geo.Data,
                    Center = geo.Center,
                    BotSide = geo.BotSide,
                    Point = overwatch,
                    Look = geo.Center + Vector3.up * 1.2f,
                    Until = Time.time + 3f,
                }
            );
            leader.SupportIds.Add(mate.ProfileId);
            overwatchName = mate.name;
            TacticDiagnostics.Count("door.role.overwatchAssigned");
        }
        float leaderSide = Mathf.Sign(Vector3.Dot(leader.Stack - geo.Center, geo.Axis));
        if (leaderSide == 0f)
        {
            leaderSide = 1f;
        }
        Vector3[] stackSpots =
        [
            geo.Center + geo.BotSide * STACK_DEPTH - geo.Axis * leaderSide * (geo.HalfWidth + STACK_SIDE_GAP),
            geo.Center + geo.BotSide * (STACK_DEPTH + 0.6f) + geo.Axis * leaderSide * (geo.HalfWidth + STACK_SIDE_GAP + 0.7f),
        ];
        int spot = 0;
        while (index < candidates.Count && spot < stackSpots.Length)
        {
            if (!SampleOnBotSide(stackSpots[spot], geo, out Vector3 point))
            {
                spot++;
                continue;
            }
            BotComponent mate = candidates[index++];
            mate.DoorTactic.ReceiveRole(
                new RoleAssignment
                {
                    Role = EPlan.Assault,
                    Leader = Bot,
                    EnemyProfileId = enemyId,
                    Door = geo.Data,
                    Center = geo.Center,
                    BotSide = geo.BotSide,
                    Point = point,
                    Look = geo.Center - geo.BotSide * 1.5f + Vector3.up * 1.2f,
                    Until = Time.time + 3f,
                }
            );
            leader.SupportIds.Add(mate.ProfileId);
            assaultNames.Add(mate.name);
            TacticDiagnostics.Count("door.role.assaultAssigned");
            spot++;
        }
        Log(
            $"{Who()} BREACH roles for door {geo.Data.Id}: overwatch={overwatchName} assault=[{string.Join(", ", assaultNames)}] (candidates={candidates.Count})"
        );
    }

    private bool SupportsReady(Session leader)
    {
        var members = Bot.Squad.Members;
        if (members == null || leader.SupportIds.Count == 0)
        {
            return true;
        }
        foreach (string id in leader.SupportIds)
        {
            if (!members.TryGetValue(id, out BotComponent mate) || mate == null || mate.IsDead)
            {
                continue;
            }
            var tactic = mate.DoorTactic;
            if (tactic.Plan == EPlan.Assault && tactic.Step != EStep.AssaultReady)
            {
                return false;
            }
        }
        return true;
    }

    private Vector3 PickRushPoint(DoorGeometry geo, Enemy enemy, float lateral)
    {
        Vector3? known = enemy.KnownPlaces.LastKnownPosition;
        Vector3 raw = geo.Center - geo.BotSide * 3f + geo.Axis * lateral;
        if (known != null && HorizontalDistance(known.Value, geo.Center) < MAX_ENEMY_DOOR_DIST && lateral == 0f)
        {
            raw = known.Value;
        }
        return SampleNav(raw, out Vector3 point) ? point : raw;
    }

    /// <summary>
    /// Throws whatever grenade the bot would normally use (frag first, then flash) into the room.
    /// Never when a teammate is on the room side within 6m of the target; teammates stacked on our side
    /// are behind the wall. Returns the fuse time, or 0 when nothing was thrown.
    /// </summary>
    private float ThrowIntoRoom(Session s, Vector3 target, string label)
    {
        var grenades = BotOwner.WeaponManager?.Grenades;
        if (grenades == null || grenades.ThrowindNow || !grenades.HaveGrenade || !grenades.ReadyToThrow)
        {
            TacticDiagnostics.Count($"door.breach.{label}.noGrenade");
            return 0f;
        }
        var members = Bot.Squad.Members;
        if (members != null)
        {
            foreach (var member in members.Values)
            {
                if (member == null || ReferenceEquals(member, Bot) || member.IsDead)
                {
                    continue;
                }
                bool roomSide = Vector3.Dot(member.Position - s.Center, s.BotSide) < 0f;
                if (roomSide && HorizontalDistance(member.Position, target) < 6f)
                {
                    Log($"{Who()} {label} grenade cancelled: teammate {member.name} is inside the room near the target");
                    TacticDiagnostics.Count($"door.breach.{label}.teammateInRoom");
                    return 0f;
                }
            }
        }
        grenades.CheckGrenade();
        ThrowWeap nade = grenades.grenade;
        if (nade == null)
        {
            TacticDiagnostics.Count($"door.breach.{label}.noGrenade");
            return 0f;
        }
        Vector3 from = Bot.Transform.WeaponData.WeaponRoot;
        Vector3 aim = target + Vector3.up * 0.25f;
        AIGreandeAng[] angles = [AIGreandeAng.ang25, AIGreandeAng.ang35, AIGreandeAng.ang15, AIGreandeAng.ang45];
        foreach (AIGreandeAng angle in angles)
        {
            AIGreanageThrowData data = AIGrenadeHelper.CanThrowGrenade2(from, aim, grenades.MaxPower * 0.9f, angle, -1f, 0.66f);
            if (!data.CanThrow)
            {
                continue;
            }
            data.GrenadeType = null;
            grenades.SetThrowParams(nade);
            if (grenades.SetThrowData(data) && grenades.DoThrow())
            {
                TacticDiagnostics.Count($"door.breach.{label}.thrown");
                Log($"{Who()} {label} grenade {nade.ShortName.Localized()} ({nade.ThrowType}, fuse {nade.GetExplDelay:0.0}s) into the room");
                return Mathf.Max(nade.GetExplDelay, 1f);
            }
        }
        TacticDiagnostics.Count($"door.breach.{label}.noArc");
        return 0f;
    }

    private bool FindOverwatchPoint(DoorGeometry geo, out Vector3 result)
    {
        // Opposite edge of the frame from the leader's stack, back far enough for a cross angle and to
        // stay out of the door grenade's teammate radius.
        float side = -Mathf.Sign(Vector3.Dot(Bot.Position - geo.Center, geo.Axis));
        if (side == 0f)
        {
            side = -1f;
        }
        float[] depths = [7f, 6.5f, 6f, 5.5f];
        float[] laterals = [geo.HalfWidth + 1.5f, geo.HalfWidth + 0.8f, 0f];
        foreach (float depth in depths)
        {
            foreach (float lateral in laterals)
            {
                Vector3 raw = geo.Center + geo.BotSide * depth + geo.Axis * side * lateral;
                if (SampleOnBotSide(raw, geo, out Vector3 point) && Bot.Mover.CanGoToPoint(point, out _, true))
                {
                    result = point;
                    return true;
                }
            }
        }
        result = default;
        return false;
    }

    private void ReceiveRole(RoleAssignment role)
    {
        _pendingRole = role;
    }

    /// <summary>
    /// Support side: picked up from ShallUse on this bot's own decision tick.
    /// </summary>
    private bool TryStartSupportRole(Enemy enemy, out string reason)
    {
        RoleAssignment role = _pendingRole;
        reason = string.Empty;
        if (role == null)
        {
            return false;
        }
        _pendingRole = null;
        if (!Settings.Enabled || !Settings.SquadRoles)
        {
            reason = "rolesDisabled";
            return false;
        }
        if (role.Until < Time.time || role.Leader == null || role.Leader.IsDead || role.Leader.DoorTactic.LeadingDoorId != role.Door.Id)
        {
            reason = "roleExpired";
            TacticDiagnostics.Count("door.role.expiredBeforeStart");
            return false;
        }
        if (enemy == null || enemy.EnemyProfileId != role.EnemyProfileId || enemy.IsVisible)
        {
            reason = "roleEnemyMismatch";
            TacticDiagnostics.Count("door.role.enemyMismatch");
            return false;
        }
        var s = new Session
        {
            Plan = role.Role,
            Enemy = enemy,
            Door = role.Door,
            Center = role.Center,
            BotSide = role.BotSide,
            InsidePoint = role.Center - role.BotSide * 1.5f + Vector3.up * 1.2f,
            OverwatchPoint = role.Point,
            HoldLook = role.Look,
            HoldTime = SESSION_MAX_TIME,
            HoldPose = role.Role == EPlan.Overwatch ? 0.6f : 0.8f,
            IsSupport = true,
            Leader = role.Leader,
            StartTime = Time.time,
            FirstStep = role.Role switch
            {
                EPlan.Overwatch => EStep.MoveToOverwatch,
                EPlan.Assault => EStep.MoveToAssault,
                _ => EStep.Hold,
            },
        };
        if (role.Role == EPlan.Assault)
        {
            // Spread the rush: each assault aims at a different spot in the room.
            Vector3 axis = Vector3.Cross(role.BotSide, Vector3.up);
            float lateral = Vector3.Dot(role.Point - role.Center, axis) > 0f ? 1.2f : -1.2f;
            Vector3 raw = role.Center - role.BotSide * 3f + axis * lateral;
            s.RushPoint = SampleNav(raw, out Vector3 rush) ? rush : raw;
        }
        _session = s;
        TacticDiagnostics.Count($"door.role.start.{role.Role}");
        Log(
            $"{Who()} START plan={role.Role} for leader {role.Leader.name} door={role.Door.Id} "
                + $"point={HorizontalDistance(role.Point, role.Center):0.0}m from door"
        );
        SetStep(s.FirstStep, "squadRole");
        reason = $"support:{role.Role}";
        return true;
    }

    /// <summary>
    /// Support bots end shortly after the leader stops working the door (done, dead, or switched).
    /// </summary>
    private bool LeaderStillOnDoor(Session s, float time)
    {
        BotComponent leader = s.Leader;
        if (leader == null || leader.IsDead)
        {
            End("leaderGone");
            return false;
        }
        if (leader.DoorTactic.LeadingDoorId == s.Door.Id)
        {
            s.LeaderGoneTime = -1f;
            return true;
        }
        if (s.LeaderGoneTime < 0f)
        {
            s.LeaderGoneTime = time;
        }
        if (time - s.LeaderGoneTime > 2f)
        {
            End("leaderDone");
            return false;
        }
        return true;
    }

    // ---------------------------------------------------------------- third party

    private int _resumeDoorId = -1;
    private string _resumeEnemyId;
    private string _resumeReason;
    private float _resumeUntil;

    private static bool IsThirdPartyResult(string result)
    {
        return result.StartsWith("underFire")
            || result.StartsWith("otherEnemyOnOurSide")
            || result.StartsWith("closeGunfire")
            || result.StartsWith("goalEnemyChanged");
    }

    /// <summary>
    /// Someone other than the room enemy was heard shooting close by (any side of the door).
    /// </summary>
    private bool CloseThirdPartyGunfire(Session s, out string what)
    {
        what = string.Empty;
        float maxDist = Settings.CloseGunfireDistance;
        if (maxDist <= 0f)
        {
            return false;
        }
        var enemies = Bot.EnemyController.KnownEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy other = enemies[i];
            if (other == null || ReferenceEquals(other, s.Enemy))
            {
                continue;
            }
            var hearing = other.Hearing;
            if (hearing == null || Time.time - hearing.LastHeardSoundTime > 2f)
            {
                continue;
            }
            if (hearing.LastHeardSoundType != SAINSoundType.Shot && hearing.LastHeardSoundType != SAINSoundType.SuppressedShot)
            {
                continue;
            }
            float dist = HorizontalDistance(hearing.LastHeardSoundPosition, Bot.Position);
            if (dist < maxDist)
            {
                what = $"third party {other.EnemyName} shooting {dist:0}m away";
                return true;
            }
        }
        return false;
    }

    // ---------------------------------------------------------------- listening

    /// <summary>
    /// True if the enemy was just heard making a "coming out" noise near this door: sprint, jump,
    /// landing, or the door itself. Plain footsteps (incl. slow walking) don't count - a player who
    /// sneaks up to the door isn't committing yet, and a fake rush sound only makes the bot re-hold.
    /// </summary>
    private bool EnemyComingOut(Session s, out string what)
    {
        what = string.Empty;
        var hearing = s.Enemy?.Hearing;
        if (hearing == null || Time.time - hearing.LastHeardSoundTime > 1.2f)
        {
            return false;
        }
        switch (hearing.LastHeardSoundType)
        {
            case SAINSoundType.Sprint:
            case SAINSoundType.Jump:
            case SAINSoundType.Land:
            case SAINSoundType.Door:
            case SAINSoundType.DoorBreach:
                break;
            default:
                return false;
        }
        if (HorizontalDistance(hearing.LastHeardSoundPosition, s.Center) > 7f)
        {
            return false;
        }
        what = $"{hearing.LastHeardSoundType} {HorizontalDistance(hearing.LastHeardSoundPosition, s.Center):0.0}m from the door";
        return true;
    }

    /// <summary>
    /// Door grenade setup: if the enemy sounds like they're coming out, drop the grenade plan, gun up
    /// and hold the door from beside the frame instead of running.
    /// </summary>
    private bool AbortIfEnemyComingOut(Session s, string when)
    {
        if (!EnemyComingOut(s, out string what))
        {
            return false;
        }
        Log($"{Who()} heard {what} {when} -> no grenade, gun up, hold the door");
        TacticDiagnostics.Count("door.nadeAbortedOnSound");
        s.WantDoorNade = false;
        if (s.HoldTime < 3f)
        {
            // Breach/peek plans have little or no hold time of their own: hold the door a few seconds.
            s.HoldTime = Random.Range(3f, 6f);
        }
        SetStep(EStep.Hold, "heardComingOut");
        return true;
    }

    // ---------------------------------------------------------------- emergency retreat

    private float _emergencyWindowUntil;
    private float _retreatUntil;

    /// <summary>
    /// Checked by EnemyDecisionClass before anything else (right after the ammo check). Right after a
    /// fake grenade/heal the bot's gun isn't up; if the enemy takes the bait and comes at the bot faster
    /// than it can shoot, run back to cover (SAIN Retreat) and re-take the fight from there. If the gun
    /// is already up this returns false and SAIN's normal StandAndShoot takes the shot.
    /// </summary>
    public bool ShallEmergencyRetreat(Enemy enemy, out string reason)
    {
        float time = Time.time;
        if (_pullBackUntil > time && time >= _peekShotUntil)
        {
            if (!_pullBackLogged)
            {
                _pullBackLogged = true;
                TacticDiagnostics.Count("door.postBlast.pullBack");
                Log($"{Who()} post-blast peek shot done -> pulling back");
            }
            reason = "peekShotPullBack";
            return true;
        }
        if (_retreatUntil > time)
        {
            if (!WeaponReady() || time < _retreatUntil - (3f - EMERGENCY_MIN_RETREAT_TIME))
            {
                reason = "emergencyRetreatActive";
                return true;
            }
            _retreatUntil = 0f;
            Log($"{Who()} emergency retreat over: gun ready={WeaponReady()}, back to normal SAIN decisions");
        }
        if (_emergencyWindowUntil < time || enemy == null || WeaponReady())
        {
            reason = string.Empty;
            return false;
        }
        Vector3? known = enemy.KnownPlaces.LastKnownPosition;
        bool close = known != null && HorizontalDistance(known.Value, Bot.Position) < Settings.EmergencyRetreatDistance;
        // Only when the enemy is actually out and visible: a heard rush may be a fake, the tactic's own
        // sound check already cancels the trick and re-holds the door for that.
        bool coming = enemy.IsVisible;
        if (!close || !coming)
        {
            reason = string.Empty;
            return false;
        }
        _emergencyWindowUntil = 0f;
        _retreatUntil = time + 3f;
        Log(
            $"{Who()} EMERGENCY RETREAT: enemy {(enemy.IsVisible ? "visible" : "heard")} at "
                + $"{HorizontalDistance(known.Value, Bot.Position):0.0}m while gun not ready (hands={Player.HandsController?.GetType().Name})"
        );
        TacticDiagnostics.Count("emergencyRetreat.start");
        CancelFakeHeal("emergencyRetreat");
        ForceRestoreWeapon();
        End("emergencyRetreat");
        reason = "emergencyRetreatStart";
        return true;
    }

    private bool WeaponReady()
    {
        var weaponManager = BotOwner.WeaponManager;
        if (weaponManager == null)
        {
            return false;
        }
        if (Player.HandsController is IGrenadeController || Player.HandsController is IMedsController)
        {
            return false;
        }
        return !weaponManager.Selector.IsChanging;
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
        // Door-proximity slowdown (BotPathData) is only lifted for the quick peek hops and for backing
        // away from our own grenade next to the door.
        Bot.Mover.IgnoreDoorSlow =
            step == EStep.PeekOut || step == EStep.PeekBack || step == EStep.MoveToFarHold || step == EStep.BreachRush;
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
        if (_session != null && Bot.Decision.CurrentCombatDecision == ECombatDecision.DoorTactic)
        {
            // SAINLayer restarts the action on ANY decision change (self action, squad decision). Still our
            // decision -> the new DoorTacticAction continues this session; don't cancel tricks or end it.
            TacticDiagnostics.Count("door.actionRestartKept");
            return;
        }
        Bot.Mover.IgnoreDoorSlow = false;
        CancelFakeHeal("interrupted");
        ForceRestoreWeapon();
        if (_session == null)
        {
            return;
        }
        ECombatDecision next = Bot.Decision.CurrentCombatDecision;
        string why = next == ECombatDecision.StandAndShoot
            ? "enemySpotted(StandAndShoot)"
            : BotOwner.Memory.IsUnderFire ? $"underFire(interrupted:{next})" : $"interrupted({next})";
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
        // The tactic walks at 0.45-0.7 and crouches; SAIN's StandAndShoot never resets either, so without this
        // the bot kept slow-walking (and backing off at slow-walk speed) after the tactic ended (first raid test).
        Bot.Mover.SetTargetMoveSpeed(1f);
        Bot.Mover.SetTargetPose(1f);
        CancelFakeHeal("sessionEnd");
        if (_doorClaims.TryGetValue(s.Door.Id, out DoorClaim claim) && claim.ProfileId == Bot.ProfileId)
        {
            _doorClaims.Remove(s.Door.Id);
        }
        string resultKey = result.Contains("(") ? result.Substring(0, result.IndexOf('(')) : result.Split(':')[0];
        TacticDiagnostics.Count($"door.end.{s.Plan}.{resultKey}");
        float time = Time.time;
        if (s.PostBlastPeek && result.StartsWith("enemySpotted"))
        {
            _peekShotUntil = time + Settings.PeekShotTime;
            _pullBackUntil = _peekShotUntil + Settings.PullBackTime;
            _pullBackLogged = false;
            TacticDiagnostics.Count("door.postBlast.peekShot");
            Log($"{Who()} post-blast peek: enemy seen -> shoot for {Settings.PeekShotTime:0.0}s then pull back {Settings.PullBackTime:0.0}s");
        }
        _doorCooldowns[s.Door.Id] = time + DOOR_COOLDOWN_AFTER_SESSION;
        _nextAllowedTime = time + GLOBAL_COOLDOWN;
        if (!s.IsSupport && Settings.ResumeAfterThirdParty && IsThirdPartyResult(result) && s.Enemy != null)
        {
            // Come back to this door once the third party is dealt with: short cooldown, no re-roll.
            _doorCooldowns[s.Door.Id] = time + 5f;
            _nextAllowedTime = time + 3f;
            _resumeDoorId = s.Door.Id;
            _resumeEnemyId = s.Enemy.EnemyProfileId;
            _resumeUntil = time + Settings.ResumeWindow;
            _resumeReason = result;
            TacticDiagnostics.Count("door.resumeArmed");
            Log($"{Who()} door {s.Door.Id} dropped for a third party ({result}) - will resume within {Settings.ResumeWindow:0}s if the room enemy is still there");
        }
        Log(
            $"{Who()} END plan={s.Plan} lastStep={s.Step} result={result} duration={time - s.StartTime:0.0}s "
                + $"jumped={s.Jumped} fakeNade={s.FakeNadeDrawn} fakeHeal={s.FakeHealStarted} nadeThrown={s.NadeThrown} enemyVisibleNow={s.Enemy?.IsVisible}"
        );
    }

    public override void Init()
    {
        var controller = BotManagerComponent.Instance?.GrenadeController;
        if (controller != null)
        {
            controller.OnGrenadeThrown += OnAnyGrenadeThrown;
            _subscribedController = controller;
        }
        base.Init();
    }

    public override void Dispose()
    {
        if (_session != null)
        {
            End("disposed");
        }
        if (_subscribedController != null)
        {
            _subscribedController.OnGrenadeThrown -= OnAnyGrenadeThrown;
            _subscribedController = null;
        }
        base.Dispose();
    }

    // ---------------------------------------------------------------- own door grenade safety

    // First raid test: a bot threw its door grenade, then (session over) SAIN walked it back through that
    // door, it shot the player and died to its own grenade. SAIN's grenade avoidance never includes the
    // thrower (GrenadeController only notifies OTHER players), so the door-grenade (Trap) nades are tracked
    // here and (1) no bot opens a door next to one, (2) a bot that ends up next to one retreats.
    private const float OWN_NADE_DANGER_RADIUS = 7f;
    private const float OWN_NADE_DOOR_RADIUS = 6f;
    private const float OWN_NADE_MAX_AGE = 15f;

    private static readonly List<(Grenade grenade, float time)> _liveTacticNades = new();
    private GrenadeController _subscribedController;
    private float _expectOwnNadeUntil;
    private float _nextOwnNadeLogTime;

    private void OnAnyGrenadeThrown(Grenade grenade, Vector3 dangerPoint, string profileId)
    {
        if (grenade == null || profileId != Bot.ProfileId || Time.time > _expectOwnNadeUntil)
        {
            return;
        }
        _expectOwnNadeUntil = 0f;
        _liveTacticNades.Add((grenade, Time.time));
        if (_session != null)
        {
            _session.OwnNade = grenade;
            _session.OwnNadeTracked = true;
        }
        TacticDiagnostics.Count("door.ownNadeTracked");
        Log($"{Who()} own door grenade released, tracking it (no bot opens a door within {OWN_NADE_DOOR_RADIUS:0}m of it, "
            + $"bots within {OWN_NADE_DANGER_RADIUS:0}m in its line of sight retreat)");
    }

    private static void CleanTacticNades()
    {
        float time = Time.time;
        _liveTacticNades.RemoveAll(x => x.grenade == null || time - x.time > OWN_NADE_MAX_AGE);
    }

    /// <summary>
    /// A live door-trap grenade (any bot's) within radius of the point.
    /// </summary>
    public static bool LiveTacticNadeNear(Vector3 point, float radius, out Vector3 nadePos)
    {
        CleanTacticNades();
        foreach (var entry in _liveTacticNades)
        {
            Vector3 pos = entry.grenade.transform.position;
            if ((pos - point).sqrMagnitude < radius * radius)
            {
                nadePos = pos;
                return true;
            }
        }
        nadePos = default;
        return false;
    }

    /// <summary>
    /// Called from EnemyDecisionClass right after the ammo check (before shooting): the bot is close to a
    /// live door-trap grenade with nothing in between -> Retreat. Skipped while our own session is walking
    /// away from it or holding the far spot.
    /// </summary>
    public bool ShallAvoidTacticGrenade(out string reason)
    {
        reason = string.Empty;
        if (_liveTacticNades.Count == 0)
        {
            return false;
        }
        if (_session != null && (_session.Step == EStep.ThrowNade || _session.Step == EStep.MoveToFarHold))
        {
            return false;
        }
        Vector3 chest = Bot.Position + Vector3.up * 1.2f;
        if (!LiveTacticNadeNear(chest, OWN_NADE_DANGER_RADIUS, out Vector3 nadePos))
        {
            return false;
        }
        if (Physics.Linecast(nadePos + Vector3.up * 0.3f, chest, LayersMaskController.HighPolyWithTerrainMask))
        {
            return false;
        }
        if (_session != null)
        {
            End("ownGrenadeTooClose");
        }
        reason = "tacticGrenadeNear";
        if (_nextOwnNadeLogTime < Time.time)
        {
            _nextOwnNadeLogTime = Time.time + 2f;
            TacticDiagnostics.Count("door.ownNadeRetreat");
            Log($"{Who()} live door grenade {(nadePos - chest).magnitude:0.0}m away in line of sight -> retreat instead of fighting here");
        }
        return true;
    }

    // ---------------------------------------------------------------- settings / logging

    /// <summary>
    /// F6 editor: Global Settings > General > Door Tactics (zzap). Read live so edits apply immediately.
    /// </summary>
    private static DoorTacticSettings Settings
    {
        get { return GlobalSettingsClass.Instance.General.DoorTactics; }
    }

    private string Who()
    {
        return $"[DoorTactic] [{Bot.name}] [{Bot.Info.Personality}]";
    }

    private static void Log(string message)
    {
        if (Settings.DiagnosticLogs)
        {
            Logger.LogWarning(message);
        }
    }

    private void LogVerbose(string reason)
    {
        if (!Settings.VerboseLogs)
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
