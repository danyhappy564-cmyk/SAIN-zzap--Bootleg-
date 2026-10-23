using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SAIN.Preset.Shared.Attributes;
using SAIN.Preset.Shared.BotSettings.SAINSettings;

namespace SAIN.Preset.Shared.Preset;

/// <summary>
/// zzap (2026-10-23, bot type settings redesign): the "Advanced - every number" list on the bot type page, F6 and web alike.
/// Category order of <see cref="SAINSettingsClass"/>, named fields only, [Hidden] left out; EFT's own config values (named
/// "VANILLA EFT CONFIG VALUE : ...") go last in their own "vanilla" group.
/// </summary>
public static class BotAdvancedFields
{
    private const string VanillaPrefix = "VANILLA EFT CONFIG VALUE";

    private static List<(string Group, FieldInfo Category, FieldInfo Field)> _list;

    public static IReadOnlyList<(string Group, FieldInfo Category, FieldInfo Field)> List()
    {
        if (_list != null)
        {
            return _list;
        }
        var main = new List<(string, FieldInfo, FieldInfo)>();
        var vanilla = new List<(string, FieldInfo, FieldInfo)>();
        foreach (var category in typeof(SAINSettingsClass).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!category.FieldType.IsClass || category.FieldType == typeof(string))
            {
                continue;
            }
            foreach (var field in category.FieldType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                string name = field.GetCustomAttribute<NameAttribute>()?.Value;
                if (name == null || field.GetCustomAttribute<HiddenAttribute>() != null)
                {
                    continue;
                }
                if (name.StartsWith(VanillaPrefix))
                {
                    vanilla.Add(("vanilla", category, field));
                }
                else
                {
                    main.Add((category.Name, category, field));
                }
            }
        }
        _list = main.Concat(vanilla).ToList();
        return _list;
    }
}
