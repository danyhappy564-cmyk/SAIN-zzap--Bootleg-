using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using SAIN.Components;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.SAINComponent.Classes.Tactics;
using SPT.Reflection.Patching;
using UnityEngine;

namespace SAIN.Patches.Shoot;

/// <summary>
/// zzap fork: in a fight, bots reload like a player double-tapping R - the empty magazine is dropped instead of stowed
/// (FirearmController.QuickReloadMag, same result callback as BSG's BotReload.ReloadMagazine). F6 Close Combat > Quick Reload.
/// </summary>
public class QuickReloadPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotReload), nameof(BotReload.ReloadMagazine));
    }

    [PatchPrefix]
    public static bool PatchPrefix(BotReload __instance, Magazine foundMag)
    {
        var settings = GlobalSettingsClass.Instance?.General?.CloseCombat;
        BotOwner owner = __instance._owner;
        if (settings == null || !settings.QuickReload || owner == null || foundMag == null)
        {
            return true;
        }
        if (!SAINEnableClass.GetSAIN(owner.ProfileId, out BotComponent bot))
        {
            return true;
        }
        if (settings.PmcOnly && !bot.Info.Profile.IsPMC)
        {
            return true;
        }
        var enemy = bot.GoalEnemy;
        bool inFight = owner.Memory.IsUnderFire || (enemy != null && enemy.Seen && enemy.TimeSinceSeen < 10f);
        var controller = __instance.ShootController;
        if (!inFight || controller == null)
        {
            return true;
        }
        __instance._reloadType = BotReload.EReloadType.MagReload;
        controller.QuickReloadMag(
            foundMag,
            delegate
            {
                __instance._nextReloadTime = Time.time + 0.5f;
                __instance.Reloading = false;
                __instance.AddAmmoToMagazines();
            }
        );
        TacticDiagnostics.Count("weapon.quickReload");
        return false;
    }
}
