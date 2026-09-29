using System;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using SAIN.SAINComponent.Classes.Tactics;
using SPT.Reflection.Patching;

namespace SAIN.Patches.Shoot;

/// <summary>zzap fork: every accepted BotWeaponSelector.TryChangeToSlot -> one [Weapon] journal line (see WeaponLog).</summary>
public class WeaponChangeLogPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotWeaponSelector), nameof(BotWeaponSelector.TryChangeToSlot));
    }

    [PatchPrefix]
    public static void PatchPrefix(BotWeaponSelector __instance, out EquipmentSlot __state)
    {
        __state = __instance.EquipmentSlot;
    }

    [PatchPostfix]
    public static void PatchPostfix(BotWeaponSelector __instance, EquipmentSlot slot, bool __result, EquipmentSlot __state)
    {
        if (!__result || slot == __state)
        {
            return;
        }
        try
        {
            WeaponLog.OnChange(__instance._owner, __state, slot);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[Weapon] log failed: {ex.Message}");
        }
    }
}
