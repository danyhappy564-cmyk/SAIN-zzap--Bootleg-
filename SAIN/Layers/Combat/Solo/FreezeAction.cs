using DrakiaXYZ.BigBrain.Brains;
using EFT;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.GlobalSettings.Categories.General;
using SAIN.Preset.Shared.Enums;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.Classes.Tactics;
using UnityEngine;

namespace SAIN.Layers.Combat.Solo;

internal class FreezeAction(BotOwner bot) : BotAction(bot, nameof(FreezeAction)), IBotAction
{
    public override void Update(CustomLayer.ActionData data)
    {
        Enemy Enemy = Bot.GoalEnemy;
        Bot.Mover.Pose.SetPoseToCover(Enemy);
        TickPeekStep();
    }

    public override void Start()
    {
        base.Start();
        Bot.Mover.Stop();
        _startTime = Time.time;
        _cornerLogged = false;
        _peekAim = null;
        Peek.Reset();
    }

    /// <summary>
    /// Logs how the ambush ended: the next combat decision says whether the enemy walked into view
    /// (StandAndShoot), the wait ran out (Search / MoveToEngage / SeekCover) or something else took over.
    /// </summary>
    public override void Stop()
    {
        base.Stop();
        ECombatDecision next = Bot.Decision.CurrentCombatDecision;
        float held = Time.time - _startTime;
        bool timeUp = Time.time >= Bot.Decision.EnemyDecisions.TimeToUnfreeze;
        string how = next == ECombatDecision.StandAndShoot ? "enemySpotted" : timeUp ? "timeUp" : $"interrupted({next})";
        if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"freeze.end.{how.Split('(')[0]}");
        if (GlobalSettingsClass.Instance.General.FreezeAmbush.DiagnosticLogs)
        {
            Logger.LogWarning($"[Freeze] [{Bot.name}] [{Bot.Info.Personality}] END ambush: {how} after {held:0.0}s (next={next})");
        }
    }

    private float _startTime;
    private bool _cornerLogged;

    /// <summary>
    /// zzap: an ambushing bot aims at the corner the enemy has to come around (the last visible point
    /// on the path to them) rather than SAIN's default look, like a camper holding the entry angle.
    /// Toggle: F6 General > Freeze Ambush (zzap) > Watch Approach Corner.
    /// </summary>
    public override void OnSteeringTicked()
    {
        Enemy enemy = Bot.GoalEnemy;
        if (TryShootAnyTarget(enemy))
        {
            Bot.Steering.SteerByPriority(enemy, false);
            return;
        }
        var settings = GlobalSettingsClass.Instance.General.FreezeAmbush;
        Vector3? corner = enemy?.VisiblePathPoint;
        bool watchCorner = corner != null && settings.WatchApproachCorner;
        if (!_cornerLogged)
        {
            _cornerLogged = true;
            string what = watchCorner
                ? $"watching approach corner {(corner.Value - Bot.Position).magnitude:0.0}m away"
                : settings.WatchApproachCorner ? "no approach corner known, default look" : "corner watch off, default look";
            TacticDiagnostics.Count(watchCorner ? "freeze.cornerWatch" : "freeze.defaultLook");
            if (settings.DiagnosticLogs)
            {
                Logger.LogWarning($"[Freeze] [{Bot.name}] [{Bot.Info.Personality}] {what}");
            }
        }
        if (watchCorner)
        {
            Vector3 aim = SAIN.SAINComponent.Classes.Mover.SAINSteeringClass.PastCorner(Bot.Transform.WeaponRoot, corner.Value, enemy.KnownPlaces.LastKnownPosition, Bot.Transform.LookDirection);
            Bot.Steering.LookToPoint(aim);
            CheckPeekSpot(corner.Value, aim, settings);
            return;
        }
        Bot.Steering.SteerByPriority(enemy);
    }

    // zzap, 10th sim screenshots (user): bots holding a corner crouched deep behind the wall, muzzle on the wall - step to
    // the first spot where the angle can be seen (SAIN.SAINComponent.Classes.Mover.PeekSpot).
    private SAIN.SAINComponent.Classes.Mover.PeekSpot _peek;
    private Vector3? _peekAim;
    private Vector3 _peekCorner;

    private SAIN.SAINComponent.Classes.Mover.PeekSpot Peek
    {
        get { return _peek ??= new SAIN.SAINComponent.Classes.Mover.PeekSpot(Bot, "freeze"); }
    }

    private void CheckPeekSpot(Vector3 corner, Vector3 aim, FreezeAmbushSettings settings)
    {
        _peekAim = settings.StepToPeekSpot ? aim : null;
        _peekCorner = corner;
    }

    private void TickPeekStep()
    {
        if (_peekAim == null)
        {
            return;
        }
        Peek.Tick(_peekAim.Value, _peekCorner, GlobalSettingsClass.Instance.General.FreezeAmbush.PeekSpotMaxStep);
    }
}
