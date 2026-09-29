using System.Collections.Generic;
using EFT;
using SAIN.Plugin;
using SAIN.Preset.Shared.GlobalSettings;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork, simulation only: hide the oldest bot corpses once there are more than the limit (F6 General > Performance,
/// only in a preset whose name contains "TEST"). 7th sim: 64-67 fps up to ~86 corpses, then 47 -> 32 fps at 104 -> 170
/// corpses while the managed heap only went up and down - this tests whether the bodies are what costs the frames.
/// A hidden corpse is deactivated (no render/physics), not destroyed, so nothing that still references it breaks.
/// </summary>
public static class CorpseCleanup
{
    private const float MIN_AGE = 20f;

    private static readonly Queue<(Player player, float time)> _corpses = new();

    public static int Hidden { get; private set; }

    private static int Limit
    {
        get
        {
            string name = SAINPlugin.LoadedPreset?.Info?.Name;
            if (string.IsNullOrEmpty(name) || name.IndexOf("TEST", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                return 0;
            }
            float limit = GlobalSettingsClass.Instance?.General?.Performance?.SimCorpseLimit ?? 0f;
            return limit >= 1f ? Mathf.RoundToInt(limit) : 0;
        }
    }

    public static void OnBotDied(Player player)
    {
        if (player != null)
        {
            _corpses.Enqueue((player, Time.time));
        }
    }

    public static void Tick()
    {
        int limit = Limit;
        if (limit <= 0)
        {
            return;
        }
        while (_corpses.Count > limit)
        {
            var (player, time) = _corpses.Peek();
            if (Time.time - time < MIN_AGE)
            {
                return;
            }
            _corpses.Dequeue();
            if (player == null || player.gameObject == null || !player.gameObject.activeSelf || player.HealthController?.IsAlive == true)
            {
                continue;
            }
            player.gameObject.SetActive(false);
            Hidden++;
            TacticDiagnostics.Count("sim.corpseHidden");
        }
    }

    public static void Clear()
    {
        _corpses.Clear();
        Hidden = 0;
    }
}
