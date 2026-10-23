using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SAIN.Preset.Shared.Attributes;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.Models.Preset.Personalities;

namespace SAIN.Preset.Shared.Preset;

public enum BotKnobKind
{
    Number,
    Toggle,
    ToggleNumber,
    Personality,
}

/// <summary>One plain knob on the bot type page: a shown field plus the fields that move with it (same ratio).</summary>
public sealed class BotKnob
{
    public string Id;
    public string Group;
    public string NameKo;
    public string NameEn;
    public string HelpKo;
    public string HelpEn;
    public BotKnobKind Kind;
    public float Min;
    public float Max = 1f;
    public float Step = 0.01f;
    public bool HigherIsStronger = true;
    /// <summary>A narrower slider than the field's [MinMax] (UI only, the field's range still applies when typing).</summary>
    public float SliderMax;
    public string UnitKo = string.Empty;
    public string UnitEn = string.Empty;

    /// <summary>The number on the knob (first numeric field), the on/off field and the personality field, each null if absent.</summary>
    internal FieldPath Shown;
    internal FieldPath OnOff;
    internal FieldPath Personality;
    /// <summary>Every field this knob owns (reset / changed checks).</summary>
    internal FieldPath[] Fields = Array.Empty<FieldPath>();
    /// <summary>The numeric fields scaled together with <see cref="Shown"/>.</summary>
    internal FieldPath[] Scaled = Array.Empty<FieldPath>();

    // Text and slider rules shared with the global quick settings (DeltaText, units, reversed sliders).
    internal QuickSetting Text;

    public string Name(bool korean) => korean ? NameKo : NameEn;

    public string Help(bool korean) => korean ? HelpKo : HelpEn;

    public float Get(SAINSettingsClass s) => Shown != null ? Shown.GetFloat(s) : 0f;

    public bool GetOn(SAINSettingsClass s) => OnOff != null && OnOff.GetBool(s);

    public EPersonality GetPersonality(SAINSettingsClass s) => Personality != null ? (EPersonality)Personality.Get(s) : EPersonality.None;

    public string DeltaText(float value, float reference, bool korean) => Text.DeltaText(value, reference, korean);

    public string Format(float value, bool korean) => Text.Format(value, korean);

    public float ToSlider(float value) => Text.ToSlider(value);

    public float FromSlider(float position) => Text.FromSlider(position);

    public float Round(float value) => Text.Round(value);

    public int Direction(float value, float reference) => Text.Direction(value, reference);
}

/// <summary>A field inside <see cref="SAINSettingsClass"/>: category field (Aiming, Core...) then the value field.</summary>
internal sealed class FieldPath
{
    public readonly FieldInfo Category;
    public readonly FieldInfo Field;
    public readonly GUIValuesAttribute Range;

    public FieldPath(string category, string field)
    {
        Category = typeof(SAINSettingsClass).GetField(category) ?? throw new ArgumentException(category);
        Field = Category.FieldType.GetField(field) ?? throw new ArgumentException(category + "." + field);
        Range = Field.GetCustomAttributes(typeof(GUIValuesAttribute), true).FirstOrDefault() as GUIValuesAttribute;
    }

    public object Get(SAINSettingsClass s) => Field.GetValue(Category.GetValue(s));

    public float GetFloat(SAINSettingsClass s) => Convert.ToSingle(Get(s));

    public bool GetBool(SAINSettingsClass s) => (bool)Get(s);

    public void Set(SAINSettingsClass s, object value) => Field.SetValue(Category.GetValue(s), value);

    public void SetFloat(SAINSettingsClass s, float value)
    {
        if (Range != null)
        {
            value = Math.Max(Range.Min, Math.Min(Range.Max, value));
        }
        Set(s, value);
    }
}

/// <summary>
/// zzap (2026-10-23, bot type settings redesign, user: "too many numbers, can't tell what they mean or whether a change took"):
/// nine plain knobs per bot type, shared by F6 and the web page. Scope null = all four difficulty slots: a number moves every
/// slot by the same ratio (the gaps between difficulties stay), on/off and personality set every slot alike. Ranges come from
/// each shown field's [MinMax]/[Percentage]. Reset copies the reference slot's fields, so it is exact after any moves.
/// </summary>
public static class BotQuickSettings
{
    private static FieldPath P(string category, string field) => new(category, field);

    private static BotKnob Number(string id, string group, string ko, string en, string helpKo, string helpEn, bool higherIsStronger,
        string unitKo, string unitEn, Func<float, float, bool, string> change, params FieldPath[] fields)
    {
        var knob = new BotKnob
        {
            Id = id, Group = group, NameKo = ko, NameEn = en, HelpKo = helpKo, HelpEn = helpEn, Kind = BotKnobKind.Number,
            HigherIsStronger = higherIsStronger, UnitKo = unitKo, UnitEn = unitEn, Shown = fields[0], Fields = fields, Scaled = fields,
        };
        return Finish(knob, change);
    }

    private static BotKnob Finish(BotKnob knob, Func<float, float, bool, string> change)
    {
        var range = knob.Shown?.Range;
        if (range != null)
        {
            knob.Min = range.Min;
            knob.Max = range.Max;
            // The field's own rounding (final review 10/23: range/200 made spotting's step 0.05 = 25% of zzap's 0.2).
            knob.Step = range.Rounding > 0f ? 1f / range.Rounding : 0.01f;
        }
        if (knob.SliderMax > 0f && knob.SliderMax < knob.Max)
        {
            knob.Max = knob.SliderMax;
        }
        knob.Text = new QuickSetting
        {
            Id = knob.Id, NameKo = knob.NameKo, NameEn = knob.NameEn, Min = knob.Min, Max = knob.Max, Step = knob.Step,
            HigherIsStronger = knob.HigherIsStronger, UnitKo = knob.UnitKo, UnitEn = knob.UnitEn, Change = change,
        };
        return knob;
    }

    private static BotKnob Spotting(BotKnob knob)
    {
        knob.SliderMax = 1f;
        return Finish(knob, knob.Text.Change);
    }

    public static readonly BotKnob[] All =
    {
        Number("aimSpeed", "aim", "조준 속도", "Aim speed", "적을 보고 쏘기까지 걸리는 시간", "Time from seeing an enemy to shooting", false, "초", " s",
            QuickSettings.Diff("초", " s", "느림", "빠름", "slower", "faster"),
            P("Aiming", "MAX_AIM_TIME"), P("Aiming", "DistanceAimTimeMultiplier"), P("Aiming", "AngleAimTimeMultiplier"), P("Aiming", "FIRST_CONTACT_ADD_SEC")),
        Number("scatter", "aim", "탄 퍼짐", "Bullet scatter", "연사·이동 중 총이 흔들리는 정도", "How much the gun wanders in full auto and on the move", false, "배", "x",
            QuickSettings.Ratio("큼", "작음", "wider", "tighter"),
            P("Shoot", "AUTOMATIC_FIRE_SCATTERING_COEF"), P("Aiming", "COEF_IF_MOVE")),
        Finish(new BotKnob
        {
            Id = "headshot", Group = "aim", NameKo = "머리 노리기", NameEn = "Aim for the head", HelpKo = "헤드샷을 노릴 확률", HelpEn = "Chance to go for headshots",
            Kind = BotKnobKind.ToggleNumber, UnitKo = "%", UnitEn = "%", Shown = P("Aiming", "AimForHeadChance"), OnOff = P("Aiming", "AimForHead"),
            Fields = new[] { P("Aiming", "AimForHead"), P("Aiming", "AimForHeadChance") }, Scaled = new[] { P("Aiming", "AimForHeadChance") },
        }, QuickSettings.Diff("%p", " pp", "더", "덜", "more", "less")),
        Number("sight", "sense", "시야 거리", "Sight distance", "적을 볼 수 있는 기본 거리", "Base distance at which enemies can be seen", true, "m", " m",
            QuickSettings.Diff("m", " m", "김", "짧음", "farther", "shorter"), P("Core", "VisibleDistance")),
        // GainSightCoef's [MinMax] runs to 10 while zzap uses about 0.2-0.3: the slider stops at 1 so zzap isn't jammed at the left.
        Spotting(Number("spotting", "sense", "발견 속도", "Spotting speed", "보이는 적을 알아채는 빠르기", "How fast a visible enemy is noticed", true, "", "",
            QuickSettings.Ratio("빨리", "늦게", "sooner", "later"), P("Core", "GainSightCoef"))),
        Number("hearing", "sense", "소리 듣기 거리", "Hearing distance", "발소리·총소리를 듣는 거리 배율", "Multiplier on how far steps and shots are heard", true, "배", "x",
            QuickSettings.Ratio("김", "짧음", "farther", "shorter"), P("Core", "HearingDistanceMulti")),
        Number("aggression", "mind", "공격성", "Aggression", "잃은 적을 얼마나 빨리, 덜 조심스럽게 찾으러 가나", "How soon and how boldly a lost enemy is searched for", true, "배", "x",
            QuickSettings.Ratio("공격적", "신중", "more aggressive", "more careful"), P("Mind", "Aggression")),
        Finish(new BotKnob
        {
            Id = "personality", Group = "mind", NameKo = "성격 지정", NameEn = "Personality", HelpKo = "지정 안 함 = 확률로 섞음", HelpEn = "Unassigned = mixed by chance",
            Kind = BotKnobKind.Personality, Personality = P("Mind", "PersonalityOverride"), Fields = new[] { P("Mind", "PersonalityOverride") },
        }, null),
        Finish(new BotKnob
        {
            Id = "grenade", Group = "mind", NameKo = "수류탄 쓰기", NameEn = "Uses grenades", HelpKo = "끄면 수류탄을 던지지 않음", HelpEn = "Off = never throws grenades",
            Kind = BotKnobKind.Toggle, OnOff = P("Core", "CanGrenade"), Fields = new[] { P("Core", "CanGrenade") },
        }, null),
    };

    private static IEnumerable<SAINSettingsClass> Slots(SAINSettingsGroupClass group, ESainBotDifficulty? scope)
    {
        if (group?.Settings == null)
        {
            yield break;
        }
        if (scope.HasValue)
        {
            if (group.Settings.TryGetValue(scope.Value, out var one) && one != null)
            {
                yield return one;
            }
            yield break;
        }
        foreach (var slot in group.Settings.Values)
        {
            if (slot != null)
            {
                yield return slot;
            }
        }
    }

    /// <summary>Set the knob's number: one slot (scope) directly, or every slot by the ratio to the shown slot's value.</summary>
    public static void Apply(BotKnob knob, SAINSettingsGroupClass group, ESainBotDifficulty? scope, ESainBotDifficulty shown, float newValue)
    {
        if (knob?.Shown == null || group?.Settings == null)
        {
            return;
        }
        if (scope.HasValue)
        {
            foreach (var slot in Slots(group, scope))
            {
                SetDirect(knob, slot, newValue);
            }
            return;
        }
        float v0 = group.Settings.TryGetValue(shown, out var shownSlot) && shownSlot != null ? knob.Get(shownSlot) : 0f;
        if (Math.Abs(v0) < 1e-6f)
        {
            foreach (var slot in Slots(group, null))
            {
                SetDirect(knob, slot, newValue);
            }
            return;
        }
        float r = newValue / v0;
        foreach (var slot in Slots(group, null))
        {
            foreach (var field in knob.Scaled)
            {
                field.SetFloat(slot, field.GetFloat(slot) * r);
            }
        }
        if (shownSlot != null)
        {
            knob.Shown.SetFloat(shownSlot, newValue); // the shown slot lands exactly on the knob's value
        }
    }

    // One slot: the shown field gets the value, the fields moving with it keep their ratio to it.
    private static void SetDirect(BotKnob knob, SAINSettingsClass slot, float value)
    {
        float old = knob.Get(slot);
        if (Math.Abs(old) > 1e-6f)
        {
            float r = value / old;
            foreach (var field in knob.Scaled)
            {
                if (field != knob.Shown)
                {
                    field.SetFloat(slot, field.GetFloat(slot) * r);
                }
            }
        }
        knob.Shown.SetFloat(slot, value);
    }

    public static void ApplyToggle(BotKnob knob, SAINSettingsGroupClass group, ESainBotDifficulty? scope, bool on)
    {
        if (knob?.OnOff == null)
        {
            return;
        }
        foreach (var slot in Slots(group, scope))
        {
            knob.OnOff.Set(slot, on);
        }
    }

    public static void ApplyPersonality(BotKnob knob, SAINSettingsGroupClass group, ESainBotDifficulty? scope, EPersonality personality)
    {
        if (knob?.Personality == null)
        {
            return;
        }
        foreach (var slot in Slots(group, scope))
        {
            knob.Personality.Set(slot, personality);
        }
    }

    /// <summary>Copy the knob's fields back from the reference preset's same slots (missing reference or slot: left as is).</summary>
    public static void Reset(BotKnob knob, SAINSettingsGroupClass group, SAINSettingsGroupClass reference, ESainBotDifficulty? scope)
    {
        if (knob == null || group?.Settings == null || reference?.Settings == null)
        {
            return;
        }
        foreach (var pair in group.Settings)
        {
            if ((scope.HasValue && pair.Key != scope.Value) || pair.Value == null || !reference.Settings.TryGetValue(pair.Key, out var from) || from == null)
            {
                continue;
            }
            foreach (var field in knob.Fields)
            {
                field.Set(pair.Value, field.Get(from));
            }
        }
    }

    public static bool IsChanged(BotKnob knob, SAINSettingsGroupClass group, SAINSettingsGroupClass reference)
    {
        return knob != null && Differs(group, reference, knob.Fields);
    }

    /// <summary>Any knob or advanced field differs from the reference (the dot in the bot list).</summary>
    public static bool AnyChanged(SAINSettingsGroupClass group, SAINSettingsGroupClass reference)
    {
        return Differs(group, reference, AllSimpleFields);
    }

    private static bool Differs(SAINSettingsGroupClass group, SAINSettingsGroupClass reference, IReadOnlyList<FieldPath> fields)
    {
        if (group?.Settings == null || reference?.Settings == null)
        {
            return false;
        }
        foreach (var pair in group.Settings)
        {
            if (pair.Value == null || !reference.Settings.TryGetValue(pair.Key, out var other) || other == null)
            {
                continue;
            }
            foreach (var field in fields)
            {
                object a = field.Get(pair.Value);
                object b = field.Get(other);
                if (a is float fa && b is float fb ? Math.Abs(fa - fb) > 1e-4f : !Equals(a, b))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// zzap (2026-10-23, user: "switching easy/normal/hard doesn't change the numbers"): most zzap bots carry the same values in
    /// all four difficulty slots, so the page says so - and marks the knobs whose slots do differ (sight distance on bosses...).
    /// </summary>
    public static bool AllSlotsSame(SAINSettingsGroupClass group)
    {
        var slots = group?.Settings?.Values.Where(s => s != null).ToList();
        if (slots == null || slots.Count < 2)
        {
            return true;
        }
        foreach (var row in BotAdvancedFields.Rows())
        {
            object first = row.Get(slots[0]);
            for (int i = 1; i < slots.Count; i++)
            {
                if (!SameValue(first, row.Get(slots[i])))
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>The knob's fields are not the same in every slot the group has.</summary>
    public static bool DiffersBySlot(BotKnob knob, SAINSettingsGroupClass group)
    {
        var slots = group?.Settings?.Values.Where(s => s != null).ToList();
        if (knob == null || slots == null || slots.Count < 2)
        {
            return false;
        }
        foreach (var field in knob.Fields)
        {
            object first = field.Get(slots[0]);
            for (int i = 1; i < slots.Count; i++)
            {
                if (!SameValue(first, field.Get(slots[i])))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool SameValue(object a, object b) => a is float fa && b is float fb ? Math.Abs(fa - fb) <= 1e-4f : Equals(a, b);

    // Every float / int / bool / enum value field of every category (Aiming, Core...).
    private static readonly FieldPath[] AllSimpleFields = typeof(SAINSettingsClass)
        .GetFields(BindingFlags.Public | BindingFlags.Instance)
        .Where(c => c.FieldType.IsClass && c.FieldType != typeof(string))
        .SelectMany(c => c.FieldType.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => f.FieldType == typeof(float) || f.FieldType == typeof(int) || f.FieldType == typeof(bool) || f.FieldType.IsEnum)
            .Select(f => new FieldPath(c.Name, f.Name)))
        .ToArray();
}
