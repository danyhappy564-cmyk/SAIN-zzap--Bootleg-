using System.Reflection;
using EFT;
using HarmonyLib;
using SAIN.Components;
using SPT.Reflection.Patching;

namespace SAIN.Patches.GameWorld;

/// <summary>
/// This patch makes sure that when the <see cref="BotsController">BotsController</see> is activated, it also activates in SAIN's <see cref="GameWorldComponent">GameWorldComponent</see>
/// </summary>
public class GetBotControllerPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotsController), nameof(BotsController.Init));
    }

    [PatchPostfix]
    public static void Patch(BotsController __instance)
    {
        // zzap (2026-10-23): make sure modded bot types (Black Division, RUAF, UNTAR...) still have SAIN's layers
        // and their vanilla combat layers excluded before the first bot of this raid spawns.
        try
        {
            BigBrainHandler.BrainAssignment.ReassertModdedBotLayers();
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"[SAIN ModdedBots] raid start layer check failed: {ex}");
        }

        GameWorldComponent gameWorld = GameWorldComponent.Instance;
        if (gameWorld == null)
        {
            Logger.LogError("gameWorld Null");
            return;
        }
        gameWorld.Activate(__instance);
    }
}
