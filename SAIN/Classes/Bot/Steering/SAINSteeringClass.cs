using EFT;
using SAIN.Components;
using SAIN.Components.PlayerComponentSpace;
using SAIN.Models.Enums;
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
        if (ShallHideHeadRunning(dir, out float pitch))
        {
            Vector3 flat = new(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude > 0.01f)
            {
                float rad = pitch * Mathf.Deg2Rad;
                dir = flat.normalized * Mathf.Cos(rad) + Vector3.down * Mathf.Sin(rad);
            }
        }
        PlayerComponent.CharacterController.SetTargetLookDirection(dir, BotOwner, Bot);
    }

    // zzap: players running away under fire (back to the enemy) look at the floor while they sprint - the head drops and
    // is a much smaller target from behind (user tip, live servers). Bots now do the same while sprinting away from an
    // enemy that sees them or shot at them in the last 3s. F6: General > Close Combat (zzap) > Retreat Head Down.
    private bool _headDown;

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
            if (exposed && enemy.RealDistance < settings.RetreatHeadDownMaxDistance)
            {
                Vector3 toEnemy = enemy.EnemyPosition - Bot.Position;
                toEnemy.y = 0f;
                Vector3 flatLook = new(lookDir.x, 0f, lookDir.z);
                result = toEnemy.sqrMagnitude > 0.01f && flatLook.sqrMagnitude > 0.01f && Vector3.Angle(flatLook, toEnemy) > 110f;
            }
        }
        if (result)
        {
            pitch = settings.RetreatHeadDownPitch;
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
