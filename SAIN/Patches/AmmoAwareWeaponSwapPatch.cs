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
        if (main.BulletCount <= 0 && !main.CheckHaveAmmoForReload())
        {
            return false;
        }
        return true;
    }
}
