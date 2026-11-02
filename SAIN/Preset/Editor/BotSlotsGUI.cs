using System;
using System.Linq;
using SAIN.Preset.Server;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using SAIN.Preset.Shared.Preset;
using UnityEngine;

namespace SAIN.Editor;

/// <summary>공용 슬롯의 F6 표시. 서버 저장은 프리셋 저장과 별개이며 실패하면 기존 목록을 유지합니다.</summary>
internal static class BotSlotsGUI
{
    private static BotSettingsSlots _slots;
    private static bool _loaded;
    private static string _message;
    private static int _menu=-1;
    private static string _name="";
    private static string _code="";
    private static bool _codeOpen; // changed only through Defer, so Layout and Repaint draw the same controls
    private static string T(string ko,string en) => EditorText.Korean?ko:en;
    public static void Draw(SAINSettingsGroupClass source,LevelSources sources,Action<BotSettingsSlot> apply, bool canSave=true)
    {
        if(!_loaded)
        {
            _loaded=true;
            ZzapUI.Defer(()=>
            {
                try { _slots=BotSettingsSlots.Deserialize(ServerDataClient.Get(BotSettingsSlots.DataKey)); }
                catch { _message=T("내 설정을 불러오지 못했습니다. 다시 열어 주세요.","Could not load custom slots. Reopen this page."); }
            });
        }
        GUILayout.Label(T("내 설정 · F6와 웹이 같은 슬롯을 씁니다", "Custom slots are shared by F6 and the web page"),ZzapUI.Section);
        if(_slots==null) { GUILayout.Label(_message??T("불러오는 중…","Loading…"),ZzapUI.Small); return; }
        GUILayout.BeginHorizontal();
        for(int i=0;i<_slots.Slots.Count;i++)
        {
            if(i>0 && i%3==0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            int index=i; var slot=_slots.Slots[i];
            Color old=GUI.color;
            if(slot==null) GUI.color=new Color(old.r,old.g,old.b,old.a*0.45f);
            if(GUILayout.Button(slot?.Name??T($"내 설정 {i+1}",$"Custom {i+1}"),slot==null?ZzapUI.Button:ZzapUI.Pill,GUILayout.Height(28f)))
                ZzapUI.Defer(()=> { if(slot==null) _message=T("비어 있음 — ＋로 저장","Empty — use ＋ to save"); else apply(slot); });
            GUI.color=old;
            if(slot!=null && GUILayout.Button("⋯",ZzapUI.Button,GUILayout.Width(24f),GUILayout.Height(28f)))
                ZzapUI.Defer(()=> { _menu=_menu==index?-1:index; _name=slot.Name; });
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUI.enabled=canSave && source!=null && (_slots.Slots.Any(s=>s==null) || _slots.Slots.Count<BotSettingsSlots.MaxSlots);
        if(GUILayout.Button(T("＋ 지금 값 저장","＋ Save current"),ZzapUI.Button,GUILayout.Height(28f)))
            Save(next=>next.Add(BotSettingsSlots.Capture(T($"내 설정 {NextNumber()}",$"Custom {NextNumber()}"),source,sources)));
        GUI.enabled=true;
        if(GUILayout.Button(T("공유 코드 붙여넣기","Paste share code"),ZzapUI.Button,GUILayout.Height(28f)))
            ZzapUI.Defer(()=>
            {
                _code=GUIUtility.systemCopyBuffer??"";
                _menu=-2;
                _codeOpen=true;
            });
        GUILayout.EndHorizontal();
        if(_menu>=0 && _menu<_slots.Slots.Count && _slots.Slots[_menu]!=null)
        {
            int index=_menu;
            _name=GUILayout.TextField(_name,24,ZzapUI.FieldStyle,GUILayout.Height(28f));
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(T("이름 바꾸기","Rename"),ZzapUI.Button)) Save(next=>next.Rename(index,_name));
            GUI.enabled=canSave && source!=null;
            if(GUILayout.Button(T("덮어쓰기","Overwrite"),ZzapUI.Button)) Save(next=>next.Slots[index]=BotSettingsSlots.Capture(next.Slots[index].Name,source,sources));
            GUI.enabled=true;
            if(GUILayout.Button(T("삭제","Delete"),ZzapUI.Button)) Save(next=>next.Delete(index));
            if(GUILayout.Button(T("공유 코드 복사","Copy share code"),ZzapUI.Button)) ZzapUI.Defer(()=>
            {
                try { _code=BotSettingsSlots.Share(_slots.Slots[index]); _codeOpen=true; GUIUtility.systemCopyBuffer=_code; _message=T("공유 코드를 복사했습니다.","Share code copied."); }
                catch { _message=T("공유 코드가 너무 큽니다.","Share code is too large."); }
            });
            GUILayout.EndHorizontal();
        }
        if(_codeOpen)
        {
            _code=GUILayout.TextField(_code,ZzapUI.FieldStyle,GUILayout.Height(32f));
            if(_menu==-2 && GUILayout.Button(T("내 설정에 추가","Add to custom slots"),ZzapUI.Button)) ZzapUI.Defer(()=>
            {
                if(!BotSettingsSlots.TryReadShare(_code,out var slot,out int skipped,out var reason)) { _message=BotSettingsSlots.Error(reason,EditorText.Korean); return; }
                if(SaveNow(next=> { if(next.Add(slot)<0) throw new InvalidOperationException(); }) && skipped>0)
                    _message+=T($" 모르는 필드 {skipped}개를 건너뛰었습니다.",$" Skipped {skipped} unknown field(s).");
            });
        }
        if(!string.IsNullOrEmpty(_message)) GUILayout.Label(_message,ZzapUI.Small);
    }
    private static int NextNumber() { int empty=_slots.Slots.FindIndex(s=>s==null); return empty<0?_slots.Slots.Count+1:empty+1; }
    private static void Save(Action<BotSettingsSlots> change) => ZzapUI.Defer(()=>SaveNow(change));
    private static bool SaveNow(Action<BotSettingsSlots> change)
    {
        try
        {
            var next=BotSettingsSlots.Deserialize(_slots.Serialize()); change(next);
            ServerDataClient.Save(BotSettingsSlots.DataKey,next.Serialize()); _slots=next; _menu=-1; _codeOpen=false; _code="";
            _message=T("내 설정을 저장했습니다.","Custom slots saved.");
            return true;
        }
        catch { _message=T("저장하지 못했습니다. 이름과 슬롯 수(최대 9칸)를 확인하세요.","Could not save. Check the name, or the 9-slot limit."); return false; }
    }
    public static void Reopen() { _loaded=false; _slots=null; _menu=-1; _codeOpen=false; _code=""; }
}
