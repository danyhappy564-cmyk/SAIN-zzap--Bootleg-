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

    private static string Translate(Dictionary<string, string> map, string english)
    {
        if (!Korean || string.IsNullOrEmpty(english))
        {
            return english;
        }
        return map.TryGetValue(english, out string korean) ? korean : english;
    }
}
