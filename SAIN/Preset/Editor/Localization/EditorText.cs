using System.Collections.Generic;
using SAIN.Plugin;

namespace SAIN.Editor;

/// <summary>
/// zzap fork: Korean text for the F6 editor. The English text stays the key, so an entry without a
/// translation (e.g. a setting added later) just shows in English. Switch with the "한/EN" button
/// in the editor's top bar (saved as <see cref="PresetEditorDefaults.KoreanUI"/>).
/// </summary>
public static class EditorText
{
    public static bool Korean
    {
        get { return PresetHandler.EditorDefaults?.KoreanUI ?? true; }
    }

    /// <summary>Setting names, descriptions and categories from the preset attributes.</summary>
    public static string Setting(string english)
    {
        return Translate(KoreanSettingText.Map, english);
    }

    /// <summary>Editor buttons, tabs, labels and tooltips.</summary>
    public static string UI(string english)
    {
        return Translate(KoreanUIText.Map, english);
    }

    /// <summary>Option values (enum values, list keys) — personalities/bot types/decisions have no entry and stay English.</summary>
    public static string Value(object value)
    {
        if (value == null)
        {
            return string.Empty;
        }
        string raw = value.ToString();
        if (!Korean || !(value is System.Enum))
        {
            return raw;
        }
        return KoreanValueText.Map.TryGetValue(value.GetType().Name + "." + raw, out string korean) ? korean : raw;
    }

    private static string Translate(Dictionary<string, string> map, string english)
    {
        if (!Korean || string.IsNullOrEmpty(english))
        {
            return english;
        }
        return map.TryGetValue(english, out string korean) ? korean : english;
    }
}
