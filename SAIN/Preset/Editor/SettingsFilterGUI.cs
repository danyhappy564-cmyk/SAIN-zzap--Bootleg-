using System.Collections.Generic;
using System.Linq;
using SAIN.Attributes;
using SAIN.Plugin;
using Newtonsoft.Json;
using SAIN.Preset.Shared.Preset;
using UnityEngine;

namespace SAIN.Editor;

/// <summary>전역 설정 전체 목록. 화면 행은 Layout에서만 만들고 값 쓰기는 Defer로 미룹니다.</summary>
internal static class SettingsFilterGUI
{
    private sealed class Row
    {
        public SettingsObjectField Value;
        public ConfigInfoClass Info;
        public bool Changed, New;
    }
    private static bool _newOnly,_changedOnly;
    private static readonly List<Row> _rows=new();
    private static readonly GUIEntryConfig Config=new();
    private static string T(string ko,string en)=>EditorText.Korean?ko:en;
    public static bool Active=>_newOnly || _changedOnly;
    public static void Filters()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(T("새 항목만 보기","New only"),ZzapUI.Small);
        bool next=ZzapUI.Switch(_newOnly,26f);
        if(next!=_newOnly) ZzapUI.Defer(()=>_newOnly=next);
        GUILayout.Label(T("바뀐 항목만 보기","Changed only"),ZzapUI.Small);
        bool changed=ZzapUI.Switch(_changedOnly,26f);
        if(changed!=_changedOnly) ZzapUI.Defer(()=>_changedOnly=changed);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }
    public static void Draw(object target,object baseline,string search=null)
    {
        if(Event.current.type==EventType.Layout)
        {
            _rows.Clear();
            foreach(var value in SettingsFieldStatus.Rows(target,baseline))
            {
                var info=AttributesGUI.GetAttributeInfo(value.Field);
                if(info==null || info.DoNotShowGUI || (_newOnly && !value.New) || (_changedOnly && !value.Changed)) continue;
                if(!string.IsNullOrEmpty(search) && AttributesGUI.SkipForSearch(info,search.ToLowerInvariant())) continue;
                _rows.Add(new Row { Value=value,Info=info,Changed=value.Changed,New=value.New });
            }
        }
        string heading=null;
        foreach(var row in _rows)
        {
            var field=row.Value;
            if(heading!=field.Heading) { heading=field.Heading; if(heading.Length>0) GUILayout.Label(string.Join(" › ",heading.Split(new[]{" › "},System.StringSplitOptions.None).Select(EditorText.UI)),ZzapUI.Section); }
            if(row.New || row.Changed) GUILayout.Label((row.New?"<color=#38bdf8>NEW</color>":"")+(row.Changed?" <color=#facc15>●</color>":""),ZzapUI.Small);
            object value=field.Field.GetValue(field.Owner);
            object before=value;
            // Existing editor retains its list/dictionary controls and range rules.
            bool edited;
            if(value is float || value is bool || value is int || value is SAIN.Preset.Shared.Models.Preset.Personalities.EPersonality)
                value=AttributesGUI.EditFloatBoolInt(ref value,field.Owner,row.Info,Config,0,out edited,false,false,showDefaultReset:false);
            else value=AttributesGUI.EditValue(ref value,field.Owner,row.Info,out edited,0,Config);
            if(edited && !Equals(before,value))
            {
                object selected=value;
                ZzapUI.Defer(()=> { field.Field.SetValue(field.Owner,selected); ConfigEditingTracker.Add(row.Info.Name,selected); });
            }
            if(row.Changed && GUILayout.Button(T("원래대로","Reset"),ZzapUI.Button,GUILayout.Width(90f)))
                ZzapUI.Defer(()=> { field.Field.SetValue(field.Owner,JsonConvert.DeserializeObject(JsonConvert.SerializeObject(field.Baseline),field.Field.FieldType)); ConfigEditingTracker.Add(row.Info.Name,"reset"); });
        }
    }
}
