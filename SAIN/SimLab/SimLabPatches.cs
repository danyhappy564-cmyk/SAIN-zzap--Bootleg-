using System;
using System.Reflection;
using System.Threading.Tasks;
using EFT;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace SAIN.SimLab;

/// <summary>zzap SimLab: the main menu finished loading (start, and every return from a raid) - the runner may start the next map.</summary>
public class SimLabMenuReadyPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(TarkovApplication), nameof(TarkovApplication.OnApplicationLoaded));
    }

    [PatchPostfix]
    public static void PatchPostfix()
    {
        SimLab.MenuReadyTime = Time.realtimeSinceStartup;
    }
}

/// <summary>
/// zzap SimLab spectator: bots never add the spectating player as an enemy (all of BSG's ways to add one - seen, heard, shot,
/// group sharing, spawn - go through BotsGroup.AddEnemy). Only while a sim raid runs in spectator mode.
/// </summary>
public class SimLabSpectatorEnemyPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotsGroup), nameof(BotsGroup.AddEnemy), new[] { typeof(IPlayer), typeof(EBotEnemyCause) });
    }

    [PatchPrefix]
    public static bool PatchPrefix(IPlayer person, ref bool __result)
    {
        if (!SimLab.SpectatorActive || !SimLab.IsSpectatorPlayer(person))
        {
            return true;
        }
        __result = false;
        return false;
    }
}

/// <summary>
/// zzap SimLab: after a raid the sim ended itself, skip the session result screens (exit status, kills, experience...) and go
/// straight back to the main menu, doing the same profile refresh the result screens do first.
/// </summary>
public class SimLabSkipResultsPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(TarkovApplication), nameof(TarkovApplication.ShowSessionResult));
    }

    [PatchPrefix]
    public static bool PatchPrefix(TarkovApplication __instance, ref Task __result)
    {
        if (!SimLab.SkipNextResults)
        {
            return true;
        }
        SimLab.SkipNextResults = false;
        __result = SkipAsync(__instance);
        return false;
    }

    private static async Task SkipAsync(TarkovApplication app)
    {
        try
        {
            var session = app.Session;
            await session.GetProfiles();
            await DataPrepareOperation.SelectProfile(session);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[SimLab] profile refresh after the raid failed: {ex}");
        }
        try
        {
            MonoBehaviourSingleton<PreloaderUI>.Instance.SetLoaderStatus(false);
        }
        catch
        {
        }
        Logger.LogWarning("[SimLab] result screens skipped - back to the main menu");
        await app.ComebackToMainMenu();
    }
}

/// <summary>
/// zzap SimLab: where a PMC squad spawns in a sim raid (user 2026-10-09 screenshot: two USEC from different squads standing
/// shoulder to shoulder in a doorway, then turning on each other). BSG's <c>SpawnSystem.GetPmcSpawnPoints</c> sorts the zone's
/// PMC points by distance from the alive HUMAN PMCs only and ignores bots - with the spectator as the only human and Factory's
/// one big zone, every squad of both sides went to the same far corner. In a sim raid the point farthest from every alive
/// player (bots included) wins instead; filters (PMC category, spawn cooldown, side, not inside someone) are BSG's own.
/// Normal raids run the original.
/// </summary>
public class SimLabSpawnPointPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(EFT.Game.Spawning.SpawnSystem), nameof(EFT.Game.Spawning.SpawnSystem.GetPmcSpawnPoints));
    }

    [PatchPrefix]
    public static bool PatchPrefix(EFT.Game.Spawning.SpawnSystem.SpawnPointsFilteredCollection filteredPoints, int maxCount, BotZone zone, BotCreationData creationData, float time)
    {
        if (!SimLab.Active || SimLab.Raid?.Applied != true || filteredPoints == null || zone == null || creationData == null)
        {
            return true;
        }
        try
        {
            var alive = Comfort.Common.Singleton<GameWorld>.Instance?.AllAlivePlayersList;
            if (alive == null)
            {
                return true;
            }
            var players = new System.Collections.Generic.List<IPlayer>(alive.Count);
            foreach (var p in alive)
            {
                if (p != null)
                {
                    players.Add(p);
                }
            }
            filteredPoints.InsertRange(zone.SpawnPoints);
            filteredPoints.ApplyFilter(sp => EFT.Game.Spawning.SpawnCategoryExtension.ContainBotPmcCategory(sp.Categories));
            filteredPoints.ApplyFilter(sp => EFT.Game.Spawning.SpawnPointExtension.IsValid(sp, time));
            filteredPoints.ApplyFilter(sp => EFT.Game.Spawning.SpawnPointExtension.IsValid(sp, creationData.Side));
            filteredPoints.ApplyFilter(sp => EFT.Game.Spawning.SpawnPointExtension.IsNotCollided(sp, players, out _));
            if (filteredPoints.ValidPointsCount == 0)
            {
                return false;
            }
            filteredPoints.ApplySorting(sp => -EFT.Game.Spawning.SpawnPointExtension.MinDistanceSqr(sp, players));
            filteredPoints.ClampPointsCountToMaximum(maxCount);
            if (filteredPoints.ValidPointsCount != 0)
            {
                filteredPoints.MultiplicatePointOnIndex(0, maxCount);
            }
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[SimLab] spawn point pick failed, game's own pick used: {ex.Message}");
            filteredPoints.Clear();
            return true;
        }
    }
}
