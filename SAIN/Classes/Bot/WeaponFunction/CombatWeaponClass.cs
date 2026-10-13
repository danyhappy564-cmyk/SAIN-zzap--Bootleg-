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
            return s != null && (!s.PmcOnly || !Bot.Info.Profile.SkippedByPmcOnly);
        }
    }

    private bool _pistolByUs;
    private bool _longRangeByUs;
    private float _nextLongRangeCheck;

    /// <summary>
    /// Called from the reload decision right before a reload starts. True = the pistol is coming out instead.
    /// </summary>
    /// <summary>
    /// EFT put our pistol away itself (BotWeaponSelector.ManualUpdate / CanReload -> TryChangeToMain, or BotFirstAid
    /// cancelling a heal -> TakePrevWeapon): same 5 s hold-off as our own return, so the main gun gets reloaded first
    /// (Labs sim Bot143). Checked here and in PistolReady - ManualUpdate returns early while the weapon isn't ready, so the
    /// reload check could offer the pistol again first (15:34 sim Bot81: pistol again 1.7 s after a cancelled heal).
    /// </summary>
    private void NoteEftPutPistolAway(EquipmentSlot slot)
    {
        if (_pistolByUs && slot != EquipmentSlot.Holster)
        {
            _pistolByUs = false;
            _pistolBackHeldNoted = false;
            _pistolRefusedUntil = Time.time + 5f;
            TacticDiagnostics.Count("weapon.pistolBackByEft");
        }
        // 10/13 21:47 sim (Bot161 MCX 0/30 +3 mags): the pistol drawn by the out-of-ammo fallback (not by us, so the check
        // above missed it) was put away by EFT's reload (TryChangeToMain) and the reload utility drew it again 1.8s later,
        // before the main gun was reloaded. Any pistol -> other gun change we didn't make gets the same 5s hold-off.
        else if (_lastSlot == EquipmentSlot.Holster && slot != EquipmentSlot.Holster && Time.time - _ourChangeTime > 1f)
        {
            _pistolRefusedUntil = Mathf.Max(_pistolRefusedUntil, Time.time + 5f);
            TacticDiagnostics.Count("weapon.pistolAwayByEft.holdOff");
        }
        _lastSlot = slot;
    }

    private EquipmentSlot _lastSlot = EquipmentSlot.FirstPrimaryWeapon;
    private float _ourChangeTime = -10f;

    /// <summary>
    /// SAIN drew the DMR / sniper for a far enemy and still holds it. EFT's reload check (BotReloadMagazine.CanReload ->
    /// TryChangeToMain) would draw the main gun back whenever the enemy is far - i.e. every time this gun needs a reload
    /// (NoTryChangeToEmptyMainPatch skips that while this is true).
    /// </summary>
    public bool HoldsLongRangeByChoice
    {
        get
        {
            var wm = BotOwner?.WeaponManager;
            return _longRangeByUs && wm?.Selector != null && wm.Selector.EquipmentSlot == EquipmentSlot.SecondPrimaryWeapon
                && wm.SecondWeaponInfo != null && WeaponLog.HasAmmoFor(Bot.Player, wm.SecondWeaponInfo.weapon);
        }
    }

    /// <summary>A loaded pistol we could switch to right now (not already in hands).</summary>
    public bool PistolReady()
    {
        var wm = BotOwner.WeaponManager;
        if (wm?.Selector != null)
        {
            NoteEftPutPistolAway(wm.Selector.EquipmentSlot);
        }
        if (!Applies || !Settings.PistolSwap || wm?.Selector == null || wm.Selector.EquipmentSlot == EquipmentSlot.Holster)
        {
            return false;
        }
        if (Time.time < _pistolRefusedUntil)
        {
            return false;
        }
        var pistol = wm.PistolWeaponInfo;
        // BotWeaponInfo.BulletCount reads the gun IN HAND (here the main gun), not the pistol (kb 2.34).
        return pistol != null && WeaponLog.LoadedRounds(pistol.weapon) > 0;
    }

    // zzap: the game refused the holster swap (13th/14th sims: 245-306 refusals, the same bots re-asking every tick and
    // never reloading in between) - don't offer the pistol again for 3s.
    private float _pistolRefusedUntil;
    private bool _pistolBackHeldNoted;

    /// <summary>ReloadUtility picked the pistol (mag nearly empty, enemy in sight within 30m).</summary>
    public bool SwapToPistol(Enemy enemy, float ammoRatio)
    {
        if (!PistolReady() || !ForceChange(EquipmentSlot.Holster))
        {
            _pistolRefusedUntil = Time.time + 3f;
            TacticDiagnostics.Count("weapon.pistolSwap.refused");
            return false;
        }
        _pistolByUs = true;
        TacticDiagnostics.Count("weapon.pistolSwap");
        if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat($"[Weapon] [{Bot.name}] mag {ammoRatio:P0}, enemy {enemy.RealDistance:0}m in sight -> PISTOL instead of reload");
        return true;
    }

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
        if (selector.EquipmentSlot == EquipmentSlot.Holster || Time.time < _pistolRefusedUntil)
        {
            return false;
        }
        bool inSight = enemy.IsVisible || (enemy.Seen && enemy.TimeSinceSeen < 1.5f);
        if (!inSight || enemy.RealDistance > s.PistolSwapMaxDistance)
        {
            return false;
        }
        var pistol = wm.PistolWeaponInfo;
        if (pistol == null || WeaponLog.LoadedRounds(pistol.weapon) <= 0)
        {
            TacticDiagnostics.Count("weapon.pistolSwap.noPistolOrAmmo");
            return false;
        }
        if (!ForceChange(EquipmentSlot.Holster))
        {
            _pistolRefusedUntil = Time.time + 3f;
            TacticDiagnostics.Count("weapon.pistolSwap.refused");
            return false;
        }
        _pistolByUs = true;
        TacticDiagnostics.Count("weapon.pistolSwap");
        if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat($"[Weapon] [{Bot.name}] magazine empty, enemy {enemy.RealDistance:0}m in sight -> PISTOL instead of reload");
        return true;
    }

    public override void ManualUpdate()
    {
        // zzap (Remix 4.2 feedback): keep BSG's per-slot weapon table in step with looted guns (WeaponInfoSync)
        WeaponInfoSync.Tick(BotOwner);
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

        NoteEftPutPistolAway(slot);

        if (slot == EquipmentSlot.Holster && _pistolByUs)
        {
            bool empty = wm.PistolWeaponInfo == null || wm.PistolWeaponInfo.BulletCount <= 0;
            // 10/12 sim ping-pong (Bot27, 1 round in the main gun): the goal enemy switched to one shooting at us but not seen
            // yet, which counted as "gone" -> main gun back unreloaded -> he showed up 2s later -> pistol again. Someone
            // shooting at us in the last 6s is not gone.
            bool shotAtMe = enemy != null && enemy.Status.ShotAtMe && enemy.Status.TimeLastShotAtMe > 0f
                && Time.time - enemy.Status.TimeLastShotAtMe < 6f;
            bool gone = enemy == null || (!enemy.IsVisible && !shotAtMe && (!enemy.Seen || enemy.TimeSinceSeen > 6f));
            bool far = enemy != null && enemy.RealDistance > s.PistolSwapMaxDistance + 15f;
            if (!gone && !_pistolBackHeldNoted && enemy != null && !enemy.IsVisible && (!enemy.Seen || enemy.TimeSinceSeen > 6f))
            {
                _pistolBackHeldNoted = true;
                TacticDiagnostics.Count("weapon.pistolBackHeld");
            }
            if (empty || gone || far)
            {
                string why = empty ? "pistol empty" : gone ? "enemy gone" : "enemy far";
                if (ForceChange(selector._mainWeapon))
                {
                    _pistolByUs = false;
                    // Give the main gun time to be reloaded before the pistol is offered again.
                    _pistolRefusedUntil = Time.time + 5f;
                    _pistolBackHeldNoted = false;
                    TacticDiagnostics.Count("weapon.pistolBack");
                    if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat($"[Weapon] [{Bot.name}] back to the main gun ({why})");
                }
            }
            return;
        }

        if (slot == EquipmentSlot.SecondPrimaryWeapon && _longRangeByUs)
        {
            bool close = enemy != null && enemy.RealDistance < s.SniperSwapBackDistance;
            bool gone = enemy == null || !enemy.Seen || enemy.TimeSinceSeen > 20f;
            // 23:30 sim Reserve Bot132 (G28 <-> Vector 4 times in 20 s): the goal enemy flipped between the one in sight at 81m
            // and an old one unseen for 33 s, and each flip read as "gone". Another far enemy seen in the last 20 s keeps it.
            if (gone && !close && OtherFarEnemySeen(s.SniperSwapBackDistance))
            {
                gone = false;
                if (!_keptLongRangeNoted)
                {
                    _keptLongRangeNoted = true;
                    TacticDiagnostics.Count("weapon.keptLongRange.otherEnemy");
                }
            }
            // WeaponLog.HasAmmoFor: EFT's CheckHaveAmmoForReload answers for the gun in hand and may change weapon itself
            bool empty = wm.SecondWeaponInfo == null || !WeaponLog.HasAmmoFor(Bot.Player, wm.SecondWeaponInfo.weapon);
            if (close || gone || empty)
            {
                string why = close ? $"enemy {enemy.RealDistance:0}m" : gone ? "enemy gone" : "no ammo";
                if (ForceChange(selector._mainWeapon))
                {
                    _longRangeByUs = false;
                    _keptLongRangeNoted = false;
                    TacticDiagnostics.Count("weapon.longRangeBack");
                    if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat($"[Weapon] [{Bot.name}] long-range weapon away, main gun ({why})");
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
        // the 2nd gun isn't in hand here - BulletCount / CheckHaveAmmoForReload would answer for the main gun
        if (second == null || !WeaponLog.HasAmmoFor(Bot.Player, second.weapon))
        {
            return;
        }
        if (ForceChange(EquipmentSlot.SecondPrimaryWeapon))
        {
            _longRangeByUs = true;
            _pistolByUs = false;
            TacticDiagnostics.Count("weapon.longRange");
            if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat(
                $"[Weapon] [{Bot.name}] enemy {enemy.RealDistance:0}m -> {((Weapon)selector.SecondPrimaryWeaponItem).ShortName.Localized()} (long range)"
            );
        }
    }

    private bool _keptLongRangeNoted;

    // Any known enemy (not only the goal enemy) seen in the last 20 s and still farther than the swap-back distance.
    private bool OtherFarEnemySeen(float minDistance)
    {
        var known = Bot.EnemyController.KnownEnemies;
        for (int i = 0; i < known.Count; i++)
        {
            Enemy other = known[i];
            if (other != null && other.Seen && other.TimeSinceSeen <= 20f && other.RealDistance >= minDistance)
            {
                return true;
            }
        }
        return false;
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
        _ourChangeTime = Time.time;
        return selector.TryChangeToSlot(slot, true);
    }
}
