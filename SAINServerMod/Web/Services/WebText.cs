using SAIN.Editor;

namespace SAINServerMod.Web.Services;

/// <summary>
/// zzap fork: Korean / English for the SAIN web pages (user 2026-10-09: "all web pages in Korean, switchable to English").
/// Korean is the default; the choice is saved in ServerConfig.json (WebLanguage) and switched with the 한국어/English button
/// in the SAIN nav bar. Page text is written inline as T("한국어", "English"); preset setting names/descriptions use the same
/// Korean dictionary as the F6 editor (SAIN.Preset.Shared/Localization), so a setting missing there just shows in English.
/// </summary>
public static class WebText
{
    public static bool Korean { get; set; } = true;

    public static string T(string korean, string english)
    {
        return Korean ? korean : english;
    }

    /// <summary>Preset setting name / description / category (English attribute text is the key).</summary>
    public static string Setting(string? english)
    {
        if (string.IsNullOrEmpty(english))
        {
            return english ?? string.Empty;
        }
        return Korean && KoreanSettingText.Map.TryGetValue(english, out string? korean) ? korean : english;
    }

    /// <summary>Enum option value ("EnumType.Value" key, same as F6). Personalities, bot types, decisions stay English.</summary>
    public static string Value(object? value)
    {
        if (value == null)
        {
            return string.Empty;
        }
        string raw = value.ToString() ?? string.Empty;
        if (!Korean || value is not Enum)
        {
            return raw;
        }
        return KoreanValueText.Map.TryGetValue(value.GetType().Name + "." + raw, out string? korean) ? korean : raw;
    }
}
