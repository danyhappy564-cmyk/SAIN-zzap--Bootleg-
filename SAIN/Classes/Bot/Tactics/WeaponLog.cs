using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using EFT;
using EFT.InventoryLogic;
using SAIN.Components;
using SAIN.SAINComponent.Classes.EnemyClasses;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork: every weapon change a SAIN bot makes, whoever asked for it (BSG, original SAIN or zzap code), as one
/// [Weapon] journal line - from -> to, who called it, each gun's rounds and spare magazines, the fight around it - plus a
/// PING-PONG flag when a bot changes 3+ times in 20s (user report 2026-09-29: main <-> pistol / main <-> second gun loops).
/// </summary>
public static class WeaponLog
{
    private const int PING_PONG_COUNT = 3;
    private const float PING_PONG_WINDOW = 20f;

    private static readonly Dictionary<string, List<float>> _recent = new();

    public static void OnChange(BotOwner owner, EquipmentSlot from, EquipmentSlot to)
    {
        // Only a log: the caller stack walk and per-gun ammo text are built when something will print them.
        if (!TacticDiagnostics.LogOn && !TacticDiagnostics.CountOn)
        {
            return;
        }
        if (owner == null || !SAINEnableClass.GetSAIN(owner.ProfileId, out BotComponent bot) || bot == null)
        {
            return;
        }
        if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"weapon.change.{from}->{to}");

        float now = Time.time;
        if (!_recent.TryGetValue(owner.ProfileId, out var times))
        {
            times = new List<float>();
            _recent[owner.ProfileId] = times;
        }
        times.Add(now);
        times.RemoveAll(t => now - t > PING_PONG_WINDOW);
        bool pingPong = times.Count >= PING_PONG_COUNT;
        if (pingPong)
        {
            TacticDiagnostics.Count("weapon.pingPong");
        }

        Enemy enemy = bot.GoalEnemy;
        string fight = enemy == null
            ? "no enemy"
            : $"enemy {(enemy.IsVisible ? "visible" : $"hidden {enemy.TimeSinceSeen:0}s")} {enemy.RealDistance:0}m";
        var d = bot.Decision;
        if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat(
            $"[Weapon] [{bot.name}] [{bot.Info.Personality}] {from} -> {to} | via {Caller()} | {fight}, combat={d.CurrentCombatDecision} self={d.CurrentSelfDecision} "
                + $"underFire={owner.Memory.IsUnderFire} | {Guns(owner)}"
                + (pingPong ? $" | PING-PONG ({times.Count} changes in {PING_PONG_WINDOW:0}s)" : "")
        );
    }

    /// <summary>"main M4A1 0/30 +0 mags (loose ammo: yes) | 2nd - | pistol PM 8/8 +1" for the bot's guns.</summary>
    public static string Guns(BotOwner owner)
    {
        var wm = owner?.WeaponManager;
        if (wm?.info == null)
        {
            return "guns ?";
        }
        var sb = new StringBuilder();
        EquipmentSlot inHands = wm.Selector != null ? wm.Selector.EquipmentSlot : EquipmentSlot.FirstPrimaryWeapon;
        Append(sb, owner, wm, EquipmentSlot.FirstPrimaryWeapon, "main", inHands);
        Append(sb, owner, wm, EquipmentSlot.SecondPrimaryWeapon, "2nd", inHands);
        Append(sb, owner, wm, EquipmentSlot.Holster, "pistol", inHands);
        return sb.ToString();
    }

    private static void Append(StringBuilder sb, BotOwner owner, BotWeaponManager wm, EquipmentSlot slot, string label, EquipmentSlot inHands)
    {
        if (sb.Length > 0)
        {
            sb.Append(" | ");
        }
        sb.Append(slot == inHands ? "*" : "").Append(label).Append(' ');
        if (!wm.info.TryGetValue(slot, out BotWeaponInfo info) || info?.weapon == null)
        {
            sb.Append('-');
            return;
        }
        Weapon weapon = info.weapon;
        int max = weapon.GetCurrentMagazine()?.MaxCount ?? 0;
        // This slot's own rounds (info.BulletCount reads the gun in hand - 10/10 sim journal showed the main gun's count on
        // every slot). EFT's reload check says "nothing to reload" for a full gun too, so it's only reported when not full.
        int loaded = LoadedRounds(weapon);
        sb.Append(weapon.ShortName.Localized()).Append(' ').Append(loaded).Append('/').Append(max)
            .Append(" +").Append(SpareMagazines(owner.GetPlayer, weapon)).Append(" mags")
            .Append(loaded >= max || info.CheckHaveAmmoForReload() ? "" : " (no reload ammo)");
    }

    /// <summary>A gun other than the one in hand that has rounds loaded or ammo to reload - main first, then 2nd, pistol.</summary>
    public static bool PickGunWithAmmo(BotWeaponManager wm, EquipmentSlot current, out EquipmentSlot target)
    {
        target = current;
        if (wm?.info == null || wm.Selector == null)
        {
            return false;
        }
        foreach (EquipmentSlot slot in new[] { wm.Selector._mainWeapon, EquipmentSlot.FirstPrimaryWeapon, EquipmentSlot.SecondPrimaryWeapon, EquipmentSlot.Holster })
        {
            if (slot == current || !wm.info.TryGetValue(slot, out BotWeaponInfo info) || info == null)
            {
                continue;
            }
            // BotWeaponInfo.BulletCount reads the gun IN HAND (BotReload.BulletCount uses the shoot controller), not this
            // slot's gun - count this slot's own magazine + chamber (Remix 4.2 feedback: the M4 in slot 2 was never chosen).
            if (LoadedRounds(info.weapon) > 0 || info.CheckHaveAmmoForReload())
            {
                target = slot;
                return true;
            }
        }
        return false;
    }

    /// <summary>Rounds in this gun's magazine + chamber (whether or not it is in hand).</summary>
    public static int LoadedRounds(Weapon weapon)
    {
        if (weapon == null)
        {
            return 0;
        }
        int chamber = weapon.ChamberAmmoCount;
        if (weapon.ReloadMode == Weapon.EReloadMode.OnlyBarrel)
        {
            return chamber;
        }
        return (weapon.GetCurrentMagazine()?.Count ?? 0) + chamber;
    }

    /// <summary>Magazines the bot carries that fit this weapon, not counting the one in it.</summary>
    public static int SpareMagazines(Player player, Weapon weapon)
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

    /// <summary>The methods that asked for the change (skips the selector's own plumbing and Harmony frames).</summary>
    private static string Caller()
    {
        var sb = new StringBuilder();
        int taken = 0;
        var frames = new StackTrace(2, false).GetFrames();
        if (frames == null)
        {
            return "?";
        }
        foreach (var frame in frames)
        {
            var method = frame.GetMethod();
            var type = method?.DeclaringType;
            if (method == null || type == null)
            {
                continue;
            }
            string name = method.Name;
            if (type == typeof(WeaponLog) || name.Contains("TryChangeToSlot") || name.StartsWith("DMD<") || type.Namespace?.StartsWith("HarmonyLib") == true
                || type.Name.Contains("Patch"))
            {
                continue;
            }
            if (sb.Length > 0)
            {
                sb.Append(" <- ");
            }
            sb.Append(type.Name).Append('.').Append(name);
            if (++taken >= 3)
            {
                break;
            }
        }
        return sb.Length > 0 ? sb.ToString() : "?";
    }

    public static void Clear()
    {
        _recent.Clear();
    }
}
