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
        public GUIContent Label;
        public string Heading;
    }
    private static bool _newOnly,_changedOnly;
    private static readonly List<Row> _rows=new();
    private static readonly GUIEntryConfig Config=new();
    private static string T(string ko,string en)=>EditorText.Korean?ko:en;
    public static bool Active=>_newOnly || _changedOnly;
    public static void Filters()
    {
        GUILayout.BeginHorizontal();
        bool next=ZzapUI.Switch(_newOnly,26f,showState:false);
        if(next!=_newOnly) ZzapUI.Defer(()=>_newOnly=next);
        GUILayout.Label(T("새 항목만 보기","New only"),ZzapUI.SmallLine,GUILayout.Height(26f),GUILayout.ExpandWidth(false));
        GUILayout.Space(18f);
        bool changed=ZzapUI.Switch(_changedOnly,26f,showState:false);
        if(changed!=_changedOnly) ZzapUI.Defer(()=>_changedOnly=changed);
        GUILayout.Label(T("바뀐 항목만 보기","Changed only"),ZzapUI.SmallLine,GUILayout.Height(26f),GUILayout.ExpandWidth(false));
        GUILayout.Space(18f);
        GUILayout.Label(T("기준: zzap 원본 · 두 필터는 함께 적용됩니다","Compared with zzap original · both filters apply together"),ZzapUI.SmallLine,GUILayout.Height(26f),GUILayout.ExpandWidth(false));
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
                _rows.Add(new Row { Value=value,Info=info,Changed=value.Changed,New=value.New,
                    Label=new GUIContent(info.Name,info.Description),
                    Heading=string.Join(" › ",value.Heading.Split(new[]{" › "},System.StringSplitOptions.None).Select(EditorText.UI)) });
            }
        }
        string heading=null;
        foreach(var row in _rows)
        {
            var field=row.Value;
            if(heading!=field.Heading) { heading=field.Heading; if(heading.Length>0) GUILayout.Label(row.Heading,ZzapUI.Section); }
            object value=field.Field.GetValue(field.Owner);
            object before=value;
            // Existing editor retains its list/dictionary controls and range rules.
            bool edited;
            if(value is float || value is bool || value is int || value is SAIN.Preset.Shared.Models.Preset.Personalities.EPersonality)
            {
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(GUILayout.Width(ZzapUI.NAME_WIDTH));
                GUILayout.BeginHorizontal();
                GUILayout.Label(row.Label,ZzapUI.Name,GUILayout.ExpandWidth(false));
                ZzapUI.ChangeMark(row.Changed);
                if(row.New) GUILayout.Label("NEW",ZzapUI.NewTag,GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();
                GUILayout.Label(row.Info.Description,ZzapUI.Small);
                GUILayout.EndVertical();
                value=AttributesGUI.EditFloatBoolInt(ref value,field.Owner,row.Info,Config,0,out edited,false,false,showDefaultReset:false);
            }
            else value=AttributesGUI.EditValue(ref value,field.Owner,row.Info,out edited,0,Config);
            if(edited && !Equals(before,value))
            {
                object selected=value;
                ZzapUI.Defer(()=> { field.Field.SetValue(field.Owner,selected); ConfigEditingTracker.Add(row.Info.Name,selected); });
            }
            GUI.enabled=row.Changed;
            if(GUILayout.Button(T("원래대로","Reset"),ZzapUI.Button,GUILayout.Width(90f)))
                ZzapUI.Defer(()=> { field.Field.SetValue(field.Owner,JsonConvert.DeserializeObject(JsonConvert.SerializeObject(field.Baseline),field.Field.FieldType)); ConfigEditingTracker.Add(row.Info.Name,"reset"); });
            GUI.enabled=true;
            if(before is float || before is bool || before is int || before is SAIN.Preset.Shared.Models.Preset.Personalities.EPersonality) GUILayout.EndHorizontal();
            ZzapUI.Separator(5f);
        }
    }
}
