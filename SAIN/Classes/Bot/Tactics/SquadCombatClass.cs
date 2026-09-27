using System.Collections.Generic;
using EFT;
using SAIN.Components;
using SAIN.Models.Enums;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.GlobalSettings.Categories.General;
using SAIN.SAINComponent.Classes.EnemyClasses;
using UnityEngine;
using UnityEngine.AI;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork: squad engagement framework for a live firefight (ECombatDecision.SquadTactic, SAIN combat
/// layer, so ORBIT is unaffected). Vanilla SAIN's squad layer only acts after the bot hasn't seen its
/// enemy for 10s+, so mid-fight each squad bot fights alone. This runs for squadmates who know the enemy
/// but can't see it right now:
///   Crossfire - a teammate is shooting at the enemy: take a different angle on it (spread out).
///   CoverMate - a teammate nearby is reloading/healing: stand next to them and hold their enemy's angle.
///   Trade     - a teammate just went down: aggressive personalities push that enemy's position,
///               cautious ones take an angle on it.
/// As soon as the enemy is visible SAIN's normal StandAndShoot takes over and the behaviour ends.
/// F6: General > Squad Combat (zzap). Logs: [SquadCombat] + [Tactics] SUMMARY keys squad.*.
/// </summary>
public class SquadCombatClass : BotComponentClassBase
{
    public enum EMode
    {
        None,
        Crossfire,
        CoverMate,
        TradePush,
        TradeAngle,
    }

    private const float DECISION_THROTTLE = 0.5f;
    private const float COOLDOWN_AFTER_END = 4f;
    private const float CROSSFIRE_HOLD_MAX = 20f;
    private const float COVER_HOLD_MAX = 12f;
    private const float TRADE_PUSH_MAX = 15f;
    private const float MAX_PATH_LENGTH = 35f;
    private const float ARRIVE_DIST = 0.8f;
    private const float MOVE_TIMEOUT = 12f;

    public SquadCombatClass(BotComponent bot)
        : base(bot)
    {
        CanEverTick = false;
    }

    public bool Active
    {
        get { return _session != null; }
    }

    public EMode Mode
    {
        get { return _session?.Mode ?? EMode.None; }
    }

    public Vector3? LookTarget
    {
        get { return _session?.Look; }
    }

    private static SquadCombatSettings Settings
    {
        get { return GlobalSettingsClass.Instance.General.SquadCombat; }
    }

    private sealed class Session
    {
        public EMode Mode;
        public string EnemyId;
        public Enemy Enemy;
        public Vector3 Target;
        public Vector3 Look;
        public BotComponent Mate;
        public bool Arrived;
        public float StartTime;
        public float ArriveTime;
        public float NextMoveOrder;
    }

    private Session _session;
    private float _nextDecisionTime;
    private float _cooldownUntil;

    // ---------------------------------------------------------------- decision

    /// <summary>
    /// Called from EnemyDecisionClass after the door tactic check. True while a behaviour runs or one just started.
    /// </summary>
    public bool ShallUse(Enemy enemy, out string reason)
    {
        if (_session != null)
        {
            if (enemy == null || enemy.EnemyProfileId != _session.EnemyId)
            {
                End("goalEnemyChanged");
                reason = "goalEnemyChanged";
                return false;
            }
            reason = $"active:{_session.Mode}";
            return true;
        }

        float time = Time.time;
        if (_nextDecisionTime > time)
        {
            reason = "throttled";
            return false;
        }
        _nextDecisionTime = time + DECISION_THROTTLE;
        TrackTeammates(time);

        if (!Settings.Enabled || _cooldownUntil > time)
        {
            reason = "disabledOrCooldown";
            return false;
        }
        if (!Bot.Squad.BotInGroup || Bot.Squad.Members == null || Bot.Squad.Members.Count <= 1)
        {
            reason = "solo";
            return false;
        }
        if (Bot.DoorTactic.Active)
        {
            reason = "doorTacticActive";
            return false;
        }
        if (enemy == null || enemy.IsVisible || BotOwner.Memory.IsUnderFire)
        {
            reason = "visibleOrUnderFire";
            return false;
        }
        Vector3? known = enemy.KnownPlaces.LastKnownPosition;
        if (known == null || enemy.TimeSinceLastKnownUpdated > 20f)
        {
            reason = "enemyInfoStale";
            return false;
        }
        if (Flat(known.Value - Bot.Position).magnitude > Settings.MaxEnemyDistance)
        {
            reason = "enemyTooFar";
            return false;
        }

        reason = "noSquadBehaviour";
        if (Settings.Trade && TryStartTrade(enemy, known.Value, out reason))
        {
            return true;
        }
        if (Settings.CoverTeammate && TryStartCover(enemy, out reason))
        {
            return true;
        }
        if (Settings.Crossfire && TryStartCrossfire(enemy, known.Value, out reason))
        {
            return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- teammate tracking (for trades)

    private readonly Dictionary<string, string> _mateEnemy = new();
    private readonly Dictionary<string, string> _mateNames = new();
    private string _downedMateEnemyId;
    private string _downedMateName;
    private float _downedTime = -1000f;

    private void TrackTeammates(float time)
    {
        var members = Bot.Squad.Members;
        if (members == null)
        {
            return;
        }
        _stillHere.Clear();
        foreach (var member in members.Values)
        {
            if (member == null || ReferenceEquals(member, Bot))
            {
                continue;
            }
            if (member.IsDead)
            {
                continue;
            }
            _stillHere.Add(member.ProfileId);
            _mateEnemy[member.ProfileId] = member.GoalEnemy?.EnemyProfileId;
            _mateNames[member.ProfileId] = member.name;
        }
        _gone.Clear();
        foreach (var id in _mateEnemy.Keys)
        {
            if (!_stillHere.Contains(id))
            {
                _gone.Add(id);
            }
        }
        foreach (var id in _gone)
        {
            string enemyId = _mateEnemy[id];
            _mateNames.TryGetValue(id, out string name);
            _mateEnemy.Remove(id);
            _mateNames.Remove(id);
            _downedTime = time;
            _downedMateEnemyId = enemyId;
            _downedMateName = name;
            TacticDiagnostics.Count("squad.teammateDown");
            Log($"{Who()} teammate {name} went down (was fighting {enemyId ?? "nobody"})");
        }
    }

    private readonly HashSet<string> _stillHere = new();
    private readonly List<string> _gone = new();

    private bool TryStartTrade(Enemy enemy, Vector3 known, out string reason)
    {
        reason = string.Empty;
        if (Time.time - _downedTime > Settings.TradeWindow || _downedMateEnemyId == null)
        {
            return false;
        }
        if (_downedMateEnemyId != enemy.EnemyProfileId)
        {
            reason = "tradeOtherEnemy";
            return false;
        }
        _downedMateEnemyId = null;
        bool aggressive = Bot.Info.PersonalitySettings.Rush.CanRushEnemyReloadHeal;
        if (aggressive)
        {
            if (!Bot.Mover.CanGoToPoint(known, out _, false))
            {
                reason = "tradeNoPath";
                return false;
            }
            Start(EMode.TradePush, enemy, known, known + Vector3.up * 1.3f, null, $"trade for {_downedMateName}: pushing the killer's position");
            reason = "tradePush";
            return true;
        }
        if (FindAnglePoint(enemy, known, Bot.Position, out Vector3 point, out string why))
        {
            Start(EMode.TradeAngle, enemy, point, known + Vector3.up * 1.3f, null, $"trade for {_downedMateName}: taking an angle on the killer");
            reason = "tradeAngle";
            return true;
        }
        reason = $"tradeNoAngle({why})";
        return false;
    }

    // ---------------------------------------------------------------- cover a reloading / healing teammate

    /// <summary>Teammate profile id -> profile id of the bot covering them (one coverer per teammate).</summary>
    private static readonly Dictionary<string, string> _coverClaims = new();

    private bool TryStartCover(Enemy enemy, out string reason)
    {
        reason = string.Empty;
        foreach (var member in Bot.Squad.Members.Values)
        {
            if (member == null || ReferenceEquals(member, Bot) || member.IsDead)
            {
                continue;
            }
            if (!MateBusy(member, out string busy))
            {
                continue;
            }
            if (Flat(member.Position - Bot.Position).magnitude > Settings.CoverMaxDistance)
            {
                continue;
            }
            if (_coverClaims.TryGetValue(member.ProfileId, out string coverer) && coverer != Bot.ProfileId)
            {
                continue;
            }
            Enemy theirs = member.GoalEnemy ?? enemy;
            Vector3? theirKnown = theirs.KnownPlaces.LastKnownPosition;
            if (theirKnown == null)
            {
                continue;
            }
            // Beside the teammate, offset sideways relative to their enemy, so both guns face it.
            Vector3 toEnemy = Flat(theirKnown.Value - member.Position).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, toEnemy);
            Vector3 spot = member.Position + side * 1.6f;
            if (!SampleNav(spot, out Vector3 point))
            {
                spot = member.Position - side * 1.6f;
                if (!SampleNav(spot, out point))
                {
                    continue;
                }
            }
            if (!Bot.Mover.CanGoToPoint(point, out _, true))
            {
                continue;
            }
            _coverClaims[member.ProfileId] = Bot.ProfileId;
            Start(EMode.CoverMate, enemy, point, theirKnown.Value + Vector3.up * 1.3f, member, $"covering {member.name} ({busy})");
            reason = "coverMate";
            return true;
        }
        return false;
    }

    private static bool MateBusy(BotComponent mate, out string what)
    {
        what = string.Empty;
        var owner = mate.BotOwner;
        if (owner?.WeaponManager?.Reload?.Reloading == true)
        {
            what = "reloading";
            return true;
        }
        if (owner?.Medecine?.Using == true)
        {
            what = "healing";
            return true;
        }
        ESelfActionType self = mate.Decision.CurrentSelfDecision;
        if (self == ESelfActionType.Surgery || self == ESelfActionType.FirstAid || self == ESelfActionType.Reload)
        {
            what = self.ToString();
            return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- crossfire

    private static readonly List<(Vector3 point, float until)> _claimedPoints = new();
    private static readonly List<Vector3> _candidates = new();
    private static readonly float[] RING_SCALES = { 1f, 0.7f, 0.45f };
    private static readonly float[] SIDESTEPS = { 3f, 6f, 9f, 12f };

    private bool TryStartCrossfire(Enemy enemy, Vector3 known, out string reason)
    {
        reason = string.Empty;
        BotComponent engaging = null;
        foreach (var member in Bot.Squad.Members.Values)
        {
            if (member == null || ReferenceEquals(member, Bot) || member.IsDead)
            {
                continue;
            }
            Enemy theirs = member.GoalEnemy;
            if (theirs != null && theirs.EnemyProfileId == enemy.EnemyProfileId && theirs.IsVisible)
            {
                engaging = member;
                break;
            }
        }
        if (engaging == null)
        {
            reason = "noTeammateEngaging";
            return false;
        }
        Vector3 enemyPos = engaging.GoalEnemy.EnemyPosition;
        if (!FindAnglePoint(enemy, enemyPos, engaging.Position, out Vector3 point, out string why))
        {
            reason = $"noCrossfirePoint({why})";
            TacticDiagnostics.Count($"squad.crossfire.noPoint.{why}");
            return false;
        }
        Start(EMode.Crossfire, enemy, point, enemyPos + Vector3.up * 1.3f, engaging, $"crossfire with {engaging.name}");
        reason = "crossfire";
        return true;
    }

    /// <summary>
    /// A reachable spot at roughly our current range from the enemy, rotated away from the reference
    /// position (the engaging teammate, or ourselves for a trade), clear line of sight to the enemy,
    /// and not bunched up with any teammate or another bot's chosen spot.
    /// </summary>
    private bool FindAnglePoint(Enemy enemy, Vector3 enemyPos, Vector3 reference, out Vector3 result, out string why)
    {
        result = default;
        why = "none";
        Vector3 fromEnemy = Flat(reference - enemyPos);
        if (fromEnemy.sqrMagnitude < 1f)
        {
            fromEnemy = Flat(Bot.Position - enemyPos);
        }
        float range = Mathf.Clamp(Flat(Bot.Position - enemyPos).magnitude, 10f, 40f);
        float baseAngle = Mathf.Atan2(fromEnemy.z, fromEnemy.x) * Mathf.Rad2Deg;
        float minAngle = Settings.CrossfireMinAngle;
        float bestPath = float.MaxValue;
        int tested = 0;
        int noLos = 0;
        int tooClose = 0;
        CleanClaims();

        // Candidates: (1) rotated around the enemy at three ranges, (2) sidesteps from where we stand.
        // A single ring at our own range mostly landed inside buildings/walls in the first test (Customs dorms).
        _candidates.Clear();
        foreach (float scale in RING_SCALES)
        {
            float r = Mathf.Max(range * scale, 8f);
            for (float offset = minAngle; offset <= 120f; offset += 15f)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    float angle = (baseAngle + offset * sign) * Mathf.Deg2Rad;
                    _candidates.Add(enemyPos + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * r);
                }
            }
        }
        Vector3 toEnemyFromBot = Flat(enemyPos - Bot.Position);
        if (toEnemyFromBot.sqrMagnitude > 1f)
        {
            Vector3 lateral = Vector3.Cross(Vector3.up, toEnemyFromBot.normalized);
            foreach (float step in SIDESTEPS)
            {
                _candidates.Add(Bot.Position + lateral * step);
                _candidates.Add(Bot.Position - lateral * step);
            }
        }

        Vector3 refDir = fromEnemy.normalized;
        foreach (Vector3 raw in _candidates)
        {
            if (!SampleNav(raw, out Vector3 point))
            {
                continue;
            }
            Vector3 candDir = Flat(point - enemyPos);
            if (candDir.magnitude < 5f || Vector3.Angle(candDir, refDir) < minAngle)
            {
                continue;
            }
            tested++;
            if (TooCloseToTeammates(point))
            {
                tooClose++;
                continue;
            }
            if (Physics.Linecast(point + Vector3.up * 1.5f, enemyPos + Vector3.up * 1.3f, LayersMaskController.HighPolyWithTerrainMask))
            {
                noLos++;
                continue;
            }
            if (!Bot.Mover.CanGoToPoint(point, out NavMeshPath path, true))
            {
                continue;
            }
            float length = PathLength(path);
            if (length > MAX_PATH_LENGTH || length >= bestPath)
            {
                continue;
            }
            bestPath = length;
            result = point;
        }
        if (bestPath < float.MaxValue)
        {
            _claimedPoints.Add((result, Time.time + CROSSFIRE_HOLD_MAX));
            return true;
        }
        why = tested == 0 ? "noNavmesh" : tooClose == tested ? "bunched" : noLos > 0 ? "noLineOfSight" : "noPath";
        return false;
    }

    private bool TooCloseToTeammates(Vector3 point)
    {
        float min = Settings.MinSpacing;
        foreach (var member in Bot.Squad.Members.Values)
        {
            if (member == null || ReferenceEquals(member, Bot) || member.IsDead)
            {
                continue;
            }
            if (Flat(member.Position - point).magnitude < min)
            {
                return true;
            }
        }
        foreach (var claim in _claimedPoints)
        {
            if (Flat(claim.point - point).magnitude < min)
            {
                return true;
            }
        }
        return false;
    }

    private static void CleanClaims()
    {
        _claimedPoints.RemoveAll(x => x.until < Time.time);
    }

    // ---------------------------------------------------------------- execution

    private void Start(EMode mode, Enemy enemy, Vector3 target, Vector3 look, BotComponent mate, string why)
    {
        _session = new Session
        {
            Mode = mode,
            EnemyId = enemy.EnemyProfileId,
            Enemy = enemy,
            Target = target,
            Look = look,
            Mate = mate,
            StartTime = Time.time,
        };
        TacticDiagnostics.Count($"squad.start.{mode}");
        Log($"{Who()} START {mode}: {why} (move {Flat(target - Bot.Position).magnitude:0.0}m)");
    }

    public void Tick()
    {
        Session s = _session;
        if (s == null)
        {
            return;
        }
        float time = Time.time;
        float elapsed = time - s.StartTime;

        if (BotOwner.Memory.IsUnderFire && s.Mode != EMode.TradePush)
        {
            End("underFire");
            return;
        }

        switch (s.Mode)
        {
            case EMode.CoverMate:
                if (s.Mate == null || s.Mate.IsDead)
                {
                    End("mateGone");
                    return;
                }
                if (!MateBusy(s.Mate, out _) && (s.Arrived || elapsed > 3f))
                {
                    End("mateDone");
                    return;
                }
                if (elapsed > COVER_HOLD_MAX)
                {
                    End("coverTimeout");
                    return;
                }
                break;

            case EMode.Crossfire:
            case EMode.TradeAngle:
                if (s.Arrived && time - s.ArriveTime > CROSSFIRE_HOLD_MAX)
                {
                    End("holdTimeout");
                    return;
                }
                if (s.Enemy.TimeSinceLastKnownUpdated > 25f)
                {
                    End("enemyInfoStale");
                    return;
                }
                break;

            case EMode.TradePush:
                if (elapsed > TRADE_PUSH_MAX)
                {
                    End("pushTimeout");
                    return;
                }
                break;
        }

        if (!s.Arrived)
        {
            if (Flat(s.Target - Bot.Position).magnitude < ARRIVE_DIST)
            {
                s.Arrived = true;
                s.ArriveTime = time;
                TacticDiagnostics.Count($"squad.arrived.{s.Mode}");
                Log($"{Who()} {s.Mode} in position after {elapsed:0.0}s");
                if (s.Mode == EMode.TradePush)
                {
                    End("reachedKillerPosition");
                    return;
                }
            }
            else if (elapsed > MOVE_TIMEOUT)
            {
                End("moveTimeout");
                return;
            }
            else if (s.NextMoveOrder < time)
            {
                s.NextMoveOrder = time + 0.75f;
                bool sprint = s.Mode == EMode.TradePush || Flat(s.Target - Bot.Position).magnitude > 8f;
                bool ok = sprint
                    ? Bot.Mover.RunToPoint(s.Target, s.Mode != EMode.TradePush, ARRIVE_DIST * 0.7f, ESprintUrgency.High)
                    : Bot.Mover.WalkToPoint(s.Target, true, ARRIVE_DIST * 0.7f);
                if (!ok)
                {
                    End("noPath");
                    return;
                }
            }
            return;
        }

        Bot.Mover.Stop();
        Bot.Mover.SetTargetPose(0.8f);
    }

    public void OnActionStopped()
    {
        if (_session == null)
        {
            return;
        }
        ECombatDecision next = Bot.Decision.CurrentCombatDecision;
        if (next == ECombatDecision.SquadTactic)
        {
            // SAINLayer restarts the current action whenever ANY decision changes (self action like reload,
            // squad decision, enemy). The combat decision is still ours, so the new action keeps this session.
            TacticDiagnostics.Count("squad.actionRestartKept");
            return;
        }
        End(next == ECombatDecision.StandAndShoot ? "enemySpotted(StandAndShoot)" : $"interrupted({next})");
    }

    public void End(string result)
    {
        Session s = _session;
        if (s == null)
        {
            return;
        }
        _session = null;
        _cooldownUntil = Time.time + COOLDOWN_AFTER_END;
        Bot.Mover.SetTargetMoveSpeed(1f);
        Bot.Mover.SetTargetPose(1f);
        if (s.Mode == EMode.CoverMate && s.Mate != null && _coverClaims.TryGetValue(s.Mate.ProfileId, out string coverer) && coverer == Bot.ProfileId)
        {
            _coverClaims.Remove(s.Mate.ProfileId);
        }
        string key = result.Contains("(") ? result.Substring(0, result.IndexOf('(')) : result;
        TacticDiagnostics.Count($"squad.end.{s.Mode}.{key}");
        Log($"{Who()} END {s.Mode} result={result} after {Time.time - s.StartTime:0.0}s arrived={s.Arrived}");
    }

    public override void Dispose()
    {
        End("disposed");
        base.Dispose();
    }

    // ---------------------------------------------------------------- helpers

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    private static bool SampleNav(Vector3 point, out Vector3 result)
    {
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, -1))
        {
            result = hit.position;
            return true;
        }
        result = point;
        return false;
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

    private string Who()
    {
        return $"[SquadCombat] [{Bot.name}] [{Bot.Info.Personality}]";
    }

    private static void Log(string message)
    {
        if (Settings.DiagnosticLogs)
        {
            Logger.LogWarning(message);
        }
    }
}
