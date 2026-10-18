using System.Text.RegularExpressions;

namespace SAINServerMod.Web.Updates;

/// <summary>
/// zzap fork: the web updates page lists this fork's GitHub releases, whose tags are release names, not semver
/// ("remake5", "remix5.1", older "Remake4.1" / "remake4.2" / "fix7" / "FIX3"). SAIN's own version number stays 4.5.1 for
/// preset compatibility, so it cannot tell releases apart - the running release name is kept here instead.
/// </summary>
public static class ZzapRelease
{
    /// <summary>Release this build belongs to. Bump together with the README / release notes on every release.</summary>
    public const string Current = "Remix5.1";

    public const string Repository = "danyhappy564-cmyk/SAIN-zzap--Bootleg-";

    // remakeN = (N, 0), RemixN.x = (N, x). Tags "Remake4.1" / "remake4.2" were remixes named before the scheme and still sort
    // right by their numbers. The fixN tags predate remake1 and sort below it.
    private static readonly Regex _name = new(@"(?<kind>remake|remix|fix)\s*(?<major>\d+)?(?:\.(?<minor>\d+))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool TryParse(string? text, out (int Era, int Major, int Minor) key)
    {
        key = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        Match m = _name.Match(text);
        if (!m.Success)
        {
            return false;
        }
        int major = m.Groups["major"].Success ? int.Parse(m.Groups["major"].Value) : 0;
        int minor = m.Groups["minor"].Success ? int.Parse(m.Groups["minor"].Value) : 0;
        bool fix = string.Equals(m.Groups["kind"].Value, "fix", StringComparison.OrdinalIgnoreCase);
        key = (fix ? 0 : 1, major, minor);
        return true;
    }

    public static int Compare((int Era, int Major, int Minor) a, (int Era, int Major, int Minor) b)
    {
        int c = a.Era.CompareTo(b.Era);
        if (c != 0)
        {
            return c;
        }
        c = a.Major.CompareTo(b.Major);
        return c != 0 ? c : a.Minor.CompareTo(b.Minor);
    }

    /// <summary>Tidy title from the tag ("remix5.1" → "Remix 5.1", "remake5" → "remake 5", "fix7" → "fix 7").</summary>
    public static string Label(string tag)
    {
        if (!TryParse(tag, out var key))
        {
            return tag;
        }
        if (key.Era == 0)
        {
            return key.Major > 0 ? $"fix {key.Major}" : "fix";
        }
        return key.Minor > 0 ? $"Remix {key.Major}.{key.Minor}" : $"remake {key.Major}";
    }

    /// <summary>
    /// The part of a zzap release body worth showing on the page: the Korean notes run from the install / changes heading to
    /// the first long dash rule, the English ones from "&lt;Changes (English)&gt;" to the next rule; the porting notice after
    /// them is left to GitHub. A body without those markers is shown whole.
    /// </summary>
    public static string Section(string? body, bool korean)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }
        string[] lines = body.Replace("\r\n", "\n").Split('\n');
        int start = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            bool hit = korean
                ? line.Contains("<설치", StringComparison.Ordinal) || line.Contains("<변경점>", StringComparison.Ordinal)
                : line.Contains("<Changes (English)>", StringComparison.OrdinalIgnoreCase);
            if (hit)
            {
                start = i;
                break;
            }
        }
        if (start < 0)
        {
            return body;
        }
        int end = lines.Length;
        for (int i = start + 1; i < lines.Length; i++)
        {
            if (IsLongRule(lines[i]))
            {
                end = i;
                break;
            }
        }
        return string.Join('\n', lines[start..end]);
    }

    // The KO/EN/notice parts are split by a rule of 20+ dashes; a plain "---" between items is kept.
    private static bool IsLongRule(string line)
    {
        string t = line.Trim();
        return t.Length >= 20 && t.All(c => c == '-');
    }
}
