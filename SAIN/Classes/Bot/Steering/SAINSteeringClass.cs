using EFT;
using SAIN.Components;
using SAIN.Components.PlayerComponentSpace;
using SAIN.Models.Enums;
using SAIN.Preset.Shared.Enums;
using SAIN.SAINComponent.Classes.EnemyClasses;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Mover;

public class SAINSteeringClass : BotComponentClassBase
{
    public SAINSteeringClass(BotComponent sain)
        : base(sain)
    {
        TickRequirement = ESAINTickState.OnlyNoSleep;
        _randomLook = new RandomLookClass(this);
        _steerPriorityClass = new SteerPriorityClass(this);
        HeardSoundSteering = new HeardSoundSteeringClass(this);
    }

    public ESteerPriority CurrentSteerPriority
    {
        get { return _steerPriorityClass.CurrentSteerPriority; }
    }

    public ESteerPriority LastSteerPriority
    {
        get { return _steerPriorityClass.LastSteerPriority; }
    }

    public EEnemySteerDir EnemySteerDir { get; private set; }

    public Vector3 WeaponRootOffset
    {
        get { return Bot.Transform.WeaponRoot - Bot.Position; }
    }

    public bool SteerByPriority(Enemy enemy = null, bool lookRandom = true, bool ignoreRunningPath = false)
    {
        enemy ??= Bot.GoalEnemy;

        switch (_steerPriorityClass.GetCurrentSteerPriority(lookRandom, ignoreRunningPath, enemy))
        {
            case ESteerPriority.RunningPath:
                return true;

            case ESteerPriority.Aiming:
                //LookToPoint(Bot.Aim.EndTargetPoint());
                return true;

            case ESteerPriority.ManualShooting:
                LookToPoint(Bot.ManualShoot.ShootPosition);
                return true;

            case ESteerPriority.EnemyVisible:
                LookToEnemy(enemy);
                return true;

            case ESteerPriority.UnderFire:
                LookToUnderFirePos();
                return true;

            case ESteerPriority.LastHit:
                LookToLastHitPos();
                return true;

            case ESteerPriority.EnemyLastKnownLong:
            case ESteerPriority.EnemyLastKnown:
                if (!LookToLastKnownEnemyPosition(enemy))
                {
                    LookToRandomPosition();
                }
                return true;

            case ESteerPriority.HeardThreat:
                HeardSoundSteering.LookToHeardPosition();
                return true;

            case ESteerPriority.RandomLook:
                LookToRandomPosition();
                return true;

            default:
                return false;
        }
    }

    public bool LookToLastKnownEnemyPosition(Enemy enemy)
    {
        if (FindLastKnownTarget(enemy, out Vector3 Position))
        {
            LookToPoint(Position);
            return true;
        }
        return false;
    }

    public bool LookToMovingDirection(bool sprint = false)
    {
        var pathFollower = Bot.Mover;
        if (pathFollower.Moving)
        {
            LookToFloorPoint(pathFollower.ActivePath.GetCurrentCorner().Position);
            return true;
        }
        if (BotOwner?.Mover?.HasPathAndNoComplete == true)
        {
            LookToFloorPoint(BotOwner.Mover.CurrentCornerPoint);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Look Directly at point
    /// </summary>
    public void LookToPoint(Vector3 point)
    {
        Vector3 direction = point - Bot.Transform.WeaponRoot;
        _targetLookDirection = direction.normalized;
    }

    /// <summary>
    /// Adds the bots current height to the input position
    /// </summary>
    public void LookToFloorPoint(Vector3 point)
    {
        LookToPoint(point + WeaponRootOffset);
    }

    public void LookToEnemy(Enemy enemy)
    {
        if (enemy != null)
        {
            LookToPoint(enemy.EnemyPosition + WeaponRootOffset);
        }
    }

    public void LookToRandomPosition()
    {
        Vector3? point = _randomLook.UpdateRandomLook();
        if (point != null)
        {
            LookToPoint(point.Value);
        }
    }

    public float AngleToPointFromLookDir(Vector3 point)
    {
        Vector3 direction = (point - BotOwner.WeaponRoot.position).normalized;
        return Vector3.Angle(_lookDirection, direction);
    }

    public float AngleToDirectionFromLookDir(Vector3 direction)
    {
        return Vector3.Angle(_lookDirection, direction);
    }

    public override void Init()
    {
        HeardSoundSteering.Init();
        base.Init();
    }

    public override void ManualUpdate()
    {
        base.ManualUpdate();
        HeardSoundSteering.ManualUpdate();
    }

    public override void Dispose()
    {
        HeardSoundSteering.Dispose();
        base.Dispose();
    }

    public bool FindLastKnownTarget(Enemy enemy, out Vector3 Result)
    {
        if (enemy == null)
        {
            EnemySteerDir = EEnemySteerDir.NullEnemy_ERROR;
            Result = Vector3.zero;
            return false;
        }
        if (enemy.FindLookPoint(out Vector3 Position, out EEnemySteerDir EnumValue))
        {
            EnemySteerDir = EnumValue;
            Result = Position;
            return true;
        }
        EnemySteerDir = EEnemySteerDir.None;
        Result = Vector3.zero;
        return false;
    }

    private void LookToUnderFirePos()
    {
        if (LookToLastKnownEnemyPosition(Bot.Memory.LastUnderFireEnemy))
        {
            return;
        }
        LookToPoint(Bot.Memory.UnderFireFromPosition + WeaponRootOffset);
    }

    private void LookToLastHitPos()
    {
        var enemyWhoShotMe = _steerPriorityClass.EnemyWhoLastShotMe;
        if (LookToLastKnownEnemyPosition(enemyWhoShotMe))
        {
            return;
        }
        if (enemyWhoShotMe != null)
        {
            var lastShotPos = enemyWhoShotMe.Status.LastShotPosition;
            if (lastShotPos != null)
            {
                LookToPoint(lastShotPos.Value + WeaponRootOffset);
                return;
            }
        }
        LookToRandomPosition();
    }

    internal bool IsLookingAtPoint(Vector3 point, out float dotResult, float dotProductThreshold = 0.66f)
    {
        Vector3 lookDirection = Bot.PlayerComponent.CharacterController.CurrentControlLookDirection;
        Vector3 pointDirection = point - Bot.Transform.WeaponData.WeaponRoot;
        dotResult = Vector3.Dot(lookDirection, pointDirection.normalized);
        return dotResult >= dotProductThreshold;
    }

    internal void TickPlayerSteering()
    {
        Vector3 dir = _targetLookDirection;
        if (CornerPreAim(out Vector3 preAim))
        {
            dir = preAim;
        }
        if (ShallHideHeadRunning(dir, out float pitch))
        {
            Vector3 flat = new(dir.x, 0f, dir.z);
            // Retreat weave: swing the view with the run, like a player moving the mouse left/right while sprinting.
            float yaw = SAIN.SAINComponent.Classes.Tactics.RetreatWeave.LookYaw(Bot);
            if (yaw != 0f)
            {
                flat = Quaternion.AngleAxis(yaw, Vector3.up) * flat;
            }
            if (flat.sqrMagnitude > 0.01f)
            {
                float rad = pitch * Mathf.Deg2Rad;
                dir = flat.normalized * Mathf.Cos(rad) + Vector3.down * Mathf.Sin(rad);
            }
        }
        PlayerComponent.CharacterController.SetTargetLookDirection(dir, BotOwner, Bot);
    }

    // zzap: corner pre-aim (2026-09-29 field report: bots opened a door / rounded a corner with half the body out and only
    // then turned toward the player - an easy kill for someone holding it). Moving toward a known enemy out of sight:
    // within 6m of the corner/doorway he'd appear from (SAIN's blind corner on the path to him), stop sprinting, aim at
    // that corner at chest height and hold the lean to its side, so the first thing out is the muzzle on the angle and as
    // little body as possible.
    private float _nextPreAimLog;

    private bool CornerPreAim(out Vector3 direction)
    {
        direction = default;
        var settings = SAIN.Preset.Shared.GlobalSettings.GlobalSettingsClass.Instance?.General?.CloseCombat;
        Enemy enemy = Bot.GoalEnemy;
        if (settings == null || !settings.CornerPreAim || enemy == null || enemy.IsVisible || !(enemy.Seen || enemy.Heard))
        {
            return false;
        }
        if (enemy.TimeSinceLastKnownUpdated > 30f || _headDown || Bot.BotOwner.Medecine?.Using == true)
        {
            return false;
        }
        var decision = Bot.Decision.CurrentCombatDecision;
        if (decision == ECombatDecision.AvoidGrenade || decision == ECombatDecision.Retreat || decision == ECombatDecision.RunAway
            || decision == ECombatDecision.DoorTactic)
        {
            return false;
        }
        if (Bot.Player.Velocity.magnitude < 0.5f)
        {
            return false;
        }
        Vector3? cornerOpt = enemy.VisiblePathPoint;
        if (cornerOpt == null)
        {
            return false;
        }
        Vector3 corner = cornerOpt.Value;
        float dist = (corner - Bot.Position).magnitude;
        if (dist > 6f || dist < 0.6f)
        {
            return false;
        }
        if (Bot.Mover.Running)
        {
            Bot.Mover.ActivePath?.RequestEndSprint(ESprintUrgency.None, "corner pre-aim");
        }
        var side = Bot.Mover.Lean.FindLeanFromBlindCornerAngle(enemy);
        if (side != SAIN.Preset.Shared.Enums.LeanSetting.None)
        {
            Bot.Mover.Lean.FastLean(side);
            Bot.Mover.Lean.HoldLean(0.3f);
        }
        Vector3 target = PastCorner(Bot.Transform.WeaponRoot, corner, enemy.KnownPlaces.LastKnownPosition, Bot.Transform.LookDirection);
        direction = (target - Bot.Transform.WeaponRoot).normalized;
        if (Time.time > _nextPreAimLog)
        {
            _nextPreAimLog = Time.time + 5f;
            SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count("corner.preAim");
            SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.LogCloseCombat(
                $"[PreAim] [{Bot.name}] {decision}: corner {dist:0.0}m toward {enemy.EnemyPlayer?.Profile?.Nickname} (known {enemy.TimeSinceLastKnownUpdated:0}s ago) -> aim at it, lean {side}");
        }
        return true;
    }

    /// <summary>
    /// zzap: keep a tactical look point (corner pre-aim, freeze corner watch, prefire) within +-25 deg of level. The
    /// enemy's next path corner is often on a staircase or a platform right next to the bot, and aiming at "corner +
    /// 1.3m" from 1-2m away made bots stare at the ceiling / sky while clearing (field 2026-09-29, Factory ground floor).
    /// A player checks a corner at head height; to cover a staircase he tilts a little, never straight up.
    /// 10 deg (was 25 - 3rd sim screenshots: crouched bots entering corners/doors with the muzzle pointing up).
    /// </summary>
    public static Vector3 ClampPitch(Vector3 from, Vector3 target, Vector3 forward, float maxDeg = 10f)
    {
        Vector3 d = target - from;
        Vector3 flat = new(d.x, 0f, d.z);
        float h = flat.magnitude;
        if (h < 0.3f)
        {
            // Right under / over the point: nothing sensible to aim at up there - keep looking ahead, level.
            Vector3 f = new(forward.x, 0f, forward.z);
            return from + (f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward) * 2f;
        }
        float maxRise = Mathf.Max(h, 1.5f) * Mathf.Tan(maxDeg * Mathf.Deg2Rad);
        return new Vector3(target.x, from.y + Mathf.Clamp(d.y, -maxRise, maxRise), target.z);
    }

    /// <summary>
    /// zzap: where to hold the gun when checking a corner - not the corner point itself (that is the wall edge: 3rd sim
    /// screenshot, a bot aiming into the wall from 1m) but the space just past it, toward where the enemy was: corner +
    /// up to 2m toward his last known position, at chest height, then kept level (ClampPitch).
    /// </summary>
    public static Vector3 PastCorner(Vector3 weaponRoot, Vector3 corner, Vector3? enemyPos, Vector3 forward)
    {
        Vector3 p = corner;
        if (enemyPos != null)
        {
            Vector3 d = enemyPos.Value - corner;
            d.y = 0f;
            float len = d.magnitude;
            if (len > 0.5f)
            {
                p = corner + d / len * Mathf.Min(1f, len);
            }

            // 9th sim, user: "holding a door/corner the muzzle hugs the wall". With the enemy behind the wall, corner ->
            // enemy runs ALONG the wall, so the aim line grazed the wall edge. A player holds the angle a little off the
            // edge, into the open side the enemy steps out to: push the point sideways from the bot->corner line, toward
            // the enemy's side, by ~30% of the distance to the corner (0.6-1.5m) - about 17 degrees off the edge.
            Vector3 v = corner - weaponRoot;
            v.y = 0f;
            Vector3 e = enemyPos.Value - weaponRoot;
            e.y = 0f;
            float dist = v.magnitude;
            if (dist > 0.5f)
            {
                float cross = v.x * e.z - v.z * e.x;
                if (Mathf.Abs(cross) > 0.01f)
                {
                    Vector3 left = new Vector3(-v.z, 0f, v.x) / dist;
                    Vector3 open = cross > 0f ? left : -left;
                    p += open * Mathf.Clamp(dist * 0.3f, 0.6f, 1.5f);
                }
            }
        }
        return ClampPitch(weaponRoot, p + Vector3.up * 1.3f, forward);
    }

    public bool HeadDownActive
    {
        get { return _headDown; }
    }

    // zzap: players running away under fire (back to the enemy) look at the floor while they sprint - the head drops and
    // is a much smaller target from behind (user tip, live servers). Bots now do the same while sprinting away from an
    // enemy that sees them or shot at them in the last 3s. F6: General > Close Combat (zzap) > Retreat Head Down.
    private bool _headDown;
    private float _headDownUntil;

    private bool ShallHideHeadRunning(Vector3 lookDir, out float pitch)
    {
        pitch = 0f;
        bool result = false;
        var settings = SAIN.Preset.Shared.GlobalSettings.GlobalSettingsClass.Instance?.General?.CloseCombat;
        Enemy enemy = Bot.GoalEnemy;
        if (settings != null && settings.RetreatHeadDown && enemy != null && Bot.Player.IsSprintEnabled)
        {
            float lastShot = enemy.Status.TimeLastShotAtMe;
            bool exposed = enemy.IsVisible || (enemy.Seen && enemy.TimeSinceSeen < 2f) || (lastShot > 0f && Time.time - lastShot < 3f);
            // User: keep it up the whole way to cover, not just the moment he's shooting. Once started, a run to cover /
            // retreat keeps it until the bot gets there (enemy seen or shooting in the last 10s).
            var decision = Bot.Decision.CurrentCombatDecision;
            bool runningToCover = (decision == ECombatDecision.SeekCover || decision == ECombatDecision.Retreat || decision == ECombatDecision.RunAway)
                && Bot.Cover.CoverInUse == null
                && ((enemy.Seen && enemy.TimeSinceSeen < 10f) || (lastShot > 0f && Time.time - lastShot < 10f));
            if ((exposed || (runningToCover && _headDown)) && enemy.RealDistance < settings.RetreatHeadDownMaxDistance)
            {
                Vector3 toEnemy = enemy.EnemyPosition - Bot.Position;
                toEnemy.y = 0f;
                Vector3 flatLook = new(lookDir.x, 0f, lookDir.z);
                float limit = runningToCover ? 90f : 110f;
                result = toEnemy.sqrMagnitude > 0.01f && flatLook.sqrMagnitude > 0.01f && Vector3.Angle(flatLook, toEnemy) > limit;
            }
        }
        // Keep it at least 0.8s once started - at 40 deg for a split second it wasn't noticeable (field report).
        if (!result && _headDown && Time.time < _headDownUntil && Bot.Player.IsSprintEnabled)
        {
            result = true;
        }
        if (result)
        {
            pitch = settings.RetreatHeadDownPitch;
            if (!_headDown)
            {
                _headDownUntil = Time.time + 0.8f;
            }
        }
        if (result != _headDown)
        {
            _headDown = result;
            if (result)
            {
                SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count("retreat.headDown");
                SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.LogCloseCombat(
                    $"[HeadDown] [{Bot.name}] sprinting away from {enemy.EnemyPlayer?.Profile?.Nickname} ({enemy.RealDistance:0}m) -> looking {pitch:0} deg down"
                );
            }
        }
        return result;
    }

    private Vector3 _targetLookDirection = Vector3.forward;

    public HeardSoundSteeringClass HeardSoundSteering { get; }
    private readonly RandomLookClass _randomLook;
    private readonly SteerPriorityClass _steerPriorityClass;

    private Vector3 _lookDirection
    {
        get { return Bot.LookDirection; }
    }
}
