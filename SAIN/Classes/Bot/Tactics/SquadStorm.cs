using System.Collections.Generic;
using System.Runtime.CompilerServices;
using EFT;
using EFT.InventoryLogic;
using SAIN.Components;
using SAIN.Models.Enums;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.Classes.WeaponFunction;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork: squad storm (user 2026-09-29: "if the enemy isn't much of a threat or is badly geared, rush him all
/// together"). A squad (2+ alive members within 35m) sizes up its enemy once per 20s: how weak he is from his gear and
/// state (no/low armor, no helmet, pistol or no gun, hurt, busy healing/reloading, alone). Weak enough -> the squad
/// storms him: each member joins by personality (Chad/GigaChad/Wreckless always, Normal 80%, Timmy 60%, Rat/Turtle 35%,
/// Coward 10%) and runs at him through SAIN's rush (sprint, corner chase, dog fight when in sight). Fire lane guard keeps
/// them out of each other's fire. F6: General > Squad Combat (zzap) > Squad Storm Weak Enemy. Logged as [Storm].
/// </summary>
public static class SquadStorm
{
    private const float DECISION_TIME = 20f;
    private const float SQUAD_RADIUS = 35f;

    private sealed class Decision
    {
        public float Until;
        public bool Storm;
        public float Weakness;
        public string Why;
        public readonly Dictionary<string, bool> Joined = new();
    }

    private static readonly Dictionary<string, Decision> _decisions = new();
    private static readonly List<ArmorComponent> _armor = new();

    private static readonly Dictionary<EPersonality, float> _join = new()
    {
        { EPersonality.GigaChad, 100f },
        { EPersonality.Chad, 100f },
        { EPersonality.Wreckless, 100f },
        { EPersonality.Normal, 80f },
        { EPersonality.Timmy, 60f },
        { EPersonality.Rat, 35f },
        { EPersonality.SnappingTurtle, 35f },
        { EPersonality.Coward, 10f },
    };

    public static bool ShallStorm(BotComponent bot, Enemy enemy, out string reason)
    {
        reason = string.Empty;
        var settings = GlobalSettingsClass.Instance?.General?.SquadCombat;
        if (settings == null || !settings.Enabled || !settings.SquadStorm || enemy?.EnemyPlayer == null)
        {
            return false;
        }
        if (!enemy.Seen || enemy.TimeSinceLastKnownUpdated > 12f || enemy.Path.PathLength > settings.SquadStormMaxDistance)
        {
            return false;
        }
        var health = bot.Memory.Health.HealthStatus;
        if (health == ETagStatus.BadlyInjured || health == ETagStatus.Dying || bot.Decision.CurrentSelfDecision != ESelfActionType.None)
        {
            return false;
        }
        if (SAINBotSuppressClass.CalcAmmoRatio(bot.BotOwner, out _) < 0.4f)
        {
            return false;
        }
        var group = bot.BotOwner.BotsGroup;
        if (group == null)
        {
            return false;
        }
        string key = $"{RuntimeHelpers.GetHashCode(group)}:{enemy.EnemyProfileId}";
        float time = Time.time;
        if (!_decisions.TryGetValue(key, out Decision d) || time > d.Until)
        {
            int near = SquadNear(bot);
            if (near < 2)
            {
                return false;
            }
            d = new Decision { Until = time + DECISION_TIME };
            d.Weakness = Weakness(bot, enemy, out d.Why);
            d.Storm = d.Weakness >= settings.SquadStormWeakness;
            _decisions[key] = d;
            TacticDiagnostics.Count(d.Storm ? "squad.storm.start" : "squad.storm.notWeak");
            TacticDiagnostics.LogCloseCombat(
                $"[Storm] [{bot.name}]'s squad ({near} close) sizes up {enemy.EnemyPlayer.Profile?.Nickname}: weakness {d.Weakness:0.00} ({d.Why}) -> "
                    + (d.Storm ? "STORM him" : $"no (needs {settings.SquadStormWeakness:0.00})")
            );
        }
        if (!d.Storm)
        {
            return false;
        }
        if (!d.Joined.TryGetValue(bot.ProfileId, out bool joined))
        {
            float chance = _join.TryGetValue(bot.Info.Personality, out float c) ? c : 50f;
            joined = Random.value * 100f < chance;
            d.Joined[bot.ProfileId] = joined;
            TacticDiagnostics.Count(joined ? $"squad.storm.join.{bot.Info.Personality}" : "squad.storm.stayBack");
            TacticDiagnostics.LogCloseCombat($"[Storm] [{bot.name}] [{bot.Info.Personality}] {(joined ? "joins the storm" : "stays back")}");
        }
        if (joined)
        {
            reason = "squadStorm";
        }
        return joined;
    }

    private static int SquadNear(BotComponent bot)
    {
        int count = 1;
        var members = bot.Squad?.Members;
        if (members == null)
        {
            return count;
        }
        foreach (var m in members.Values)
        {
            if (m == null || ReferenceEquals(m, bot) || m.IsDead)
            {
                continue;
            }
            if ((m.Position - bot.Position).sqrMagnitude < SQUAD_RADIUS * SQUAD_RADIUS)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>0 = dangerous, 1 = a pushover. From what the bot can see of his kit and state.</summary>
    public static float Weakness(BotComponent bot, Enemy enemy, out string why)
    {
        var parts = new List<string>();
        float score = 0f;
        Player p = enemy.EnemyPlayer;
        // The human player's kit may be anything (dev tools, stacked gear) - judging him by it misleads the bots. When set,
        // his gear counts as neutral and only his state (hurt, busy, alone) and the learned outcomes decide.
        bool ignoreGear = !enemy.IsAI && GlobalSettingsClass.Instance?.General?.PlayerStyle?.IgnorePlayerGear == true;
        if (ignoreGear)
        {
            score += 0.1f;
            parts.Add("player gear ignored");
        }
        else
        {
            score += GearWeakness(enemy, p, parts);
        }
        switch (p.HealthStatus)
        {
            case ETagStatus.Injured:
                score += 0.05f;
                parts.Add("hurt");
                break;
            case ETagStatus.BadlyInjured:
                score += 0.2f;
                parts.Add("badly hurt");
                break;
            case ETagStatus.Dying:
                score += 0.35f;
                parts.Add("dying");
                break;
        }
        if (enemy.Status.VulnerableAction != EEnemyAction.None)
        {
            score += 0.15f;
            parts.Add($"busy ({enemy.Status.VulnerableAction})");
        }
        if (Alone(bot, enemy))
        {
            score += 0.1f;
            parts.Add("alone");
        }
        why = parts.Count > 0 ? string.Join(", ", parts) : "well geared";
        return Mathf.Clamp01(score);
    }

    private static float GearWeakness(Enemy enemy, Player p, List<string> parts)
    {
        float score = 0f;
        var equipment = p.Inventory?.Equipment;
        float body = HighestClass(equipment, EquipmentSlot.ArmorVest);
        if (body <= 0f)
        {
            body = HighestClass(equipment, EquipmentSlot.TacticalVest);
        }
        if (body <= 0f)
        {
            score += 0.3f;
            parts.Add("no armor");
        }
        else if (body <= 2f)
        {
            score += 0.2f;
            parts.Add($"armor class {body:0}");
        }
        else if (body <= 3f)
        {
            score += 0.08f;
            parts.Add("armor class 3");
        }
        float helmet = HighestClass(equipment, EquipmentSlot.Headwear);
        if (helmet <= 0f)
        {
            score += 0.15f;
            parts.Add("no helmet");
        }
        else if (helmet <= 2f)
        {
            score += 0.05f;
            parts.Add("weak helmet");
        }
        var weapon = enemy.EnemyPlayerComponent?.Equipment?.CurrentWeaponInfo;
        if (weapon == null || p.HandsController is not Player.FirearmController)
        {
            score += 0.35f;
            parts.Add("no gun out");
        }
        else if (weapon.WeaponClass == EWeaponClass.pistol)
        {
            score += 0.3f;
            parts.Add("pistol");
        }
        else if (weapon.WeaponClass == EWeaponClass.smg)
        {
            score += 0.05f;
            parts.Add("smg");
        }
        return score;
    }

    private static bool Alone(BotComponent bot, Enemy enemy)
    {
        var known = bot.EnemyController?.KnownEnemies;
        if (known == null)
        {
            return true;
        }
        Vector3 pos = enemy.EnemyPosition;
        foreach (Enemy other in known)
        {
            if (other == null || ReferenceEquals(other, enemy) || !other.EnemyPlayer)
            {
                continue;
            }
            if ((other.EnemyPosition - pos).sqrMagnitude < 20f * 20f && other.TimeSinceLastKnownUpdated < 20f)
            {
                return false;
            }
        }
        return true;
    }

    private static float HighestClass(InventoryEquipment equipment, EquipmentSlot slot)
    {
        Item item = equipment?.GetSlot(slot)?.ContainedItem;
        if (item == null)
        {
            return 0f;
        }
        _armor.Clear();
        item.GetItemComponentsInChildrenNonAlloc(_armor, true);
        float best = 0f;
        foreach (var a in _armor)
        {
            if (a.ArmorClass > best)
            {
                best = a.ArmorClass;
            }
        }
        _armor.Clear();
        return best;
    }

    public static void Clear()
    {
        _decisions.Clear();
    }
}
