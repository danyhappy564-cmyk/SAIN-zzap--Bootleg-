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
        // zzap reload utility: drop the magazine only when it's urgent (shot at, enemy in sight, or seen <3s ago within 35m).
        // With time and 30%+ still in the mag, a normal reload keeps those rounds (tactical reload).
        bool urgent = owner.Memory.IsUnderFire || (enemy != null && (enemy.IsVisible || (enemy.Seen && enemy.TimeSinceSeen < 3f && enemy.RealDistance < 35f)));
        if (!urgent && settings.UtilityReload)
        {
            float ratio = SAIN.SAINComponent.Classes.WeaponFunction.SAINBotSuppressClass.CalcAmmoRatio(owner, out _);
            if (ratio >= 0.3f)
            {
                TacticDiagnostics.Count("weapon.tacticalReloadKeepMag");
                return true;
            }
        }
        // A dropped magazine is gone for good (bots don't pick mags back up), and BSG only refills magazines the bot still
        // carries. Dropping every time left bots with loose rounds but no magazine: CanReload false -> "no ammo" -> weapon
        // swapping main <-> pistol / second gun (user report 2026-09-29). Keep the last spare: drop only with 2+ spares.
        int spares = SpareMagazines(owner.GetPlayer, controller.Item as Weapon);
        if (spares < 2)
        {
            TacticDiagnostics.Count("weapon.quickReload.keptLastSpare");
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

    /// <summary>Magazines the bot carries that fit this weapon, not counting the one in it.</summary>
    private static int SpareMagazines(Player player, Weapon weapon)
    {
        var slot = weapon?.GetMagazineSlot();
        var equipment = player?.InventoryController?.Inventory?.Equipment;
        if (slot == null || equipment == null)
        {
            return 0;
        }
        var current = weapon.GetCurrentMagazine();
        int count = 0;
        foreach (Item item in equipment.GetAllItems())
        {
            if (item is Magazine mag && !ReferenceEquals(mag, current) && slot.CanAccept(mag))
            {
                count++;
            }
        }
        return count;
    }
}
