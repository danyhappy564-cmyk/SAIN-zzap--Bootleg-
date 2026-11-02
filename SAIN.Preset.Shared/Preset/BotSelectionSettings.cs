using System;
using System.Collections.Generic;
using System.Linq;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.GlobalSettings.Categories.General;
using SAIN.Preset.Shared.Models.Preset.Personalities;

namespace SAIN.Preset.Shared.Preset;

/// <summary>선택된 봇만 다루는 공용 계산. 호출자가 UI의 외부 바닐라 잠금도 걸러 줍니다.</summary>
public static class BotSelectionSettings
{
    public static IEnumerable<SAINSettingsGroupClass> Editable(IEnumerable<SAINSettingsGroupClass> groups, VanillaBotSettings global)
        => groups.Where(g => g != null && BotAiPolicy.Resolve(g.WildSpawnType, global, g).CanEditSain);

    public static bool Same(object a, object b) => a is float fa && b is float fb ? Math.Abs(fa-fb) <= 0.0001f : Equals(a,b);

    public static bool KnobMixed(IEnumerable<SAINSettingsGroupClass> groups, BotKnob knob)
    {
        var slots=groups.Where(g=>g!=null).Select(g=>(Group:g, Slot:BotLevels.NormalSlot(g))).Where(p=>p.Slot!=null).ToArray();
        if (slots.Length < 2) return false;
        var first=slots[0];
        foreach (var pair in slots.Skip(1))
        {
            if (knob.Kind == BotKnobKind.Personality)
            {
                if (!BotPersonalityPool.Candidates(first.Group.WildSpawnType,first.Slot.Mind).OrderBy(p=>p)
                    .SequenceEqual(BotPersonalityPool.Candidates(pair.Group.WildSpawnType,pair.Slot.Mind).OrderBy(p=>p))) return true;
            }
            else if ((knob.Kind == BotKnobKind.Toggle || knob.Kind == BotKnobKind.ToggleNumber) && knob.GetOn(first.Slot)!=knob.GetOn(pair.Slot)) return true;
            if ((knob.Kind == BotKnobKind.Number || knob.Kind == BotKnobKind.ToggleNumber) && knob.Format(knob.Get(first.Slot),false)!=knob.Format(knob.Get(pair.Slot),false)) return true;
        }
        return false;
    }

    public static bool FieldMixed(IEnumerable<SAINSettingsGroupClass> groups, BotFieldRow row, ESainBotDifficulty shown)
    {
        var values=groups.Where(g=>g?.Settings!=null && g.Settings.ContainsKey(shown)).Select(g=>row.Get(g.Settings[shown])).ToArray();
        return values.Length>1 && values.Skip(1).Any(v=>!Same(values[0],v));
    }

    public static void Apply(IEnumerable<SAINSettingsGroupClass> groups, BotKnob knob, float value)
    {
        foreach(var g in groups) BotQuickSettings.Apply(knob,g,null,Shown(g),value);
    }
    private static ESainBotDifficulty Shown(SAINSettingsGroupClass g) => g.Settings.FirstOrDefault(p=>p.Value==BotLevels.NormalSlot(g)).Key;
    public static void ApplyToggle(IEnumerable<SAINSettingsGroupClass> groups, BotKnob knob, bool value)
    { foreach(var g in groups) BotQuickSettings.ApplyToggle(knob,g,null,value); }
    public static void ApplyPersonality(IEnumerable<SAINSettingsGroupClass> groups, BotKnob knob, EPersonality value)
    { foreach(var g in groups) BotQuickSettings.ApplyPersonality(knob,g,null,value); }
    public static void Reset(IEnumerable<SAINSettingsGroupClass> groups, BotKnob knob, Func<SAINSettingsGroupClass,LevelSources> sources)
    { foreach(var g in groups) BotQuickSettings.Reset(knob,g,Baseline(g,sources(g)),null); }
    public static bool IsChanged(IEnumerable<SAINSettingsGroupClass> groups, BotKnob knob, Func<SAINSettingsGroupClass,LevelSources> sources)
        => groups.Any(g=>BotQuickSettings.IsChanged(knob,g,Baseline(g,sources(g))));
    public static void ApplyLevel(IEnumerable<SAINSettingsGroupClass> groups, EBotLevel level, Func<SAINSettingsGroupClass,LevelSources> sources)
    { foreach(var g in groups) BotLevels.Apply(level,g,sources(g)); }

    /// <summary>묶음 고급 편집은 같은 수를 각 난이도 칸에 직접 씁니다.</summary>
    public static void ApplyField(IEnumerable<SAINSettingsGroupClass> groups, BotFieldRow row, ESainBotDifficulty? scope, object value)
    {
        foreach(var g in groups)
            foreach(var difficulty in g.Settings.Keys.Where(d=>!scope.HasValue || d==scope.Value).ToArray())
                BotAdvancedEdit.Apply(g,difficulty,difficulty,row,value);
    }

    /// <summary>필드 기준: 프리셋 계열 원본(슬롯 저장은 zzap)에 선택 난이도가 쓰는 필드만 합칩니다.</summary>
    public static SAINSettingsGroupClass Baseline(SAINSettingsGroupClass group, LevelSources src, bool slot=false)
    {
        var baseline=BotLevels.Snapshot(slot ? src?.Reference ?? src?.BaselineBot : src?.BaselineBot ?? src?.Reference);
        if (baseline==null) return null;
        if (group.DifficultyLevel!=EBotLevel.None && src!=null)
            BotLevels.Apply(group.DifficultyLevel,baseline,src);
        return baseline;
    }
}
