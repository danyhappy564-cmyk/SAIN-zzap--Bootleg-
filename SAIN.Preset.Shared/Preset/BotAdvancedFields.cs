using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SAIN.Preset.Shared.Attributes;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using SAIN.Preset.Shared.Enums;

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

    private static List<BotFieldRow> _rows;

    /// <summary>
    /// The same list as value rows: a nested settings object (Aiming.AimTargetWeights, "Part Weights") becomes one row per value
    /// field inside it, so every row is a float / int / bool / enum that F6 and the web edit the same way (final review 10/23).
    /// </summary>
    public static IReadOnlyList<BotFieldRow> Rows()
    {
        if (_rows != null)
        {
            return _rows;
        }
        var rows = new List<BotFieldRow>();
        foreach (var (group, category, field) in List())
        {
            if (IsValue(field.FieldType))
            {
                rows.Add(new BotFieldRow(group, category, null, field));
                continue;
            }
            if (!field.FieldType.IsClass || field.FieldType == typeof(string))
            {
                continue;
            }
            foreach (var leaf in field.FieldType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (IsValue(leaf.FieldType) && leaf.GetCustomAttribute<HiddenAttribute>() == null)
                {
                    rows.Add(new BotFieldRow(group, category, field, leaf));
                }
            }
        }
        _rows = rows;
        return _rows;
    }

    private static bool IsValue(Type type) => type == typeof(float) || type == typeof(int) || type == typeof(bool) || type.IsEnum;
}

/// <summary>One editable value in a <see cref="SAINSettingsClass"/>: category, optional nested object, value field.</summary>
public sealed class BotFieldRow
{
    public readonly string Group;
    public readonly FieldInfo Category;
    public readonly FieldInfo Nested;
    public readonly FieldInfo Field;
    public readonly GUIValuesAttribute Range;

    public BotFieldRow(string group, FieldInfo category, FieldInfo nested, FieldInfo field)
    {
        Group = group;
        Category = category;
        Nested = nested;
        Field = field;
        Range = field.GetCustomAttributes(typeof(GUIValuesAttribute), true).FirstOrDefault() as GUIValuesAttribute;
    }

    /// <summary>The object holding the value in this slot (category object, or the nested object inside it).</summary>
    public object Owner(SAINSettingsClass slot)
    {
        object owner = slot == null ? null : Category.GetValue(slot);
        return owner == null || Nested == null ? owner : Nested.GetValue(owner);
    }

    public object Get(SAINSettingsClass slot)
    {
        object owner = Owner(slot);
        return owner == null ? null : Field.GetValue(owner);
    }
}

/// <summary>
/// zzap (2026-10-23, final review): editing one advanced value on the bot type page, shared by F6 and the web so they cannot
/// drift. Scope null = all slots: numbers move by the ratio of the shown slot's change (clamped to the field's range, ints
/// rounded), the shown slot lands exactly on the new value; bools / enums set every slot. Reset copies values per slot.
/// </summary>
public static class BotAdvancedEdit
{
    public static void Apply(SAINSettingsGroupClass group, ESainBotDifficulty? scope, ESainBotDifficulty shown, BotFieldRow row, object value)
    {
        if (group?.Settings == null || row == null || value == null)
        {
            return;
        }
        bool numeric = row.Field.FieldType == typeof(float) || row.Field.FieldType == typeof(int);
        double before = 0d;
        if (numeric && !scope.HasValue && group.Settings.TryGetValue(shown, out var shownSlot) && row.Get(shownSlot) is object b)
        {
            before = Convert.ToDouble(b);
        }
        double after = numeric ? Convert.ToDouble(value) : 0d;
        foreach (var pair in group.Settings)
        {
            if (pair.Value == null || (scope.HasValue && pair.Key != scope.Value))
            {
                continue;
            }
            object owner = row.Owner(pair.Value);
            if (owner == null)
            {
                continue;
            }
            if (!numeric)
            {
                row.Field.SetValue(owner, value);
                continue;
            }
            double target = scope.HasValue || pair.Key == shown || Math.Abs(before) < 1e-6d
                ? after
                : Convert.ToDouble(row.Field.GetValue(owner)) * (after / before);
            SetNumber(row, owner, target);
        }
    }

    private static void SetNumber(BotFieldRow row, object owner, double value)
    {
        if (row.Range != null)
        {
            value = Math.Max(row.Range.Min, Math.Min(row.Range.Max, value));
        }
        row.Field.SetValue(owner, row.Field.FieldType == typeof(int) ? (object)(int)Math.Round(value) : (float)value);
    }

    public static void Reset(SAINSettingsGroupClass group, SAINSettingsGroupClass reference, ESainBotDifficulty? scope, BotFieldRow row)
    {
        if (group?.Settings == null || reference?.Settings == null || row == null)
        {
            return;
        }
        foreach (var pair in group.Settings)
        {
            if (pair.Value == null || (scope.HasValue && pair.Key != scope.Value) || !reference.Settings.TryGetValue(pair.Key, out var from))
            {
                continue;
            }
            object owner = row.Owner(pair.Value);
            object fromOwner = row.Owner(from);
            if (owner != null && fromOwner != null)
            {
                row.Field.SetValue(owner, row.Field.GetValue(fromOwner));
            }
        }
    }

    /// <summary>Differs from the reference in any slot within the scope; no reference = false.</summary>
    public static bool Changed(SAINSettingsGroupClass group, SAINSettingsGroupClass reference, ESainBotDifficulty? scope, BotFieldRow row)
    {
        if (group?.Settings == null || reference?.Settings == null || row == null)
        {
            return false;
        }
        foreach (var pair in group.Settings)
        {
            if (pair.Value == null || (scope.HasValue && pair.Key != scope.Value) || !reference.Settings.TryGetValue(pair.Key, out var from))
            {
                continue;
            }
            object a = row.Get(pair.Value);
            object b = row.Get(from);
            if (a is float fa && b is float fb ? Math.Abs(fa - fb) > 1e-4f : !Equals(a, b))
            {
                return true;
            }
        }
        return false;
    }
}
