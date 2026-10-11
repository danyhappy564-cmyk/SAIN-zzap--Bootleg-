using System.Collections.Generic;
using SAIN.Components;
using SAIN.Models.Enums;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SAIN.SAINComponent.Classes.EnemyClasses;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap remake5 footstep discipline (user 2026-10-11, after bots started hearing other bots' footsteps 10/9). SAIN already has
/// stealthy search for "sneaky" personalities and after hearing someone from peace, and a per-personality chance to sprint while
/// searching - but nothing looks at how close the enemy is: a searching bot sprinted right up to the enemy's last known spot, and
/// MoveToEngage sprinted to any firing position more than 15 m away. Now, going after a known enemy that isn't in sight:
///   within 30 m of him  no sprinting (walk)
///   within 12 m         crouch-walk (pose 0.6, speed 0.45)
/// A style, so rolled once per bot per enemy (rule 2026-10-02): Rat / SnappingTurtle / Coward always, Timmy 80%, Normal 70%,
/// Chad 50%, GigaChad 30%, Wreckless 15%. A looting enemy is always crept up on. Not while under fire, not for a squad "help"
/// run, not for a sniper the personality always sprints at. 20 s at most per enemy, then normal movement (no stalls).
/// Counters: quiet.start / quiet.timeout / quiet.skip.{why} / quiet.move.{sprint|walk|crouch} (SimLabProbe 2 s samples).
/// </summary>
public static class FootstepDiscipline
{
    public const float WALK_DISTANCE = 30f;
    public const float CROUCH_DISTANCE = 12f;
    private const float MAX_SECONDS = 20f;

    private sealed class State
    {
        public string EnemyId;
        public bool Quiet;
        public float Start = -1f;
        public float LastApplied;
        public bool TimedOut;
        public bool LootBooked;
    }

    private static readonly Dictionary<string, State> _states = new();

    private static float Chance(EPersonality p)
    {
        return p switch
        {
            EPersonality.Rat or EPersonality.SnappingTurtle or EPersonality.Coward => 1f,
            EPersonality.Timmy => 0.8f,
            EPersonality.Normal => 0.7f,
            EPersonality.Chad => 0.5f,
            EPersonality.GigaChad => 0.3f,
            EPersonality.Wreckless => 0.15f,
            _ => 0.6f,
        };
    }

    /// <summary>
    /// True when the bot should move quietly toward this enemy now; speed / pose are the caps to apply (crouch band lower).
    /// Called from SearchClass / SearchAction / MoveToEngageAction every movement update.
    /// </summary>
    public static bool Quiet(BotComponent bot, Enemy enemy, out float speed, out float pose)
    {
        speed = 1f;
        pose = 1f;
        if (bot == null || enemy == null || enemy.IsVisible)
        {
            return false;
        }
        float path = enemy.Path.PathLength;
        if (path <= 0f || path > WALK_DISTANCE)
        {
            return false;
        }
        var owner = bot.BotOwner;
        if (owner?.Memory != null && Time.time - owner.Memory.UnderFireTime < 5f)
        {
            return false;
        }
        if (bot.Decision.CurrentSquadDecision == ESquadDecision.Help)
        {
            return false;
        }
        if (enemy.IsSniper && bot.Info.PersonalitySettings.General.ENEMYSNIPER_ALWAYS_SPRINT_SEARCH)
        {
            return false;
        }
        float time = Time.time;
        string id = bot.ProfileId;
        if (!_states.TryGetValue(id, out State s) || s.EnemyId != enemy.EnemyProfileId || time - s.LastApplied > 30f)
        {
            bool looting = enemy.Status.VulnerableAction == EEnemyAction.Looting;
            s = new State { EnemyId = enemy.EnemyProfileId, Quiet = looting || Random.value < Chance(bot.Info.Personality), Start = time };
            _states[id] = s;
            if (_states.Count > 256)
            {
                _states.Clear();
                _states[id] = s;
            }
            if (TacticDiagnostics.CountOn)
            {
                TacticDiagnostics.Count(s.Quiet ? "quiet.start" : $"quiet.skip.roll{bot.Info.Personality}");
            }
            if (s.Quiet && TacticDiagnostics.LogOn)
            {
                TacticDiagnostics.LogCloseCombat($"[Quiet] [{bot.name}] [{bot.Info.Personality}] quiet approach on {enemy.EnemyName} ({path:0}m path{(looting ? ", he's looting" : "")})");
            }
        }
        s.LastApplied = time;
        if (!s.Quiet || s.TimedOut)
        {
            return false;
        }
        if (time - s.Start > MAX_SECONDS)
        {
            s.TimedOut = true;
            TacticDiagnostics.Count("quiet.timeout");
            if (TacticDiagnostics.LogOn)
            {
                TacticDiagnostics.LogCloseCombat($"[Quiet] [{bot.name}] {MAX_SECONDS:0}s of quiet approach on {enemy.EnemyName} - moving normally again ({path:0}m)");
            }
            return false;
        }
        if (!s.LootBooked && enemy.Status.VulnerableAction == EEnemyAction.Looting)
        {
            s.LootBooked = true;
            TacticDiagnostics.Count("vuln.loot.sneak");
        }
        if (path <= CROUCH_DISTANCE)
        {
            speed = 0.45f;
            pose = 0.6f;
        }
        else
        {
            speed = 0.7f;
            pose = 1f;
        }
        return true;
    }

    /// <summary>Only "no sprint now" (callers that pick sprint separately).</summary>
    public static bool BlockSprint(BotComponent bot, Enemy enemy)
    {
        return Quiet(bot, enemy, out _, out _);
    }

    public static void Clear()
    {
        _states.Clear();
    }
}
