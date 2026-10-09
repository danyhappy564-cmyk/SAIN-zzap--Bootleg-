using System;
using System.Collections.Generic;
using EFT;
using EFT.Game.Spawning;
using HarmonyLib;
using SAIN.SAINComponent.Classes.Tactics;
using UnityEngine;

namespace SAIN.SimLab;

/// <summary>
/// zzap SimLab: the sim's squads are spawned by ABPS, in the sim's zones (user 2026-10-09: "vanilla can't spawn the squads -
/// borrow ABPS"; Customs got 6 bots from 4 squads in 20 min with the game's own boss spawner, stuck in its 20 s retry delay).
/// ABPS's game plugin already takes over every pmcUSEC/pmcBEAR boss wave (PmcSpawnHookPatch, prefix on BotBossSpawn.TrySpawn)
/// and spawns the group itself, but it picks the spot from all player spawn points of the map, ignoring the wave's zone. So:
///   1) a prefix on BotBossSpawn.TrySpawn (runs before ABPS's) remembers which wave is being spawned;
///   2) a postfix on ABPS's own spot picker (PmcSpawnHookPatch.GetValidSpawnPoints) replaces its pick, in a sim raid only, with
///      the point of that wave's zone farthest from every living player (15 m at least) plus up to the needed escort points
///      within 20 m of it. No such point: ABPS's own pick is kept (the squad spawns, just elsewhere - counted).
/// ABPS's code and settings are untouched; both patches are put on by hand at the first sim raid and only when ABPS is there.
/// </summary>
public static class SimLabAbpsSpawn
{
    private const float MIN_DISTANCE = 15f;
    private const float ESCORT_RADIUS = 20f;
    private const string ABPS_HOOK = "BotPlacementSystemClient.Patches.PmcSpawnHookPatch";

    private static bool _tried;
    public static bool Active { get; private set; }

    [ThreadStatic]
    private static BossLocationSpawn _wave;

    public static void EnsurePatched()
    {
        if (_tried)
        {
            return;
        }
        _tried = true;
        try
        {
            var hook = AccessTools.TypeByName(ABPS_HOOK);
            var picker = hook == null ? null : AccessTools.Method(hook, "GetValidSpawnPoints");
            if (picker == null)
            {
                Logger.LogInfo("[SimLab] ABPS not found - sim squads use the game's spawner");
                return;
            }
            var harmony = new Harmony("zzap.SAIN.SimLab.AbpsSpawn");
            var trySpawn = AccessTools.Method(typeof(BotBossSpawn), nameof(BotBossSpawn.TrySpawn));
            harmony.Patch(trySpawn, prefix: new HarmonyMethod(typeof(SimLabAbpsSpawn), nameof(RememberWave)) { priority = Priority.First });
            harmony.Patch(picker, postfix: new HarmonyMethod(typeof(SimLabAbpsSpawn), nameof(PickInZone)));
            Active = true;
            Logger.LogWarning("[SimLab] ABPS found - sim squads are spawned by ABPS in the sim zones");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[SimLab] ABPS spawn hook failed (ABPS picks the spots on its own): {ex}");
        }
    }

    public static void RememberWave(BossLocationSpawn wave)
    {
        _wave = wave;
    }

    public static void PickInZone(ref List<ISpawnPoint> __result, int neededPoints)
    {
        var wave = _wave;
        _wave = null;
        if (wave == null || SimLab.Raid?.Applied != true || string.IsNullOrEmpty(wave.BossZone))
        {
            return;
        }
        try
        {
            var picked = Pick(wave.BossZone, Math.Max(1, neededPoints));
            if (picked == null)
            {
                TacticDiagnostics.Count("sim.abpsZone.fallback");
                return;
            }
            __result = picked;
            TacticDiagnostics.Count("sim.abpsZone.used");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[SimLab] zone pick failed, ABPS's pick kept: {ex.Message}");
        }
    }

    /// <summary>The point of these zones farthest from every living player, if at least <paramref name="minDistance"/> away.</summary>
    public static ISpawnPoint FarthestPoint(string zones, float minDistance)
    {
        var alive = Comfort.Common.Singleton<GameWorld>.Instance?.AllAlivePlayersList;
        ISpawnPoint best = null;
        float bestDist = minDistance;
        foreach (var point in ZonePoints(zones))
        {
            float d = MinDistance(point.Position, alive);
            if (d >= bestDist)
            {
                best = point;
                bestDist = d;
            }
        }
        return best;
    }

    private static List<ISpawnPoint> ZonePoints(string zones)
    {
        var points = new List<ISpawnPoint>();
        foreach (string name in zones.Split(','))
        {
            var zone = SimLabProbe.Zone(name);
            if (zone?.SpawnPoints == null)
            {
                continue;
            }
            foreach (ISpawnPoint point in zone.SpawnPoints)
            {
                if (point != null && !point.IsSnipeZone)
                {
                    points.Add(point);
                }
            }
        }
        return points;
    }

    private static List<ISpawnPoint> Pick(string zones, int needed)
    {
        var alive = Comfort.Common.Singleton<GameWorld>.Instance?.AllAlivePlayersList;
        var points = ZonePoints(zones);
        ISpawnPoint best = FarthestPoint(zones, MIN_DISTANCE);
        if (best == null)
        {
            return null;
        }
        var result = new List<ISpawnPoint> { best };
        foreach (var point in points)
        {
            if (result.Count >= needed)
            {
                break;
            }
            if (point != best && Vector3.Distance(point.Position, best.Position) <= ESCORT_RADIUS && MinDistance(point.Position, alive) >= MIN_DISTANCE * 0.5f)
            {
                result.Add(point);
            }
        }
        while (result.Count < needed)
        {
            result.Add(best); // ABPS pads the same way
        }
        return result;
    }

    private static float MinDistance(Vector3 pos, List<Player> alive)
    {
        float best = float.MaxValue;
        if (alive == null)
        {
            return best;
        }
        foreach (var p in alive)
        {
            if (p == null)
            {
                continue;
            }
            float d = Vector3.Distance(pos, p.Position);
            if (d < best)
            {
                best = d;
            }
        }
        return best;
    }
}
