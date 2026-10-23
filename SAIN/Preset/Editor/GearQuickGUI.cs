using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SAIN.Plugin;
using SAIN.Preset.Shared.GearStealthValues;
using UnityEngine;

namespace SAIN.Editor;

/// <summary>Search and batch editing of existing equipment visibility multipliers.</summary>
public static class GearQuickGUI
{
    private static string _query="";
    private static readonly HashSet<string> _selected=new();
    private static float _batch=1f;
    private static string T(string ko,string en) => EditorText.Korean ? ko : en;
    private static string Key(ItemStealthValue item) => item.EquipmentType+":"+item.ItemID+":"+item.Name;
    public static void Draw(EEquipmentType type)
    {
        var all=SAINPlugin.LoadedPreset.GearStealthValuesClass.ItemStealthValues;
        if (!all.TryGetValue(type,out var items)) return;
        var saved=QuickObjectGUI.Saved()?.GearStealthValues.Values.SelectMany(v=>v).GroupBy(Key).ToDictionary(g=>g.Key,g=>g.First().StealthValue)
            ?? new Dictionary<string,float>();
        GUILayout.Label(T("높을수록 먼 거리에서 눈에 덜 띕니다 · 1배=기본 · 30m 이내 미적용, 30~60m 점진 적용 · 소음과 무관", "Higher = harder to see at range · 1 = neutral · fades in from 30–60m · not sound"),ZzapUI.Small);
        string search=ZzapUI.Search(_query,T("장비 이름 / ID 검색", "Search name / ID"));
        if (search!=_query) ZzapUI.Defer(()=>_query=search);
        var visible=items.Where(i=>string.IsNullOrWhiteSpace(_query) || (i.Name??"").IndexOf(_query,StringComparison.OrdinalIgnoreCase)>=0 || (i.ItemID??"").IndexOf(_query,StringComparison.OrdinalIgnoreCase)>=0).ToList();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(T("검색 결과 선택", "Select results"),ZzapUI.Button)) ZzapUI.Defer(()=>{foreach(var i in visible) _selected.Add(Key(i));});
        if (GUILayout.Button(T("선택 해제", "Clear selection"),ZzapUI.Button)) ZzapUI.Defer(()=>_selected.Clear());
        GUILayout.Label(T("선택한 항목에", "For selected"),ZzapUI.Small);
        _batch=GUILayout.HorizontalSlider(_batch,.1f,2f,GUILayout.Width(140f));
        _batch=Mathf.Clamp(QuickObjectGUI.NumberInput("gear.batch",_batch,70f),.1f,2f);
        var selected=items.Where(i=>_selected.Contains(Key(i))).ToList();
        GUI.enabled=selected.Count>0;
        if (GUILayout.Button(T("적용", "Apply")+$" ({selected.Count})",ZzapUI.Button))
        {
            float value=(float)Math.Round(_batch,2);
            ZzapUI.Defer(()=>{foreach(var i in selected) i.StealthValue=value; ConfigEditingTracker.Add("gear batch",value);});
        }
        if (GUILayout.Button(T("저장값으로", "Restore saved"),ZzapUI.Button))
            ZzapUI.Defer(()=>{foreach(var i in selected) if(saved.TryGetValue(Key(i),out float v)) i.StealthValue=v; ConfigEditingTracker.Add("gear batch","saved");});
        GUI.enabled=true;
        GUILayout.EndHorizontal();
        foreach(var i in visible)
        {
            bool picked=_selected.Contains(Key(i));
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(picked?"☑":"☐",ZzapUI.Button,GUILayout.Width(32f))) ZzapUI.Defer(()=>{if(!_selected.Remove(Key(i))) _selected.Add(Key(i));});
            GUILayout.Label(new GUIContent(i.Name,i.ItemID),ZzapUI.Name,GUILayout.Width(220f));
            float value=GUILayout.HorizontalSlider(i.StealthValue,.1f,2f,GUILayout.MinWidth(120f));
            float number=QuickObjectGUI.NumberInput("gear."+Key(i),i.StealthValue,70f);
            if(number!=i.StealthValue) value=number;
            if(!float.IsNaN(value)&&!float.IsInfinity(value)&&Math.Abs(value-i.StealthValue)>.00001f)
            { float captured=Mathf.Clamp(value,.1f,2f); ZzapUI.Defer(()=>{i.StealthValue=captured; ConfigEditingTracker.Add(i.Name,captured);}); }
            bool changed=saved.TryGetValue(Key(i),out float baseline)&&Math.Abs(baseline-i.StealthValue)>.00001f;
            GUILayout.Label(changed?"●":"",ZzapUI.Small,GUILayout.Width(18f));
            GUI.enabled=changed;
            if(GUILayout.Button(T("되돌리기", "Reset"),ZzapUI.Button,GUILayout.Width(80f))) ZzapUI.Defer(()=>{i.StealthValue=baseline; ConfigEditingTracker.Add(i.Name,baseline);});
            GUI.enabled=true;
            GUILayout.EndHorizontal();
        }
    }
}
