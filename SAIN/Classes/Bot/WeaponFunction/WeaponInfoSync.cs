using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using SAIN.Components;
using SAIN.SAINComponent.Classes.Tactics;
using SPT.Reflection.Patching;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.WeaponFunction;

/// <summary>
/// zzap fork (Remix 4.2 feedback 2026-10-10: "PMC out of ammo / wrong ammo doesn't switch to its other gun and just stares",
/// "had spare mags but never reloaded", "PP from a scav in slot 1, fully kitted M4 in slot 2 - came at me with a knife").
/// BSG keeps one BotWeaponInfo per weapon slot in <c>BotWeaponManager.info</c> - the gun and its BotReload - and creates it only
/// the first time that slot is drawn (<c>UpdateFirearmsController</c>: "if (!info.ContainsKey(slot))"); it is never refreshed.
/// BotReload looks for magazines / ammo that fit the gun it was created with (<c>_weapon</c>). When a bot loots a gun (ORBIT's
/// loot swap; it refreshes the selector's weapon list but not this table), the slot keeps the OLD gun's entry: the bot searches
/// mags for a gun it no longer has, so it doesn't reload the new one, and a slot never drawn (the M4 in slot 2) has no entry at
/// all, so "is there another gun with ammo" never sees it and the out-of-ammo fallback goes to the knife.
/// This keeps the table in step with the equipment: a slot whose gun changed gets a fresh entry, a gun slot without one gets one,
/// an emptied slot loses its entry, and the entry in use is the one for the gun in hand. Every second per SAIN bot, and right
/// after anything calls BotWeaponSelector.UpdateWeaponsList (ORBIT does after a swap).
/// </summary>
public static class WeaponInfoSync
{
    private static readonly EquipmentSlot[] GUN_SLOTS = { EquipmentSlot.FirstPrimaryWeapon, EquipmentSlot.SecondPrimaryWeapon, EquipmentSlot.Holster };

    /// <summary>Returns true when something was changed.</summary>
    public static bool Sync(BotOwner owner)
    {
        var wm = owner?.WeaponManager;
        var player = owner?.GetPlayer;
        var equipment = player?.InventoryController?.Inventory?.Equipment;
        if (wm?.info == null || equipment == null || wm.Reload?.Reloading == true)
        {
            return false;
        }
        bool changed = false;
        foreach (EquipmentSlot slot in GUN_SLOTS)
        {
            Weapon gun = equipment.GetSlot(slot)?.ContainedItem as Weapon;
            bool has = wm.info.TryGetValue(slot, out BotWeaponInfo entry) && entry != null;
            if (gun == null)
            {
                // emptied slot: drop the entry unless it's the one in use (the gun may still be in hand mid-swap)
                if (has && !ReferenceEquals(entry, wm._currentWeaponInfo))
                {
                    wm.info.Remove(slot);
                    TacticDiagnostics.Count("weapon.infoSync.removed");
                    changed = true;
                }
                continue;
            }
            if (has && ReferenceEquals(entry.weapon, gun))
            {
                continue;
            }
            var fresh = new BotWeaponInfo(owner, gun, slot, wm.ChangeToMode);
            wm.info[slot] = fresh;
            TacticDiagnostics.Count(has ? "weapon.infoSync.replaced" : "weapon.infoSync.added");
            if (has && TacticDiagnostics.LogOn)
            {
                TacticDiagnostics.LogCloseCombat(
                    $"[Weapon] [{owner.name}] {slot}: weapon table still had {entry.weapon?.ShortName?.Localized() ?? "?"} - now {gun.ShortName.Localized()} (looted gun)"
                );
            }
            changed = true;
        }
        // the entry in use must be the one for the gun actually in hand
        Item inHands = player.HandsController?.Item;
        if (inHands is Weapon && !ReferenceEquals(wm._currentWeaponInfo?.weapon, inHands))
        {
            foreach (var kv in wm.info)
            {
                if (kv.Value != null && ReferenceEquals(kv.Value.weapon, inHands))
                {
                    wm._currentWeaponInfo = kv.Value;
                    TacticDiagnostics.Count("weapon.infoSync.current");
                    changed = true;
                    break;
                }
            }
        }
        return changed;
    }

    private static readonly System.Collections.Generic.Dictionary<string, float> _next = new();

    /// <summary>Once a second per bot (CombatWeaponClass tick).</summary>
    public static void Tick(BotOwner owner)
    {
        if (owner == null)
        {
            return;
        }
        float now = Time.time;
        if (_next.TryGetValue(owner.ProfileId, out float next) && now < next)
        {
            return;
        }
        _next[owner.ProfileId] = now + 1f;
        try
        {
            Sync(owner);
        }
        catch (System.Exception ex)
        {
            TacticDiagnostics.Count("weapon.infoSync.threw");
            if (!_loggedError)
            {
                _loggedError = true;
                Logger.LogWarning($"[SAIN zzap] weapon table sync threw (logged once): {ex}");
            }
        }
    }

    private static bool _loggedError;

    public static void Clear()
    {
        _next.Clear();
    }
}

/// <summary>Right after the selector re-reads the weapon slots (ORBIT calls this after a loot swap), fix the weapon table too.</summary>
public class WeaponInfoSyncPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotWeaponSelector), nameof(BotWeaponSelector.UpdateWeaponsList));
    }

    [PatchPostfix]
    public static void PatchPostfix(BotWeaponSelector __instance)
    {
        BotOwner owner = __instance?._owner;
        if (owner == null || !SAINEnableClass.GetSAIN(owner.ProfileId, out BotComponent bot) || bot == null)
        {
            return;
        }
        try
        {
            WeaponInfoSync.Sync(owner);
        }
        catch (System.Exception)
        {
            TacticDiagnostics.Count("weapon.infoSync.threw");
        }
    }
}
