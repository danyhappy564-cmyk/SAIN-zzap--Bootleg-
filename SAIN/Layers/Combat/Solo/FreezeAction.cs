using DrakiaXYZ.BigBrain.Brains;
using EFT;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.GlobalSettings.Categories.General;
using SAIN.Preset.Shared.Enums;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.Classes.Tactics;
using UnityEngine;
using UnityEngine.AI;

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
        _peekSpot = null;
        _peekTries = 0;
        _nextPeekCheck = Time.time + 0.3f;
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

    // zzap, 10th sim screenshots (user): bots holding a corner crouched deep behind the wall, muzzle on the wall - they
    // turned toward the angle but never stepped to where the angle can be seen ("should be at the corner edge"). Holding
    // still is the point of the freeze, so the bot only moves when the line from its gun to the aim point is blocked, and
    // then just far enough (the first spot on the way to the corner where the line opens = least exposure).
    private Vector3? _peekSpot;
    private float _peekMoveUntil;
    private float _nextPeekCheck;
    private int _peekTries;

    private const float PEEK_STEP = 0.3f;

    private void CheckPeekSpot(Vector3 corner, Vector3 aim, FreezeAmbushSettings settings)
    {
        if (!settings.StepToPeekSpot || _peekSpot != null || _peekTries >= 3 || Time.time < _nextPeekCheck)
        {
            return;
        }
        _nextPeekCheck = Time.time + 1.5f;
        Vector3 gun = Bot.Transform.WeaponRoot;
        int mask = LayersMaskController.HighPolyWithTerrainMask;
        if (!Physics.Linecast(gun, aim, mask))
        {
            return;
        }
        _peekTries++;
        Vector3 from = Bot.Position;
        float gunUp = gun.y - from.y;
        Vector3 dir = corner - from;
        dir.y = 0f;
        float toCorner = dir.magnitude;
        if (toCorner < 0.9f)
        {
            return;
        }
        dir /= toCorner;
        float max = Mathf.Min(settings.PeekSpotMaxStep, toCorner - 0.6f);
        for (float step = PEEK_STEP; step <= max + 0.01f; step += PEEK_STEP)
        {
            if (!NavMesh.SamplePosition(from + dir * step, out NavMeshHit hit, 0.4f, -1))
            {
                continue;
            }
            Vector3 spot = hit.position;
            if (NavMesh.Raycast(from, spot, out _, -1))
            {
                break;
            }
            if (Physics.Linecast(spot + Vector3.up * gunUp, aim, mask))
            {
                continue;
            }
            _peekSpot = spot;
            _peekMoveUntil = Time.time + 4f;
            if (TacticDiagnostics.CountOn) TacticDiagnostics.Count("freeze.peekStep");
            if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat(
                $"[Freeze] [{Bot.name}] [{Bot.Info.Personality}] wall between gun and the angle -> stepping {(spot - from).magnitude:0.0}m toward the corner edge to see past it");
            return;
        }
        if (TacticDiagnostics.CountOn) TacticDiagnostics.Count("freeze.peekStepNone");
    }

    private void TickPeekStep()
    {
        if (_peekSpot == null)
        {
            return;
        }
        Vector3 spot = _peekSpot.Value;
        Vector3 d = spot - Bot.Position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.25f * 0.25f || Time.time > _peekMoveUntil || !Bot.Mover.WalkToPoint(spot, true, 0.2f))
        {
            _peekSpot = null;
            Bot.Mover.Stop();
            _nextPeekCheck = Time.time + 1f;
            return;
        }
        Bot.Mover.SetTargetMoveSpeed(0.45f);
    }
}
