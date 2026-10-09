using System;
using EFT;
using EFT.Ballistics;
using SAIN.Helpers.Events;
using SAIN.Preset.Shared.Enums;

namespace SAIN.SAINComponent.Classes.EnemyClasses;

public class EnemyEvents
{
    public EnemyToggleEventTimeTracked OnEnemyLineOfSightChanged { get; }
    public EnemyToggleEventTimeTracked OnEnemyKnownChanged { get; }
    public EnemyToggleEventTimeTracked OnActiveThreatChanged { get; }
    public EnemyToggleEventTimeTracked OnVisionChange { get; }
    public EnemyToggleEventTimeTracked OnSearch { get; }
    public EnemyToggleEventTimeTracked OnEnemyCanShootChanged { get; }

    public event Action<Enemy> OnEnemyLocationsSearched;
    public event Action<Enemy> OnFirstSeen;
    public event Action<Enemy> OnEnemyShot;
    public event Action<Enemy> OnBeingShotByEnemy;
    public event Action<Enemy, EnemyPlace> OnPositionUpdated;
    public event Action<Enemy, SAINSoundType, bool, EnemyPlace> OnEnemyHeard;
    public event Action<Enemy, ETagStatus> OnHealthStatusChanged;

    public EnemyEvents(EnemyData enemy)
    {
        Enemy = enemy.Enemy;
        OnEnemyLineOfSightChanged = new EnemyToggleEventTimeTracked(enemy.Enemy, false);
        OnEnemyKnownChanged = new EnemyToggleEventTimeTracked(enemy.Enemy, false);
        OnActiveThreatChanged = new EnemyToggleEventTimeTracked(enemy.Enemy, false);
        OnVisionChange = new EnemyToggleEventTimeTracked(enemy.Enemy, false);
        OnSearch = new EnemyToggleEventTimeTracked(enemy.Enemy, false);
        OnEnemyCanShootChanged = new EnemyToggleEventTimeTracked(enemy.Enemy, false);
    }

    private readonly Enemy Enemy;

    public void Init(Player enemyPlayer)
    {
        enemyPlayer.BeingHitAction += enemyHit;
    }

    public void Dispose(Player enemyPlayer)
    {
        if (enemyPlayer != null)
        {
            enemyPlayer.BeingHitAction -= enemyHit;
        }
    }

    public void EnemyLocationsSearched()
    {
        OnEnemyLocationsSearched?.Invoke(Enemy);
    }

    public void LastKnownUpdated(EnemyPlace place, float currentTime)
    {
        OnPositionUpdated?.Invoke(Enemy, place);
        OnEnemyKnownChanged.CheckToggle(true, currentTime);
    }

    public void ShotByEnemy()
    {
        OnBeingShotByEnemy?.Invoke(Enemy);
    }

    public void HealthStatusChanged(ETagStatus status)
    {
        OnHealthStatusChanged?.Invoke(Enemy, status);
    }

    public void EnemyFirstSeen()
    {
        CountFirstSeen();
        OnFirstSeen?.Invoke(Enemy);
    }

    /// <summary>
    /// zzap (user 2026-10-09: "bots often don't notice someone coming by sound, they react only once they see him"): measurement
    /// only. At the moment a bot first sees an enemy, was that enemy heard in the 10 s before - and by what kind of sound - and how
    /// far away is he. hear.firstSeen.&lt;heard.move|heard.gun|heard.other|notHeard&gt;.&lt;close|mid|far&gt; (close &lt;15 m, mid &lt;40 m).
    /// </summary>
    private void CountFirstSeen()
    {
        if (!SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.CountOn)
        {
            return;
        }
        var hearing = Enemy.Hearing;
        string heard = "notHeard";
        if (hearing != null && hearing.Heard && hearing.TimeSinceHeard < 10f)
        {
            heard = hearing.LastHeardSoundType switch
            {
                SAINSoundType.FootStep or SAINSoundType.Sprint or SAINSoundType.Jump or SAINSoundType.Land or SAINSoundType.Prone
                    or SAINSoundType.Bush or SAINSoundType.GearSound or SAINSoundType.TurnSound or SAINSoundType.Door
                    or SAINSoundType.DoorBreach => "heard.move",
                SAINSoundType.Shot or SAINSoundType.SuppressedShot or SAINSoundType.BulletImpact => "heard.gun",
                _ => "heard.other",
            };
        }
        float distance = Enemy.RealDistance;
        string range = distance < 15f ? "close" : distance < 40f ? "mid" : "far";
        SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count($"hear.firstSeen.{heard}.{range}");
    }

    public void EnemyHeard(SAINSoundType type, bool gunFire, EnemyPlace place)
    {
        OnEnemyHeard?.Invoke(Enemy, type, gunFire, place);
    }

    private void enemyHit(DamageInfo damage, EBodyPart _, float _2)
    {
        var damageSource = damage.Player?.iPlayer;
        if (damageSource == null)
        {
            return;
        }
        if (damageSource.ProfileId == Enemy.Bot.ProfileId)
        {
            OnEnemyShot?.Invoke(Enemy);
        }
    }

    public class EnemyToggleEvent : ToggleEventForObject<Enemy>
    {
        public EnemyToggleEvent(Enemy enemy, bool defaultValue)
            : base(enemy, defaultValue) { }
    }

    public class EnemyToggleEventTimeTracked : ToggleEventForObjectTimeTracked<Enemy>
    {
        public EnemyToggleEventTimeTracked(Enemy enemy, bool defaultValue)
            : base(enemy, defaultValue) { }
    }
}
