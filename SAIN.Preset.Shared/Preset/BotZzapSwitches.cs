using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SAIN.Preset.Shared.Attributes;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using SAIN.Preset.Shared.BotSettings.SAINSettings.Categories;
using SAIN.Preset.Shared.GlobalSettings;

namespace SAIN.Preset.Shared.Preset;

/// <summary>One per-bot ZZAP switch and the global switch it overrides (for "Global = On/Off" next to it).</summary>
public sealed class BotZzapSwitch
{
    public FieldInfo Field;
    /// <summary>English name (the localization key), from the field's [Name].</summary>
    public string Name;
    public string Description;
    public Func<GlobalSettingsClass, bool> Global;
}

/// <summary>
/// zzap (user 2026-11-04): the bot settings' ZZAP part - <see cref="SAINZzapBotSettings"/> edited for every difficulty slot of
/// every picked bot type at once (F6 and the web). A switch whose slots or bots differ reads as mixed (null).
/// </summary>
public static class BotZzapSwitches
{
    public static readonly BotZzapSwitch[] All =
    {
        S(nameof(SAINZzapBotSettings.DoorTactics), g => g.General.DoorTactics.Enabled),
        S(nameof(SAINZzapBotSettings.SquadCombat), g => g.General.SquadCombat.Enabled),
        S(nameof(SAINZzapBotSettings.Reposition), g => g.General.Reposition.Enabled),
        S(nameof(SAINZzapBotSettings.CloseCombat), g => true),
        S(nameof(SAINZzapBotSettings.DiamondStep), g => g.General.CloseCombat.DiamondStep),
        S(nameof(SAINZzapBotSettings.LeanSpam), g => g.General.CloseCombat.LeanSpam),
        S(nameof(SAINZzapBotSettings.CornerJiggle), g => g.General.CloseCombat.CornerJiggle),
    };

    private static BotZzapSwitch S(string field, Func<GlobalSettingsClass, bool> global)
    {
        var info = typeof(SAINZzapBotSettings).GetField(field) ?? throw new InvalidOperationException("SAINZzapBotSettings." + field + " missing");
        return new BotZzapSwitch
        {
            Field = info,
            Name = info.GetCustomAttribute<NameAttribute>()?.Value ?? field,
            Description = info.GetCustomAttribute<DescriptionAttribute>()?.Value,
            Global = global,
        };
    }

    private static IEnumerable<SAINZzapBotSettings> Slots(IEnumerable<SAINSettingsGroupClass> groups)
        => groups.Where(g => g?.Settings != null).SelectMany(g => g.Settings.Values).Where(s => s != null).Select(s => s.Zzap ??= new SAINZzapBotSettings());

    /// <summary>The value shared by every slot of every group; null when they differ (or there is nothing).</summary>
    public static EZzapToggle? Get(IEnumerable<SAINSettingsGroupClass> groups, BotZzapSwitch sw)
    {
        EZzapToggle? value = null;
        foreach (var slot in Slots(groups))
        {
            var v = (EZzapToggle)sw.Field.GetValue(slot);
            if (value == null)
            {
                value = v;
            }
            else if (value != v)
            {
                return null;
            }
        }
        return value;
    }

    public static void Set(IEnumerable<SAINSettingsGroupClass> groups, BotZzapSwitch sw, EZzapToggle value)
    {
        foreach (var slot in Slots(groups))
        {
            sw.Field.SetValue(slot, value);
        }
    }

    /// <summary>Count of switches not on Global in these groups (for the tab badge).</summary>
    public static int Changed(IEnumerable<SAINSettingsGroupClass> groups)
    {
        var list = groups.ToList();
        return All.Count(sw => Get(list, sw) is EZzapToggle v ? v != EZzapToggle.Global : true);
    }

    /// <summary>"Close Combat Moves" has no single global switch - its Global is "by Skip Scavs".</summary>
    public static bool HasGlobalSwitch(BotZzapSwitch sw) => sw.Field.Name != nameof(SAINZzapBotSettings.CloseCombat);
}
