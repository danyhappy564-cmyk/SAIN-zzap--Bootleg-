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
