using EFT;
using SAIN.Components;
using SAIN.Models.Enums;
using SAIN.SAINComponent.Classes.EnemyClasses;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.WeaponFunction;

public class ManualShootClass : BotComponentClassBase
{
    public ManualShootClass(BotComponent bot)
        : base(bot)
    {
        TickRequirement = ESAINTickState.OnlyNoSleep;
    }

    public override void Init()
    {
        Bot.EnemyController.Events.OnEnemyRemoved += CheckClearEnemy;
        base.Init();
    }

    public override void ManualUpdate()
    {
        CheckReset();
        base.ManualUpdate();
    }

    public override void Dispose()
    {
        Bot.EnemyController.Events.OnEnemyRemoved -= CheckClearEnemy;
        base.Dispose();
    }

    private Enemy ManualShootEnemy;

    private void CheckClearEnemy(string ID, Enemy Enemy)
    {
        if (Enemy == ManualShootEnemy)
        {
            Reset();
        }
    }

    public void Reset()
    {
        BotOwner.ShootData.EndShoot();
        Reason = EShootReason.None;
        ShootPosition = Vector3.zero;
        ManualShootEnemy = null;
    }

    private void CheckReset()
    {
        if (
            Reason != EShootReason.None
            && (
                ManualShootEnemy?.EnemyPlayer?.HealthController?.IsAlive != true
                || !BotOwner.WeaponManager.HaveBullets
                || _timeStartManualShoot + 2f < Time.time
            )
        )
        {
            Reset();
        }
    }

    public bool TryShoot(Enemy Enemy, Vector3 targetPos, bool checkFF = true, EShootReason reason = EShootReason.None)
    {
        if (Enemy != null && ShotIntoGeometry(Enemy, targetPos, reason))
        {
            Reset();
            return false;
        }
        if (
            Enemy != null
            && CanShoot(checkFF)
            && Bot.Steering.AngleToPointFromLookDir(targetPos) <= 10
            && Bot.FriendlyFire.UpdateFriendlyFireStatus(
                targetPos,
                Bot.Transform.WeaponData.FirePort,
                Bot.Transform.WeaponData.PointDirection,
                Bot
            )
        )
        {
            if (!Shooting)
            {
                if (BotOwner.ShootData.Shoot())
                {
                    _timeStartManualShoot = Time.time;
                }
                else
                {
                    return false;
                }
            }

            ManualShootEnemy = Enemy;
            ShootPosition = targetPos;
            Reason = reason;
            return true;
        }
        Reset();
        return false;
    }

    // zzap (2026-10-13, user: "the enemy was upstairs and not visible at all - the bot only heard him, fired into the wall and
    // fought it"): suppression / blind fire / prefire aim at where an unseen enemy was heard, and nothing checked that the
    // bullets can get there. A shot at an unseen enemy is refused when the line from the muzzle is blocked well short of the
    // target AND either the enemy is on another floor (2.3m+ height difference: the shot goes into a ceiling / floor / wall
    // between the floors) or the wall is right in front of the muzzle (2.5m - not suppressing anything). Blind fire is exempt
    // from the wall-in-front part: its whole point is holding the gun up over the cover in front. One cast per 0.3s per bot.
    private const float FLOOR_GAP = 2.3f;
    private const float WALL_IN_FACE = 2.5f;
    private float _geoCheckTime = -1f;
    private Vector3 _geoCheckTarget;
    private bool _geoBlocked;

    private bool ShotIntoGeometry(Enemy enemy, Vector3 target, EShootReason reason)
    {
        if (enemy.IsVisible)
        {
            return false;
        }
        float now = Time.time;
        if (now - _geoCheckTime < 0.3f && (target - _geoCheckTarget).sqrMagnitude < 1f)
        {
            return _geoBlocked;
        }
        _geoCheckTime = now;
        _geoCheckTarget = target;
        _geoBlocked = false;

        Vector3 muzzle = Bot.Transform.WeaponData.FirePort;
        Vector3 line = target - muzzle;
        float dist = line.magnitude;
        if (dist < 0.5f || !Physics.Raycast(muzzle, line / dist, out RaycastHit hit, dist, LayersMaskController.HighPolyWithTerrainMask))
        {
            return false;
        }
        if (dist - hit.distance < 0.75f)
        {
            return false;
        }
        Vector3 enemyPos = enemy.KnownPlaces.LastKnownPosition ?? target;
        bool otherFloor = Mathf.Abs(enemyPos.y - Bot.Position.y) >= FLOOR_GAP;
        bool wallInFace = reason != EShootReason.Blindfire && hit.distance < WALL_IN_FACE;
        if (!otherFloor && !wallInFace)
        {
            return false;
        }
        _geoBlocked = true;
        if (SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.CountOn)
        {
            SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count($"shoot.blocked.{(otherFloor ? "otherFloor" : "wallInFace")}.{reason}");
        }
        return true;
    }

    public bool Shooting
    {
        get { return BotOwner.ShootData.Shooting; }
    }

    public bool CanShoot(bool checkFF = true)
    {
        if (checkFF && !Bot.FriendlyFire.ClearShot)
        {
            //BotOwner.ShootData.EndShoot();
            //return false;
        }
        BotWeaponManager weaponManager = BotOwner.WeaponManager;
        if (weaponManager.IsMelee)
        {
            return false;
        }
        if (!weaponManager.IsWeaponReady)
        {
            return false;
        }
        if (weaponManager.Reload.Reloading)
        {
            return false;
        }
        if (!BotOwner.ShootData.CanShootByState)
        {
            return false;
        }
        if (!weaponManager.HaveBullets)
        {
            return false;
        }
        return true;
    }

    private float _timeStartManualShoot;

    public Vector3 ShootPosition { get; set; }

    public EShootReason Reason { get; private set; }
}
