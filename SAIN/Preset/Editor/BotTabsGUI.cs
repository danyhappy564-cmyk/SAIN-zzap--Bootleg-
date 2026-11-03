using System;
using System.Collections.Generic;
using System.Linq;
using SAIN.Plugin;
using SAIN.Preset.Shared;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using SAIN.Preset.Shared.BotSettings.SAINSettings.Categories;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.Preset;
using UnityEngine;

namespace SAIN.Editor;

/// <summary>
/// zzap (user 2026-11-04: "the bot settings are too crowded - split them into tabs; a ZZAP part per bot; personality right there;
/// per-map numbers per bot type"): the tab bar of the bot settings page and its two new tabs (ZZAP, maps). The quick knobs,
/// personality and advanced numbers stay in BotSelectionClass. Every change goes through <c>write</c> (applied next frame).
/// </summary>
internal static class BotTabsGUI
{
    public enum Tab
    {
        Quick,
        Personality,
        Zzap,
        Maps,
        Advanced,
    }

    public static Tab Current = Tab.Quick;

    private static bool Korean => EditorText.Korean;

    private static string T(string ko, string en) => Korean ? ko : en;

    private static string Name(Tab tab) => tab switch
    {
        Tab.Quick => T("빠른 설정", "Quick"),
        Tab.Personality => T("성격", "Personality"),
        Tab.Zzap => T("ZZAP (전술 등..)", "ZZAP (tactics etc.)"),
        Tab.Maps => T("맵별 보정", "Per map"),
        _ => T("고급 · 숫자로 직접", "Advanced · exact numbers"),
    };

    /// <param name="badge">Optional count per tab (changed items), shown after the name.</param>
    public static void TabBar(Action<Action> write, Func<Tab, string> badge = null)
    {
        GUILayout.Space(10f);
        GUILayout.BeginHorizontal();
        foreach (Tab tab in Enum.GetValues(typeof(Tab)))
        {
            string text = Name(tab);
            string extra = badge?.Invoke(tab);
            if (!string.IsNullOrEmpty(extra))
            {
                text += "  <color=#facc15>" + extra + "</color>";
            }
            if (GUILayout.Button(text, Current == tab ? ZzapUI.Pill : ZzapUI.Button, GUILayout.Height(30f), GUILayout.ExpandWidth(false)))
            {
                var picked = tab;
                write(() => Current = picked);
            }
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(6f);
    }

    // ---------------------------------------------------------------- ZZAP

    private static readonly EZzapToggle[] Toggles = { EZzapToggle.Global, EZzapToggle.On, EZzapToggle.Off };

    private static string ToggleName(EZzapToggle toggle) => toggle switch
    {
        EZzapToggle.On => T("켬", "On"),
        EZzapToggle.Off => T("끔", "Off"),
        _ => T("전역 따름", "Global"),
    };

    public static void ZzapPanel(List<SAINSettingsGroupClass> groups, Action<Action> write, Action<string, object> track)
    {
        ZzapUI.StepHeading("Z", T("ZZAP (전술 등..) — 이 봇 종류만 따로", "ZZAP (tactics etc.) — for these bot types only"),
            T("전역 따름 = 'ZZAP (전술 등..)' 탭의 스위치대로", "Global = as on the 'ZZAP (tactics etc.)' tab"));
        GUILayout.Label(T("난이도 4칸 모두에 적용됩니다. 켬은 전역 스위치·'스캐브 제외'보다 우선합니다(누가 다이아몬드 스텝을 하는지 같은 성격 규칙은 그대로).",
            "Applies to all four difficulty slots. On wins over the global switch and Skip Scavs (personality rules such as who diamond-steps stay)."), ZzapUI.Small);
        var global = SAINPlugin.LoadedPreset.GlobalSettings;
        foreach (var sw in BotZzapSwitches.All)
        {
            var value = BotZzapSwitches.Get(groups, sw);
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal(ZzapUI.Panel1);
            GUILayout.BeginVertical();
            string now = BotZzapSwitches.HasGlobalSwitch(sw)
                ? T("  · 전역: ", "  · global: ") + (sw.Global(global) ? T("켬", "on") : T("끔", "off"))
                : string.Empty;
            GUILayout.Label("<b>" + EditorText.Setting(sw.Name) + "</b><size=12><color=#a1a1aa>" + now + "</color></size>"
                + (value == null ? "  <color=#facc15>" + T("섞임", "mixed") + "</color>" : string.Empty), ZzapUI.Name);
            GUILayout.Label(EditorText.Setting(sw.Description), ZzapUI.Small);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            foreach (var toggle in Toggles)
            {
                if (GUILayout.Button(ToggleName(toggle), value == toggle ? ZzapUI.Pill : ZzapUI.Button, GUILayout.Width(86f), GUILayout.Height(30f)) && value != toggle)
                {
                    var picked = toggle;
                    var target = sw;
                    write(() =>
                    {
                        BotZzapSwitches.Set(groups, target, picked);
                        track(target.Name, picked);
                    });
                }
            }
            GUILayout.EndHorizontal();
        }
    }

    // ---------------------------------------------------------------- maps

    private static ELocation _map = ELocation.Labs;

    public static void MapsPanel(List<SAINSettingsGroupClass> groups, Action<Action> write, Action<string, object> track)
    {
        ZzapUI.StepHeading("M", T("맵별 보정 — 이 봇 종류만 따로", "Per map — for these bot types only"),
            T("1.0 = 그대로 · 전역 '맵별 보정' 위에 곱해짐", "1.0 = unchanged · multiplies the global per-map modifiers"));
        GUILayout.Label(T("예: 연구소에서만 청각 0.6 → 연구소에서 이 봇들은 소리를 덜 멀리서 듣고, 다른 맵은 그대로입니다. 다음 레이드부터 적용.",
            "E.g. Labs hearing 0.6 -> on Labs these bots hear from less far, other maps unchanged. From the next raid."), ZzapUI.Small);

        GUILayout.BeginHorizontal();
        int column = 0;
        foreach (var map in BotMapModifiers.Maps)
        {
            bool changed = groups.Any(g => BotMapModifiers.Get(g, map) != null);
            string label = BotMapModifiers.MapName(map, Korean) + (changed ? " <color=#facc15>●</color>" : string.Empty);
            if (GUILayout.Button(label, _map == map ? ZzapUI.Pill : ZzapUI.Button, GUILayout.Height(28f), GUILayout.MinWidth(96f)))
            {
                var picked = map;
                write(() => _map = picked);
            }
            if (++column == 6)
            {
                column = 0;
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
            }
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        var map0 = _map;
        var globalMap = SAINPlugin.LoadedPreset.GlobalSettings.Location?.LocationSettings != null
            && SAINPlugin.LoadedPreset.GlobalSettings.Location.LocationSettings.TryGetValue(map0, out var gm) ? gm : null;
        GUILayout.Space(6f);
        foreach (var field in BotMapModifiers.Fields)
        {
            var values = groups.Select(g => BotMapModifiers.Value(g, map0, field)).Distinct().ToList();
            float value = values.Count == 0 ? 1f : values[0];
            bool mixed = values.Count > 1;
            GUILayout.BeginHorizontal(ZzapUI.Panel1);
            GUILayout.BeginVertical(GUILayout.Width(330f));
            string globalText = globalMap != null && Math.Abs(field.Get(globalMap) - 1f) > 0.001f
                ? T($"  · 전역 이 맵 {field.Get(globalMap):0.##}배", $"  · global on this map {field.Get(globalMap):0.##}x")
                : string.Empty;
            GUILayout.Label("<b>" + field.Name(Korean) + "</b><size=12><color=#a1a1aa>" + globalText + "</color></size>"
                + (mixed ? "  <color=#facc15>" + T("섞임", "mixed") + "</color>" : string.Empty), ZzapUI.Name);
            GUILayout.Label(field.Help(Korean), ZzapUI.Small);
            GUILayout.EndVertical();
            float next = ZzapUI.Slider(value, BotMapModifiers.MIN, BotMapModifiers.MAX, GUILayout.MinWidth(220f), GUILayout.ExpandWidth(true));
            string color = Math.Abs(value - 1f) < 0.001f ? "#a1a1aa" : "#facc15";
            GUILayout.Label($"<color={color}>{value:0.00}x</color>", ZzapUI.Value, GUILayout.Width(64f));
            bool reset = GUILayout.Button(T("1배", "1x"), ZzapUI.Button, GUILayout.Width(52f), GUILayout.Height(28f));
            GUILayout.EndHorizontal();
            if (reset)
            {
                next = 1f;
            }
            next = (float)Math.Round(next, 2);
            if (Math.Abs(next - value) > 0.001f || (reset && mixed))
            {
                var f = field;
                float v = next;
                write(() =>
                {
                    foreach (var g in groups) BotMapModifiers.Set(g, map0, f, v);
                    track(f.NameEn + " @" + map0, v);
                });
            }
            GUILayout.Space(3f);
        }
        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(string.Format(T("{0} 전부 1배로", "Reset {0} to 1x"), BotMapModifiers.MapName(map0, Korean)), ZzapUI.Button, GUILayout.Height(28f), GUILayout.ExpandWidth(false)))
        {
            write(() =>
            {
                foreach (var g in groups) BotMapModifiers.Reset(g, map0);
                track("Per map @" + map0, "reset");
            });
        }
        var changedMaps = groups.SelectMany(BotMapModifiers.ChangedMaps).Distinct().Select(m => BotMapModifiers.MapName(m, Korean)).ToList();
        GUILayout.Label(changedMaps.Count == 0 ? T("바꾼 맵 없음", "No map changed") : T("바꾼 맵: ", "Changed maps: ") + string.Join(", ", changedMaps), ZzapUI.Small);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }
}
