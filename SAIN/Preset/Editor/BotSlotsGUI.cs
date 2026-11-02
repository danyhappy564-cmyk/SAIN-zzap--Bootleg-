using System;
using System.Linq;
using SAIN.Preset.Server;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using SAIN.Preset.Shared.Preset;
using UnityEngine;

namespace SAIN.Editor;

/// <summary>공용 슬롯 표시. 저장소·적용 순서는 유지하고 관리·공유는 오버레이에 표시합니다.</summary>
internal static class BotSlotsGUI
{
    private static BotSettingsSlots _slots, _labelsFor;
    private static bool _loaded, _labelsKorean, _codeOpen, _rename;
    private static string _message, _name="", _code="";
    private static int _menu=-1;
    private static string[] _labels;
    private static GUIContent[] _contents;
    private static SAINSettingsGroupClass _source;
    private static LevelSources _sources;
    private static bool _canSave;
    private static string T(string ko,string en)=>EditorText.Korean?ko:en;
    public static bool OverlayOpen=>_menu>=0 || _codeOpen;
    public static void Draw(SAINSettingsGroupClass source,LevelSources sources,Action<BotSettingsSlot> apply,bool canSave=true,string note=null)
    {
        if(!_loaded) { _loaded=true; ZzapUI.Defer(()=> {
            try { _slots=BotSettingsSlots.Deserialize(ServerDataClient.Get(BotSettingsSlots.DataKey)); }
            catch { _message=T("내 설정을 불러오지 못했습니다. 다시 열어 주세요.","Could not load custom slots. Reopen this page."); }
        }); }
        if(Event.current.type==EventType.Layout) {
            _source=source; _sources=sources; _canSave=canSave;
            if(_slots!=null && (!ReferenceEquals(_labelsFor,_slots) || _labelsKorean!=EditorText.Korean)) {
                _labelsFor=_slots; _labelsKorean=EditorText.Korean;
                _labels=_slots.Slots.Select((slot,i)=>slot?.Name??T($"내 설정 {i+1}",$"Custom {i+1}")).ToArray();
                _contents=_labels.Select(label=>new GUIContent(label.Length>12?label.Substring(0,12)+"…":label,label)).ToArray();
            }
        }
        GUILayout.Space(10f);
        GUILayout.BeginVertical(ZzapUI.Frame);
        GUILayout.BeginHorizontal();
        GUILayout.Label(T("내 설정","Custom"),ZzapUI.Name,GUILayout.ExpandWidth(false));
        if(_slots==null) GUILayout.Label(_message??T("불러오는 중…","Loading…"),ZzapUI.Small);
        else {
            // At most nine slots; compact rows wrap at three on narrower windows.
            for(int i=0;i<_slots.Slots.Count;i++) {
                if(i>0 && i%3==0 && RectLayout.MainWindow.width<1500f) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                int index=i; var slot=_slots.Slots[i];
                if(GUILayout.Button(_contents[i],ZzapUI.Button,GUILayout.Height(28f),GUILayout.MaxWidth(124f),GUILayout.ExpandWidth(false)))
                    ZzapUI.Defer(()=> { if(slot==null) _message=T("비어 있음 — ＋로 저장","Empty — use ＋ to save"); else apply(slot); });
                if(GUILayout.Button("...",ZzapUI.Button,GUILayout.Width(36f),GUILayout.Height(28f)))
                    ZzapUI.Defer(()=> { _menu=_menu==index?-1:index; _name=_labels[index]; _rename=false; _message=null; });
            }
            GUI.enabled=canSave && source!=null && (_slots.Slots.Any(s=>s==null)||_slots.Slots.Count<BotSettingsSlots.MaxSlots);
            if(GUILayout.Button(T("＋ 지금 값 저장","＋ Save current"),ZzapUI.Quiet,GUILayout.Height(28f),GUILayout.ExpandWidth(false)))
                Save(next=>next.Add(BotSettingsSlots.Capture(T($"내 설정 {NextNumber()}",$"Custom {NextNumber()}"),source,sources)));
            GUI.enabled=true;
            if(GUILayout.Button(T("공유 코드 붙여넣기","Paste share code"),ZzapUI.Quiet,GUILayout.Height(28f),GUILayout.ExpandWidth(false)))
                ZzapUI.Defer(()=> { _code=GUIUtility.systemCopyBuffer??""; _menu=-2; _codeOpen=true; _message=null; });
        }
        GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();
        GUILayout.Label(note??T("F6와 웹은 같은 내 설정 슬롯을 함께 씁니다. 슬롯을 누르면 지금 편집 중인 봇에 적용됩니다.","Custom slots are shared by F6 and the web page. Click a slot to apply it to the bots being edited."),ZzapUI.Small);
        if(!string.IsNullOrEmpty(_message)) GUILayout.Label(_message,ZzapUI.Small);
        GUILayout.EndVertical();
    }
    public static void DrawOverlay()
    {
        if(!OverlayOpen || _slots==null) return;
        bool hasSlot=_menu>=0 && _menu<_slots.Slots.Count && _slots.Slots[_menu]!=null;
        if(!_codeOpen && !_rename) {
            // A small centred dialog, like the guide. It used to drop down under [...] from a screen-space anchor, but in game the
            // anchor and the click landed in different spaces, so every click counted as "outside" and closed it (2026-11-03).
            ZzapUI.BeginModal(360f,300f);
            GUILayout.BeginHorizontal();
            GUILayout.Label(hasSlot?_labels[_menu]:T("내 설정","Custom"),ZzapUI.Title);
            if(GUILayout.Button("×",ZzapUI.Quiet,GUILayout.Width(30f))) ZzapUI.Defer(Close);
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            int index=_menu;
            GUI.enabled=hasSlot;
            if(GUILayout.Button(T("이름 바꾸기","Rename"),ZzapUI.Quiet)) ZzapUI.Defer(()=>_rename=true);
            GUI.enabled=hasSlot && _canSave && _source!=null;
            if(GUILayout.Button(T("덮어쓰기","Overwrite"),ZzapUI.Quiet)) Save(next=>next.Slots[index]=BotSettingsSlots.Capture(next.Slots[index].Name,_source,_sources));
            GUI.enabled=hasSlot;
            if(GUILayout.Button(T("삭제","Delete"),ZzapUI.Quiet)) Save(next=>next.Delete(index));
            if(GUILayout.Button(T("공유 코드 복사","Copy share code"),ZzapUI.Quiet)) ZzapUI.Defer(()=> {
                try { _code=BotSettingsSlots.Share(_slots.Slots[index]); _codeOpen=true; _message=null; }
                catch { _message=T("공유 코드가 너무 큽니다.","Share code is too large."); }
            });
            GUI.enabled=true; ZzapUI.EndModal(); return;
        }
        ZzapUI.BeginModal(640f,_rename?250f:350f);
        GUILayout.BeginHorizontal();
        GUILayout.Label(_rename?T("내 설정 이름 바꾸기","Rename custom slot"):_menu==-2?T("공유 코드 붙여넣기","Paste share code"):T("공유 코드 복사","Copy share code"),ZzapUI.Title);
        if(GUILayout.Button("×",ZzapUI.Quiet,GUILayout.Width(30f))) ZzapUI.Defer(Close);
        GUILayout.EndHorizontal(); GUILayout.Space(14f);
        if(_rename) {
            GUILayout.Label(T("새 이름 · 최대 24자","New name · Up to 24 characters"),ZzapUI.Small);
            string name=GUILayout.TextField(_name,24,ZzapUI.FieldStyle,GUILayout.Height(32f));
            if(name!=_name) ZzapUI.Defer(()=>_name=name);
        } else {
            GUILayout.Label(_menu==-2?T("공유 코드를 붙여 넣으면 새 내 설정 슬롯이 생깁니다.","Paste a share code to create a new custom slot."):T("다른 화면에서 붙여넣으면 같은 슬롯이 생깁니다.","Paste it into another view to create the same slot."),ZzapUI.Small);
            string code=GUILayout.TextArea(_code,ZzapUI.FieldStyle,GUILayout.Height(130f));
            if(code!=_code && _menu==-2) ZzapUI.Defer(()=>_code=code);
            GUILayout.Label(_menu==-2?T("코드가 안 들어오면 칸을 누르고 Ctrl+V를 눌러 주세요.","If the code is not filled in, click the box and press Ctrl+V.")
                :T("복사되지 않으면 코드를 선택하고 Ctrl+C를 눌러 주세요.","If copying is blocked, select the code and press Ctrl+C."),ZzapUI.Small);
        }
        if(_message!=null) GUILayout.Label(_message,ZzapUI.Small);
        GUILayout.FlexibleSpace(); GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();
        if(GUILayout.Button(T("닫기","Close"),ZzapUI.Button)) ZzapUI.Defer(Close);
        if(_rename) {
            int index=_menu;
            if(GUILayout.Button(T("이름 바꾸기","Rename"),ZzapUI.ButtonPrimary)) Save(next=>next.Rename(index,_name));
        } else if(_menu==-2) {
            if(GUILayout.Button(T("새 슬롯 만들기","Create new slot"),ZzapUI.ButtonPrimary)) ZzapUI.Defer(()=> {
                if(!BotSettingsSlots.TryReadShare(_code,out var slot,out int skipped,out var reason)) { _message=BotSettingsSlots.Error(reason,EditorText.Korean); return; }
                if(SaveNow(next=> { if(next.Add(slot)<0) throw new InvalidOperationException(); }) && skipped>0)
                    _message+=T($" 모르는 필드 {skipped}개를 건너뛰었습니다.",$" Skipped {skipped} unknown field(s).");
            });
        } else if(GUILayout.Button(T("코드 복사","Copy code"),ZzapUI.ButtonPrimary))
            ZzapUI.Defer(()=> { GUIUtility.systemCopyBuffer=_code; _message=T("공유 코드를 복사했습니다.","Share code copied."); });
        GUILayout.EndHorizontal(); ZzapUI.EndModal();
    }
    private static void Close() { _menu=-1; _codeOpen=_rename=false; }
    private static int NextNumber() { int empty=_slots.Slots.FindIndex(s=>s==null); return empty<0?_slots.Slots.Count+1:empty+1; }
    private static void Save(Action<BotSettingsSlots> change)=>ZzapUI.Defer(()=>SaveNow(change));
    private static bool SaveNow(Action<BotSettingsSlots> change)
    {
        try {
            var next=BotSettingsSlots.Deserialize(_slots.Serialize()); change(next);
            ServerDataClient.Save(BotSettingsSlots.DataKey,next.Serialize()); _slots=next; Close(); _code="";
            _message=T("내 설정을 저장했습니다.","Custom slots saved."); return true;
        } catch { _message=T("저장하지 못했습니다. 이름과 슬롯 수(최대 9칸)를 확인하세요.","Could not save. Check the name, or the 9-slot limit."); return false; }
    }
    public static void Reopen() { _loaded=false; _slots=null; _labelsFor=null; Close(); _code=""; }
}
