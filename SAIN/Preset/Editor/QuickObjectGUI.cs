using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SAIN.Plugin;
using SAIN.Preset;
using SAIN.Preset.Server;
using SAIN.Preset.Shared.Preset;
using UnityEngine;

namespace SAIN.Editor;

/// <summary>Quick controls that edit the existing fields and restore the server's last saved values.</summary>
public static class QuickObjectGUI
{
    private static SAINPresetClass _preset;
    private static SAINPresetBundle _saved;
    private static bool _wasDirty;
    private static readonly HashSet<string> _advanced = new();
    private static readonly Dictionary<string,string> _text = new();
    private static string T(string ko,string en) => EditorText.Korean ? ko : en;
    public static SAINPresetBundle Saved()
    {
        var preset = SAINPlugin.LoadedPreset;
        bool dirty = ConfigEditingTracker.UnsavedChanges;
        if (!ReferenceEquals(_preset,preset) || _wasDirty && !dirty || _saved == null)
        {
            _preset = preset;
            _saved = null;
            if (preset?.Info?.IsCustom == true) PresetSync.TryGetSavedCustomBundle(preset.Info.Name,out _saved);
            else if (preset?.Info != null) _saved = PresetSync.SavedServerDefaults().FirstOrDefault(b => b?.Info?.BaseSAINDifficulty == preset.Info.BaseSAINDifficulty);
            _text.Clear();
        }
        _wasDirty = dirty;
        return _saved;
    }
    public static bool Advanced(string key)
    {
        bool open = _advanced.Contains(key);
        if (ZzapUI.ListItem((open ? "▾ " : "▸ ") + T("고급 · 모든 설정", "Advanced · all settings"),null,open))
            ZzapUI.Defer(() => { if (!_advanced.Remove(key)) _advanced.Add(key); });
        return open;
    }
    private static readonly string[] ReferenceNames = { "zzap", "zzap 쉬움", "zzap 보통", "" };
    private static string _reference = "zzap";
    public static string ReferenceLabel => _reference.Length == 0 ? T("저장값", "Saved") : _reference;
    public static SAINPresetBundle Reference()
        => _reference.Length == 0 ? Saved() : PresetSync.TryGetSavedCustomBundle(_reference, out var bundle) ? bundle : Saved();
    public static SAINPresetBundle Defaults()
    {
        string name = PresetEditorReference.BaseName(SAINPlugin.LoadedPreset?.Info?.Name);
        return name != null && PresetSync.TryGetSavedCustomBundle(name, out var original) ? original : Saved();
    }
    public static void ReferenceBar()
    {
        GUILayout.BeginHorizontal(ZzapUI.Panel1);
        GUILayout.Label(T("비교 기준", "Compare with"), ZzapUI.Section, GUILayout.Width(95f), GUILayout.Height(28f));
        foreach (string name in ReferenceNames)
        {
            if (name.Length > 0 && !PresetSync.TryGetSavedCustomBundle(name, out _)) continue;
            string label = name.Length == 0 ? T("저장값", "Saved") : name;
            if (GUILayout.Button(label, _reference == name ? ZzapUI.Pill : ZzapUI.Button, GUILayout.MinWidth(72f), GUILayout.Height(28f)))
            {
                string captured = name; ZzapUI.Defer(() => _reference = captured);
            }
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Label(T("노란 선: 기준 위치 · 빨강: 수치 증가 · 초록: 수치 감소 (강함의 뜻은 항목 설명 참고)", "Yellow: reference · red: higher value · green: lower value (see each setting's meaning)"), ZzapUI.Small);
        GUILayout.Space(6f);
    }
    public static void Draw(object target, object original, string key, Func<SAINPresetBundle, object> select = null, bool showReference = true)
    {
        var fields = SettingsQuickFields.For(target);
        if (fields.Count == 0) return;
        var saved = PresetEditorReference.Values(original);
        object defaultTarget = select?.Invoke(Defaults()) ?? original;
        var defaults = PresetEditorReference.Values(defaultTarget);
        var comparison = PresetEditorReference.Values(select?.Invoke(Reference()) ?? original);
        int changed = fields.Count(f => saved.TryGetValue(f.Key, out float value) && Math.Abs(f.Value - value) > .00001f);
        GUILayout.BeginHorizontal();
        GUILayout.Label(T("빠른 조절", "Quick controls") + $" · {changed}" + T("개 변경", " changed"), ZzapUI.Section);
        GUILayout.FlexibleSpace();
        GUI.enabled = defaults.Count > 0;
        if (GUILayout.Button(new GUIContent(T("기본값으로", "Defaults"), T("이 빠른 조절 항목을 현재 프리셋 계열의 원본값으로 복원합니다.", "Restore these quick controls from the original preset family.")), ZzapUI.Button, GUILayout.MinWidth(110f), GUILayout.Height(28f)))
            ZzapUI.Defer(() => { if (PresetEditorReference.ResetQuick(target, defaultTarget) > 0) ConfigEditingTracker.Add(key, "defaults"); });
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        if (showReference) ReferenceBar();
        foreach (var field in fields)
        {
            float value = field.Value;
            bool edited = saved.TryGetValue(field.Key, out float stored) && Math.Abs(value - stored) > .00001f;
            bool hasDefault = defaults.TryGetValue(field.Key, out float baseline);
            bool differs = hasDefault && Math.Abs(value - baseline) > .00001f;
            float reference = comparison.TryGetValue(field.Key, out float compared) ? compared : value;
            int direction = Math.Abs(value - reference) < .00001f ? 0 : value > reference ? 1 : -1;
            GUILayout.BeginVertical(ZzapUI.Panel1);
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("<b>" + EditorText.Setting(field.Name) + "</b>" + (edited ? " <color=#facc15>●</color>" : ""), EditorText.Setting(field.Help)), ZzapUI.Name, GUILayout.MinWidth(100f), GUILayout.ExpandWidth(true));
            if (field.Toggle)
            {
                bool on = ZzapUI.Switch(value >= .5f, 28f);
                if (on != (value >= .5f)) Set(field, on ? 1 : 0, key);
            }
            else
            {
                float next = NumberInput(key + "." + field.Key, value, 90f);
                if (Math.Abs(next - value) > .00001f) Set(field, next, key);
            }
            GUI.enabled = differs;
            if (GUILayout.Button(T("기본값", "Default"), ZzapUI.Button, GUILayout.Width(80f), GUILayout.Height(26f))) Set(field, baseline, key);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (!field.Toggle)
            {
                string change = direction == 0 ? T("변화 없음", "unchanged") : direction > 0 ? T("▲ 수치 증가", "▲ increased") : T("▼ 수치 감소", "▼ decreased");
                string color = direction > 0 ? "#f87171" : direction < 0 ? "#4ade80" : "#71717a";
                GUILayout.Label($"<color=#71717a>{ReferenceLabel} {reference:0.###}</color> → <b>{value:0.###}</b>  <color={color}>{change}</color>", ZzapUI.Small);
                float position = ZzapUI.DiffSlider(value, field.Min, field.Max, reference, direction, GUILayout.Height(26f), GUILayout.ExpandWidth(true));
                if (Math.Abs(position - value) > .00001f) Set(field, (float)(Math.Round(position / field.Step) * field.Step), key);
            }
            else
                GUILayout.Label(ReferenceLabel + ": " + (reference >= .5f ? T("켬", "On") : T("끔", "Off")), ZzapUI.Small);
            if (!string.IsNullOrEmpty(field.Help)) GUILayout.Label(EditorText.Setting(field.Help), ZzapUI.Small);
            GUILayout.EndVertical();
            GUILayout.Space(6f);
        }
    }
    public static float NumberInput(string id, float value, float width = 80f)
    {
        if (!_text.ContainsKey(id) || GUI.GetNameOfFocusedControl() != id) _text[id] = value.ToString("0.###", CultureInfo.InvariantCulture);
        GUI.SetNextControlName(id);
        string text = GUILayout.TextField(_text[id], ZzapUI.Button, GUILayout.Width(width));
        if (text == _text[id]) return value;
        _text[id] = text;
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) && !float.IsNaN(parsed) && !float.IsInfinity(parsed) ? parsed : value;
    }

    private static void Set(SettingsQuickField f,float value,string key) => ZzapUI.Defer(() =>
    {
        float before=f.Value; f.Set(value);
        if (Math.Abs(f.Value-before)>.00001f) ConfigEditingTracker.Add(key+"."+f.Key,f.Value);
    });
}
