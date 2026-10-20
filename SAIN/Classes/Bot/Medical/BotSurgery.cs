using SAIN.Components;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.Classes.Tactics;
using UnityEngine;

namespace SAIN.SAINComponent.Classes;

public class BotSurgery : BotBase
{
    public BotSurgery(BotComponent bot)
        : base(bot) { }

    public bool SurgeryStarted
    {
        get { return _surgeryStarted; }
        set
        {
            if (_surgeryStarted != value && value)
            {
                SurgeryStartTime = Time.time;
            }
            _surgeryStarted = value;
        }
    }

    public void StartSurgery() { }

    public float SurgeryStartTime { get; private set; }

    private bool _surgeryStarted;

    public bool AreaClearForSurgery { get; private set; }

    public bool CheckAreaClearForSurgery()
    {
        // zzap: store the result - AreaClearForSurgery was never assigned, so it always read false: a started surgery was
        // cancelled on the next tick and restarted 2s later, over and over (field 2026-09-29: self=Surgery every 2s with an
        // enemy 8m away), and the surgery action never actually operated.
        AreaClearForSurgery = CheckCanStartUsingKit() && (CheckEnemies() || CheckRelaxed());
        return AreaClearForSurgery;
    }

    public bool CheckCanStartUsingKit()
    {
        var kit = BotOwner?.Medecine?.SurgicalKit;
        // zzap (2026-10-21): a surgery in progress counts - EFT's ShallStartUse is false while the kit is Using, so the
        // continue check (checkContinueSurgery, every decision tick) read "area not clear" and cancelled it right after it began.
        return BotOwner?.Medecine?.FirstAid?.IsBleeding == false && kit != null && (kit.Using || kit.ShallStartUse());
    }

    // zzap (user 2026-10-21, 2nd sim Hauzman: both legs destroyed, Dying, 70 s in cover - EFT first aid skips destroyed parts and
    // the rule above (every known enemy 60 s unseen and unheard, 80 m+ path) never held on Factory: 0 combat surgeries in 30 min).
    // A second, per-personality rule: in cover, nobody in sight, every known enemy quiet for Quiet s and the nearest 'Dist' m+ off
    // by path, then a roll (kept 20 s, or re-rolling every 2 s would make it ~100 %). Once started it goes on unless an enemy is in
    // sight, the bot is hit, or one is under half that distance. HealUtility still weighs it against waiting.
    private static (float chance, float quiet, float dist) Relaxed(EPersonality p) => p switch
    {
        EPersonality.Rat => (80f, 20f, 30f),
        EPersonality.Coward => (80f, 20f, 35f),
        EPersonality.SnappingTurtle => (70f, 20f, 30f),
        EPersonality.Timmy => (60f, 25f, 40f),
        EPersonality.Chad => (35f, 30f, 45f),
        EPersonality.GigaChad => (25f, 30f, 50f),
        EPersonality.Wreckless => (15f, 35f, 50f),
        _ => (50f, 25f, 40f),
    };

    private bool CheckRelaxed()
    {
        var (chance, quiet, dist) = Relaxed(Bot.Info.Personality);
        if (BotOwner.Medecine.SurgicalKit.Using && _relaxedActive)
        {
            bool keep = Bot.Medical.TimeSinceShot >= 1f && NearestPath(0f, out _) >= dist * 0.5f;
            if (!keep)
            {
                TacticDiagnostics.Count("heal.surgery.relaxedCancelled");
                _relaxedActive = false;
            }
            return keep;
        }
        _relaxedActive = false;
        var cover = Bot.Cover.CoverInUse;
        if (cover == null || !cover.BotInThisCover || NearestPath(quiet, out bool quietOk) < dist || !quietOk)
        {
            return false;
        }
        if (Time.time >= _relaxedRollUntil)
        {
            _relaxedRollUntil = Time.time + 20f;
            _relaxedRollHit = TacticDiagnostics.Roll($"surgeryRelaxed.{Bot.Info.Personality}", chance);
        }
        if (!_relaxedRollHit)
        {
            return false;
        }
        _relaxedActive = true;
        if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"heal.surgery.relaxed.{Bot.Info.Personality}");
        return true;
    }

    /// <summary>Shortest path to a living known enemy (0 when one is in sight); quietOk = none seen or heard within quiet s.</summary>
    private float NearestPath(float quiet, out bool quietOk)
    {
        quietOk = true;
        float nearest = float.MaxValue;
        foreach (Enemy enemy in Bot.EnemyController.KnownEnemies)
        {
            if (enemy.IsVisible)
            {
                quietOk = false;
                return 0f;
            }
            if ((enemy.Seen && enemy.TimeSinceSeen < quiet) || enemy.TimeSinceLastKnownUpdated < quiet)
            {
                quietOk = false;
            }
            nearest = Mathf.Min(nearest, enemy.Path.PathLength);
        }
        return nearest;
    }

    private bool _relaxedActive;
    private bool _relaxedRollHit;
    private float _relaxedRollUntil;

    private bool CheckEnemies()
    {
        if (_nextCheckEnemiesTime < Time.time)
        {
            _nextCheckEnemiesTime = Time.time + 0.1f;
            const float minPathDist = 80f;
            const float minTimeSinceLastKnown = 60f;
            const float minTimeSinceSeen = 60f;

            _allClear = true;
            EnemyList enemies = Bot.EnemyController.KnownEnemies;
            foreach (Enemy enemy in enemies)
            {
                if (enemy.IsVisible)
                {
                    _allClear = false;
                    break;
                }
                if (enemy.Seen && enemy.TimeSinceSeen < minTimeSinceSeen)
                {
                    _allClear = false;
                    break;
                }
                if (enemy.TimeSinceLastKnownUpdated < minTimeSinceLastKnown)
                {
                    _allClear = false;
                    break;
                }
                if (enemy.Path.PathLength < minPathDist)
                {
                    _allClear = false;
                    break;
                }
            }
        }
        return _allClear;
    }

    private bool checkEnemies(float minPathDist, float minTimeSinceLastKnown)
    {
        bool allClear = true;
        var enemies = Bot.EnemyController.Enemies;
        foreach (var enemy in enemies.Values)
        {
            if (!checkThisEnemy(enemy, minPathDist, minTimeSinceLastKnown))
            {
                allClear = false;
                break;
            }
        }
        return allClear;
    }

    private bool checkThisEnemy(Enemy enemy, float minPathDist, float minTimeSinceLastKnown)
    {
        if (enemy?.EnemyPlayer?.HealthController.IsAlive == true && (enemy.Seen || enemy.Heard) && enemy.TimeSinceLastKnownUpdated < 360f)
        {
            if (enemy.IsVisible)
            {
                return false;
            }
            if (enemy.TimeSinceLastKnownUpdated < minTimeSinceLastKnown)
            {
                return false;
            }
            if (enemy.Path.PathLength < minPathDist)
            {
                return false;
            }
        }
        return true;
    }

    private bool _allClear;
    private float _nextCheckEnemiesTime;
}
