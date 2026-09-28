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
    public int Meds;
    public string Summary = "?";

    private float _nextRefresh;
    private Weapon _weapon;

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
        Meds = 0;
        try
        {
            var inventory = bot.Player?.Inventory;
            if (inventory != null)
            {
                foreach (Item item in inventory.GetPlayerItems(EPlayerItems.Equipment))
                {
                    if (item is ThrowWeap)
                    {
                        Grenades++;
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
        Summary = $"{cls}{(Bolt ? " bolt" : "")}{(FullAuto ? $" auto {FireRate}rpm" : "")}{(optic ? " scope" : redDot ? " dot" : "")} ideal {IdealRange:0}m, "
            + $"cqb {Cqb:0.0} long {Long:0.0}, nades {Grenades} meds {Meds}";
    }

    public static void Clear()
    {
        _profiles.Clear();
    }
}
