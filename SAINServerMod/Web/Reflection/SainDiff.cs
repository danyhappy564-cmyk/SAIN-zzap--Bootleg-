using System.Collections;
using SAIN.Preset.Shared.Preset;
using SAINServerMod.Web.Services;

namespace SAINServerMod.Web.Reflection;

/// <summary>One value that differs between two presets, with a way to put the reference value back.</summary>
public sealed record SainDiffEntry(string Where, string Name, string Before, string After, Action Revert);

/// <summary>
/// zzap fork: lists the values that differ between an edited preset and a reference (the saved state or a default preset),
/// for the web page's "changed values" tab (user 2026-10-18: "easier to see and set"). Walks both objects side by side
/// with the same reflection the editors use, so it only reports what the editors can show.
/// </summary>
public static class SainDiff
{
    private const int MaxDepth = 10;
    private const int MaxEntries = 500;

    public static List<SainDiffEntry> Compare(SAINPresetBundle edited, SAINPresetBundle reference)
    {
        var list = new List<SainDiffEntry>();
        Walk(edited.GlobalSettings, reference.GlobalSettings, WebText.T("전역", "Global"), list, 0);
        WalkDictionary(edited.BotSettings, reference.BotSettings, WebText.T("봇", "Bots"), list, 0);
        WalkDictionary(edited.Personalities, reference.Personalities, WebText.T("성격", "Personalities"), list, 0);
        return list;
    }

    public static int Count(SAINPresetBundle edited, SAINPresetBundle reference)
    {
        return Compare(edited, reference).Count;
    }

    private static void Walk(object? a, object? b, string where, List<SainDiffEntry> list, int depth)
    {
        if (a == null || b == null || depth > MaxDepth || list.Count >= MaxEntries || a.GetType() != b.GetType())
        {
            return;
        }
        var left = SainReflection.Members(a);
        var right = SainReflection.Members(b);
        for (int i = 0; i < left.Count && i < right.Count; i++)
        {
            var ma = left[i];
            var mb = right[i];
            switch (ma.Kind)
            {
                case SainFieldKind.Bool:
                case SainFieldKind.Float:
                case SainFieldKind.Int:
                case SainFieldKind.Enum:
                    object? va = ma.GetValue();
                    object? vb = mb.GetValue();
                    if (!Same(va, vb))
                    {
                        var target = ma;
                        list.Add(new SainDiffEntry(where, WebText.Setting(ma.Meta.Name), Show(vb), Show(va), () => target.SetValue(vb)));
                    }
                    break;
                case SainFieldKind.Nested:
                    Walk(ma.GetValue(), mb.GetValue(), where + " › " + WebText.Setting(ma.Meta.Name), list, depth + 1);
                    break;
                case SainFieldKind.Dictionary:
                    // A bot type's per-difficulty "Settings" map adds nothing to the breadcrumb (Bots › pmcUSEC › easy › Aiming).
                    string label = ma.Member.Name == "Settings" ? where : where + " › " + WebText.Setting(ma.Meta.Name);
                    WalkDictionary(ma.GetValue() as IDictionary, mb.GetValue() as IDictionary, label, list, depth + 1);
                    break;
            }
        }
    }

    private static void WalkDictionary(IDictionary? a, IDictionary? b, string where, List<SainDiffEntry> list, int depth)
    {
        if (a == null || b == null || depth > MaxDepth)
        {
            return;
        }
        foreach (object key in a.Keys)
        {
            if (list.Count >= MaxEntries || !b.Contains(key))
            {
                continue;
            }
            object? va = a[key];
            object? vb = b[key];
            string label = where + " › " + WebText.Value(key);
            if (va == null || vb == null)
            {
                continue;
            }
            var kind = SainValue.Classify(va.GetType());
            if (kind is SainFieldKind.Bool or SainFieldKind.Float or SainFieldKind.Int or SainFieldKind.Enum)
            {
                if (!Same(va, vb))
                {
                    var dict = a;
                    var k = key;
                    list.Add(new SainDiffEntry(where, WebText.Value(key), Show(vb), Show(va), () => dict[k] = vb));
                }
            }
            else if (kind == SainFieldKind.Nested)
            {
                Walk(va, vb, label, list, depth + 1);
            }
            else if (kind == SainFieldKind.Dictionary)
            {
                WalkDictionary(va as IDictionary, vb as IDictionary, label, list, depth + 1);
            }
        }
    }

    private static bool Same(object? a, object? b)
    {
        if (a is float fa && b is float fb)
        {
            return Math.Abs(fa - fb) < 0.0001f;
        }
        if (a is double da && b is double db)
        {
            return Math.Abs(da - db) < 0.0001d;
        }
        return Equals(a, b);
    }

    private static string Show(object? value)
    {
        return value switch
        {
            null => "-",
            bool v => v ? WebText.T("켬", "On") : WebText.T("끔", "Off"),
            float f => Math.Round(f, 3).ToString("0.###"),
            double d => Math.Round(d, 3).ToString("0.###"),
            Enum => WebText.Value(value),
            _ => value.ToString() ?? "-",
        };
    }
}
