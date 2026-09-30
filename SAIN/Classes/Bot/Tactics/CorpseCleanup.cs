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

    // Every death is checked once, 3s later, by what the body looks like - whatever the cause (user 2026-09-29: "you never
    // find the walking corpses in the log"): no ragdoll corpse built, character controller still on, or the body moved
    // 1.5m+ since it died -> [DeadBug] "body still standing/moving".
    private static readonly List<(Player player, float time, Vector3 pos, string name)> _toInspect = new();

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
            _toInspect.Add((player, Time.time, player.Position, player.name));
        }
    }

    private static void Inspect()
    {
        for (int i = _toInspect.Count - 1; i >= 0; i--)
        {
            var (player, time, pos, name) = _toInspect[i];
            if (Time.time - time < 3f)
            {
                continue;
            }
            _toInspect.RemoveAt(i);
            if (player == null || player.gameObject == null || !player.gameObject.activeSelf)
            {
                continue;
            }
            bool noCorpse = SAIN.Patches.Generic.DeathRescuePatch.CorpseRef(player) == null;
            bool controllerOn = false;
            try
            {
                controllerOn = player._characterController != null && player._characterController.isEnabled;
            }
            catch
            {
            }
            Vector3 d = player.Position - pos;
            d.y = 0f;
            float moved = d.magnitude;
            if (noCorpse || controllerOn || moved > 1.5f)
            {
                TacticDiagnostics.Count("deadBug.bodyNotRagdoll");
                if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat(
                    $"[DeadBug] {name} 3s after death: ragdoll corpse built={!noCorpse}, character controller on={controllerOn}, body moved {moved:0.0}m "
                        + $"at ({player.Position.x:0},{player.Position.z:0}) - WALKING/STANDING CORPSE (see [DeadBug] handler lines above and BepInEx LogOutput.log)"
                );
            }
            else
            {
                TacticDiagnostics.Count("deadBug.bodyOk");
            }
        }
    }

    public static void Tick()
    {
        Inspect();
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
            if (Delete && TryDelete(player))
            {
                continue;
            }
            player.gameObject.SetActive(false);
            Hidden++;
            TacticDiagnostics.Count("sim.corpseHidden");
        }
    }

    // zzap, 11th sim (user 2026-10-01): hiding keeps the whole body in memory (83 corpses, 43 hidden, RAM still +293 MB per
    // death). Experiment to split "the corpses" from "what new spawns load": really remove old corpses through EFT's own
    // loot removal (GameWorld.DestroyLoot -> out of the loot lists, body disposed, object back to its pool) and compare
    // RAM per death. TEST preset only, off in code.
    private static bool Delete
    {
        get { return GlobalSettingsClass.Instance?.General?.Performance?.SimCorpseDelete == true; }
    }

    public static int Deleted { get; private set; }

    private static bool TryDelete(Player player)
    {
        var corpse = SAIN.Patches.Generic.DeathRescuePatch.CorpseRef(player);
        var world = Comfort.Common.Singleton<GameWorld>.Instance;
        if (corpse == null || world == null)
        {
            return false;
        }
        try
        {
            world.DestroyLoot(corpse);
            Deleted++;
            TacticDiagnostics.Count("sim.corpseDeleted");
            // 13th sim (user: "delete was on but the corpses stayed"): DestroyLoot took the corpse off the loot lists but the
            // body object stayed in the scene (EFT never removes corpses mid-raid; the player object isn't returned to a pool),
            // and because this counted as done the corpse wasn't hidden either. Hide whatever is left.
            if (player != null && player.gameObject != null && player.gameObject.activeSelf)
            {
                player.gameObject.SetActive(false);
                TacticDiagnostics.Count("sim.corpseDeletedStillThere");
            }
            return true;
        }
        catch (System.Exception ex)
        {
            TacticDiagnostics.Count("sim.corpseDeleteFailed");
            Logger.LogWarning($"[SAIN zzap] sim corpse delete failed for {player.name}, hiding instead: {ex.Message}");
            return false;
        }
    }

    public static void Clear()
    {
        _corpses.Clear();
        _toInspect.Clear();
        Hidden = 0;
        Deleted = 0;
    }
}
