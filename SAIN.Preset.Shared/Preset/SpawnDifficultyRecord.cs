using System;
using System.Collections.Generic;
using SAIN.Preset.Shared.Enums;

namespace SAIN.Preset.Shared.Preset;

/// <summary>One raid's spawned SAIN bots: role name (ESainWildSpawnType) -> difficulty name (ESainBotDifficulty) -> count.</summary>
public sealed class SpawnRaidCounts
{
    public string Time;
    public string Map;
    public Dictionary<string, Dictionary<string, int>> Counts = new();
}

/// <summary>
/// zzap (2026-10-23, bot type settings redesign): which difficulty each bot type really spawned at in the last raids, so the
/// bot type page can say which of the four difficulty slots is in use (user: "can't tell whether a setting took"). The client
/// counts during a raid and stores the list under <see cref="DataKey"/> through /sain/data/save; F6 and the web read it.
/// No JSON here (netstandard2.1): each side serializes these fields itself, as with SimLabModels.
/// </summary>
public static class SpawnDifficultyRecord
{
    public const string DataKey = "spawnDifficulties";
    public const int KeepRaids = 5;

    public static List<SpawnRaidCounts> Append(List<SpawnRaidCounts> existing, SpawnRaidCounts raid)
    {
        var list = existing != null ? new List<SpawnRaidCounts>(existing) : new List<SpawnRaidCounts>();
        list.Add(raid);
        if (list.Count > KeepRaids)
        {
            list.RemoveRange(0, list.Count - KeepRaids);
        }
        return list;
    }

    public static Dictionary<ESainBotDifficulty, int> Totals(List<SpawnRaidCounts> raids, ESainWildSpawnType role)
    {
        var totals = new Dictionary<ESainBotDifficulty, int>();
        if (raids == null)
        {
            return totals;
        }
        string name = role.ToString();
        foreach (var raid in raids)
        {
            if (raid?.Counts == null || !raid.Counts.TryGetValue(name, out var byDifficulty) || byDifficulty == null)
            {
                continue;
            }
            foreach (var pair in byDifficulty)
            {
                if (pair.Value > 0 && Enum.TryParse(pair.Key, true, out ESainBotDifficulty difficulty) && Enum.IsDefined(typeof(ESainBotDifficulty), difficulty))
                {
                    totals.TryGetValue(difficulty, out int n);
                    totals[difficulty] = n + pair.Value;
                }
            }
        }
        return totals;
    }

    /// <summary>The most used difficulty (ties: the lower one), normal when nothing was recorded.</summary>
    public static ESainBotDifficulty ShownDifficulty(Dictionary<ESainBotDifficulty, int> totals)
    {
        var shown = ESainBotDifficulty.normal;
        int best = 0;
        if (totals == null)
        {
            return shown;
        }
        foreach (var pair in totals)
        {
            if (pair.Value > best || (pair.Value == best && best > 0 && pair.Key < shown))
            {
                best = pair.Value;
                shown = pair.Key;
            }
        }
        return shown;
    }
}
