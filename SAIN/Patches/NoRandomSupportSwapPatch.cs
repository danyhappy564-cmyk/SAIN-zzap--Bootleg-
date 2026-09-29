using System.Reflection;
using EFT;
using HarmonyLib;
using SAIN.Components;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.SAINComponent.Classes.Tactics;
using SPT.Reflection.Patching;

namespace SAIN.Patches.Shoot;

/// <summary>
/// zzap fork: BSG's BotWeaponSelector.ShallChangeIfNoAmmo - asked by BotReload whenever the magazine is empty - rolls a
/// chance to draw the "support" weapon (second primary, else pistol) instead of reloading when the enemy is in sight at
/// mid range. Its way back (TryChangeWeapon toggles main/support, TryChangeToMain only past a distance) made bots cycle
/// support -> main -> support in a fight and keep spraying the support gun at an enemy in a standoff (user report
/// 2026-09-29). With the reload utility on, reload-vs-pistol is its decision; this vanilla roll is switched off.
/// The underbarrel launcher case is left to BSG.
/// </summary>
public class NoRandomSupportSwapPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotWeaponSelector), nameof(BotWeaponSelector.ShallChangeIfNoAmmo));
    }

    [PatchPrefix]
    public static bool PatchPrefix(BotWeaponSelector __instance, ref bool __result)
    {
        var settings = GlobalSettingsClass.Instance?.General?.CloseCombat;
        BotOwner owner = __instance._owner;
        if (settings == null || !settings.UtilityReload || owner == null)
        {
            return true;
        }
        if (!SAINEnableClass.GetSAIN(owner.ProfileId, out BotComponent bot) || bot == null)
        {
            return true;
        }
        if (owner.WeaponManager?.UnderbarrelLauncherController?.CanUseInsteadOfReload() == true)
        {
            return true;
        }
        TacticDiagnostics.Count("weapon.vanillaSupportSwapBlocked");
        __result = false;
        return false;
    }
}
