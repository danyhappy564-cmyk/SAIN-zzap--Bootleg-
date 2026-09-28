using System.Collections.Generic;
using EFT;
using SAIN.Components;
using SAIN.Models.Enums;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.GlobalSettings.Categories.General;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.Classes.WeaponFunction;
using UnityEngine;
using UnityEngine.AI;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork: corner chase. In a close fight the enemy breaks line of sight around a corner (user: "he jumps out to the
/// right"). Vanilla SAIN mostly stayed put or walked the path hugging the wall, muzzle first. Now pushy bots follow him
/// (RushEnemy decision, reason "cornerChase") and take the corner one of four ways, picked once per lost-sight episode:
///   Prefire  - only when sure he's right at the corner (last seen under 2.5s ago or heard there) AND the magazine has
///              enough rounds left to fire and still fight (no reloading once inside): shoots the corner while stepping out.
///   JumpShot - bunny-hop around the corner if there is headroom (GigaChad/Chad/Wreckless).
///   LeanIn   - hold the lean toward the corner side (E for a right corner) and walk in.
///   Pie      - swing wide first (keep distance from the wall) and slice the corner slowly with the lean held.
/// F6: General > Close Combat (zzap) > Corner Chase. Logged as [Chase].
/// </summary>
public sealed class CornerChase(BotComponent bot)
{
    public enum EStyle
    {
        None,
        Prefire,
        JumpShot,
        LeanIn,
        Pie,
    }

    private readonly BotComponent Bot = bot;
    private float _episodeSeenTime = -1f;
    private EStyle _style;
    private LeanSetting _side;
    private bool _jumped;
    private float _prefireStart = -1f;
    private bool _prefireDone;
    private Vector3? _widePoint;
    private bool _wideReached;

    public EStyle Style
    {
        get { return _style; }
    }

    /// <summary>Where the bot should look while the chase drives it (the corner, or the spot it prefires into).</summary>
    public Vector3? LookPoint { get; private set; }

    private static CloseCombatSettings Settings
    {
        get { return GlobalSettingsClass.Instance?.General?.CloseCombat; }
    }

    private static readonly Dictionary<EPersonality, float> _chaseChance = new()
    {
        { EPersonality.GigaChad, 90f },
        { EPersonality.Chad, 75f },
        { EPersonality.Wreckless, 85f },
        { EPersonality.Normal, 40f },
    };

    private static readonly Dictionary<string, (float seenTime, bool chase)> _rolls = new();

    /// <summary>
    /// Decision hook (EnemyDecisionClass.shallRushEnemy): the enemy was fought close and just broke sight. Rolled once per
    /// lost-sight episode per bot (personality chance); stays on until the window runs out or the enemy shows up again.
    /// </summary>
    public static bool ShallChase(BotComponent bot, Enemy enemy, out string reason)
    {
        reason = string.Empty;
        var settings = Settings;
        if (settings == null || !settings.CornerChase || enemy == null || enemy.IsVisible || !enemy.Seen)
        {
            return false;
        }
        float sinceSeen = enemy.TimeSinceSeen;
        if (sinceSeen < 0.3f || sinceSeen > settings.CornerChaseWindow)
        {
            return false;
        }
        if (enemy.Path.PathLength > settings.CornerChaseMaxDistance || bot.BotOwner.Memory.IsUnderFire)
        {
            return false;
        }
        if (bot.Decision.CurrentSelfDecision != ESelfActionType.None)
        {
            return false;
        }
        if (!_chaseChance.TryGetValue(bot.Info.Personality, out float chance))
        {
            return false;
        }
        float seenTime = Time.time - sinceSeen;
        string id = bot.ProfileId;
        if (!_rolls.TryGetValue(id, out var roll) || Mathf.Abs(roll.seenTime - seenTime) > 0.2f)
        {
            roll = (seenTime, Random.value * 100f < chance);
            _rolls[id] = roll;
            TacticDiagnostics.Count(roll.chase ? $"chase.start.{bot.Info.Personality}" : "chase.rollFailed");
        }
        if (roll.chase)
        {
            reason = "cornerChase";
        }
        return roll.chase;
    }

    public void Reset()
    {
        _episodeSeenTime = -1f;
        _style = EStyle.None;
        LookPoint = null;
        _widePoint = null;
        if (_prefireStart > 0f)
        {
            Bot.ManualShoot.Reset();
        }
        _prefireStart = -1f;
    }

    /// <summary>
    /// Every frame while the enemy is out of sight in RushEnemyAction. Returns true while it drives the movement itself
    /// (walking to the wide pie point) so the action skips its own re-path.
    /// </summary>
    public bool Tick(Enemy enemy)
    {
        var settings = Settings;
        LookPoint = null;
        if (settings == null || !settings.CornerChase || enemy == null || !enemy.Seen || Bot.Player?.HealthController?.IsAlive != true)
        {
            return false;
        }
        float seenTime = Time.time - enemy.TimeSinceSeen;
        if (_episodeSeenTime < 0f || Mathf.Abs(seenTime - _episodeSeenTime) > 0.2f)
        {
            Reset();
            _episodeSeenTime = seenTime;
            PickStyle(settings, enemy);
        }
        Vector3? lastKnown = enemy.LastKnownPosition;
        Vector3? cornerOpt = enemy.VisiblePathPoint ?? lastKnown;
        if (cornerOpt == null)
        {
            return false;
        }
        Vector3 corner = cornerOpt.Value;
        float cornerDist = (corner - Bot.Position).magnitude;
        if (cornerDist > 7f)
        {
            return false;
        }
        LeanSetting side = Bot.Mover.Lean.FindLeanFromBlindCornerAngle(enemy);
        if (side != LeanSetting.None)
        {
            _side = side;
        }
        LookPoint = corner + Bot.Steering.WeaponRootOffset;

        // Only the jump shot keeps sprinting into the corner.
        if (_style != EStyle.JumpShot && Bot.Mover.Running)
        {
            Bot.Mover.ActivePath?.RequestEndSprint(ESprintUrgency.None, "corner chase");
        }
        if (_style != EStyle.JumpShot && _side != LeanSetting.None)
        {
            Bot.Mover.Lean.FastLean(_side);
            Bot.Mover.Lean.HoldLean(0.3f);
        }

        switch (_style)
        {
            case EStyle.Pie:
                Bot.Mover.SetTargetMoveSpeed(settings.CornerChasePieSpeed);
                return TickPie(corner);

            case EStyle.LeanIn:
                Bot.Mover.SetTargetMoveSpeed(0.7f);
                return false;

            case EStyle.JumpShot:
                Bot.Mover.SetTargetMoveSpeed(1f);
                if (!_jumped && cornerDist < 1.8f && Bot.Player.MovementContext.IsGrounded && Bot.Mover.TryJump())
                {
                    _jumped = true;
                    TacticDiagnostics.Count("chase.jumpShot.jumped");
                }
                return false;

            case EStyle.Prefire:
                Bot.Mover.SetTargetMoveSpeed(0.6f);
                if (lastKnown != null && cornerDist < 4f)
                {
                    Vector3 target = lastKnown.Value + Vector3.up * 1.2f;
                    LookPoint = target;
                    TickPrefire(settings, enemy, target);
                }
                return false;
        }
        return false;
    }

    private void PickStyle(CloseCombatSettings settings, Enemy enemy)
    {
        _jumped = false;
        _prefireDone = false;
        _wideReached = false;
        _side = Bot.Mover.Lean.FindLeanFromBlindCornerAngle(enemy);
        EPersonality personality = Bot.Info.Personality;
        bool pusher = personality == EPersonality.GigaChad || personality == EPersonality.Chad || personality == EPersonality.Wreckless;

        bool confident = false;
        Vector3? lastKnown = enemy.LastKnownPosition;
        Vector3? corner = enemy.VisiblePathPoint;
        if (lastKnown != null && corner != null && (lastKnown.Value - corner.Value).magnitude < 3f)
        {
            var hearing = enemy.Hearing;
            bool heardThere =
                hearing != null && Time.time - hearing.LastHeardSoundTime < 2f && (hearing.LastHeardSoundPosition - corner.Value).magnitude < 4f;
            confident = enemy.TimeSinceSeen < 2.5f || heardThere;
        }
        SAINBotSuppressClass.CalcAmmoRatio(Bot.BotOwner, out int rounds);
        int max = Bot.BotOwner.WeaponManager?.Reload?.MaxBulletCount ?? 0;
        int needed = Mathf.Max(Mathf.RoundToInt(settings.CornerChasePrefireMinRounds), Mathf.CeilToInt(max * 0.4f));
        bool ammoOk = rounds >= needed;
        bool headroom = !Physics.Raycast(Bot.Position + Vector3.up * 1.7f, Vector3.up, 0.7f, LayersMaskController.HighPolyWithTerrainMask);

        if (confident && ammoOk && Random.value * 100f < settings.CornerChasePrefireChance)
        {
            _style = EStyle.Prefire;
        }
        else if (pusher && headroom && Random.value * 100f < settings.CornerChaseJumpChance)
        {
            _style = EStyle.JumpShot;
        }
        else if (personality == EPersonality.GigaChad || personality == EPersonality.Wreckless || Random.value < 0.5f)
        {
            _style = EStyle.LeanIn;
        }
        else
        {
            _style = EStyle.Pie;
        }
        TacticDiagnostics.Count($"chase.style.{_style}");
        TacticDiagnostics.LogCloseCombat(
            $"[Chase] [{Bot.name}] [{personality}] enemy broke sight {enemy.TimeSinceSeen:0.0}s ago, corner {(corner != null ? (corner.Value - Bot.Position).magnitude : -1f):0.0}m {_side} -> {_style} "
                + $"(sure he's there {confident}, mag {rounds}/{max} need {needed}, headroom {headroom})"
        );
    }

    private bool TickPie(Vector3 corner)
    {
        if (_wideReached || _side == LeanSetting.None)
        {
            return false;
        }
        if (_widePoint == null)
        {
            // Swing to the side away from the wall, a bit short of the corner, then slice in.
            Vector3 toCorner = corner - Bot.Position;
            toCorner.y = 0f;
            if (toCorner.sqrMagnitude < 0.25f)
            {
                _wideReached = true;
                return false;
            }
            toCorner.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, toCorner);
            Vector3 away = _side == LeanSetting.Right ? -right : right;
            Vector3 wanted = corner - toCorner * 1.2f + away * 1.8f;
            if (NavMesh.SamplePosition(wanted, out NavMeshHit hit, 0.8f, -1) && Bot.Mover.WalkToPoint(hit.position))
            {
                _widePoint = hit.position;
                TacticDiagnostics.Count("chase.pie.wide");
            }
            else
            {
                _wideReached = true;
                return false;
            }
        }
        if ((_widePoint.Value - Bot.Position).magnitude < 0.6f)
        {
            _wideReached = true;
            return false;
        }
        return true;
    }

    private void TickPrefire(CloseCombatSettings settings, Enemy enemy, Vector3 target)
    {
        if (_prefireDone)
        {
            return;
        }
        float ratio = SAINBotSuppressClass.CalcAmmoRatio(Bot.BotOwner, out int rounds);
        int max = Bot.BotOwner.WeaponManager?.Reload?.MaxBulletCount ?? 0;
        // Keep enough in the magazine to win the fight that follows.
        int reserve = Mathf.Max(5, Mathf.CeilToInt(max * 0.3f));
        if (rounds <= reserve || ratio <= 0f || (_prefireStart > 0f && Time.time - _prefireStart > 1.5f))
        {
            _prefireDone = true;
            Bot.ManualShoot.Reset();
            TacticDiagnostics.LogCloseCombat($"[Chase] [{Bot.name}] prefire stop: mag {rounds}/{max} (keeps {reserve})");
            return;
        }
        if (Bot.ManualShoot.TryShoot(enemy, target, true, EShootReason.Suppress) && _prefireStart < 0f)
        {
            _prefireStart = Time.time;
            TacticDiagnostics.Count("chase.prefire.fired");
        }
    }
}
