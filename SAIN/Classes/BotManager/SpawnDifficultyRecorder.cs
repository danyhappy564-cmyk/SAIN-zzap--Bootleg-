using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SAIN.Preset.Server;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.Preset;
using SPT.Common.Utils;

namespace SAIN.Components.BotController;

/// <summary>
/// zzap (2026-10-23, bot type settings redesign): counts each SAIN bot's type and difficulty during a raid and adds the raid to
/// the server's spawnDifficulties list at raid end (last 5 raids), so the bot type page can show which difficulty slot is used.
/// </summary>
public static class SpawnDifficultyRecorder
{
    private static readonly Dictionary<string, Dictionary<string, int>> _counts = new();
    private static bool _warned;

    public static void Count(ESainWildSpawnType role, ESainBotDifficulty difficulty)
    {
        string name = role.ToString();
        if (!_counts.TryGetValue(name, out var byDifficulty))
        {
            _counts[name] = byDifficulty = new Dictionary<string, int>();
        }
        string key = difficulty.ToString();
        byDifficulty.TryGetValue(key, out int n);
        byDifficulty[key] = n + 1;
    }

    /// <summary>Raid end: hand this raid's counts to a background save (nothing counted = nothing saved).</summary>
    public static void FlushRaid(string map)
    {
        if (_counts.Count == 0)
        {
            return;
        }
        var raid = new SpawnRaidCounts { Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Map = map };
        foreach (var pair in _counts)
        {
            raid.Counts[pair.Key] = new Dictionary<string, int>(pair.Value);
        }
        _counts.Clear();
        Task.Run(() =>
        {
            try
            {
                var list = SpawnDifficultyRecord.Append(Read(), raid);
                ServerDataClient.Save(SpawnDifficultyRecord.DataKey, Json.Serialize(list));
            }
            catch (Exception ex)
            {
                if (!_warned)
                {
                    _warned = true;
                    Logger.LogWarning($"[SAIN BotTypes] spawn difficulty record not saved: {ex.Message}");
                }
            }
        });
    }

    /// <summary>The saved raids; empty when there is no record yet or it can't be read.</summary>
    public static List<SpawnRaidCounts> Load()
    {
        try
        {
            return Read();
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[SAIN BotTypes] spawn difficulty record not read: {ex.Message}");
            return new List<SpawnRaidCounts>();
        }
    }

    private static List<SpawnRaidCounts> Read()
    {
        string json = ServerDataClient.Get(SpawnDifficultyRecord.DataKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<SpawnRaidCounts>();
        }
        try
        {
            return Json.Deserialize<List<SpawnRaidCounts>>(json) ?? new List<SpawnRaidCounts>();
        }
        catch
        {
            return new List<SpawnRaidCounts>(); // a broken file reads as "no record"; the next raid end writes a good one
        }
    }
}
