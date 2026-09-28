using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.Ballistics;
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
///   Peek   - GigaChad/Chad: stack beside the doorway (a shut door is opened from the side), jump peek or
///            run-by peek, optional step peeks, then hold the doorway for a few seconds.
///   Trap   - GigaChad/SnappingTurtle: stack beside the door and hold it (the door is left as it is).
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
    }

    public enum EStep
    {
        None,
        MoveToStack,
        MoveToOverwatch,
        OpenDoorFromSide,
        PeekOut,
        PeekBack,
        FakeNadeDraw,
        FakeNadeHolster,
        FakeHealStart,
        FakeHealCancel,
        Hold,
        StepPeekOut,
        StepPeekBack,
        RunByAcross,
        RunByTurn,
        RunByBack,
    }

    private const float MAX_BOT_DOOR_DIST = 8f;
    private const float MAX_ENEMY_DOOR_DIST = 12f;
    private const float MIN_ENEMY_DEPTH = 0.6f;
    private const float MIN_BOT_DEPTH = 0.3f;
    private const float MAX_TIME_SINCE_KNOWN = 25f;
    private const float STACK_DEPTH = 0.9f;
    private const float STACK_SIDE_GAP = 0.7f;
    private const float PEEK_DEPTH = 1.25f;
    private const float PEEK_LOOK_TIME = 0.15f;
    private const float EMERGENCY_WINDOW_AFTER_FAKE = 2.5f;
    // Longest the fake grenade may stay out waiting for the draw to land; the heal/stim hard caps.
    private const float FAKE_NADE_SHOW_TIME = 1.2f;
    private const float FAKE_HEAL_SHOW_TIME = 0.4f;
    private const float FAKE_STIM_SHOW_TIME = 0.3f;
    private const float FAKE_MAX_DOOR_DIST = 2.5f;
    private const float EMERGENCY_MIN_RETREAT_TIME = 1.5f;
    private const float ARRIVE_DIST = 0.6f;
    private const float MOVE_STEP_TIMEOUT = 12f;
    private const float SESSION_MAX_TIME = 150f;
    private const float DOOR_COOLDOWN_AFTER_SESSION = 60f;
    private const float DOOR_COOLDOWN_AFTER_ROLL_FAIL = 25f;
    private const float GLOBAL_COOLDOWN = 8f;
    private const float FAIL_COOLDOWN = 30f;
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
        if (OtherEnemyActive(enemy, out string otherEnemy))
        {
            // Also stops the third-party resume from pulling the bot back to the door while that enemy is around.
            reason = $"otherEnemyActive({otherEnemy})";
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
                + $"fakeNade={session.WantFakeNade} fakeHeal={session.WantFakeHeal} hold={session.HoldTime:0}s"
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

            ClosedSegment(data.Link, out Vector3 closedA, out Vector3 closedB);
            Vector3 center = (closedA + closedB) * 0.5f;
            Vector3 axis = closedB - closedA;
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
                    // Not where the leaf swings to: opening the door from there shoves the leaf into the bot's face
                    // (it then walked into it, jittered and spun - field report + screenshot).
                    OpenSegment(geo.Data.Link, out Vector3 openA, out Vector3 openB);
                    if (DistanceToSegmentFlat(result, openA, openB) < 0.8f)
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

    /// <summary>
    /// The doorway (closed leaf) segment. Vanilla links: Close1 -> Close2_Normal. DrakiaXYZ-Waypoints adds its own
    /// links for locked/breachable doors ("DoorLink_Custom_N") with the fields swapped: Close2_Normal is the OPEN
    /// leaf tip and Open2 the SHUT one (Waypoints DoorLinkPatch). Using Close* there gave a doorway rotated by the
    /// opening angle.
    /// </summary>
    private static void ClosedSegment(NavMeshDoorLink link, out Vector3 a, out Vector3 b)
    {
        if (IsWaypointsLink(link))
        {
            a = link.Open1;
            b = link.Open2;
            return;
        }
        a = link.Close1;
        b = link.Close2_Normal;
    }

    private static void OpenSegment(NavMeshDoorLink link, out Vector3 a, out Vector3 b)
    {
        if (IsWaypointsLink(link))
        {
            a = link.Close1;
            b = link.Close2_Normal;
            return;
        }
        a = link.Open1;
        b = link.Open2;
    }

    private static bool IsWaypointsLink(NavMeshDoorLink link)
    {
        return link != null && link.gameObject.name.StartsWith("DoorLink_Custom_");
    }

    private static float DistanceToSegmentFlat(Vector3 p, Vector3 a, Vector3 b)
    {
        p.y = 0f;
        a.y = 0f;
        b.y = 0f;
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude < 0.0001f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
        return (a + ab * t - p).magnitude;
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
        public bool WantFakeNade;
        public bool WantFakeHeal;
        public bool FakeHealStarted;
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
        public int StepPeeksLeft;
        public int PeekStyle; // 0 = undecided, 1 = jump peek, 2 = run-by
        public Vector3 RunByFar;
        public Vector3 StepPoint;
        public bool StepCrouch;
        public float StepHoldTime;
        public float ProgressCheckTime;
        public Vector3 ProgressCheckPos;
        public bool PeekPointOk;
        public EDoorState DoorStateAtHoldStart;
        public Vector3? LookTarget;
        public Vector3? HoldLook;
        public bool IsSupport;
        public BotComponent Leader;
        public Vector3 OverwatchPoint;
        public float LeaderGoneTime = -1f;
        public readonly HashSet<string> SupportIds = new();
    }

    private Session BuildSession(EPersonality personality, Enemy enemy, DoorGeometry geo, out string reason)
    {
        bool testMode = Settings.TestModeAllPmcGigaChad && Bot.Info.Profile.IsPMC;
        bool doorOpen = geo.Data.Door.DoorState == EDoorState.Open;
        var grenades = BotOwner.WeaponManager?.Grenades;
        bool haveNade = grenades != null && grenades.HaveGrenade;

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

        switch (personality)
        {
            case EPersonality.GigaChad:
            case EPersonality.Chad:
                // Shut door: opened first from the stack point beside the frame, out of the room's line of sight.
                bool peekAllowed = Settings.JumpPeek && peekPointOk;
                bool trapAllowed = personality == EPersonality.GigaChad && Settings.RoomTrap;
                if (peekAllowed && (!trapAllowed || Random.value < Settings.GigaChadPeekChance / 100f))
                {
                    s.Plan = EPlan.Peek;
                    s.FirstStep = EStep.MoveToStack;
                    s.NeedsOpenForPeek = !doorOpen;
                    float fakeChance = testMode ? 1f : (personality == EPersonality.GigaChad ? Settings.GigaChadFakeTrickChance : Settings.ChadFakeTrickChance) / 100f;
                    s.WantFakeNade = Settings.FakeGrenade && haveNade && Random.value < fakeChance;
                    s.WantFakeHeal = !s.WantFakeNade && CanFakeHeal() && Random.value < fakeChance;
                    s.HoldTime = Random.Range(3f, 6f);
                    s.HoldPose = 0.8f;
                }
                else if (trapAllowed)
                {
                    BuildTrap(s);
                    s.HoldTime = Random.Range(20f, 40f);
                    s.HoldPose = 0.7f;
                    s.WantFakeHeal = CanFakeHeal() && Random.value < Settings.GigaChadTrapFakeHealChance / 100f;
                    // Trap holders fake too: grenade draw sound right at the frame, gun straight back up.
                    s.WantFakeNade = !s.WantFakeHeal && Settings.FakeGrenade && haveNade && Random.value < (testMode ? 1f : Settings.GigaChadFakeTrickChance / 100f);
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
                BuildTrap(s);
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

    /// <summary>
    /// Hold the door from beside the frame. The door is left as it is: closing it and then running off with a
    /// door grenade was removed (field test: bots "closed the door and ran far away" for no visible reason).
    /// </summary>
    private static void BuildTrap(Session s)
    {
        s.Plan = EPlan.Trap;
        s.FirstStep = EStep.MoveToStack;
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

        if (OtherEnemyActive(s.Enemy, out string other))
        {
            Log($"{Who()} abort at step {s.Step}: {other}");
            TacticDiagnostics.Count("door.abort.otherEnemyActive");
            End($"otherEnemyActive({other})");
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
                if (MoveStep(s, s.Stack, false, stepTime))
                {
                    if (s.Plan == EPlan.Peek)
                    {
                        SetStep(s.NeedsOpenForPeek ? EStep.OpenDoorFromSide : PeekEntry(s), "atStack");
                    }
                    else
                    {
                        SetStep(FakeOrHold(s), "atStack");
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
                // BSG never fires the completion callback for bot door animations; the door sits in Interacting
                // until SAIN's DoorHandler watchdog finalizes it (~3s). Giving up at 1.1s aborted 6 of 14 peeks.
                EDoorState opening = s.Door.Door != null ? s.Door.Door.DoorState : EDoorState.None;
                if (stepTime > 1.1f && (opening == EDoorState.Open || stepTime > 4.5f))
                {
                    EDoorState state = opening;
                    if (state != EDoorState.Open)
                    {
                        Log($"{Who()} door {s.Door.Id} still {state} after opening, giving up");
                        End("doorDidNotOpen");
                        break;
                    }
                    SetStep(PeekEntry(s), "doorOpened");
                }
                break;

            case EStep.RunByAcross:
                // Run-by (user suggestion): sprint along the corridor straight past the open doorway, "glancing" in,
                // to the far side of the frame...
                Bot.Mover.IgnoreDoorSlow = true;
                s.LookTarget = s.InsidePoint;
                if (MoveStep(s, s.RunByFar, true, stepTime))
                {
                    SetStep(EStep.RunByTurn, "pastDoorway");
                }
                break;

            case EStep.RunByTurn:
                // ...turn around (a beat, gun toward the room)...
                s.LookTarget = s.InsidePoint;
                Bot.Mover.Stop();
                if (stepTime > 0.35f)
                {
                    SetStep(EStep.RunByBack, "turned");
                }
                break;

            case EStep.RunByBack:
                // ...and run past it once more back to the stack, then settle in (step peeks / trick / hold).
                Bot.Mover.IgnoreDoorSlow = true;
                s.LookTarget = s.InsidePoint;
                if (MoveStep(s, s.Stack, true, stepTime))
                {
                    TacticDiagnostics.Count("door.runBy.done");
                    Bot.Mover.IgnoreDoorSlow = false;
                    if (s.StepPeeksLeft == 0 && Settings.StepPeek && TryPrepareStepPeeks(s))
                    {
                        SetStep(EStep.StepPeekOut, "runByDoneStepPeeks");
                        break;
                    }
                    SetStep(FakeOrHold(s), "runByDone");
                }
                break;

            case EStep.PeekOut:
                // Corridor-side bunny hop: sprint-jump out in front of the doorway (never into the room),
                // head snapped sideways into the room the whole time.
                Bot.Mover.IgnoreDoorSlow = true;
                s.LookTarget = s.InsidePoint;
                if (!s.Jumped)
                {
                    s.Jumped = s.PeekStyle != 3 && JumpSafe(s.PeekPoint) && Bot.Mover.TryJump();
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
                    s.JumpedBack = s.PeekStyle != 3 && JumpSafe(s.Stack) && Bot.Mover.TryJump();
                    if (s.JumpedBack)
                    {
                        TacticDiagnostics.Count("door.jumpBack");
                        Log($"{Who()} jump peek BACK at door {s.Door.Id}");
                    }
                }
                if (MoveStep(s, s.Stack, true, stepTime))
                {
                    Bot.Mover.IgnoreDoorSlow = false;
                    if (s.StepPeeksLeft == 0 && Settings.StepPeek && TryPrepareStepPeeks(s))
                    {
                        SetStep(EStep.StepPeekOut, "backAtStackStepPeeks");
                        break;
                    }
                    SetStep(FakeOrHold(s), "backAtStack");
                }
                break;

            case EStep.StepPeekOut:
                // Reference clip 4 (7-12s): at the frame, short A/D taps into the doorway line and back, gun on the room,
                // alternating stance and which corner of the room is checked.
                Bot.Mover.IgnoreDoorSlow = true;
                Bot.Mover.SetTargetPose(s.StepCrouch ? 0.6f : 1f);
                s.LookTarget = s.InsidePoint + geoAxisOffset(s);
                if (MoveStep(s, s.StepPoint, false, stepTime) || stepTime > 0.45f)
                {
                    if (stepTime > s.StepHoldTime)
                    {
                        SetStep(EStep.StepPeekBack, "stepOut");
                    }
                }
                break;

            case EStep.StepPeekBack:
                Bot.Mover.IgnoreDoorSlow = true;
                s.LookTarget = s.InsidePoint;
                if (MoveStep(s, s.Stack, false, stepTime) || stepTime > 0.45f)
                {
                    s.StepPeeksLeft--;
                    TacticDiagnostics.Count("door.stepPeek");
                    if (s.StepPeeksLeft > 0)
                    {
                        s.StepCrouch = !s.StepCrouch;
                        s.StepHoldTime = Random.Range(0.15f, 0.3f);
                        SetStep(EStep.StepPeekOut, "stepAgain");
                    }
                    else
                    {
                        Bot.Mover.IgnoreDoorSlow = false;
                        Bot.Mover.SetTargetPose(1f);
                        SetStep(FakeOrHold(s), "stepPeeksDone");
                    }
                }
                break;

            case EStep.FakeNadeDraw:
                // 2026-09-27 field test: the grenade stayed out too long (draw -> 0.7s show -> put away every 0.5s)
                // and looked like a real throw being decided. Now: the draw sound is the whole trick - the grenade
                // goes away the moment the draw lands (callback), and running footsteps cancel it at any point.
                s.LookTarget = s.InsidePoint;
                if (EnemyComingOut(s, out string heardNade))
                {
                    Log($"{Who()} heard {heardNade} {(s.FakeNadeDrawn ? "during" : "before")} fake grenade -> cancel, gun up on the door");
                    TacticDiagnostics.Count("trick.cancelledOnSound");
                    SetStep(s.FakeNadeDrawn ? EStep.FakeNadeHolster : EStep.Hold, "heardComingOut");
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
                if (!_fakeDrawPending || stepTime > FAKE_NADE_SHOW_TIME)
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
                if (EnemyComingOut(s, out string heardHeal))
                {
                    Log($"{Who()} heard {heardHeal} {(s.FakeHealStarted ? "during" : "before")} fake heal/stim -> cancel, gun up on the door");
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
                // Field test: the heal sometimes went through ("just healed"). Cancel the moment the item is in
                // hands (the sound has played by then), hard cap as a fallback.
                bool medsOut = Player.HandsController is IMedsController && stepTime > 0.1f;
                if (medsOut || stepTime > (_fakeStimRunning ? FAKE_STIM_SHOW_TIME : FAKE_HEAL_SHOW_TIME))
                {
                    SetStep(EStep.FakeHealCancel, medsOut ? "medsInHands" : _fakeStimRunning ? "fakeStimShown" : "fakeHealShown");
                }
                break;

            case EStep.FakeHealCancel:
                s.LookTarget = s.InsidePoint;
                CancelFakeHeal("done");
                SetStep(EStep.Hold, "fakeHealCancelled");
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
    /// First peek move after reaching the stack (and opening): jump peek, or the run-by when the jump isn't safe here
    /// (low ceiling / step or stairs between stack and peek point) or on the F6 run-by roll. Decided once per session.
    /// </summary>
    private EStep PeekEntry(Session s)
    {
        if (s.PeekStyle == 0)
        {
            OpenSegment(s.Door.Link, out Vector3 leafA, out Vector3 leafB);
            bool leafClear = DistanceToSegmentFlat(s.PeekPoint, leafA, leafB) >= 0.5f;
            if (!leafClear)
            {
                TacticDiagnostics.Count("door.jumpLeafTooClose");
            }
            bool laneClear = JumpLaneClear(s.Stack, s.PeekPoint);
            if (!laneClear)
            {
                TacticDiagnostics.Count("door.jumpFrameInTheWay");
            }
            bool jumpOk = leafClear && laneClear && JumpSafe(s.PeekPoint);
            bool runByOk = FindRunByPoint(s);
            bool rollRunBy = Random.value * 100f < Settings.RunByChance;
            // 1 = jump peek, 2 = run-by, 3 = plain step out (neither is safe here).
            s.PeekStyle = runByOk && (!jumpOk || rollRunBy) ? 2 : jumpOk ? 1 : 3;
            if (!jumpOk)
            {
                TacticDiagnostics.Count("door.jumpUnsafe");
            }
            Log($"{Who()} peek style at door {s.Door.Id}: {(s.PeekStyle == 2 ? "RUN-BY" : s.PeekStyle == 1 ? "jump peek" : "plain step peek")} (jumpSafe={jumpOk}, runByPoint={runByOk})");
            if (s.PeekStyle == 2)
            {
                TacticDiagnostics.Count("door.runBy.start");
            }
        }
        return s.PeekStyle == 2 ? EStep.RunByAcross : EStep.PeekOut;
    }

    /// <summary>
    /// A jumping body (0.35m radius, chest height) from the stack to the peek point must not clip the door frame -
    /// the field test showed the hop catching the frame corner.
    /// </summary>
    private static bool JumpLaneClear(Vector3 from, Vector3 to)
    {
        Vector3 a = from + Vector3.up * 1.1f;
        Vector3 b = to + Vector3.up * 1.1f;
        Vector3 dir = b - a;
        float len = dir.magnitude;
        if (len < 0.05f)
        {
            return true;
        }
        return !Physics.SphereCast(a, 0.35f, dir / len, out _, len, LayersMaskController.HighPolyWithTerrainMask);
    }

    /// <summary>
    /// Headroom for a jump (a low ceiling cuts it off / bounces the bot) and no step between here and the landing spot
    /// (entrance stairs: the landing snags and the bot gets stuck).
    /// </summary>
    private bool JumpSafe(Vector3 landing)
    {
        Vector3 head = Bot.Position + Vector3.up * 1.7f;
        if (Physics.Raycast(head, Vector3.up, 0.6f, LayersMaskController.HighPolyWithTerrainMask))
        {
            return false;
        }
        if (Physics.Raycast(landing + Vector3.up * 1.7f, Vector3.up, 0.6f, LayersMaskController.HighPolyWithTerrainMask))
        {
            return false;
        }
        return Mathf.Abs(landing.y - Bot.Position.y) <= 0.25f;
    }

    /// <summary>
    /// Mirror of the stack on the other side of the doorway (corridor side), so the run crosses the whole opening.
    /// </summary>
    private bool FindRunByPoint(Session s)
    {
        Vector3 axis = Vector3.Cross(s.BotSide, Vector3.up).normalized;
        Vector3 center = s.Center;
        float stackSide = Mathf.Sign(Vector3.Dot(s.Stack - center, axis));
        if (stackSide == 0f)
        {
            stackSide = 1f;
        }
        float halfWidth = Mathf.Max(0.4f, Mathf.Abs(Vector3.Dot(s.Stack - center, axis)) - 0.4f);
        foreach (float extra in new[] { 1.4f, 1.0f, 1.8f })
        {
            Vector3 raw = center + s.BotSide * 1.1f - axis * stackSide * (halfWidth + extra);
            if (!NavMesh.SamplePosition(raw, out NavMeshHit hit, 0.5f, -1))
            {
                continue;
            }
            if (Vector3.Dot(hit.position - center, s.BotSide) < 0.3f)
            {
                continue;
            }
            if (!RunByLaneClear(s, s.Stack, hit.position))
            {
                continue;
            }
            if (Bot.Mover.CanGoToPoint(hit.position, out NavMeshPath path, true) && PathLength(path) < 9f)
            {
                s.RunByFar = hit.position;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The straight run from the stack across the doorway must not hit the open leaf (a door that opens into the
    /// corridor sticks out across it - the bot would run face-first into it, like the "stuck at the door" clip) or
    /// anything else on the navmesh (NavMesh.Raycast: the leaf's carve may not be in yet).
    /// </summary>
    private bool RunByLaneClear(Session s, Vector3 from, Vector3 to)
    {
        if (NavMesh.Raycast(from, to, out _, -1))
        {
            TacticDiagnostics.Count("door.runBy.laneBlocked");
            return false;
        }
        OpenSegment(s.Door.Link, out Vector3 openA, out Vector3 openB);
        float length = HorizontalDistance(from, to);
        int samples = Mathf.Max(2, Mathf.CeilToInt(length / 0.25f));
        for (int i = 0; i <= samples; i++)
        {
            Vector3 p = Vector3.Lerp(from, to, i / (float)samples);
            if (DistanceToSegmentFlat(p, openA, openB) < 0.55f)
            {
                TacticDiagnostics.Count("door.runBy.leafInTheWay");
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 2-3 short steps from the stack toward the doorway line (40-55% of the way to the peek point), 0.15-0.3s out each.
    /// </summary>
    private bool TryPrepareStepPeeks(Session s)
    {
        if (!s.PeekPointOk || s.Door.Door == null || s.Door.Door.DoorState != EDoorState.Open)
        {
            return false;
        }
        Vector3 toward = s.PeekPoint - s.Stack;
        foreach (float f in new[] { 0.5f, 0.4f, 0.55f })
        {
            if (NavMesh.SamplePosition(s.Stack + toward * f, out NavMeshHit hit, 0.4f, -1))
            {
                s.StepPoint = hit.position;
                s.StepPeeksLeft = Random.Range(2, 4);
                s.StepCrouch = Random.value < 0.5f;
                s.StepHoldTime = Random.Range(0.15f, 0.3f);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Alternate which side of the room the step peeks check (left/right of the room point along the door axis).
    /// </summary>
    private static Vector3 geoAxisOffset(Session s)
    {
        Vector3 side = Vector3.Cross(Vector3.up, s.BotSide).normalized;
        return side * (s.StepPeeksLeft % 2 == 0 ? 1.5f : -1.5f);
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
        // Not getting anywhere (wedged behind a door leaf / against a wall): give up after 2.5s instead of pushing
        // into it for the full 12s (2026-09-27 video: 2.5m to the stack took 12s and ended in moveTimeout).
        if (stepTime < 0.1f || s.ProgressCheckTime <= 0f)
        {
            s.ProgressCheckTime = Time.time + 2.5f;
            s.ProgressCheckPos = Bot.Position;
        }
        else if (Time.time > s.ProgressCheckTime)
        {
            if (HorizontalDistance(Bot.Position, s.ProgressCheckPos) < 0.4f)
            {
                End($"stuck:{s.Step}");
                return false;
            }
            s.ProgressCheckTime = Time.time + 2.5f;
            s.ProgressCheckPos = Bot.Position;
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
            // Approach at 0.75: at 0.45 a bot needed ~13s for an 8m approach and timed out at the door (second raid
            // test: 5 trap / 2 peek moveTimeouts, one bot restarting on door after door while walking slowly).
            Bot.Mover.SetTargetMoveSpeed(0.75f);
        }
        return false;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return (a - b).magnitude;
    }

    /// <summary>
    /// Decides at the frame whether the planned fake is worth doing right now (2026-09-27: fakes fired "out of the
    /// blue" with nobody around to bait, so it looked like a random heal/throw). Only when the room enemy was heard
    /// or located within the last 8s, is within 10m of the door, nobody is running at us, and the bot is at the frame.
    /// </summary>
    private EStep FakeOrHold(Session s)
    {
        if (!s.WantFakeNade && !s.WantFakeHeal)
        {
            return EStep.Hold;
        }
        string what = s.WantFakeNade ? "fake grenade" : "fake heal/stim";
        string why = null;
        Enemy enemy = s.Enemy;
        Vector3? known = enemy?.KnownPlaces.LastKnownPosition;
        if (enemy == null || known == null || enemy.TimeSinceLastKnownUpdated > 8f)
        {
            why = "enemyInfoOld";
        }
        else if (HorizontalDistance(known.Value, s.Center) > 10f)
        {
            why = "enemyNotNearDoor";
        }
        else if (EnemyComingOut(s, out string heard))
        {
            why = $"heard({heard})";
        }
        else if (!CloseEnoughForFake(s, what))
        {
            why = "tooFarFromDoor";
        }
        else if (!SafeForFake(s, out string unsafeWhy))
        {
            why = unsafeWhy;
        }
        if (why != null)
        {
            Log($"{Who()} {what} skipped: {why}");
            TacticDiagnostics.Count($"trick.skipped.{why.Split('(')[0]}");
            s.WantFakeNade = false;
            s.WantFakeHeal = false;
            return EStep.Hold;
        }
        return s.WantFakeNade ? EStep.FakeNadeDraw : EStep.FakeHealStart;
    }

    /// <summary>
    /// 2026-09-28 field report: fakes were tried outdoors "because nobody can see me right now" with the whole body in the
    /// open, and the bot died mid-fake. A fake is only safe from real cover: (1) no known enemy has a line of sight to the
    /// bot's chest from where it was last known, (2) indoors, or boxed in (5 of 8 directions blocked within 4m at chest
    /// height - a single wall at your back outdoors doesn't count), (3) not being shot at.
    /// </summary>
    private bool SafeForFake(Session s, out string why)
    {
        if (BotOwner.Memory.IsUnderFire)
        {
            why = "underFire";
            return false;
        }
        Vector3 chest = Bot.Position + Vector3.up * 1.3f;
        var enemies = Bot.EnemyController.KnownEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (e == null)
            {
                continue;
            }
            if (e.IsVisible)
            {
                why = $"exposed({e.EnemyName} visible)";
                return false;
            }
            Vector3? pos = e.KnownPlaces.LastKnownPosition;
            if (pos == null || e.TimeSinceLastKnownUpdated > 30f)
            {
                continue;
            }
            if (!Physics.Linecast(pos.Value + Vector3.up * 1.5f, chest, LayersMaskController.HighPolyWithTerrainMask))
            {
                why = $"exposed(line of sight to {e.EnemyName})";
                return false;
            }
        }
        if (!Bot.Memory.Location.IsIndoors)
        {
            int blocked = 0;
            for (int k = 0; k < 8; k++)
            {
                Vector3 dir = Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward;
                if (Physics.Raycast(chest, dir, 4f, LayersMaskController.HighPolyWithTerrainMask))
                {
                    blocked++;
                }
            }
            if (blocked < 5)
            {
                why = $"exposed(outdoors, open {8 - blocked}/8 sides)";
                return false;
            }
        }
        why = null;
        return true;
    }

    /// <summary>
    /// Fakes only work right at the frame: the point is that the enemy bolts out at the sound and runs into
    /// the bot's gun a moment later. From further back it's just a noise and the gun isn't up in time.
    /// </summary>
    private bool CloseEnoughForFake(Session s, string what)
    {
        float dist = HorizontalDistance(Bot.Position, s.Center);
        if (dist <= FAKE_MAX_DOOR_DIST)
        {
            return true;
        }
        Log($"{Who()} {what} skipped: {dist:0.0}m from the door (max {FAKE_MAX_DOOR_DIST:0.0}m)");
        TacticDiagnostics.Count("trick.tooFarFromDoor");
        return false;
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
        _fakeDrawPending = true;
        _fakeDrawPendingUntil = Time.time + 2.5f;
        _lastFakeNadeTime = Time.time;
        Callback<IGrenadeController> callback = result =>
        {
            _fakeDrawPending = false;
            Log($"{Who()} fake grenade drawn: {(result.Value != null ? "in hands" : "FAILED")}");
            if (!Alive())
            {
                // Killed during the draw: never change the hands of a corpse (2026-09-28: standing corpse right when a
                // fake grenade was being drawn - a weapon swap landing on a dead player can stop the ragdoll).
                TacticDiagnostics.Count("fakeNade.drawLandedDead");
                return;
            }
            if (result.Value != null)
            {
                // The draw sound has played - that was the trick. Put it straight away, whatever step we're in
                // (or if the tactic already ended), so a live grenade never sits in hand.
                bool ok = BotOwner.WeaponManager?.Selector?.TakePrevWeapon() == true;
                if (ok)
                {
                    _putAwayIssuedTime = Time.time;
                }
                Log($"{Who()} fake grenade drawn -> put away at once: TakePrevWeapon={ok}");
                TacticDiagnostics.Count(ok ? "fakeNade.putAwayOnLand" : "fakeNade.putAwayOnLandRefused");
            }
            TacticDiagnostics.Count(result.Value != null ? "fakeNade.drawn" : "fakeNade.drawFailed");
        };
        Player.SetInHands(nade, callback);
        _emergencyWindowUntil = Time.time + FAKE_NADE_SHOW_TIME + EMERGENCY_WINDOW_AFTER_FAKE;
        return true;
    }

    /// <summary>
    /// Puts the weapon back if a grenade is in hands and no real throw is running. Returns true
    /// once the hands no longer hold a grenade.
    /// </summary>
    /// <summary>The bot's player is alive (hands/weapon changes must never be issued to a corpse).</summary>
    private bool Alive()
    {
        return Player != null && Player.HealthController?.IsAlive == true;
    }

    private bool RestoreWeaponIfHoldingGrenade()
    {
        if (!Alive())
        {
            return true;
        }
        // Field log: the holster step ran while the draw animation was still in progress (hands not a grenade yet),
        // "succeeded" at once, then the draw finished and the bot stood 5s with a live grenade in hand - and SAIN's
        // normal throw logic threw it at the player who rushed in. Wait until the draw has landed (callback) first.
        if (_fakeDrawPending && Time.time < _fakeDrawPendingUntil)
        {
            return false;
        }
        if (Player.HandsController is not IGrenadeController)
        {
            return true;
        }
        var weaponManager = BotOwner.WeaponManager;
        if (weaponManager == null || weaponManager.Grenades.ThrowindNow || weaponManager.Selector.IsChanging)
        {
            return false;
        }
        // The put-away issued in the draw callback is still animating: don't hammer TakePrevWeapon (log showed 3 refusals).
        if (Time.time - _putAwayIssuedTime < 1.5f)
        {
            return false;
        }
        if (_nextHolsterAttempt < Time.time)
        {
            _nextHolsterAttempt = Time.time + 0.2f;
            bool ok = weaponManager.Selector.TakePrevWeapon();
            Log($"{Who()} fake grenade put away: TakePrevWeapon={ok}");
            TacticDiagnostics.Count(ok ? "fakeNade.putAway" : "fakeNade.putAwayRefused");
        }
        return false;
    }

    private float _nextHolsterAttempt;
    private float _putAwayIssuedTime = -100f;
    private bool _fakeDrawPending;
    private float _fakeDrawPendingUntil;
    private float _lastFakeNadeTime = -100f;

    /// <summary>
    /// The fake grenade is a bluff: for a few seconds after it, SAIN's normal grenade logic must not turn it into
    /// a real throw at whoever reacts to the sound (GrenadeThrowDecider checks this).
    /// </summary>
    public bool RecentFakeGrenade
    {
        get { return Time.time - _lastFakeNadeTime < 8f; }
    }

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
        // Real healing comes first: bleeding or badly hurt -> no fake, SAIN's own heal decision handles it.
        var status = Bot.Memory.Health.HealthStatus;
        if (medecine.FirstAid?.IsBleeding == true || status == ETagStatus.BadlyInjured || status == ETagStatus.Dying)
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
            _emergencyWindowUntil = Time.time + FAKE_HEAL_SHOW_TIME + EMERGENCY_WINDOW_AFTER_FAKE;
        }
        Log($"{Who()} fake heal start: {(_fakeHealRunning ? "healing (will cancel)" : "FAILED to start")}");
        TacticDiagnostics.Count(_fakeHealRunning ? "fakeHeal.started" : "fakeHeal.failedToStart");
        return _fakeHealRunning;
    }

    private void CancelFakeHeal(string why)
    {
        if (!Alive())
        {
            _fakeStimRunning = false;
            _fakeHealRunning = false;
            return;
        }
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
            // BSG's own cancel: TakePrevWeapon + heal cooldown. The cooldown (Mind.HEAL_DELAY_SEC) would also hold back
            // the REAL heal SAIN decides on later, so give it back right away.
            float before = firstAid._nextPosibleUseTime;
            firstAid.StopUse();
            firstAid._nextPosibleUseTime = Mathf.Min(firstAid._nextPosibleUseTime, Mathf.Max(before, Time.time + 0.5f));
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
        if (!Alive() || Player.HandsController is not IGrenadeController)
        {
            return;
        }
        var weaponManager = BotOwner.WeaponManager;
        if (weaponManager == null || weaponManager.Grenades.ThrowindNow)
        {
            return;
        }
        if (Time.time - _putAwayIssuedTime < 1.5f)
        {
            // Already being put away (fake grenade callback).
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
                    if (Alive() && Player.HandsController is IGrenadeController && !weaponManager.Grenades.ThrowindNow)
                    {
                        Log($"{Who()} retry put away grenade: TakePrevWeapon={weaponManager.Selector.TakePrevWeapon()}");
                    }
                }
            );
        }
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
                _ => EStep.Hold,
            },
        };
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
            || result.StartsWith("otherEnemyActive")
            || result.StartsWith("otherEnemyOnOurSide")
            || result.StartsWith("closeGunfire")
            || result.StartsWith("goalEnemyChanged");
    }

    /// <summary>
    /// 2026-09-27 field report: once the room enemy was the target, the bot kept working the door until a third party
    /// actually shot it. Now any other known enemy that is visible, or was heard within 25m in the last 3s, drops the
    /// door (and blocks starting / resuming one) so SAIN's normal decisions deal with them.
    /// </summary>
    private bool OtherEnemyActive(Enemy goal, out string what)
    {
        what = string.Empty;
        var enemies = Bot.EnemyController.KnownEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy other = enemies[i];
            if (other == null || ReferenceEquals(other, goal))
            {
                continue;
            }
            if (other.IsVisible)
            {
                what = $"{other.EnemyName} visible";
                return true;
            }
            var hearing = other.Hearing;
            if (hearing != null && Time.time - hearing.LastHeardSoundTime < 3f)
            {
                float dist = HorizontalDistance(hearing.LastHeardSoundPosition, Bot.Position);
                if (dist < 25f)
                {
                    what = $"{other.EnemyName} heard ({hearing.LastHeardSoundType}) {dist:0}m away";
                    return true;
                }
            }
        }
        return false;
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
    /// True if running footsteps (sprint) were just heard from ANY known enemy within 20m of the bot or 7m of the
    /// door (user: "running sounds cancel everything"), or the room enemy jumped / landed / used the door near it.
    /// Plain walking doesn't count - someone sneaking up isn't committing yet.
    /// </summary>
    private bool EnemyComingOut(Session s, out string what)
    {
        what = string.Empty;
        var enemies = Bot.EnemyController.KnownEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            var hearing = enemy?.Hearing;
            if (hearing == null || Time.time - hearing.LastHeardSoundTime > 1.2f)
            {
                continue;
            }
            float toDoor = HorizontalDistance(hearing.LastHeardSoundPosition, s.Center);
            float toBot = HorizontalDistance(hearing.LastHeardSoundPosition, Bot.Position);
            bool near;
            switch (hearing.LastHeardSoundType)
            {
                case SAINSoundType.Sprint:
                    near = toBot < 20f || toDoor < 7f;
                    break;
                case SAINSoundType.Jump:
                case SAINSoundType.Land:
                case SAINSoundType.Door:
                case SAINSoundType.DoorBreach:
                    near = ReferenceEquals(enemy, s.Enemy) && toDoor < 7f;
                    break;
                default:
                    near = false;
                    break;
            }
            if (near)
            {
                what = $"{hearing.LastHeardSoundType} from {enemy.EnemyName} {toBot:0.0}m away ({toDoor:0.0}m from the door)";
                return true;
            }
        }
        return false;
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
        s.ProgressCheckTime = 0f;
        // Door-proximity slowdown (BotPathData) is only lifted for the quick peek hops.
        Bot.Mover.IgnoreDoorSlow = step == EStep.PeekOut || step == EStep.PeekBack;
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
        _doorCooldowns[s.Door.Id] = time + DOOR_COOLDOWN_AFTER_SESSION;
        _nextAllowedTime = time + GLOBAL_COOLDOWN;
        if (resultKey == "moveTimeout" || resultKey == "stuck" || resultKey == "noPath" || resultKey == "doorDidNotOpen" || resultKey == "doorOpenFailed")
        {
            // Failed to even get going: don't chain straight into the next door (one bot started 8 sessions in a row).
            _nextAllowedTime = time + FAIL_COOLDOWN;
        }
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
                + $"jumped={s.Jumped} fakeNade={s.FakeNadeDrawn} fakeHeal={s.FakeHealStarted} enemyVisibleNow={s.Enemy?.IsVisible}"
        );
    }

    public override void Init()
    {
        if (Player != null)
        {
            Player.OnPlayerDead += OnOwnDeath;
        }
        base.Init();
    }

    /// <summary>
    /// [Death] line: what the bot was doing when it died (2026-09-28 field report: bots stand and shoot until they die).
    /// </summary>
    private void OnOwnDeath(Player player, IPlayer lastAggressor, DamageInfo damage, EBodyPart part)
    {
        try
        {
            if (player != null)
            {
                player.OnPlayerDead -= OnOwnDeath;
                // Clear any movement input (diamond step / SAIN mover) left from the last frame alive.
                player.Move(Vector2.zero);
                player.EnableSprint(false);
            }
            var decision = Bot.Decision;
            Enemy goal = Bot.GoalEnemy;
            string enemyInfo = goal == null ? "none" : $"{(goal.IsVisible ? "visible" : "notVisible")} {goal.RealDistance:0}m";
            TacticDiagnostics.Count($"death.{decision.CurrentCombatDecision}");
            bool sawKiller = goal != null && lastAggressor != null && goal.EnemyProfileId == lastAggressor.ProfileId && goal.Seen;
            SAIN.Components.BotControllerSpace.Classes.PlayerStyleRecorder.OnBotKilled(
                Bot, lastAggressor, part, decision.CurrentCombatDecision.ToString(), sawKiller);
            TacticDiagnostics.LogCloseCombat(
                $"[Death] [{Bot.name}] [{Bot.Info.Personality}] layer={Bot.ActiveLayer} combat={decision.CurrentCombatDecision} self={decision.CurrentSelfDecision} "
                    + $"squad={decision.CurrentSquadDecision} diamond={TacticDiagnostics.GetDiamond(Bot.ProfileId)} enemy={enemyInfo} "
                    + $"underFire={BotOwner.Memory.IsUnderFire} pose={Player.PoseLevel:0.0} speed={Player.Velocity.magnitude:0.0} "
                    + $"inCover={Bot.Cover.CoverInUse != null} part={part} by={lastAggressor?.Profile?.Nickname}"
            );
        }
        catch (System.Exception ex)
        {
            Logger.LogWarning($"[Death] log failed: {ex.Message}");
        }
    }

    public override void Dispose()
    {
        if (Player != null)
        {
            Player.OnPlayerDead -= OnOwnDeath;
        }
        if (_session != null)
        {
            End("disposed");
        }
        base.Dispose();
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
