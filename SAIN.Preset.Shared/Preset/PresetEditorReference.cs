using System;
using System.Collections.Generic;
using System.Linq;

namespace SAIN.Preset.Shared.Preset;

/// <summary>Immutable original-preset references for UI comparison and quick-control defaults.</summary>
public static class PresetEditorReference
{
    public static string BaseName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return QuickSettings.ZzapPresets.OrderByDescending(p => p.Name.Length)
            .FirstOrDefault(p => string.Equals(name, p.Name, StringComparison.OrdinalIgnoreCase)
                || name.StartsWith(p.Name + " [Modified", StringComparison.OrdinalIgnoreCase))?.Name;
    }
    public static SAINPresetBundle Defaults(string name, IEnumerable<SAINPresetBundle> references, SAINPresetBundle fallback)
    {
        string baseName = BaseName(name);
        return baseName == null ? fallback : references?.FirstOrDefault(b =>
            string.Equals(b?.Info?.Name, baseName, StringComparison.OrdinalIgnoreCase)) ?? fallback;
    }
    public static Dictionary<string,float> Values(object target) => SettingsQuickFields.For(target).ToDictionary(f => f.Key, f => f.Value);
    public static int ResetQuick(object target, object original)
    {
        var values = Values(original);
        int changed = 0;
        foreach (var field in SettingsQuickFields.For(target))
        {
            if (!values.TryGetValue(field.Key, out float value) || Math.Abs(value - field.Value) < .00001f) continue;
            field.Set(value); changed++;
        }
        return changed;
    }
}
