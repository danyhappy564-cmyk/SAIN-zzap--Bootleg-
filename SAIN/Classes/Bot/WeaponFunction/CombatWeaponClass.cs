using EFT;
using EFT.InventoryLogic;
using SAIN.Components;
using SAIN.Models.Enums;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.GlobalSettings.Categories.General;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.Classes.Tactics;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.WeaponFunction;

/// <summary>
/// zzap fork (2026-09-28 field report): bots reloaded mid-fight and never used the DMR/sniper in slot 2.
///  - Pistol swap: magazine dry, enemy in sight and close -> pistol instead of reload (called from the reload decision).
///  - Long-range swap: slot 2 is a DMR/sniper and the enemy is far -> use it; back to the main gun when they close in.
/// BSG's own selector treats slot 2 as the "support" weapon for its out-of-ammo swap, which is why a sniper came out
/// at 5m and a pistol never did. Its 25s weapon-change throttle is cleared for these swaps.
/// Quick reload (drop the mag) is a Harmony patch on BotReload.ReloadMagazine (Patches/QuickReloadPatch.cs).
/// F6: General > Close Combat (zzap).
/// </summary>
public class CombatWeaponClass : BotComponentClassBase
{
    public CombatWeaponClass(BotComponent bot)
        : base(bot)
    {
        TickRequirement = ESAINTickState.OnlyBotActive;
        TickInterval = 0.25f;
    }

    private static CloseCombatSettings Settings
    {
        get { return GlobalSettingsClass.Instance?.General?.CloseCombat; }
    }

    private bool Applies
    {
        get
        {
            var s = Settings;
            return s != null && (!s.PmcOnly || Bot.Info.Profile.IsPMC);
        }
    }

    private bool _pistolByUs;
    private bool _longRangeByUs;
    private float _nextLongRangeCheck;

    /// <summary>
    /// Called from the reload decision right before a reload starts. True = the pistol is coming out instead.
    /// </summary>
    public bool TrySwapInsteadOfReload(Enemy enemy)
    {
        var s = Settings;
        if (!Applies || !s.PistolSwap || enemy == null)
        {
            return false;
        }
        var wm = BotOwner.WeaponManager;
        var selector = wm?.Selector;
        if (selector == null || wm.Reload == null || wm.Reload.BulletCount > 0)
        {
            return false;
        }
        if (selector.EquipmentSlot == EquipmentSlot.Holster)
        {
            return false;
        }
        bool inSight = enemy.IsVisible || (enemy.Seen && enemy.TimeSinceSeen < 1.5f);
        if (!inSight || enemy.RealDistance > s.PistolSwapMaxDistance)
        {
            return false;
        }
        var pistol = wm.PistolWeaponInfo;
        if (pistol == null || pistol.BulletCount <= 0)
        {
            TacticDiagnostics.Count("weapon.pistolSwap.noPistolOrAmmo");
            return false;
        }
        if (!ForceChange(EquipmentSlot.Holster))
        {
            TacticDiagnostics.Count("weapon.pistolSwap.refused");
            return false;
        }
        _pistolByUs = true;
        TacticDiagnostics.Count("weapon.pistolSwap");
        TacticDiagnostics.LogCloseCombat($"[Weapon] [{Bot.name}] magazine empty, enemy {enemy.RealDistance:0}m in sight -> PISTOL instead of reload");
        return true;
    }

    public override void ManualUpdate()
    {
        if (!Applies)
        {
            return;
        }
        var wm = BotOwner.WeaponManager;
        var selector = wm?.Selector;
        if (selector == null || !selector.IsWeaponReady || wm.Reload == null || wm.Reload.Reloading)
        {
            return;
        }
        if (Player.HandsController is not Player.FirearmController)
        {
            return;
        }
        var s = Settings;
        Enemy enemy = Bot.GoalEnemy;
        EquipmentSlot slot = selector.EquipmentSlot;

        if (slot == EquipmentSlot.Holster && _pistolByUs)
        {
            bool empty = wm.PistolWeaponInfo == null || wm.PistolWeaponInfo.BulletCount <= 0;
            bool gone = enemy == null || !enemy.Seen || enemy.TimeSinceSeen > 6f;
            bool far = enemy != null && enemy.RealDistance > s.PistolSwapMaxDistance + 15f;
            if (empty || gone || far)
            {
                string why = empty ? "pistol empty" : gone ? "enemy gone" : "enemy far";
                if (ForceChange(selector._mainWeapon))
                {
                    _pistolByUs = false;
                    TacticDiagnostics.Count("weapon.pistolBack");
                    TacticDiagnostics.LogCloseCombat($"[Weapon] [{Bot.name}] back to the main gun ({why})");
                }
            }
            return;
        }

        if (slot == EquipmentSlot.SecondPrimaryWeapon && _longRangeByUs)
        {
            bool close = enemy != null && enemy.RealDistance < s.SniperSwapBackDistance;
            bool gone = enemy == null || !enemy.Seen || enemy.TimeSinceSeen > 20f;
            bool empty = wm.SecondWeaponInfo == null || wm.SecondWeaponInfo.BulletCount <= 0 && !wm.SecondWeaponInfo.CheckHaveAmmoForReload();
            if (close || gone || empty)
            {
                string why = close ? $"enemy {enemy.RealDistance:0}m" : gone ? "enemy gone" : "no ammo";
                if (ForceChange(selector._mainWeapon))
                {
                    _longRangeByUs = false;
                    TacticDiagnostics.Count("weapon.longRangeBack");
                    TacticDiagnostics.LogCloseCombat($"[Weapon] [{Bot.name}] long-range weapon away, main gun ({why})");
                }
            }
            return;
        }

        if (!s.SniperSwap || slot != selector._mainWeapon || Time.time < _nextLongRangeCheck || enemy == null)
        {
            return;
        }
        _nextLongRangeCheck = Time.time + 1f;
        bool seen = enemy.IsVisible || (enemy.Seen && enemy.TimeSinceSeen < 2f);
        if (!seen || enemy.RealDistance < s.SniperSwapMinDistance)
        {
            return;
        }
        if (!IsLongRange(selector.SecondPrimaryWeaponItem as Weapon) || IsLongRange(selector.FirstPrimaryWeaponItem as Weapon))
        {
            return;
        }
        var second = wm.SecondWeaponInfo;
        if (second == null || (second.BulletCount <= 0 && !second.CheckHaveAmmoForReload()))
        {
            return;
        }
        if (ForceChange(EquipmentSlot.SecondPrimaryWeapon))
        {
            _longRangeByUs = true;
            _pistolByUs = false;
            TacticDiagnostics.Count("weapon.longRange");
            TacticDiagnostics.LogCloseCombat(
                $"[Weapon] [{Bot.name}] enemy {enemy.RealDistance:0}m -> {((Weapon)selector.SecondPrimaryWeaponItem).ShortName.Localized()} (long range)"
            );
        }
    }

    private static bool IsLongRange(Weapon weapon)
    {
        if (weapon == null)
        {
            return false;
        }
        string weapClass = weapon.WeapClass;
        return weapClass == "sniperRifle" || weapClass == "marksmanRifle";
    }

    private bool ForceChange(EquipmentSlot slot)
    {
        var selector = BotOwner.WeaponManager.Selector;
        // BSG throttles weapon changes to one per CHANGE_WEAPON_PERIOD (25s); these swaps are deliberate.
        selector._nextChangeTime = 0f;
        return selector.TryChangeToSlot(slot, true);
    }
}
