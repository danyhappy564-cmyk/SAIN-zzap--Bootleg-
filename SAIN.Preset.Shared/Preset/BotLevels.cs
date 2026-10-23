using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.GlobalSettings;

namespace SAIN.Preset.Shared.Preset;

/// <summary>The global values a zzap level preset differs in, plus its pmcUSEC slot (ratio for bots it doesn't list).</summary>
public sealed class LevelGlobals
{
    public float VisibleDist;
    public float GainSight;
    public float Scattering;
    public float Hearing;
    public float AccuracySpeed;
    public float Recoil;
    public bool FasterCqb;
    public SAINSettingsClass PmcSlot;
    public float PmcModifier;

    public static LevelGlobals From(GlobalSettingsClass g)
    {
        if (g == null)
        {
            return null;
        }
        return new LevelGlobals
        {
            VisibleDist = g.Difficulty.VisibleDistCoef,
            GainSight = g.Difficulty.GainSightCoef,
            Scattering = g.Difficulty.ScatteringCoef,
            Hearing = g.Difficulty.HearingDistanceCoef,
            AccuracySpeed = g.Difficulty.ACCURACY_SPEED_COEF,
            Recoil = g.Shoot.BOT_RECOIL_COEF,
            FasterCqb = g.Aiming.FasterCQBReactionsGlobal,
        };
    }

    public static LevelGlobals From(SAINPresetBundle bundle)
    {
        var level = From(bundle?.GlobalSettings);
        if (level != null && bundle.BotSettings != null && bundle.BotSettings.TryGetValue(ESainWildSpawnType.pmcUSEC, out var pmc) && pmc != null)
        {
            level.PmcSlot = BotLevels.NormalSlot(pmc);
            level.PmcModifier = pmc.DifficultyModifier;
        }
        return level;
    }
}

/// <summary>Everything a level pick reads: the reference bot, the loaded preset's globals and the three zzap level presets.</summary>
public sealed class LevelSources
{
    /// <summary>The same bot in the bundled zzap preset (else SAIN's default preset); null for a bot neither lists.</summary>
    public SAINSettingsGroupClass Reference;
    public LevelGlobals Current;
    public LevelGlobals Easy;
    public LevelGlobals Normal;
    public LevelGlobals Hard;
    /// <summary>The same bot inside "zzap 쉬움" / "zzap 보통" / "zzap"; null when that preset doesn't list it.</summary>
    public SAINSettingsGroupClass EasyBot;
    public SAINSettingsGroupClass NormalBot;
    public SAINSettingsGroupClass HardBot;
}

/// <summary>
/// zzap (2026-10-23, user: "pick Killa -> hard and Killa should become hard"; spec 2026-10-23-bot-difficulty-levels): a level
/// writes values into the bot type's four slots. Easy / Normal / Hard follow the bundled "zzap 쉬움" / "zzap 보통" / "zzap"
/// presets for this one bot: the bot fields those presets differ in are copied from the same bot there, and the global
/// coefficients they differ in are folded into the matching bot field (value x level global / loaded preset global, so the
/// level means the same under any loaded preset). Impossible = Hard with the agreed multipliers on top. Behaviour settings
/// (door tactics, close combat...) are global only and stay with the loaded preset and the personality.
/// Known limits: bot recoil is capped at 3x by its range, EFT's precision speed has no per-bot field.
/// </summary>
public static class BotLevels
{
    private enum Kind
    {
        Global,
        Copied,
        Knob,
        Flag,
    }

    private sealed class LevelField
    {
        public FieldPath Path;
        public Kind Kind;
        public Func<LevelGlobals, float> Global;
        public float Impossible = 1f;
        public string Key => Path.Category.Name + "." + Path.Field.Name;
    }

    private static LevelField G(string category, string field, Func<LevelGlobals, float> global, float impossible = 1f) =>
        new() { Path = new FieldPath(category, field), Kind = Kind.Global, Global = global, Impossible = impossible };

    private static LevelField C(string category, string field, float impossible = 1f) =>
        new() { Path = new FieldPath(category, field), Kind = Kind.Copied, Impossible = impossible };

    private static LevelField K(string category, string field, float impossible) =>
        new() { Path = new FieldPath(category, field), Kind = Kind.Knob, Impossible = impossible };

    // The aim-time knob's other fields move with MAX_AIM_TIME (same ratio), see AimFollowers.
    private static readonly LevelField[] Fields =
    {
        G("Core", "VisibleDistance", l => l.VisibleDist, 1.2f),
        G("Core", "GainSightCoef", l => l.GainSight, 1.5f),
        G("Shoot", "AUTOMATIC_FIRE_SCATTERING_COEF", l => l.Scattering, 0.7f),
        G("Aiming", "COEF_IF_MOVE", l => l.Scattering, 0.7f),
        G("Core", "HearingDistanceMulti", l => l.Hearing, 1.2f),
        G("Core", "AccuratySpeed", l => l.AccuracySpeed),
        G("Shoot", "RecoilMultiplier", l => l.Recoil),
        C("Aiming", "MAX_AIM_TIME", 0.6f),
        C("Shoot", "FireratMulti"),
        C("Shoot", "BurstMulti"),
        C("Core", "VisibleAngle"),
        C("Move", "STRAFE_SPEED"),
        C("Aiming", "MAX_AIMING_UPGRADE_BY_TIME"),
        K("Aiming", "AimForHeadChance", 1.5f),
        K("Mind", "Aggression", 1.2f),
        new() { Path = new FieldPath("Aiming", "FasterCQBReactions"), Kind = Kind.Flag },
    };

    private static readonly FieldPath[] AimFollowers =
    {
        new("Aiming", "DistanceAimTimeMultiplier"),
        new("Aiming", "AngleAimTimeMultiplier"),
        new("Aiming", "FIRST_CONTACT_ADD_SEC"),
    };

    private const string ModifierKey = "DifficultyModifier";

    public static SAINSettingsClass NormalSlot(SAINSettingsGroupClass group)
    {
        if (group?.Settings == null)
        {
            return null;
        }
        return group.Settings.TryGetValue(ESainBotDifficulty.normal, out var slot) && slot != null ? slot : group.Settings.Values.FirstOrDefault(s => s != null);
    }

    public static bool Available(EBotLevel level, LevelSources src)
    {
        if (src?.Current == null)
        {
            return false;
        }
        return level switch
        {
            EBotLevel.Easy => src.Easy != null,
            EBotLevel.Normal => src.Normal != null,
            EBotLevel.Hard or EBotLevel.Impossible => src.Hard != null,
            _ => false,
        };
    }

    private static LevelGlobals GlobalsOf(EBotLevel level, LevelSources src) => level switch
    {
        EBotLevel.Easy => src.Easy,
        EBotLevel.Normal => src.Normal,
        _ => src.Hard,
    };

    private static SAINSettingsGroupClass BotOf(EBotLevel level, LevelSources src) => level switch
    {
        EBotLevel.Easy => src.EasyBot,
        EBotLevel.Normal => src.NormalBot,
        _ => src.HardBot,
    };

    private static float Ratio(float level, float current) => current > 1e-6f && level > 1e-6f ? level / current : 1f;

    // What a level starts from: the reference bot's normal slot, else the remembered LevelBase, else the bot's own values.
    private static float Base(FieldPath path, SAINSettingsGroupClass group, LevelSources src)
    {
        var reference = NormalSlot(src.Reference);
        if (reference != null)
        {
            return path.GetFloat(reference);
        }
        string key = path.Category.Name + "." + path.Field.Name;
        if (group.LevelBase != null && group.LevelBase.TryGetValue(key, out float stored))
        {
            return stored;
        }
        var own = NormalSlot(group);
        return own != null ? path.GetFloat(own) : 0f;
    }

    private static float BaseModifier(SAINSettingsGroupClass group, LevelSources src)
    {
        if (src.Reference != null)
        {
            return src.Reference.DifficultyModifier;
        }
        return group.LevelBase != null && group.LevelBase.TryGetValue(ModifierKey, out float stored) ? stored : group.DifficultyModifier;
    }

    /// <summary>The slot values the level gives (only the fields a level writes are meaningful); null when unavailable.</summary>
    public static SAINSettingsClass Compute(EBotLevel level, SAINSettingsGroupClass group, LevelSources src)
    {
        if (group == null || !Available(level, src))
        {
            return null;
        }
        var globals = GlobalsOf(level, src);
        var levelBot = NormalSlot(BotOf(level, src));
        bool impossible = level == EBotLevel.Impossible;
        var result = new SAINSettingsClass();
        float aimBase = 0f;
        foreach (var f in Fields)
        {
            if (f.Kind == Kind.Flag)
            {
                f.Path.Set(result, globals.FasterCqb);
                continue;
            }
            float b = Base(f.Path, group, src);
            float value = f.Kind switch
            {
                Kind.Global => b * Ratio(f.Global(globals), f.Global(src.Current)),
                Kind.Copied => levelBot != null
                    ? f.Path.GetFloat(levelBot)
                    : b * (globals.PmcSlot != null && src.Hard?.PmcSlot != null ? Ratio(f.Path.GetFloat(globals.PmcSlot), f.Path.GetFloat(src.Hard.PmcSlot)) : 1f),
                _ => b,
            };
            if (impossible)
            {
                value *= f.Impossible;
            }
            f.Path.SetFloat(result, value);
            if (f.Path.Field.Name == "MAX_AIM_TIME")
            {
                aimBase = b;
            }
        }
        float aimRatio = aimBase > 1e-6f ? Fields.First(f => f.Path.Field.Name == "MAX_AIM_TIME").Path.GetFloat(result) / aimBase : 1f;
        foreach (var path in AimFollowers)
        {
            path.SetFloat(result, Base(path, group, src) * aimRatio);
        }
        return result;
    }

    /// <summary>The group-level difficulty modifier the level gives.</summary>
    public static float ComputeModifier(EBotLevel level, SAINSettingsGroupClass group, LevelSources src)
    {
        var levelBot = BotOf(level, src);
        var globals = GlobalsOf(level, src);
        float value = levelBot != null
            ? levelBot.DifficultyModifier
            : BaseModifier(group, src) * (globals?.PmcSlot != null && src.Hard?.PmcSlot != null ? Ratio(globals.PmcModifier, src.Hard.PmcModifier) : 1f);
        return Math.Max(0.01f, Math.Min(1f, value));
    }

    private static IEnumerable<FieldPath> WrittenPaths() => Fields.Select(f => f.Path).Concat(AimFollowers);

    /// <summary>Write the level into every slot the group has and remember the pick (an unavailable level does nothing).</summary>
    public static void Apply(EBotLevel level, SAINSettingsGroupClass group, LevelSources src)
    {
        if (group?.Settings == null || !Available(level, src))
        {
            return;
        }
        if (src.Reference == null && (group.LevelBase == null || group.LevelBase.Count == 0))
        {
            // First pick on a bot with no reference: remember where it started.
            group.LevelBase ??= new Dictionary<string, float>();
            var own = NormalSlot(group);
            if (own != null)
            {
                foreach (var path in WrittenPaths().Where(p => p.Field.FieldType == typeof(float)))
                {
                    group.LevelBase[path.Category.Name + "." + path.Field.Name] = path.GetFloat(own);
                }
            }
            group.LevelBase[ModifierKey] = group.DifficultyModifier;
        }
        var values = Compute(level, group, src);
        foreach (var slot in group.Settings.Values)
        {
            if (slot == null)
            {
                continue;
            }
            foreach (var path in WrittenPaths())
            {
                path.Set(slot, path.Get(values));
            }
        }
        group.DifficultyModifier = ComputeModifier(level, group, src);
        group.DifficultyLevel = level;
    }

    private static bool Same(object a, object b) => a is float fa && b is float fb ? Math.Abs(fa - fb) <= 1e-4f : Equals(a, b);

    private static bool Matches(EBotLevel level, SAINSettingsGroupClass group, LevelSources src, IEnumerable<FieldPath> paths)
    {
        var values = Compute(level, group, src);
        if (values == null)
        {
            return false;
        }
        foreach (var slot in group.Settings.Values)
        {
            if (slot != null && paths.Any(p => !Same(p.Get(slot), p.Get(values))))
            {
                return false;
            }
        }
        return true;
    }

    public static bool IsAdjusted(SAINSettingsGroupClass group, LevelSources src)
    {
        if (group?.Settings == null || group.DifficultyLevel == EBotLevel.None || !Available(group.DifficultyLevel, src))
        {
            return false;
        }
        return !Matches(group.DifficultyLevel, group, src, WrittenPaths()) || Math.Abs(group.DifficultyModifier - ComputeModifier(group.DifficultyLevel, group, src)) > 1e-4f;
    }

    /// <summary>How many of the nine knobs differ from the picked level's values (for "N hand-adjusted values").</summary>
    public static int AdjustedCount(SAINSettingsGroupClass group, LevelSources src)
    {
        var level = group?.DifficultyLevel ?? EBotLevel.None;
        if (level == EBotLevel.None || !Available(level, src))
        {
            return 0;
        }
        var values = Compute(level, group, src);
        return BotQuickSettings.All.Count(knob => KnobDiffers(knob, group, values));
    }

    // The knob's fields a level writes (the headshot on/off, personality and grenades are not level fields).
    private static List<FieldPath> LevelFieldsOf(BotKnob knob)
    {
        var written = WrittenPaths().ToList();
        return knob.Fields.Where(p => written.Any(w => w.Category == p.Category && w.Field == p.Field)).ToList();
    }

    /// <summary>A level sets this knob (false: "not affected by the level" - personality, grenades).</summary>
    public static bool AffectsKnob(BotKnob knob) => knob != null && LevelFieldsOf(knob).Count > 0;

    /// <summary>The knob's level fields differ from the level values in some slot (the knob's "reset to level" shows).</summary>
    public static bool KnobDiffers(BotKnob knob, SAINSettingsGroupClass group, SAINSettingsClass levelValues)
    {
        if (knob == null || group?.Settings == null || levelValues == null)
        {
            return false;
        }
        var paths = LevelFieldsOf(knob);
        return group.Settings.Values.Any(slot => slot != null && paths.Any(p => !Same(p.Get(slot), p.Get(levelValues))));
    }

    /// <summary>Put the knob's level fields back to the level values in every slot.</summary>
    public static void ResetKnob(BotKnob knob, SAINSettingsGroupClass group, SAINSettingsClass levelValues)
    {
        if (knob == null || group?.Settings == null || levelValues == null)
        {
            return;
        }
        var paths = LevelFieldsOf(knob);
        foreach (var slot in group.Settings.Values)
        {
            if (slot == null)
            {
                continue;
            }
            foreach (var path in paths)
            {
                path.Set(slot, path.Get(levelValues));
            }
        }
    }

    /// <summary>
    /// The list badge: the picked level (Adjusted when changed since), or for a never-picked bot Hard when its values are
    /// zzap's, else Custom.
    /// </summary>
    public static (EBotLevel Level, bool Adjusted, bool Custom) Badge(SAINSettingsGroupClass group, LevelSources src)
    {
        if (group?.Settings == null)
        {
            return (EBotLevel.None, false, true);
        }
        if (group.DifficultyLevel != EBotLevel.None)
        {
            return (group.DifficultyLevel, IsAdjusted(group, src), false);
        }
        bool zzap = Available(EBotLevel.Hard, src) && Matches(EBotLevel.Hard, group, src, WrittenPaths())
            && Math.Abs(group.DifficultyModifier - ComputeModifier(EBotLevel.Hard, group, src)) <= 1e-4f;
        return zzap ? (EBotLevel.Hard, false, false) : (EBotLevel.None, false, true);
    }

    // Every value field of a slot: the categories' plain fields and the nested ones (part weights).
    private static readonly FieldPath[] AllPlain = typeof(SAINSettingsClass)
        .GetFields(BindingFlags.Public | BindingFlags.Instance)
        .Where(c => c.FieldType.IsClass && c.FieldType != typeof(string))
        .SelectMany(c => c.FieldType.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => f.FieldType == typeof(float) || f.FieldType == typeof(int) || f.FieldType == typeof(bool) || f.FieldType.IsEnum)
            .Select(f => new FieldPath(c.Name, f.Name)))
        .ToArray();

    private static void CopySlot(SAINSettingsClass from, SAINSettingsClass to)
    {
        foreach (var path in AllPlain)
        {
            path.Set(to, path.Get(from));
        }
        foreach (var row in BotAdvancedFields.Rows())
        {
            if (row.Nested == null)
            {
                continue;
            }
            object source = row.Owner(from);
            object target = row.Owner(to);
            if (source != null && target != null)
            {
                row.Field.SetValue(target, row.Field.GetValue(source));
            }
        }
    }

    private static void CopyGroup(SAINSettingsGroupClass from, SAINSettingsGroupClass to)
    {
        var fallback = NormalSlot(from);
        foreach (var pair in to.Settings)
        {
            var source = from.Settings.TryGetValue(pair.Key, out var s) && s != null ? s : fallback;
            if (pair.Value != null && source != null)
            {
                CopySlot(source, pair.Value);
            }
        }
        to.DifficultyModifier = from.DifficultyModifier;
    }

    /// <summary>"Use original": the reference's values in every slot, no level, SAIN AI (no reference: only the last two).</summary>
    public static void ResetToOriginal(SAINSettingsGroupClass group, LevelSources src)
    {
        if (group?.Settings == null)
        {
            return;
        }
        if (src?.Reference?.Settings != null)
        {
            CopyGroup(src.Reference, group);
        }
        group.DifficultyLevel = EBotLevel.None;
        group.UseVanillaAI = false;
        group.LevelBase?.Clear();
    }

    /// <summary>"Revert": back to the copy taken when the page opened or the preset was last saved.</summary>
    public static void RestoreSaved(SAINSettingsGroupClass group, SAINSettingsGroupClass saved)
    {
        if (group?.Settings == null || saved?.Settings == null)
        {
            return;
        }
        CopyGroup(saved, group);
        group.DifficultyLevel = saved.DifficultyLevel;
        group.UseVanillaAI = saved.UseVanillaAI;
        group.LevelBase = saved.LevelBase != null ? new Dictionary<string, float>(saved.LevelBase) : new Dictionary<string, float>();
    }

    /// <summary>A detached copy of a group's values (for "Revert"), without allocating new slots in the live preset.</summary>
    public static SAINSettingsGroupClass Snapshot(SAINSettingsGroupClass group)
    {
        if (group?.Settings == null)
        {
            return null;
        }
        var copy = new SAINSettingsGroupClass(group.Settings.Keys.ToArray());
        CopyGroup(group, copy);
        copy.DifficultyLevel = group.DifficultyLevel;
        copy.UseVanillaAI = group.UseVanillaAI;
        copy.LevelBase = group.LevelBase != null ? new Dictionary<string, float>(group.LevelBase) : new Dictionary<string, float>();
        return copy;
    }
}
