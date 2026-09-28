using DrakiaXYZ.BigBrain.Brains;
using EFT;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.Classes.Tactics;
using SAIN.SAINComponent.Classes.WeaponFunction;
using UnityEngine;

namespace SAIN.Layers.Combat.Squad;

/// <summary>
/// zzap fork: post-combat tidy-up (ESquadDecision.PostCombat, SAIN squad layer 22). Fills the gap between the SAIN combat
/// layer ending and ORBIT taking over - ORBIT waits 15s after "SAIN : Combat Layer" goes inactive, and in between the bot
/// fell to BSG's vanilla layers. Runs in the SQUAD layer on purpose: ORBIT's 15s timer only watches the combat layer, so
/// this doesn't delay the handoff. For up to 14s: top the magazine up (under 70%), rejoin the squad leader if 25m+ away,
/// otherwise hold half-crouched with the gun on the last threat, glancing around it. Any enemy seen = normal combat again.
/// </summary>
internal class PostCombatAction(BotOwner bot) : BotAction(bot, nameof(PostCombatAction)), IBotAction
{
    private bool _reloadTried;
    private float _nextMove;
    private float _nextGlance;
    private Vector3 _glanceOffset;
    private Vector3? _threat;

    public override void Start()
    {
        base.Start();
        _reloadTried = false;
        _nextMove = 0f;
        Enemy enemy = Bot.GoalEnemy;
        _threat = enemy?.KnownPlaces.LastKnownPosition;
        if (_threat == null && Bot.Memory.UnderFireFromPosition != Vector3.zero)
        {
            _threat = Bot.Memory.UnderFireFromPosition;
        }
        TacticDiagnostics.Count("postCombat.start");
        TacticDiagnostics.LogCloseCombat(
            $"[PostCombat] [{Bot.name}] [{Bot.Info.Personality}] combat over -> tidy up until ORBIT takes over (ammo {SAINBotSuppressClass.CalcAmmoRatio(BotOwner, out _):P0}, "
                + $"leader {(Bot.Squad.IAmLeader || Bot.Squad.LeaderComponent == null ? "-" : $"{(Bot.Squad.LeaderComponent.Position - Bot.Position).magnitude:0}m")}, "
                + $"watching {(_threat != null ? $"last threat {(_threat.Value - Bot.Position).magnitude:0}m" : "around")})"
        );
    }

    public override void Update(CustomLayer.ActionData data)
    {
        if (!_reloadTried && BotOwner.WeaponManager?.Reload?.Reloading != true)
        {
            _reloadTried = true;
            if (SAINBotSuppressClass.CalcAmmoRatio(BotOwner, out _) < 0.7f && Bot.Decision.SelfActionDecisions.ReloadNow())
            {
                TacticDiagnostics.Count("postCombat.reload");
            }
        }
        var leader = Bot.Squad.LeaderComponent;
        if (!Bot.Squad.IAmLeader && leader != null && !leader.IsDead && (leader.Position - Bot.Position).magnitude > 25f)
        {
            if (_nextMove < Time.time)
            {
                _nextMove = Time.time + 1f;
                if (Bot.Mover.WalkToPoint(leader.Position))
                {
                    TacticDiagnostics.Count("postCombat.regroup");
                }
            }
            Bot.Mover.SetTargetPose(1f);
            Bot.Mover.SetTargetMoveSpeed(0.8f);
            return;
        }
        Bot.Mover.Stop();
        Bot.Mover.SetTargetPose(0.75f);
    }

    public override void OnSteeringTicked()
    {
        Enemy enemy = Bot.GoalEnemy;
        if (Shoot.ShootAnyVisibleEnemies(enemy))
        {
            return;
        }
        if (Bot.Mover.Moving)
        {
            Bot.Steering.LookToMovingDirection();
            return;
        }
        if (_threat != null)
        {
            if (_nextGlance < Time.time)
            {
                // Gun on the last threat, now and then a glance a bit to either side of it.
                _nextGlance = Time.time + Random.Range(1.5f, 3f);
                Vector3 to = _threat.Value - Bot.Position;
                to.y = 0f;
                Vector3 side = Vector3.Cross(Vector3.up, to.normalized);
                _glanceOffset = Random.value < 0.5f ? Vector3.zero : side * Random.Range(-0.5f, 0.5f) * to.magnitude;
            }
            Bot.Steering.LookToPoint(_threat.Value + _glanceOffset + Vector3.up * 1.3f);
            return;
        }
        Bot.Steering.LookToRandomPosition();
    }
}
