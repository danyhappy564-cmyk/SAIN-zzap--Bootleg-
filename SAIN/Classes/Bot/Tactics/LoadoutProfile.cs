using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.InventoryLogic;
using SAIN.Components;
using SAIN.Preset.Shared.Enums;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork: what the bot is carrying decides how it wants to fight (user 2026-09-29: "bolt action vs mid-range gun,
/// fire rate, how many meds and grenades... a bot with only a sniper should fight at long range"). Read from the gun in
/// hands and the inventory, refreshed every 5s:
///   IdealRange - bolt/sniper with a scope 70m (no scope 40), DMR 50/30, rifle with scope 35 / red dot 25 / irons 20,
///                SMG 12, shotgun and pistol 8.
///   Cqb  (0-1) - close-quarters strength: SMG/shotgun or full-auto at 700+ rpm high, bolt actions zero.
///   Long (0-1) - long-range strength: scoped bolt/sniper high, scoped DMR, then scoped rifles.
///   Grenades / Meds - counts in the rig and bags.
/// The utility decisions use it: snipers don't push and back off when someone closes in, close-quarters guns push and
/// don't trade shots at long range, lots of grenades -> more grenades, lots of meds -> heal more readily.
/// </summary>
public sealed class LoadoutProfile
{
    public float IdealRange = 25f;
    public float Cqb = 0.5f;
    public float Long = 0.5f;
    public bool Bolt;
    public bool FullAuto;
    public int FireRate;
    public int Grenades;
    /// <summary>Of <see cref="Grenades"/>, the ones in the secured container (not lootable from the corpse).</summary>
    public int SecureGrenades;
    public int Meds;
    public string Summary = "?";

    private float _nextRefresh;
    private Weapon _weapon;
    private bool _nadesChecked;
    private static int _loggedMany;

    private static readonly Dictionary<string, LoadoutProfile> _profiles = new();

    public static LoadoutProfile Of(BotComponent bot)
    {
        if (!_profiles.TryGetValue(bot.ProfileId, out LoadoutProfile p))
        {
            p = new LoadoutProfile();
            _profiles[bot.ProfileId] = p;
        }
        var weapon = bot.BotOwner?.WeaponManager?.CurrentWeapon;
        if (Time.time >= p._nextRefresh || weapon != p._weapon)
        {
            p.Refresh(bot, weapon);
        }
        return p;
    }

    private void Refresh(BotComponent bot, Weapon weapon)
    {
        _nextRefresh = Time.time + 5f;
        _weapon = weapon;
        var info = bot.PlayerComponent?.Equipment?.CurrentWeaponInfo;
        EWeaponClass cls = info?.WeaponClass ?? EWeaponClass.assaultRifle;
        bool optic = info?.HasOptic == true;
        bool redDot = info?.HasRedDot == true;
        Bolt = false;
        FullAuto = false;
        FireRate = 0;
        try
        {
            if (weapon != null)
            {
                Bolt = weapon.BoltAction;
                FullAuto = weapon.WeapFireType != null && weapon.WeapFireType.Contains(Weapon.EFireMode.fullauto);
                FireRate = weapon.FireRate;
            }
        }
        catch
        {
        }

        switch (cls)
        {
            case EWeaponClass.sniperRifle:
                IdealRange = optic ? 70f : 40f;
                break;
            case EWeaponClass.marksmanRifle:
                IdealRange = Bolt ? (optic ? 70f : 40f) : optic ? 50f : 30f;
                break;
            case EWeaponClass.smg:
                IdealRange = 12f;
                break;
            case EWeaponClass.shotgun:
            case EWeaponClass.pistol:
                IdealRange = 8f;
                break;
            default:
                IdealRange = optic ? 35f : redDot ? 25f : 20f;
                break;
        }
        if (Bolt && cls != EWeaponClass.sniperRifle && cls != EWeaponClass.marksmanRifle)
        {
            IdealRange = Mathf.Max(IdealRange, optic ? 60f : 35f);
        }

        if (Bolt)
        {
            Cqb = 0f;
        }
        else if (cls == EWeaponClass.smg || cls == EWeaponClass.shotgun)
        {
            Cqb = 1f;
        }
        else if (FullAuto && FireRate >= 700)
        {
            Cqb = 0.9f;
        }
        else if (FullAuto)
        {
            Cqb = 0.7f;
        }
        else if (cls == EWeaponClass.marksmanRifle || cls == EWeaponClass.sniperRifle)
        {
            Cqb = 0.25f;
        }
        else
        {
            Cqb = 0.45f;
        }

        if ((Bolt || cls == EWeaponClass.sniperRifle) && optic)
        {
            Long = 1f;
        }
        else if (cls == EWeaponClass.marksmanRifle && optic)
        {
            Long = 0.8f;
        }
        else if (Bolt || cls == EWeaponClass.sniperRifle || cls == EWeaponClass.marksmanRifle)
        {
            Long = 0.55f;
        }
        else if (optic)
        {
            Long = 0.5f;
        }
        else if (cls == EWeaponClass.smg || cls == EWeaponClass.shotgun || cls == EWeaponClass.pistol)
        {
            Long = 0.05f;
        }
        else
        {
            Long = 0.25f;
        }

        Grenades = 0;
        SecureGrenades = 0;
        Meds = 0;
        Dictionary<string, int> nadeNames = !_nadesChecked ? new Dictionary<string, int>() : null;
        try
        {
            var inventory = bot.Player?.Inventory;
            if (inventory != null)
            {
                // zzap 2026-10-12: SPT's own rogue/raider/Partisan templates put ~50 F-1 in the secured container (exusec.json
                // SecuredContainer pool) - that was the "50 grenades" of the 10/11-10/12 sims, not a server mod. Counted apart.
                Item secure = inventory.Equipment?.GetSlot(EquipmentSlot.SecuredContainer)?.ContainedItem;
                foreach (Item item in inventory.GetPlayerItems(EPlayerItems.Equipment))
                {
                    if (item is ThrowWeap)
                    {
                        Grenades++;
                        if (secure != null && IsInside(item, secure))
                        {
                            SecureGrenades++;
                            continue;
                        }
                        if (nadeNames != null)
                        {
                            string n = item.ShortName.Localized();
                            nadeNames.TryGetValue(n, out int c);
                            nadeNames[n] = c + 1;
                        }
                    }
                    else if (item is MedKit || item.GetItemComponent<MedKitComponent>() != null)
                    {
                        Meds++;
                    }
                }
            }
        }
        catch
        {
        }
        // zzap 2026-10-11: grenade count per bot, checked once: counter loadout.nades.{0|1-3|4-9|10plus}.{faction} for the
        // grenades outside the secured container (what the corpse shows), loadout.secureNades.{faction} per bot with some in it, and a
        // [Loadout] line with what the grenades are when 10 or more are outside it.
        if (nadeNames != null)
        {
            _nadesChecked = true;
            var role = bot.Info?.Profile?.WildSpawnType ?? EFT.WildSpawnType.assault;
            string faction = SAIN.SimLab.SimFaction.Of(role);
            int carried = Grenades - SecureGrenades;
            string band = carried >= 10 ? "10plus" : carried >= 4 ? "4-9" : carried >= 1 ? "1-3" : "0";
            TacticDiagnostics.Count($"loadout.nades.{band}.{faction}");
            if (SecureGrenades > 0)
            {
                TacticDiagnostics.Count($"loadout.secureNades.{faction}");
            }
            if (carried >= 10)
            {
                var parts = new List<string>();
                foreach (var kv in nadeNames)
                {
                    parts.Add($"{kv.Key} x{kv.Value}");
                }
                string line = $"[Loadout] [{bot.name}] [role {role}] spawned with {carried} grenades (+{SecureGrenades} in secured container): {string.Join(", ", parts)}";
                if (_loggedMany < 5)
                {
                    _loggedMany++;
                    Logger.LogWarning(line);
                }
                if (TacticDiagnostics.LogOn)
                {
                    RaidJournal.Line(line);
                }
            }
        }
        Summary = $"{cls}{(Bolt ? " bolt" : "")}{(FullAuto ? $" auto {FireRate}rpm" : "")}{(optic ? " scope" : redDot ? " dot" : "")} ideal {IdealRange:0}m, "
            + $"cqb {Cqb:0.0} long {Long:0.0}, nades {Grenades} meds {Meds}";
    }

    private static bool IsInside(Item item, Item container)
    {
        for (Item parent = item.Parent?.Container?.ParentItem; parent != null; parent = parent.Parent?.Container?.ParentItem)
        {
            if (ReferenceEquals(parent, container))
            {
                return true;
            }
        }
        return false;
    }

    public static void Clear()
    {
        _profiles.Clear();
        _loggedMany = 0;
    }
}
