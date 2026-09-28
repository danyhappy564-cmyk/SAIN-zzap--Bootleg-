using System.Reflection;
using HarmonyLib;
using SAIN.Components.BotControllerSpace.Classes;
using SPT.Reflection.Patching;

namespace SAIN.Patches.GameWorld;

/// <summary>
/// zzap: save the player style record at the very start of GameWorld.Dispose. GameWorld raises OnDispose only on the last
/// line of Dispose, and in the field (2026-09-28, an 18-min survived raid) SAIN's OnDispose-based teardown never ran -
/// no RAID SUMMARY while other mods' Dispose patches did. A prefix runs before anything in Dispose can throw.
/// </summary>
public class PlayerStyleSavePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(EFT.GameWorld), nameof(EFT.GameWorld.Dispose));
    }

    [PatchPrefix]
    public static void PatchPrefix()
    {
        try
        {
            PlayerStyleRecorder.Instance?.Dispose("gameWorldDispose");
        }
        catch (System.Exception ex)
        {
            Logger.LogWarning($"[PlayerStyle] save at GameWorld.Dispose failed: {ex}");
        }
    }
}
