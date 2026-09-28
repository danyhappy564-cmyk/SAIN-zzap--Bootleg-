using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.Classes.Tactics;
using UnityEngine;

namespace SAIN.Components.BotControllerSpace.Classes;

/// <summary>
/// zzap fork: learn which responses actually work against THIS player (user 2026-09-29: "the player's gear may be
/// overpowered, so don't judge him by gear - learn from outcomes: this kind of response won more often, so use it").
/// Every utility decision a bot takes against the human player opens an episode (stance + situation: in sight or not,
/// range bucket, own health). Within the episode: the bot hitting the player = a win, the player killing the bot = a loss.
/// Stats persist per player profile (PlayerStyle/&lt;profile&gt;.learned.json) across raids. When adaptation is on,
/// each stance gets a bonus from how much better (or worse) it did than that family's average, scaled by how many tries
/// back it (confidence) and the F6 strength. Recording always runs (also in the TEST preset); only the bonus needs Adapt on.
/// With adaptation off the utilities use the plain situation calculation.
/// </summary>
public static class PlayerOutcomeLearner
{
    private const float EPISODE_TIME = 6f;

    public sealed class Stat
    {
        public float N;
        public float Hits;
        public float Deaths;
    }

    private sealed class Episode
    {
        public string Key;
        public float End;
        public bool Hit;
        public bool Died;
    }

    private static Dictionary<string, Stat> _stats = new();
    private static readonly Dictionary<string, Episode> _episodes = new();
    private static string _file;
    private static string _playerId;
    private static int _episodesThisRaid;

    public static void Load(string dir, string playerProfileId)
    {
        _playerId = playerProfileId;
        _episodes.Clear();
        _episodesThisRaid = 0;
        _file = Path.Combine(dir, $"{playerProfileId}.learned.json");
        try
        {
            _stats = File.Exists(_file) ? JsonConvert.DeserializeObject<Dictionary<string, Stat>>(File.ReadAllText(_file)) ?? new() : new();
        }
        catch (Exception ex)
        {
            _stats = new();
            Logger.LogWarning($"[Learn] could not read {_file}: {ex.Message}");
        }
        float n = 0f;
        foreach (var s in _stats.Values)
        {
            n += s.N;
        }
        Logger.LogWarning($"[Learn] {_stats.Count} situations / {n:0} past responses vs this player loaded from {_file}");
    }

    public static void Save(string why)
    {
        if (_file == null)
        {
            return;
        }
        ResolveAll(float.MaxValue);
        try
        {
            File.WriteAllText(_file, JsonConvert.SerializeObject(_stats, Formatting.Indented));
            Logger.LogWarning($"[Learn] saved ({why}): {_episodesThisRaid} responses this raid, {_stats.Count} situations -> {_file}");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[Learn] could not save: {ex.Message}");
        }
    }

    public static string Key(string family, string stance, float dist, BotComponent bot)
    {
        string range = dist < 15f ? "close" : dist < 40f ? "mid" : "far";
        string hp = bot.Memory.Health.HealthStatus == ETagStatus.Healthy ? "ok" : "hurt";
        return $"{family}:{stance}:{range}:{hp}";
    }

    private static bool IsPlayer(Enemy enemy)
    {
        return enemy != null && !enemy.IsAI && _playerId != null && enemy.EnemyProfileId == _playerId;
    }

    /// <summary>A bot committed to a utility stance against the player.</summary>
    public static void Begin(BotComponent bot, Enemy enemy, string key)
    {
        if (!IsPlayer(enemy) || GlobalSettingsClass.Instance?.General?.PlayerStyle?.Enabled != true)
        {
            return;
        }
        float time = Time.time;
        if (_episodes.TryGetValue(bot.ProfileId, out Episode ep))
        {
            if (ep.Key == key && time <= ep.End)
            {
                ep.End = time + EPISODE_TIME;
                return;
            }
            Resolve(ep);
        }
        _episodes[bot.ProfileId] = new Episode { Key = key, End = time + EPISODE_TIME };
    }

    /// <summary>The player was hit (PlayerStyleRecorder.OnBeingHit).</summary>
    public static void OnPlayerHit(string attackerProfileId)
    {
        if (attackerProfileId != null && _episodes.TryGetValue(attackerProfileId, out Episode ep) && Time.time <= ep.End + 1f)
        {
            ep.Hit = true;
        }
    }

    /// <summary>A bot died (DoorTacticClass.OnOwnDeath) - counts only when the player killed it.</summary>
    public static void OnBotKilled(string botProfileId, string aggressorProfileId)
    {
        if (aggressorProfileId == null || aggressorProfileId != _playerId)
        {
            return;
        }
        if (_episodes.TryGetValue(botProfileId, out Episode ep) && Time.time <= ep.End + 3f)
        {
            ep.Died = true;
            Resolve(ep);
            _episodes.Remove(botProfileId);
        }
    }

    private static void ResolveAll(float before)
    {
        var done = new List<string>();
        foreach (var kv in _episodes)
        {
            if (kv.Value.End < before)
            {
                Resolve(kv.Value);
                done.Add(kv.Key);
            }
        }
        foreach (string id in done)
        {
            _episodes.Remove(id);
        }
    }

    private static void Resolve(Episode ep)
    {
        if (!_stats.TryGetValue(ep.Key, out Stat s))
        {
            s = new Stat();
            _stats[ep.Key] = s;
        }
        s.N += 1f;
        if (ep.Hit)
        {
            s.Hits += 1f;
        }
        if (ep.Died)
        {
            s.Deaths += 1f;
        }
        _episodesThisRaid++;
        TacticDiagnostics.Count(ep.Died ? "learn.loss" : ep.Hit ? "learn.win" : "learn.neutral");
    }

    /// <summary>
    /// Learned bonus for this stance in this situation against the player: (its result rate - the family's average) x
    /// confidence (tries / (tries + 8)) x strength, capped at +-0.25. 0 when adaptation is off or too few tries.
    /// </summary>
    public static float Bonus(BotComponent bot, Enemy enemy, string key)
    {
        var settings = GlobalSettingsClass.Instance?.General?.PlayerStyle;
        if (settings == null || !settings.AdaptEnabled || !settings.LearnFromOutcomes || !IsPlayer(enemy))
        {
            return 0f;
        }
        if (Time.time > _nextResolve)
        {
            _nextResolve = Time.time + 2f;
            ResolveAll(Time.time);
        }
        if (!_stats.TryGetValue(key, out Stat s) || s.N < 3f)
        {
            return 0f;
        }
        string family = key.Substring(0, key.IndexOf(':'));
        float sumN = 0f, sumV = 0f;
        foreach (var kv in _stats)
        {
            if (kv.Key.StartsWith(family + ":"))
            {
                sumN += kv.Value.N;
                sumV += kv.Value.Hits - 1.5f * kv.Value.Deaths;
            }
        }
        float mean = sumN > 0f ? sumV / sumN : 0f;
        float rate = (s.Hits - 1.5f * s.Deaths) / s.N;
        float confidence = s.N / (s.N + 8f);
        return Mathf.Clamp((rate - mean) * 0.5f * confidence * settings.AdaptStrength, -0.25f, 0.25f);
    }

    private static float _nextResolve;
}
