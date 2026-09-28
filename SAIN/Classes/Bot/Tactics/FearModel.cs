using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using SAIN.Components;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SAIN.SAINComponent.Classes.EnemyClasses;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork: fear (user 2026-09-29: "like the per-personality dumbness, put in fear - running away and such").
/// 0-1 per bot, from its personality plus what just happened:
///   base          Coward 0.35, Timmy / Rat 0.2, Normal / SnappingTurtle 0.1, Chad 0.05, GigaChad / Wreckless 0
///   squadmate died within 40m in the last 30s   +0.3
///   the player is on a streak (his kills in the last 5 min, vs him only)   +0.05 each, max +0.25
///   hurt          +0.25 x (1 - health)
///   outnumbered   +0.15 (more enemies around him than mates on him)
///   heavily suppressed   +0.1
/// The situational part is scaled by bravery (Wreckless x0.3, GigaChad x0.4, Chad x0.6, Normal x1, Rat x1.2, Timmy x1.3,
/// Coward x1.5) and the F6 multiplier. Utilities: fear pushes toward falling back / holding and away from pushing; above
/// 0.75 it's panic (falling back gets a big bonus). Shown as "fear 0.45 (...)" in the decision logs.
/// </summary>
public static class FearModel
{
    private static readonly Dictionary<EPersonality, float> _base = new()
    {
        { EPersonality.Coward, 0.35f },
        { EPersonality.Timmy, 0.2f },
        { EPersonality.Rat, 0.2f },
        { EPersonality.Normal, 0.1f },
        { EPersonality.SnappingTurtle, 0.1f },
        { EPersonality.Chad, 0.05f },
        { EPersonality.GigaChad, 0f },
        { EPersonality.Wreckless, 0f },
    };

    private static readonly Dictionary<EPersonality, float> _sensitivity = new()
    {
        { EPersonality.Wreckless, 0.3f },
        { EPersonality.GigaChad, 0.4f },
        { EPersonality.Chad, 0.6f },
        { EPersonality.Normal, 1f },
        { EPersonality.SnappingTurtle, 0.9f },
        { EPersonality.Rat, 1.2f },
        { EPersonality.Timmy, 1.3f },
        { EPersonality.Coward, 1.5f },
    };

    private static readonly List<(int group, Vector3 pos, float time)> _deaths = new();
    private static readonly List<float> _playerKills = new();

    public static void OnBotDied(BotComponent bot)
    {
        var group = bot?.BotOwner?.BotsGroup;
        if (group == null)
        {
            return;
        }
        _deaths.Add((RuntimeHelpers.GetHashCode(group), bot.Position, Time.time));
        if (_deaths.Count > 64)
        {
            _deaths.RemoveAt(0);
        }
    }

    public static void OnPlayerKill()
    {
        _playerKills.Add(Time.time);
        if (_playerKills.Count > 32)
        {
            _playerKills.RemoveAt(0);
        }
    }

    public static float Fear(BotComponent bot, Enemy enemy, float health, bool outnumbered, out string why)
    {
        why = string.Empty;
        var settings = GlobalSettingsClass.Instance?.General?.CloseCombat;
        if (settings == null || !settings.Fear)
        {
            return 0f;
        }
        var p = bot.Info.Personality;
        float baseFear = _base.TryGetValue(p, out float b) ? b : 0.1f;
        float sens = _sensitivity.TryGetValue(p, out float s) ? s : 1f;
        var sb = new StringBuilder();
        float situ = 0f;
        float time = Time.time;
        var group = bot.BotOwner?.BotsGroup;
        if (group != null)
        {
            int gid = RuntimeHelpers.GetHashCode(group);
            foreach (var d in _deaths)
            {
                if (d.group == gid && time - d.time < 30f && (d.pos - bot.Position).sqrMagnitude < 40f * 40f)
                {
                    situ += 0.3f;
                    sb.Append($"mate died {time - d.time:0}s ago; ");
                    break;
                }
            }
        }
        if (enemy != null && !enemy.IsAI)
        {
            int streak = 0;
            foreach (float k in _playerKills)
            {
                if (time - k < 300f)
                {
                    streak++;
                }
            }
            if (streak > 0)
            {
                situ += Mathf.Min(0.05f * streak, 0.25f);
                sb.Append($"player on a {streak}-kill streak; ");
            }
        }
        if (health < 1f)
        {
            situ += 0.25f * (1f - health);
        }
        if (outnumbered)
        {
            situ += 0.15f;
            sb.Append("outnumbered; ");
        }
        if (bot.Suppression.IsHeavySuppressed)
        {
            situ += 0.1f;
            sb.Append("pinned down; ");
        }
        float fear = Mathf.Clamp01((baseFear + situ * sens) * settings.FearMultiplier);
        if (fear > 0.2f)
        {
            why = $"fear {fear:0.00}{(fear > 0.75f ? " PANIC" : "")} ({p} base {baseFear:0.00}; {sb})";
        }
        return fear;
    }

    public static void Clear()
    {
        _deaths.Clear();
        _playerKills.Clear();
    }
}
