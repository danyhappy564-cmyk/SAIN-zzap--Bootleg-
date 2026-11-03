using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using SAIN.Preset.Shared.Enums;

namespace SAIN.Preset.Shared.Preset;

/// <summary>One multiplier of a bot type's per-map modifiers, with plain words for F6 and the web.</summary>
public sealed class MapModifierField
{
    public FieldInfo Field;
    public string NameKo;
    public string NameEn;
    public string HelpKo;
    public string HelpEn;
    /// <summary>True when a higher value makes the bot stronger (scatter and aim speed are the other way round).</summary>
    public bool HigherIsStronger;

    public string Name(bool korean) => korean ? NameKo : NameEn;

    public string Help(bool korean) => korean ? HelpKo : HelpEn;

    public float Get(DifficultySettings settings) => settings == null ? 1f : (float)Field.GetValue(settings);
}

/// <summary>
/// zzap (user 2026-11-04, player feedback: "on Labs bots should hear less, on Reserve keep the default - per bot type"):
/// reads and writes <see cref="SAINSettingsGroupClass.MapModifiers"/>. A map is stored only while one of its values is not 1x.
/// </summary>
public static class BotMapModifiers
{
    public const float MIN = 0.1f;
    public const float MAX = 3f;

    /// <summary>The maps offered (same as the global per-map modifiers: no Terminal, no Town).</summary>
    public static readonly ELocation[] Maps =
    {
        ELocation.Factory, ELocation.FactoryNight, ELocation.Customs, ELocation.Woods, ELocation.Shoreline, ELocation.Interchange,
        ELocation.Reserve, ELocation.Labs, ELocation.Lighthouse, ELocation.Streets, ELocation.GroundZero, ELocation.Labyrinth,
    };

    public static string MapName(ELocation map, bool korean)
    {
        if (!korean)
        {
            return map switch
            {
                ELocation.FactoryNight => "Factory (night)",
                ELocation.GroundZero => "Ground Zero",
                _ => map.ToString(),
            };
        }
        return map switch
        {
            ELocation.Factory => "공장",
            ELocation.FactoryNight => "공장 (밤)",
            ELocation.Customs => "세관",
            ELocation.Woods => "삼림",
            ELocation.Shoreline => "해안선",
            ELocation.Interchange => "인터체인지",
            ELocation.Reserve => "리저브",
            ELocation.Labs => "연구소",
            ELocation.Lighthouse => "등대",
            ELocation.Streets => "타르코프 시내",
            ELocation.GroundZero => "그라운드 제로",
            ELocation.Labyrinth => "미궁",
            _ => map.ToString(),
        };
    }

    public static readonly MapModifierField[] Fields =
    {
        F(nameof(DifficultySettings.HearingDistanceCoef), "청각 거리", "Hearing distance", "낮추면 소리를 덜 멀리서 들음", "Lower = hears from less far", true),
        F(nameof(DifficultySettings.VisibleDistCoef), "시야 거리", "Vision distance", "낮추면 덜 멀리 봄", "Lower = sees less far", true),
        F(nameof(DifficultySettings.GainSightCoef), "발견 속도", "Spotting speed", "낮추면 적을 늦게 알아챔", "Lower = spots enemies slower", true),
        F(nameof(DifficultySettings.AggressionCoef), "공격성", "Aggression", "낮추면 수색·돌격까지 더 기다림", "Lower = waits longer before searching or pushing", true),
        F(nameof(DifficultySettings.ScatteringCoef), "탄 퍼짐", "Scatter", "높이면 탄이 더 퍼짐 (약해짐)", "Higher = more spread (weaker)", false),
        F(nameof(DifficultySettings.PRECISION_SPEED_COEF), "정밀 조준 속도", "Precision speed", "낮추면 정밀 조준이 느려짐", "Lower = slower precise aim", true),
        F(nameof(DifficultySettings.ACCURACY_SPEED_COEF), "조준 맞추는 시간", "Aim time", "높이면 조준 맞추는 데 더 오래 걸림 (약해짐)", "Higher = longer to line up a shot (weaker)", false),
    };

    private static MapModifierField F(string field, string ko, string en, string helpKo, string helpEn, bool higherStronger)
    {
        var info = typeof(DifficultySettings).GetField(field) ?? throw new InvalidOperationException("DifficultySettings." + field + " missing");
        return new MapModifierField { Field = info, NameKo = ko, NameEn = en, HelpKo = helpKo, HelpEn = helpEn, HigherIsStronger = higherStronger };
    }

    /// <summary>This bot type's modifiers on that map; null = none (1x).</summary>
    public static DifficultySettings Get(SAINSettingsGroupClass group, ELocation map)
    {
        return group?.MapModifiers != null && group.MapModifiers.TryGetValue(map, out var settings) ? settings : null;
    }

    public static float Value(SAINSettingsGroupClass group, ELocation map, MapModifierField field) => field.Get(Get(group, map));

    /// <summary>Sets one multiplier; the map's entry is created on the first change and dropped once everything is back at 1x.</summary>
    public static void Set(SAINSettingsGroupClass group, ELocation map, MapModifierField field, float value)
    {
        if (group == null)
        {
            return;
        }
        value = (float)Math.Round(Math.Max(MIN, Math.Min(MAX, value)), 2);
        group.MapModifiers ??= new Dictionary<ELocation, DifficultySettings>();
        if (!group.MapModifiers.TryGetValue(map, out var settings))
        {
            if (Math.Abs(value - 1f) < 0.001f)
            {
                return;
            }
            settings = new DifficultySettings();
            group.MapModifiers[map] = settings;
        }
        field.Field.SetValue(settings, value);
        if (IsNeutral(settings))
        {
            group.MapModifiers.Remove(map);
        }
    }

    public static void Reset(SAINSettingsGroupClass group, ELocation map) => group?.MapModifiers?.Remove(map);

    public static bool IsNeutral(DifficultySettings settings) => settings == null || Fields.All(f => Math.Abs(f.Get(settings) - 1f) < 0.001f);

    /// <summary>Maps with any change for this bot type.</summary>
    public static IEnumerable<ELocation> ChangedMaps(SAINSettingsGroupClass group)
        => group?.MapModifiers == null ? Enumerable.Empty<ELocation>() : group.MapModifiers.Where(kv => !IsNeutral(kv.Value)).Select(kv => kv.Key);
}
