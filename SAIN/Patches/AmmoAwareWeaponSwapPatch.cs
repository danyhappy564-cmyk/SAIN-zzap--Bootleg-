using System.Collections.Generic;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using SAIN.Components;
using SAIN.SAINComponent.Classes.Tactics;
using SPT.Reflection.Patching;

namespace SAIN.Patches.Shoot;

/// <summary>
/// zzap fork: BSG's BotWeaponSelector.TryChangeWeapon is a blind toggle - on the main gun it draws the support gun, on
/// anything else it draws the main gun - and never looks at ammo. The 8th sim's [Weapon] log: one bot flipped main <->
/// second gun every 1.7s (63 PING-PONG lines) - main still had 15/30 but no reload ammo, so BotReload.TryReload ->
/// TrySwitchToLauncherOrChangeWeapon -> TryChangeWeapon drew the second gun, and BotWeaponSelector.ManualUpdate drew the
/// main gun back ("enemy not seen for 30s"). For SAIN bots: while the gun in hand still has rounds, don't switch; when it's
/// dry, draw a gun that actually has ammo (main first), or nothing.
/// </summary>
public class AmmoAwareWeaponTogglePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotWeaponSelector), nameof(BotWeaponSelector.TryChangeWeapon));
    }

    [PatchPrefix]
    public static bool PatchPrefix(BotWeaponSelector __instance, ref bool __result)
    {
        BotOwner owner = __instance._owner;
        if (owner == null || !SAINEnableClass.GetSAIN(owner.ProfileId, out BotComponent bot) || bot == null)
        {
            return true;
        }
        var wm = owner.WeaponManager;
        if (wm == null)
        {
            return true;
        }
        EquipmentSlot current = __instance.EquipmentSlot;
        if (current != EquipmentSlot.Scabbard && wm.HaveBullets)
        {
            TacticDiagnostics.Count("weapon.toggle.keptLoadedGun");
            __result = false;
            return false;
        }
        if (WeaponLog.PickGunWithAmmo(wm, current, out EquipmentSlot target))
        {
            __instance._nextChangeTime = 0f;
            __result = __instance.TryChangeToSlot(target, true);
            TacticDiagnostics.Count("weapon.toggle.toGunWithAmmo");
            return false;
        }
        TacticDiagnostics.Count("weapon.toggle.noGunWithAmmo");
        __result = false;
        return false;
    }
}

/// <summary>
/// zzap fork: BotWeaponSelector.ManualUpdate draws the main gun back whenever the support gun is out and the enemy hasn't
/// been seen for 30s - even when the main gun is empty with nothing to reload it (then the toggle above drew the support
/// gun again). Skip that return while the main gun has no ammo at all.
/// </summary>
public class NoReturnToEmptyMainPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotWeaponSelector), nameof(BotWeaponSelector.ManualUpdate));
    }

    [PatchPrefix]
    public static bool PatchPrefix(BotWeaponSelector __instance)
    {
        BotOwner owner = __instance._owner;
        if (owner == null || __instance.EquipmentSlot == __instance._mainWeapon || !SAINEnableClass.GetSAIN(owner.ProfileId, out _))
        {
            return true;
        }
        var wm = owner.WeaponManager;
        if (wm?.info == null || !wm.info.TryGetValue(__instance._mainWeapon, out BotWeaponInfo main) || main == null)
        {
            return true;
        }
        // BotWeaponInfo.BulletCount / CheckHaveAmmoForReload read the gun IN HAND (here the pistol), not the main gun - so an
        // empty main gun looked usable and was drawn back every ~2 s, then swapped out again by the reload check (10/12 sim:
        // Bot38 "Mk 16 0/30 +0 mags" <-> pistol, weapon.pingPong 70). Ask about the main gun itself (WeaponLog.HasAmmoFor).
        if (!SAIN.SAINComponent.Classes.Tactics.WeaponLog.HasAmmoFor(owner.GetPlayer, main.weapon))
        {
            // ManualUpdate runs every frame: count once per hold (10/12 Labs sim: 14172 counts were one bot x 211 s x 67 fps).
            if (_held.Add(owner.ProfileId))
            {
                SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count("weapon.keptOffEmptyMain");
            }
            return false;
        }
        _held.Remove(owner.ProfileId);
        return true;
    }

    private static readonly HashSet<string> _held = new();

    public static void Clear()
    {
        _held.Clear();
    }
}

/// <summary>
/// zzap fork, 10/12 Labs sim (Bot152: RPD 0/100 with no spare box <-> SR-1MP pistol, 65 PING-PONG in 5 minutes): EFT's
/// BotReloadMagazine.CanReload / BotReload call BotWeaponSelector.TryChangeToMain before they even look for the support gun's
/// magazine, and TryChangeToMain draws the main gun whenever the enemy is far - without checking its ammo. The empty main
/// gun came out, the out-of-ammo fallback put the pistol back 2 s later, and every 5 s it started over (the pistol never
/// got reloaded either). For SAIN bots: don't go back to a main gun that has no ammo at all; the reload then goes on with
/// the gun in hand.
/// </summary>
public class NoTryChangeToEmptyMainPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotWeaponSelector), nameof(BotWeaponSelector.TryChangeToMain));
    }

    [PatchPrefix]
    public static bool PatchPrefix(BotWeaponSelector __instance, ref bool __result)
    {
        BotOwner owner = __instance._owner;
        if (owner == null || __instance.EquipmentSlot == __instance._mainWeapon || !SAINEnableClass.GetSAIN(owner.ProfileId, out BotComponent bot))
        {
            return true;
        }
        // 10/13 21:47 sim (Bot113 Vepr 29/30 <-> T-5000M 0/5 +4 mags 16 PING-PONG lines, Bot95 9A-91 <-> SVDS 6/10): SAIN drew
        // the long-range gun for a far enemy, its reload check called this, the enemy was far -> main gun -> 1s later SAIN
        // drew the long-range gun again. While SAIN holds it on purpose (and it has ammo), it gets reloaded instead; SAIN
        // itself puts it away when the enemy closes in, is gone or it runs out (CombatWeaponClass).
        if (bot?.CombatWeapon != null && bot.CombatWeapon.HoldsLongRangeByChoice)
        {
            TacticDiagnostics.Count("weapon.keptLongRange.reload");
            __result = false;
            return false;
        }
        var wm = owner.WeaponManager;
        if (wm?.info == null || !wm.info.TryGetValue(__instance._mainWeapon, out BotWeaponInfo main) || main == null)
        {
            return true;
        }
        if (WeaponLog.HasAmmoFor(owner.GetPlayer, main.weapon))
        {
            return true;
        }
        TacticDiagnostics.Count("weapon.keptOffEmptyMain.reload");
        __result = false;
        return false;
    }
}
