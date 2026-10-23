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
    public static void Draw(object target,object original,string key)
    {
        var fields = SettingsQuickFields.For(target);
        if (fields.Count == 0) return;
        var saved = SettingsQuickFields.For(original).ToDictionary(f => f.Key,f => f.Value);
        int changed = fields.Count(f => saved.TryGetValue(f.Key,out float value) && Math.Abs(f.Value-value)>.00001f);
        GUILayout.BeginHorizontal();
        GUILayout.Label(T("빠른 조절", "Quick controls") + $" · {changed}" + T("개 변경", " changed"),ZzapUI.Section);
        if (GUILayout.Button(T("수치 −10%", "Numbers −10%"),ZzapUI.Button,GUILayout.Width(120f)))
            ZzapUI.Defer(() => { if (SettingsQuickFields.Scale(fields,.9f)>0) ConfigEditingTracker.Add(key,"-10%"); });
        if (GUILayout.Button(T("수치 +10%", "Numbers +10%"),ZzapUI.Button,GUILayout.Width(120f)))
            ZzapUI.Defer(() => { if (SettingsQuickFields.Scale(fields,1.1f)>0) ConfigEditingTracker.Add(key,"+10%"); });
        GUI.enabled = saved.Count>0;
        if (GUILayout.Button(T("저장값으로", "Restore saved"),ZzapUI.Button,GUILayout.Width(110f)))
            ZzapUI.Defer(() => { foreach (var f in fields) if (saved.TryGetValue(f.Key,out float v)) f.Set(v); ConfigEditingTracker.Add(key,"restore saved"); });
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        foreach (var f in fields)
        {
            float value=f.Value;
            bool hasSaved=saved.TryGetValue(f.Key,out float baseline);
            bool edited=hasSaved && Math.Abs(value-baseline)>.00001f;
            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(EditorText.Setting(f.Name)+(edited ? " ●" : ""),EditorText.Setting(f.Help)),ZzapUI.Name,GUILayout.Width(300f));
            if (f.Toggle)
            {
                bool on=ZzapUI.Switch(value>=.5f,26f);
                if (on!=(value>=.5f)) Set(f,on?1:0,key);
                GUILayout.FlexibleSpace();
            }
            else
            {
                float next=GUILayout.HorizontalSlider(value,f.Min,f.Max,GUILayout.MinWidth(100f),GUILayout.ExpandWidth(true));
                if (Math.Abs(next-value)>.00001f) Set(f,(float)(Math.Round(next/f.Step)*f.Step),key);
                string id=key+"."+f.Key;
                if (!_text.ContainsKey(id) || GUI.GetNameOfFocusedControl()!=id) _text[id]=value.ToString("0.###",CultureInfo.InvariantCulture);
                GUI.SetNextControlName(id);
                string text=GUILayout.TextField(_text[id],ZzapUI.Button,GUILayout.Width(90f));
                if (text!=_text[id])
                {
                    _text[id]=text;
                    if (float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out float number)) Set(f,number,key);
                }
            }
            GUI.enabled=edited;
            if (GUILayout.Button(T("되돌리기", "Reset"),ZzapUI.Button,GUILayout.Width(80f))) Set(f,baseline,key);
            GUI.enabled=true;
            GUILayout.EndHorizontal();
            GUILayout.Label(EditorText.Setting(f.Help),ZzapUI.Small);
            ZzapUI.Separator();
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
