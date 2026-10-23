using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using SAIN.Preset.Shared.Attributes;
using SAIN.Preset.Shared.Personalities.BasePersonality;

namespace SAIN.Preset.Shared.Preset;

/// <summary>A direct reference to one existing setting; both editors share bounds and batch operations.</summary>
public sealed class SettingsQuickField
{
    public object Owner { get; }
    public FieldInfo Field { get; }
    public string Key { get; }
    public string Name { get; }
    public string Help { get; }
    public bool Toggle => Field.FieldType == typeof(bool);
    public float Min { get; }
    public float Max { get; }
    public float Step { get; }
    public float Value => Toggle ? ((bool)Field.GetValue(Owner) ? 1 : 0) : Convert.ToSingle(Field.GetValue(Owner));
    public SettingsQuickField(object owner, FieldInfo field, string key)
    {
        Owner = owner; Field = field; Key = key;
        var named = field.GetCustomAttribute<NameAndDescriptionAttribute>();
        Name = field.GetCustomAttribute<NameAttribute>()?.Value ?? named?.Name ?? field.Name;
        Help = field.GetCustomAttribute<DescriptionAttribute>()?.Value ?? named?.Description ?? "";
        var range = field.GetCustomAttributes<GUIValuesAttribute>().FirstOrDefault();
        Min = range?.Min ?? 0; Max = range?.Max ?? 10;
        Step = field.FieldType == typeof(int) ? 1 : range != null && range.Rounding > 0 ? 1 / range.Rounding : .01f;
    }
    public void Set(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return;
        if (Toggle) { Field.SetValue(Owner, value >= .5f); return; }
        float bounded = Math.Max(Min, Math.Min(Max, value));
        if (Field.FieldType == typeof(int)) Field.SetValue(Owner, (int)Math.Round(bounded));
        else Field.SetValue(Owner, bounded);
    }
}

public static class SettingsQuickFields
{
    private static readonly string[] PersonalityPaths =
    {
        "Assignment.Enabled", "Assignment.CanBeRandomlyAssigned", "Behavior.Search.SprintWhileSearchChance",
        "Behavior.General.AggressionMultiplier", "Behavior.Search.SearchBaseTime", "Behavior.Search.WillSearchForEnemy",
        "Behavior.Search.WillChaseDistantGunshots", "Behavior.Rush.CanRushEnemyReloadHeal",
        "Behavior.Rush.CanJumpCorners", "Behavior.General.KickOpenAllDoors",
    };
    public static List<SettingsQuickField> For(object target)
    {
        var result = new List<SettingsQuickField>();
        if (target == null) return result;
        if (target is PersonalitySettingsClass)
        {
            foreach (string path in PersonalityPaths)
            {
                object owner = target; var parts = path.Split('.'); FieldInfo field = null;
                for (int i = 0; i < parts.Length; i++)
                {
                    field = owner?.GetType().GetField(parts[i]);
                    if (i < parts.Length - 1) owner = field?.GetValue(owner);
                }
                if (owner != null && field != null) result.Add(new SettingsQuickField(owner, field, path));
            }
            return result;
        }
        foreach (var field in target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!field.IsDefined(typeof(DataMemberAttribute)) || field.IsDefined(typeof(HiddenAttribute))
                || field.IsDefined(typeof(AdvancedAttribute))) continue;
            bool numeric = field.FieldType == typeof(float) || field.FieldType == typeof(int);
            if (field.FieldType == typeof(bool) || numeric && field.GetCustomAttributes<GUIValuesAttribute>().Any())
                result.Add(new SettingsQuickField(target, field, field.Name));
        }
        return result.Take(10).ToList();
    }
    public static int Scale(IEnumerable<SettingsQuickField> fields, float ratio)
    {
        if (float.IsNaN(ratio) || float.IsInfinity(ratio) || ratio <= 0) return 0;
        int changed = 0;
        foreach (var field in fields.Where(f => !f.Toggle))
        {
            float before = field.Value; field.Set(before * ratio);
            if (Math.Abs(field.Value - before) > .00001f) changed++;
        }
        return changed;
    }
}
