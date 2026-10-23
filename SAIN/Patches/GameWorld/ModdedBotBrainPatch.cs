using System;
using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace SAIN.Patches.GameWorld;

/// <summary>
/// zzap (2026-10-23 sim 16:55): runs after every other StandartBotBrain.Activate postfix - MoreBotsAPI's brain swap for modded
/// bot types (Priority.First) and BigBrain's exclusion pass - so a modded bot's vanilla combat layers are excluded on the brain
/// it really ends up with. See <see cref="BigBrainHandler.BrainAssignment.ExcludeVanillaLayersForModdedBot"/>.
/// </summary>
public class ModdedBotBrainPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(StandartBotBrain), nameof(StandartBotBrain.Activate));
    }

    [PatchPostfix]
    [HarmonyPriority(Priority.Last)]
    public static void Patch(StandartBotBrain __instance)
    {
        try
        {
            BotOwner owner = __instance._owner;
            if (owner?.Profile?.Info?.Settings == null)
            {
                return;
            }
            BigBrainHandler.BrainAssignment.ExcludeVanillaLayersForModdedBot(owner);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[SAIN ModdedBots] spawn layer exclusion failed: {ex}");
        }
    }
}
