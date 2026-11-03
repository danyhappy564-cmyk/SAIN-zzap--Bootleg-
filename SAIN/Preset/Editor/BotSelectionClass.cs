using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT.UI;
using Newtonsoft.Json;
using SAIN.Attributes;
using SAIN.Components.BotController;
using SAIN.Editor.Util;
using SAIN.Extensions;
using SAIN.Plugin;
using SAIN.Preset;
using SAIN.Preset.Server;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SAIN.Preset.Shared.Preset;
using UnityEngine;

namespace SAIN.Editor;

/// <summary>
/// zzap (2026-10-23, bot type page v2, user-approved mockups bot-v2 / bot-v3c): pick a difficulty level per bot type - Easy /
/// Normal / Hard follow the bundled "zzap 쉬움" / "zzap 보통" / "zzap" presets for that bot, Impossible goes past zzap
/// (BotLevels, shared with the web page) - and the list shows each bot's level. Per bot: SAIN or EFT's own AI, revert, use
/// original, nine knobs to fine-tune (always all four slots), every number under "Advanced" with per-difficulty editing.
/// ○/● and Ctrl+click build an explicit selection; ordinary name clicks open one bot and keep that selection.
/// Faction names fold the list; the faction circle toggles its editable bots.
/// Everything drawn comes from a view rebuilt on Layout events only and every write goes through ZzapUI.Defer, so a value
/// that flips a row can't add a control between Layout and Repaint (v1 final review).
/// </summary>
public static class BotSelectionClass
{
    private const float KNOB_DELTA_WIDTH = 150f;
    private const float RESET_WIDTH = 74f;
    private const float LABEL_WIDTH = 220f;

    private static readonly ESainBotDifficulty[] Difficulties = { ESainBotDifficulty.easy, ESainBotDifficulty.normal, ESainBotDifficulty.hard, ESainBotDifficulty.impossible };
    private static readonly EBotLevel[] Levels = { EBotLevel.Easy, EBotLevel.Normal, EBotLevel.Hard, EBotLevel.Impossible };
    private static readonly string[] KnobGroups = { "aim", "sense", "mind" };
    private static readonly EPersonality[] Personalities = (EPersonality[])Enum.GetValues(typeof(EPersonality));

    public static readonly string[] Sections = BotTypeDefinitions.BotTypes.Values.Select(b => b.Section).Distinct().ToArray();
    private static readonly Dictionary<string, List<BotType>> BotsBySection = Sections.ToDictionary(s => s, s => BotTypeDefinitions.BotTypes.Values.Where(b => b.Section == s).ToList());

    private static BotType _selected;
    private static string _selectedSection; // non-null = edit the explicit selection
    private static readonly HashSet<BotType> _selection = new();
    private static bool _newOnly, _changedOnly;
    private static bool _help;
    private static int _helpStep=1;
    private static string _slotMessage;
    private static readonly HashSet<string> _folded = new(Sections);
    private static ESainBotDifficulty? _advScope; // Advanced only; knobs always write all four slots
    private static string _search = string.Empty;
    private static bool _advancedOpen;
    private static bool _personalityOpen;
    private static bool _copyOpen;
    private static readonly HashSet<BotType> _copyTargets = new();
    private static Vector2 _listScroll;
    private static Vector2 _rightScroll;
    private static GUIEntryConfig _entryConfig;
    private static GUIStyle _wrapSmall;

    // Spawn record: read in the background each time the page is opened again (a raid may have ended since).
    private static volatile List<SpawnRaidCounts> _loaded;
    private static int _lastMenuFrame = -10;

    // "Revert" copies, taken when the page opens and again after a save.
    private static readonly Dictionary<BotType, SAINSettingsGroupClass> _saved = new();
    private static bool _lastUnsaved;
    private static SAINPresetClass _snapshotPreset;

    private static readonly View _view = new();
    private static bool _dirty = true;

    private static bool Korean
    {
        get { return EditorText.Korean; }
    }

    private static string T(string korean, string english)
    {
        return Korean ? korean : english;
    }

    public static void Menu()
    {
        ZzapUI.EnsureStyles();
        EnsureStyles();
        bool unsaved = ConfigEditingTracker.UnsavedChanges;
        if (Time.frameCount - _lastMenuFrame > 2 || (_lastUnsaved && !unsaved) || !ReferenceEquals(_snapshotPreset, SAINPlugin.LoadedPreset))
        {
            // (Re)opened, or just saved: fetch the record again, take fresh revert copies, recheck every badge.
            Task.Run(() => _loaded = SpawnDifficultyRecorder.Load());
            BotSlotsGUI.Reopen();
            TakeSnapshots();
            _view.AllBadgesStale = true;
            _dirty = true;
        }
        _lastUnsaved = unsaved;
        _lastMenuFrame = Time.frameCount;
        _selected ??= BotTypeDefinitions.BotTypes.Values.FirstOrDefault();
        if (Event.current.type == EventType.Layout)
        {
            _view.Update();
        }

        GUILayout.BeginHorizontal();
        BotList();
        GUILayout.Space(12f);
        GUILayout.BeginVertical(ZzapUI.Frame, GUILayout.Height(ZzapUI.PANEL_HEIGHT), GUILayout.ExpandWidth(true));
        _rightScroll = GUILayout.BeginScrollView(_rightScroll, GUILayout.ExpandHeight(true));
        SelectionBar();
        if (_view.SectionName != null)
        {
            SectionPanel();
        }
        else if (_view.Group == null)
        {
            ZzapUI.PanelHeading(T("봇 종류를 고르세요", "Pick a bot type"), T("이 프리셋에 이 봇 종류가 없습니다.", "This preset has no entry for that bot type."));
        }
        else if (_copyOpen)
        {
            CopyPanel();
        }
        else
        {
            BotPanel();
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    private static void EnsureStyles()
    {
        _wrapSmall ??= new GUIStyle(ZzapUI.Small) { wordWrap = true };
    }

    /// <summary>Queue a write for after this GUI pass and rebuild the view on the next Layout.</summary>
    private static void Write(Action action)
    {
        ZzapUI.Defer(() =>
        {
            action();
            _dirty = true;
            _view.AllBadgesStale = true;
        });
    }

    private static bool IsBatch => _selectedSection!=null;
    private static List<BotType> Targets() => IsBatch ? BotTypeDefinitions.BotTypes.Values.Where(b=>_selection.Contains(b) && Group(b)!=null).ToList()
        : _selected==null ? new List<BotType>() : new List<BotType>{_selected};
    private static IEnumerable<BotType> EditBots() => Targets().Where(b=>Ai(b).CanEditSain);
    private static IEnumerable<SAINSettingsGroupClass> EditGroups() => EditBots().Select(Group);
    private static void ToggleSelection(BotType bot)
    {
        if(Group(bot)==null || !Ai(bot).CanChooseAI) return;
        if(!_selection.Remove(bot)) _selection.Add(bot);
        _selectedSection=_selection.Count==0?null:"selection";
        _copyOpen=false;
        _rightScroll=Vector2.zero;
    }
    private static void SelectionBar()
    {
        if (_selection.Count == 0) return;
        GUILayout.BeginHorizontal(ZzapUI.Notice);
        GUILayout.BeginVertical();
        GUILayout.Label(_view.SelectionTitle, ZzapUI.NoticeText);
        GUILayout.Label(IsBatch ? T("아래 변경이 선택한 봇 모두에 적용됩니다.", "Changes below apply to all selected bots.")
            : _view.KeptSelection, ZzapUI.Small);
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        if (!IsBatch && GUILayout.Button(T("묶음 편집", "Edit selection"), ZzapUI.ButtonPrimary, GUILayout.ExpandWidth(false)))
            Write(() => { _selectedSection = "selection"; _copyOpen = false; });
        if (GUILayout.Button(T("선택 해제", "Clear selection"), ZzapUI.Button, GUILayout.ExpandWidth(false)))
            Write(() => { _selection.Clear(); _selectedSection = null; });
        GUILayout.EndHorizontal();
        GUILayout.Space(14f);
    }

    private static void SlotsRow()
    {
        var source=_view.Group;
        BotSlotsGUI.Draw(source,_view.Src,slot=>Write(()=>
        {
            int skipped=0;
            var targets=Targets().Where(b=>Ai(b).CanChooseAI).ToArray();
            if(targets.Length==0) { _slotMessage=T("편집 가능한 봇이 없습니다. 전역·고정 바닐라는 건너뜁니다.","No editable bots here. Bots locked to EFT's own AI (vanilla) are skipped."); return; }
            foreach(var bot in targets) skipped+=BotSettingsSlots.Apply(slot,Group(bot),Sources(bot));
            ConfigEditingTracker.Add(T("내 설정","Custom"),slot.Name);
            _slotMessage=T($"없는 필드·난이도 칸 {skipped}개를 건너뛰었습니다.",$"Skipped {skipped} setting(s) this bot doesn't have.");
        }), note: _view.SlotNote);
        if(_slotMessage!=null) GUILayout.Label(_slotMessage,_wrapSmall);
    }
    private static string TypesEn(int n) => n == 1 ? "1 bot type" : $"{n} bot types";
    public static bool HelpOpen => _help;
    public static void OpenHelp() => Write(() => { _help = true; _helpStep = 1; });

    public static void Guidance()
    {
        EnsureStyles();
        if (PresetHandler.EditorDefaults.BotSelectionIntroDismissed) return;
        GUILayout.BeginHorizontal(ZzapUI.Notice);
        GUILayout.Label(T("<b><color=#facc15>처음이라면 여기부터</color></b>　이름을 누르면 한 봇, 왼쪽 동그라미를 누르면 여러 봇을 함께 조절해요. 손잡이를 움직인 뒤 [저장]을 눌러요.",
            "<b><color=#facc15>New here? Start here</color></b>　Click a name to edit one bot, or use the circle to select several. Adjust the knobs, then [Save]."), _wrapSmall);
        if (GUILayout.Button(T("3단계 안내", "3-step guide"), ZzapUI.Quiet, GUILayout.ExpandWidth(false))) OpenHelp();
        if (GUILayout.Button(T("알겠어요", "Got it"), ZzapUI.Button, GUILayout.ExpandWidth(false)))
            Write(() => { PresetHandler.EditorDefaults.BotSelectionIntroDismissed = true; PresetHandler.ExportEditorDefaults(); });
        GUILayout.EndHorizontal();
        GUILayout.Space(14f);
    }

    private static string HelpTitle(int step) => step == 1 ? T("봇 고르기", "Choose bot types")
        : step == 2 ? T("난이도와 손잡이", "Level and knobs") : T("저장 · 슬롯 · 공유", "Save · Slots · Sharing");

    public static void DrawHelp()
    {
        if (!_help) return;
        EnsureStyles();
        ZzapUI.BeginModal(650f, 365f);
        GUILayout.BeginHorizontal();
        GUILayout.Label(T("처음 쓰는 사람용 안내", "Guide for new users"), ZzapUI.Title);
        if (GUILayout.Button("×", ZzapUI.Quiet, GUILayout.Width(30f))) Write(() => _help = false);
        GUILayout.EndHorizontal();
        GUILayout.Space(14f);
        GUILayout.BeginHorizontal();
        for (int i = 1; i <= 3; i++)
        {
            int step = i;
            if (GUILayout.Button(HelpTitle(i), _helpStep == i ? ZzapUI.Pill : ZzapUI.Button)) Write(() => _helpStep = step);
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(18f);
        GUILayout.Label(HelpTitle(_helpStep), ZzapUI.Section);
        GUILayout.Label(_helpStep == 1
            ? T("이름을 클릭하면 한 봇만 열립니다. 왼쪽 동그라미 또는 이름 Ctrl+클릭으로 묶음에 넣으면 동그라미가 노랗게 채워집니다. 팩션 동그라미는 그 팩션 전체를 고릅니다. 일부만 선택하면 반만 채워집니다.", "Click a name to open one bot. Use the circle or Ctrl+click a name to add it to the selection. A filled circle means selected; half-filled means some. A faction’s circle selects all its bot types.")
            : _helpStep == 2
            ? T("난이도 4개 중 하나를 고른 뒤 9개 손잡이로 조절해요. 묶음의 값이 서로 다르면 ‘섞임’으로 보입니다. 손잡이를 움직이면 같은 값이 모두에게 적용돼요. 노란 세로 금은 zzap 원본 자리, [원래대로]는 그 자리로 돌려요.", "Choose one of four Levels, then adjust the nine knobs. Different values in a selection appear as ‘mixed’. Moving a knob applies the same value to all selected bots. The yellow line marks zzap original; [Reset] restores it.")
            : T("마음에 드는 값은 [＋ 지금 값 저장]으로 슬롯에 보관해요. [...]에서 이름 바꾸기·덮어쓰기·삭제·공유를 할 수 있어요. F6와 웹이 같은 슬롯을 씁니다. 바꾼 값은 위의 [저장]을 눌러야 게임에 반영돼요. 기본 zzap 프리셋은 그대로 두고 ‘[Modified]’ 사본으로 저장됩니다.", "Keep values you like in a slot with [＋ Save current]. Use [...] to Rename, Overwrite, Delete, or share. Custom slots are shared by F6 and the web page. Click [Save] at the top to apply changes in game. Changes are saved as a '[Modified]' copy (the bundled zzap presets stay untouched)."), _wrapSmall);
        GUILayout.FlexibleSpace();
        ZzapUI.Separator(8f);
        GUILayout.Label(T("언제든 오른쪽 위 [?]로 다시 열 수 있어요.", "Reopen this guide anytime with [?] at the top right."), ZzapUI.Small);
        GUILayout.BeginHorizontal();
        GUILayout.Label(_helpStep == 1 ? "1 / 3" : _helpStep == 2 ? "2 / 3" : "3 / 3", ZzapUI.Small);
        GUILayout.FlexibleSpace();
        if (_helpStep > 1 && GUILayout.Button(T("이전", "Back"), ZzapUI.Button, GUILayout.Width(70f))) Write(() => _helpStep--);
        if (GUILayout.Button(_helpStep < 3 ? T("다음", "Next") : T("시작할게요", "Get started"), ZzapUI.ButtonPrimary, GUILayout.Width(100f)))
            Write(() => { if (_helpStep < 3) _helpStep++; else _help = false; });
        GUILayout.EndHorizontal();
        ZzapUI.EndModal();
    }

    // ---------------------------------------------------------------- data

    private static SAINSettingsGroupClass Group(BotType bot)
    {
        var all = SAINPlugin.LoadedPreset?.BotSettings?.SAINSettings;
        return bot != null && all != null && all.TryGetValue(bot.WildSpawnType, out var group) ? group : null;
    }

    private static SAINPresetBundle Bundle(string name)
    {
        return PresetSync.TryGetSavedCustomBundle(name, out var bundle) ? bundle : null;
    }

    private static SAINSettingsGroupClass BotIn(SAINPresetBundle bundle, BotType bot)
    {
        return bundle?.BotSettings != null && bundle.BotSettings.TryGetValue(bot.WildSpawnType.ToESain(), out var group) ? group : null;
    }

    /// <summary>The same bot in the bundled zzap preset, else in a SAIN default preset; null for a bot neither lists.</summary>
    private static SAINSettingsGroupClass Reference(BotType bot)
    {
        var zzap = BotIn(Bundle("zzap"), bot);
        if (zzap != null)
        {
            return zzap;
        }
        foreach (var bundle in PresetSync.SavedServerDefaults())
        {
            var group = BotIn(bundle, bot);
            if (group != null)
            {
                return group;
            }
        }
        return null;
    }

    private static LevelSources Sources(BotType bot)
    {
        var easy = Bundle("zzap 쉬움");
        var normal = Bundle("zzap 보통");
        var hard = Bundle("zzap");
        return new LevelSources
        {
            Reference = Reference(bot),
            BaselineLevel = BotLevels.PresetLevel(SAINPlugin.LoadedPreset?.Info?.Name),
            BaselineBot = FamilyBaseline(bot),
            Current = LevelGlobals.From(SAINPlugin.LoadedPreset?.GlobalSettings),
            Easy = LevelGlobals.From(easy),
            Normal = LevelGlobals.From(normal),
            Hard = LevelGlobals.From(hard),
            EasyBot = BotIn(easy, bot),
            NormalBot = BotIn(normal, bot),
            HardBot = BotIn(hard, bot),
        };
    }

    private static SAINSettingsGroupClass FamilyBaseline(BotType bot)
    {
        var level = BotLevels.PresetLevel(SAINPlugin.LoadedPreset?.Info?.Name);
        return level == EBotLevel.None ? null : BotIn(Bundle(LevelBundleName(level)), bot);
    }

    private static BotAiState Ai(BotType bot) => BotAiPolicy.Resolve(bot.WildSpawnType.ToESain(),
        SAINPlugin.LoadedPreset?.GlobalSettings?.General?.VanillaBots, Group(bot), BotSpawnController.IsExcluded(bot.WildSpawnType));

    private static string AiLabel(BotAiState ai) => ai.Mode switch
    {
        BotAiMode.GlobalVanilla => T("바닐라 · 전역", "vanilla · global"),
        BotAiMode.AlwaysVanilla => T("바닐라 · 고정", "vanilla · fixed"),
        BotAiMode.ExternalVanilla => T("바닐라 · 외부", "vanilla · external"),
        BotAiMode.PerBotVanilla => T("바닐라 · 봇별", "vanilla · per bot"),
        _ => "SAIN",
    };

    private static void TakeSnapshots()
    {
        var preset = SAINPlugin.LoadedPreset;
        bool samePreset = ReferenceEquals(_snapshotPreset, preset);
        if (!samePreset)
        {
            _saved.Clear();
        }
        _snapshotPreset = preset;
        SAINPresetBundle saved = null;
        if (preset?.Info?.IsCustom == true)
        {
            PresetSync.TryGetSavedCustomBundle(preset.Info.Name, out saved);
        }
        else if (preset?.Info != null)
        {
            saved = PresetSync.SavedServerDefaults().FirstOrDefault(b => b?.Info?.BaseSAINDifficulty == preset.Info.BaseSAINDifficulty);
        }
        // A reopened tab must not turn dirty live values into the saved baseline.
        // If the server copy is unavailable, keep the previous snapshot or disable Revert until a clean capture.
        if (saved == null && ConfigEditingTracker.UnsavedChanges)
        {
            return;
        }
        _saved.Clear();
        foreach (var bot in BotTypeDefinitions.BotTypes.Values)
        {
            var copy = BotLevels.Snapshot(saved != null ? BotIn(saved, bot) : Group(bot));
            if (copy != null)
            {
                _saved[bot] = copy;
            }
        }
    }

    private static string LevelName(EBotLevel level) => level switch
    {
        EBotLevel.Easy => T("쉬움", "Easy"),
        EBotLevel.Normal => T("보통", "Normal"),
        EBotLevel.Hard => T("어려움", "Hard"),
        EBotLevel.Impossible => T("불가능", "Impossible"),
        _ => string.Empty,
    };

    private static string LevelDescription(EBotLevel level) => level switch
    {
        EBotLevel.Easy => T("여유롭게", "Relaxed"),
        EBotLevel.Normal => T("균형 있게", "Balanced"),
        EBotLevel.Hard => T("긴장감 있게", "Challenging"),
        _ => T("아주 강하게", "Very tough"),
    };

    private static string SectionLabel(string section) => section switch
    {
        "Scavs" => T("스캐브 <color=#71717a>Scavs</color>", "Scavs"),
        "PMCs" => T("PMC <color=#71717a>PMCs</color>", "PMCs"),
        "Goons" => T("군즈 <color=#71717a>Goons</color>", "Goons"),
        "Other" => T("기타 <color=#71717a>Other</color>", "Other"),
        "Bosses" => T("보스 <color=#71717a>Bosses</color>", "Bosses"),
        "Followers" => T("추종자 <color=#71717a>Followers</color>", "Followers"),
        "Black Division" => T("블랙 디비전 <color=#71717a>Black Division</color>", "Black Division"),
        "RUAF" => T("러시아군 <color=#71717a>RUAF</color>", "RUAF"),
        "Remnant" => T("잔당 <color=#71717a>Remnant</color>", "Remnant"),
        _ => section,
    };

    private static string LevelHint(EBotLevel level) => level switch
    {
        EBotLevel.Easy => T("zzap 쉬움 프리셋 수준", "zzap easy preset level"),
        EBotLevel.Normal => T("zzap 보통 프리셋 수준", "zzap normal preset level"),
        EBotLevel.Hard => T("zzap 프리셋 수준", "zzap preset level"),
        _ => T("zzap보다 더 세게", "stronger than zzap"),
    };

    private static string LevelBundleName(EBotLevel level) => level switch
    {
        EBotLevel.Easy => "zzap 쉬움",
        EBotLevel.Normal => "zzap 보통",
        _ => "zzap",
    };

    private static string BadgeText(SAINSettingsGroupClass group, LevelSources src)
    {
        if (group == null)
        {
            return null;
        }
        if (group.UseVanillaAI)
        {
            return T("바닐라", "vanilla");
        }
        var (level, adjusted, custom) = BotLevels.Badge(group, src);
        if (custom || level == EBotLevel.None)
        {
            return T("직접 설정", "custom");
        }
        return LevelName(level) + (adjusted ? T(" (조정됨)", " (adjusted)") : string.Empty);
    }

    private static string DeltaColor(int direction)
    {
        return direction > 0 ? "#f87171" : direction < 0 ? "#4ade80" : "#71717a";
    }

    private static string PersonalityName(EPersonality personality)
    {
        return personality == EPersonality.None ? T("지정 안 함", "Unassigned") : personality.ToString();
    }

    private static string GroupName(string group)
    {
        return group switch
        {
            "aim" => T("조준", "Aim"),
            "sense" => T("감각", "Senses"),
            _ => T("성향", "Mind"),
        };
    }

    private sealed class KnobView
    {
        public BotKnob Knob;
        public GUIContent Label;
        public float Value;
        public float Reference;
        public int Direction;
        public string Delta;
        public bool On;
        public EPersonality Personality;
        public string Note;
        public bool CanReset, Mixed;
        public string CurrentText, BaseText;
    }

    private sealed class RowView
    {
        public BotFieldRow Row;
        public ConfigInfoClass Info;
        public string Heading;
        public GUIContent Label;
        public string RefText;
        public bool Changed, New, Mixed;
    }

    private sealed class LevelView
    {
        public EBotLevel Level;
        public GUIContent Content;
        public bool On;
        public bool Available;
    }

    private sealed class SectionRow
    {
        public string Name;
        public string Badge;
        public string Ai;
    }

    /// <summary>What the page shows, rebuilt on Layout when something changed (selection, record, a write, a save).</summary>
    private sealed class View
    {
        public string SectionName;
        public SAINSettingsGroupClass Group;
        public SAINSettingsGroupClass Reference;
        public LevelSources Src;
        public SAINSettingsClass LevelValues;
        public SAINSettingsClass Slot;
        public ESainBotDifficulty Shown;
        public string Badge;
        public string RecordLine;
        public string ReferenceLine;
        public string SlotsLine;
        public string AdjustedLine;
        public string AdvancedTitle;
        public string NewCount;
        public bool Vanilla;
        public string VanillaLock;
        public BotAiState Ai;
        public bool CanRevert;
        public readonly List<LevelView> LevelButtons = new();
        public readonly List<(ESainBotDifficulty? Scope, string Text, bool Seen)> Pills = new();
        public readonly List<KnobView> Knobs = new();
        public readonly List<RowView> Rows = new();
        public readonly List<SectionRow> SectionRows = new();
        public readonly Dictionary<BotType, string> Badges = new();
        public readonly Dictionary<string, string> SectionCounts = new();
        public bool AllBadgesStale = true;
        public bool? SectionVanilla;
        public string SelectionTitle, KeptSelection, TargetTitle, TargetLine, BotCount, SlotNote, KnobHeading, PoolText;
        public readonly Dictionary<string, string> SectionLabels = new();
        public readonly Dictionary<string, List<BotType>> Eligible = new();
        public readonly Dictionary<string, int> FactionMarks = new();

        private List<SpawnRaidCounts> _record;
        private bool _korean;
        private string _context;
        private BotType _bot;
        private string _section;
        private ESainBotDifficulty? _scope;
        private readonly Dictionary<BotFieldRow, ConfigInfoClass> _infos = new();
        private readonly Dictionary<BotType,SAINSettingsGroupClass> _baselines = new();

        public void Update()
        {
            var record = _loaded;
            bool languageChanged = _korean != Korean;
            var global = SAINPlugin.LoadedPreset?.GlobalSettings;
            string context = SAINPlugin.LoadedPreset?.Info?.Name + JsonConvert.SerializeObject(global?.General?.VanillaBots)
                + JsonConvert.SerializeObject(global?.Difficulty) + global?.Shoot?.BOT_RECOIL_COEF + global?.Aiming?.FasterCQBReactionsGlobal;
            if (_context != context) { AllBadgesStale = true; _context = context; }
            if (ReferenceEquals(record, _record) && !languageChanged && !_dirty && !AllBadgesStale && _bot == _selected && _section == _selectedSection && _scope == _advScope) return;
            _record = record;
            _korean = Korean;
            _bot = _selected;
            _section = _selectedSection;
            _scope = _advScope;
            _dirty = false;

            if (AllBadgesStale || languageChanged)
            {
                AllBadgesStale = false;
                foreach (var bot in BotTypeDefinitions.BotTypes.Values)
                {
                    Badges[bot] = Ai(bot).UsesVanilla ? AiLabel(Ai(bot)) : BadgeText(Group(bot), Sources(bot));
                }
            }
            SectionCounts.Clear();
            foreach (var section in Sections)
            {
                var eligible = BotsBySection[section].Where(b => BotSelectionClass.Group(b) != null && BotSelectionClass.Ai(b).CanChooseAI).ToList();
                Eligible[section] = eligible;
                int picked = eligible.Count(b => _selection.Contains(b));
                FactionMarks[section] = picked == 0 ? 0 : picked == eligible.Count ? 1 : 2;
                int total = BotsBySection[section].Count;
                SectionCounts[section] = picked > 0 ? $"{picked}/{total}" : total.ToString();
                SectionLabels[section] = SectionLabel(section);
            }

            BotCount = T($"{BotTypeDefinitions.BotTypes.Count}종", $"{BotTypeDefinitions.BotTypes.Count} types");
            var selectedNames = string.Join(" · ", BotTypeDefinitions.BotTypes.Values.Where(b => _selection.Contains(b)).Select(b => b.Name));
            SelectionTitle = T($"선택한 {_selection.Count}종 함께 편집 ({selectedNames})", $"Editing {TypesEn(_selection.Count)} together ({selectedNames})");
            KeptSelection = T($"지금은 {_selected?.Name} 한 봇만 편집합니다. 묶음은 유지됩니다.", $"Currently editing only {_selected?.Name}. Your selection is kept.");
            KnobHeading = T($"손잡이로 세부 조절 {BotQuickSettings.All.Length}개", $"Fine-tune with knobs {BotQuickSettings.All.Length}");
            Knobs.Clear();
            Rows.Clear();
            Pills.Clear();
            LevelButtons.Clear();
            SectionRows.Clear();
            _baselines.Clear();
            foreach(var bot in EditBots()) _baselines[bot]=BotSelectionSettings.Baseline(BotSelectionClass.Group(bot),Sources(bot));
            SectionName = _section;
            if (_section != null)
                _bot=Targets().FirstOrDefault(b=>BotSelectionClass.Ai(b).CanEditSain) ?? Targets().FirstOrDefault();
            Group = Group(_bot);
            if (Group == null)
            {
                return;
            }
            TargetTitle = IsBatch ? T($"선택한 {_selection.Count}종", $"{_selection.Count} selected bot {(_selection.Count == 1 ? "type" : "types")}") : _bot.Name;
            TargetLine = (IsBatch ? selectedNames : SectionLabel(_bot.Section) + " · " + _bot.Name) + "　" +
                (Targets().Count == 1 ? T("이 봇의 값만 바뀝니다.", "Changes apply to this bot only.") : T("같은 설정을 여러 봇에 한 번에 바꿉니다.", "Change the same settings for all selected bots at once."));
            PoolText = PoolLabel();
            SlotNote = T("F6와 웹은 같은 내 설정 슬롯을 함께 씁니다. 슬롯을 누르면 지금 편집 중인 봇에 적용됩니다.", "Custom slots are shared by F6 and the web page. Click a slot to apply it to the bots being edited.");
            if (IsBatch) SlotNote = T($"F6와 웹은 같은 내 설정 슬롯을 함께 씁니다. 섞인 값 저장 시 {_bot.Name} 값을 저장합니다.", $"Custom slots are shared by F6 and the web page. When values are mixed, {_bot.Name} values are saved.");
            Src = Sources(_bot);
            Reference = Src.BaselineBot ?? Src.Reference;
            Badges[_bot] = Badge = BadgeText(Group, Src);
            Ai = BotSelectionClass.Ai(_bot);
            Vanilla = Ai.UsesVanilla;
            VanillaLock = Ai.CanChooseAI ? null : T("바닐라 우선 적용: ", "Vanilla override: ") + Ai.Reason;
            if (Ai.UsesVanilla) Badges[_bot] = Badge = AiLabel(Ai);
            CanRevert = _saved.ContainsKey(_bot);
            var shownLevel = Group.DifficultyLevel == EBotLevel.None ? (Src.BaselineLevel == EBotLevel.None ? EBotLevel.Hard : Src.BaselineLevel) : Group.DifficultyLevel;
            LevelValues = Group.DifficultyLevel == EBotLevel.None && Reference != null
                ? BotLevels.NormalSlot(Reference) : BotLevels.Compute(shownLevel, Group, Src);
            foreach (var level in Levels)
            {
                bool on = BotLevels.Badge(Group, Src).Level == level;
                bool available = Ai.CanEditSain && BotLevels.Available(level, Src);
                string text = $"<b>{LevelName(level)}</b>\n<size=11>{LevelDescription(level)}</size>";
                string tip = available ? null : (Korean ? $"'{LevelBundleName(level)}' 프리셋을 찾을 수 없습니다" : $"Preset '{LevelBundleName(level)}' not found");
                LevelButtons.Add(new LevelView { Level = level, Content = new GUIContent(text, tip), On = on, Available = available });
            }
            int adjusted = BotLevels.AdjustedCount(Group, Src);
            AdjustedLine = adjusted > 0
                ? (Korean ? $"⚠ 손으로 조정한 값이 {adjusted}개 있습니다 — 다른 난이도를 누르면 그 단계 값으로 바뀝니다" : $"⚠ {adjusted} hand-adjusted value(s) - another level replaces them with that level's values")
                : null;

            // Advanced: the slot shown and edited there (per-difficulty editing), the record, the "all slots same" note.
            var totals = SpawnDifficultyRecord.Totals(record, _bot.WildSpawnType.ToESain());
            Shown = _scope ?? ESainBotDifficulty.normal;
            if (!Group.Settings.TryGetValue(Shown, out Slot) || Slot == null)
            {
                Slot = BotLevels.NormalSlot(Group);
                Shown = Group.Settings.FirstOrDefault(p => p.Value == Slot).Key;
            }
            SAINSettingsClass refSlot = Reference != null && Reference.Settings.TryGetValue(Shown, out var r) ? r : BotLevels.NormalSlot(Reference);
            RecordLine = totals.Count == 0
                ? T("실제로 나온 난이도: 아직 기록 없음 (레이드를 한 판 하면 나옵니다)", "Spawned at: no record yet (play a raid)")
                : (Korean ? $"실제로 나온 난이도 (최근 {record?.Count ?? 0}판): " : $"Spawned at (last {record?.Count ?? 0} raids): ")
                    + string.Join(" · ", Difficulties.Where(totals.ContainsKey).Select(d => Korean ? $"{EditorText.Value(d)} {totals[d]}명" : $"{d} {totals[d]}"));
            SlotsLine = BotQuickSettings.AllSlotsSame(Group) ? T("난이도 4칸 값이 모두 같습니다.", "All four difficulty slots hold the same values.") : null;
            ReferenceLine = Reference != null ? T("기준: 현재 프리셋 계열 원본", "Reference: current preset family") : T("기준 없음 (zzap 프리셋에 이 봇이 없음)", "No reference (not in the zzap preset)");
            Pills.Add((null, T("4칸 전부", "All 4"), true));
            foreach (var d in Difficulties)
            {
                if (Group.Settings.ContainsKey(d))
                {
                    bool seen = totals.Count == 0 || totals.ContainsKey(d);
                    Pills.Add((d, EditorText.Value(d) + (seen ? string.Empty : T(" (안 나옴)", " (not seen)")), seen));
                }
            }

            var normalSlot = BotLevels.NormalSlot(Group);
            if (normalSlot == null)
            {
                return;
            }
            foreach (var knob in BotQuickSettings.All)
            {
                bool affected = BotLevels.AffectsKnob(knob);
                bool changed = _baselines.TryGetValue(_bot,out var baseline) && BotQuickSettings.IsChanged(knob,Group,baseline);
                var view = new KnobView
                {
                    Knob = knob,
                    Label = new GUIContent("<b>" + knob.Name(Korean) + "</b>", knob.Help(Korean)),
                    CanReset = changed,
                    Value = knob.Get(normalSlot),
                    On = knob.GetOn(normalSlot),
                    Personality = knob.GetPersonality(normalSlot),
                };
                view.Reference = baseline != null && BotLevels.NormalSlot(baseline) != null ? knob.Get(BotLevels.NormalSlot(baseline)) : view.Value;
                view.Direction = knob.Direction(view.Value, view.Reference);
                string levelName = LevelName(shownLevel);
                view.Delta = !affected || LevelValues == null
                    ? $"<b>{knob.Format(view.Value, Korean)}</b>"
                    : view.Direction == 0
                        ? $"<color=#71717a>{(Korean ? $"{levelName} 값 그대로" : $"{levelName} value")}</color>   <b>{knob.Format(view.Value, Korean)}</b>"
                        : $"<color=#71717a>{levelName} {knob.Format(view.Reference, Korean)}</color> → <b>{knob.Format(view.Value, Korean)}</b>   <color={DeltaColor(view.Direction)}><b>{knob.DeltaText(view.Value, view.Reference, Korean)}</b></color>";
                if (!affected)
                {
                    view.Note = $"<color=#71717a>{T("난이도와 무관", "Not affected by the level")}</color>";
                }
                view.CurrentText = knob.Kind == BotKnobKind.Toggle ? (view.On ? T("켬", "On") : T("끔", "Off")) : knob.Format(view.Value, Korean);
                view.BaseText = T("원본 ", "original ") + (knob.Kind == BotKnobKind.Toggle ? (baseline != null && knob.GetOn(BotLevels.NormalSlot(baseline)) ? T("켬", "On") : T("끔", "Off")) : knob.Format(view.Reference, Korean));
                if (knob.Kind == BotKnobKind.Personality) { view.CurrentText = PoolText; view.BaseText = T("원본 ", "original ") + PersonalityName(baseline == null ? EPersonality.None : knob.GetPersonality(BotLevels.NormalSlot(baseline))); }
                Knobs.Add(view);
            }

            if (IsBatch)
            {
                foreach(var kv in Knobs)
                {
                    kv.Mixed = BotSelectionSettings.KnobMixed(EditGroups(), kv.Knob);
                    if (kv.Mixed) kv.BaseText = T("원본은 봇마다 다름", "original differs per bot");
                    kv.CanReset=EditBots().Any(b=>BotQuickSettings.IsChanged(kv.Knob,BotSelectionClass.Group(b),_baselines[b]));
                }
                UpdateSection();
            }
            int changedCount = 0;
            string lastGroup = null;
            foreach (var row in BotAdvancedFields.Rows())
            {
                if (!_infos.TryGetValue(row, out var info))
                {
                    _infos[row] = info = AttributesGUI.GetAttributeInfo(row.Field);
                }
                if (info == null)
                {
                    continue;
                }
                bool changed = EditBots().Any(b=>BotAdvancedEdit.Changed(BotSelectionClass.Group(b), _baselines[b], _scope, row));
                if ((_newOnly && !SettingsFieldStatus.IsNew(row.Field)) || (_changedOnly && !changed)) continue;
                if (changed)
                {
                    changedCount++;
                }
                string heading = null;
                if (row.Group != lastGroup)
                {
                    lastGroup = row.Group;
                    heading = row.Group == "vanilla" ? T("게임 기본값", "EFT's own values") : EditorText.UI(row.Category.Name);
                }
                string name = row.Nested != null ? AttributesGUI.GetAttributeInfo(row.Nested)?.Name + " › " + info.Name : info.Name;
                Rows.Add(new RowView
                {
                    Row = row,
                    Info = info,
                    Heading = heading,
                    Label = new GUIContent(name, info.Description),
                    New = SettingsFieldStatus.IsNew(row.Field),
                    Mixed = IsBatch && BotSelectionSettings.FieldMixed(EditGroups(), row, Shown),
                    RefText = refSlot != null ? $"<color=#71717a>{T("기준", "ref")} {row.Get(refSlot)}</color>" : null,
                    Changed = changed,
                });
            }
            AdvancedTitle = Korean
                ? $"고급 · 숫자로 직접 설정 {Rows.Count}개"
                : $"Advanced · exact numbers {Rows.Count}";
            int newCount = BotAdvancedFields.Rows().Count(row => SettingsFieldStatus.IsNew(row.Field));
            NewCount = newCount > 0 ? $"NEW {newCount}" : null;
        }

        private void UpdateSection()
        {
            LevelButtons.Clear();
            var bots = Targets();
            var counts = new Dictionary<EBotLevel, int>();
            bool anyVanilla = false, anySain = false;
            foreach (var bot in bots)
            {
                var group = Group(bot);
                if (group == null)
                {
                    continue;
                }
                var src = Sources(bot);
                var (level, _, custom) = BotLevels.Badge(group, src);
                if (!custom && level != EBotLevel.None)
                {
                    counts.TryGetValue(level, out int n);
                    counts[level] = n + 1;
                }
                if (BotSelectionClass.Ai(bot).UsesVanilla)
                {
                    anyVanilla = true;
                }
                else
                {
                    anySain = true;
                }
                SectionRows.Add(new SectionRow { Name = bot.Name, Badge = BadgeText(group, src), Ai = AiLabel(BotSelectionClass.Ai(bot)) });
            }
            SectionVanilla = anyVanilla && anySain ? null : anyVanilla;
            foreach (var level in Levels)
            {
                counts.TryGetValue(level, out int n);
                string text = $"<b>{LevelName(level)}</b>\n<size=11>{LevelDescription(level)} · {(Korean ? $"{n}종" : (n == 1 ? "1 type" : $"{n} types"))}</size>";
                LevelButtons.Add(new LevelView { Level = level, Content = new GUIContent(text), On = n == bots.Count && n > 0, Available = bots.Any(b => BotSelectionClass.Ai(b).CanEditSain && BotLevels.Available(level, Sources(b))) });
            }
        }
    }

    // ---------------------------------------------------------------- left: bot list

    private static void BotList()
    {
        GUILayout.BeginVertical(ZzapUI.Frame, GUILayout.Width(ZzapUI.SIDE_WIDTH), GUILayout.Height(ZzapUI.PANEL_HEIGHT));
        GUILayout.BeginHorizontal();
        GUILayout.Label(T("봇 고르기", "Choose bot types"), ZzapUI.Name);
        GUILayout.Label(_view.BotCount, ZzapUI.Count);
        GUILayout.EndHorizontal();
        string query = ZzapUI.Search(_search, T("봇 이름 검색", "Search bot types"));
        if(query!=_search) Write(()=>_search=query);
        GUILayout.Label(T("이름 클릭: 한 봇 열기", "Click a name: open one bot"), ZzapUI.Small);
        GUILayout.Label(T("동그라미 또는 Ctrl+클릭: 함께 편집할 묶음", "Circle or Ctrl+click: add to selection"), ZzapUI.Small);
        ZzapUI.Separator(8f);
        _listScroll = GUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
        string search = (_search ?? string.Empty).Trim();
        foreach (string section in Sections)
        {
            var bots = BotsBySection[section];
            bool anyMatch = search.Length == 0 || bots.Any(b => b.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!anyMatch)
            {
                continue;
            }
            bool folded = _folded.Contains(section) && search.Length == 0;
            GUILayout.BeginHorizontal();
            var eligible = _view.Eligible[section];
            GUI.enabled=eligible.Count>0;
            if(ZzapUI.SelectMark(_view.FactionMarks[section],T("팩션 전체 선택","Select whole faction"),29f,32f))
                Write(()=> { if(eligible.All(b=>_selection.Contains(b))) _selection.ExceptWith(eligible); else _selection.UnionWith(eligible); _selectedSection=_selection.Count==0?null:"selection"; _copyOpen=false; });
            GUI.enabled=true;
            if (GUILayout.Button(folded ? "▸" : "▾", ZzapUI.Quiet, GUILayout.Width(26f), GUILayout.Height(28f)))
            {
                string captured = section;
                Write(() =>
                {
                    if (!_folded.Remove(captured))
                    {
                        _folded.Add(captured);
                    }
                });
            }
            _view.SectionCounts.TryGetValue(section, out string count);
            if (ZzapUI.ListItem(_view.SectionLabels[section], count, false, T("누르면 접기/펼치기", "Click to collapse/expand"), 0f, 28f))
            {
                string captured = section;
                Write(() =>
                {
                    if(!_folded.Remove(captured)) _folded.Add(captured);
                    _copyOpen = false;
                    _rightScroll = Vector2.zero;
                });
            }
            GUILayout.EndHorizontal();
            if (folded)
            {
                continue;
            }
            foreach (var bot in bots)
            {
                if (search.Length > 0 && bot.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                _view.Badges.TryGetValue(bot, out string badge);
                var captured = bot;
                bool active = _selectedSection == null && bot == _selected;
                bool bundled = _selection.Contains(bot);
                ZzapUI.BeginListRow(active || bundled);
                GUI.enabled=Group(bot)!=null && Ai(bot).CanChooseAI;
                if(ZzapUI.SelectMark(bundled?1:0,T("묶음에 넣기/빼기","Add to / remove from selection"),29f,52f,active || bundled)) Write(()=>ToggleSelection(captured));
                GUI.enabled=true;
                bool ctrl=Event.current.control || SAINEditor.CtrlKeyPressed;
                if (ZzapUI.ListItem(bot.Name, badge, active, bot.Description, 0f, 52f, detailOnNewLine: true, inSelection: bundled, joined: true))
                {
                    Write(() =>
                    {
                        if(ctrl) { ToggleSelection(captured); return; }
                        _selected = captured;
                        _selectedSection = null;
                        _copyOpen = false;
                        _personalityOpen = false;
                        _rightScroll = Vector2.zero;
                    });
                }
                ZzapUI.EndListRow(active);
            }
        }
        GUILayout.EndScrollView();
        ZzapUI.Separator(6f);
        GUILayout.Label(T("이름 옆은 현재 난이도입니다.\n<color=#facc15>(조정됨)</color>은 난이도를 고른 뒤 값을 더 바꿨다는 뜻입니다.", "Next to the name is the current Level.\n<color=#facc15>(adjusted)</color> means changed after choosing a Level."), _wrapSmall);
        GUILayout.EndVertical();
    }

    // ---------------------------------------------------------------- right: one bot

    private static void HeaderButtons(Action revert, Action original, bool canRevert, bool canEdit = true)
    {
        GUI.enabled = canRevert && canEdit;
        if (GUILayout.Button(new GUIContent(T("되돌리기", "Revert"), T("마지막으로 저장한 값으로 (저장 안 한 변경 취소)", "Back to the last saved values (drops unsaved changes)")), ZzapUI.Button, GUILayout.Height(28f)))
        {
            Write(revert);
        }
        GUI.enabled = canEdit;
        if (GUILayout.Button(new GUIContent(T("원본 사용", "Use original"), T("zzap 프리셋의 원래 값으로, 난이도 선택도 지움 (AI는 SAIN으로)", "The zzap preset's values, no level picked, SAIN AI")), ZzapUI.Button, GUILayout.Height(28f)))
        {
            Write(original);
        }
        GUI.enabled = true;
    }

    private static void AiRow(bool? vanilla, string lockReason, Action<bool> set)
    {
        GUILayout.BeginHorizontal(ZzapUI.DarkFrame);
        GUILayout.Label("<b>" + T("사용할 AI", "AI to use") + "</b>", ZzapUI.Name, GUILayout.Width(110f), GUILayout.Height(28f));
        GUI.enabled = lockReason == null;
        if (GUILayout.Button("SAIN", vanilla == false ? ZzapUI.Pill : ZzapUI.Button, GUILayout.Width(90f), GUILayout.Height(28f)) && vanilla != false)
        {
            Write(() => set(false));
        }
        if (GUILayout.Button(T("게임 기본 (바닐라)", "EFT's own (vanilla)"), vanilla == true ? ZzapUI.Pill : ZzapUI.Button, GUILayout.Width(150f), GUILayout.Height(28f)) && vanilla != true)
        {
            Write(() => set(true));
        }
        GUI.enabled = true;
        string help = lockReason ?? (vanilla == null ? T("섞임 · 누르면 전부 그 AI로", "mixed · click to set all") : T("AI 변경은 게임 재시작 후 적용", "Applies after a game restart"));
        GUILayout.FlexibleSpace();
        GUILayout.Label(help, _wrapSmall, GUILayout.Height(28f), GUILayout.ExpandWidth(false));
        GUILayout.EndHorizontal();
    }

    private static void LevelRow(Action<EBotLevel> pick)
    {
        ZzapUI.StepHeading("01", T("난이도 고르기", "Choose Level"), T("고르면 아래 값이 해당 단계로 바뀝니다", "Sets the values below to this Level"));
        GUILayout.BeginHorizontal();
        foreach (var lv in _view.LevelButtons)
        {
            GUI.enabled = lv.Available;
            if (GUILayout.Button(lv.Content, lv.On ? ZzapUI.LevelOn : ZzapUI.Level, GUILayout.Height(52f), GUILayout.ExpandWidth(true)))
            {
                var captured = lv.Level;
                Sounds.PlaySound(EUISoundType.MenuCheckBox);
                Write(() => pick(captured));
            }
            GUI.enabled = true;
        }
        GUILayout.EndHorizontal();
    }

    private static void BotPanel()
    {
        var group = _view.Group;
        var src = _view.Src;
        var bot = _selected;
        GUILayout.BeginHorizontal();
        GUILayout.Label(_view.TargetTitle, ZzapUI.Title, GUILayout.ExpandWidth(false));
        GUILayout.Label(T("한 봇 편집", "One bot"), ZzapUI.Tag, GUILayout.ExpandWidth(false));
        GUILayout.FlexibleSpace();
        HeaderButtons(
            () =>
            {
                if (!Ai(bot).CanEditSain) return;
                BotLevels.RestoreSaved(group, _saved.TryGetValue(bot, out var saved) ? saved : null);
                ConfigEditingTracker.Add(bot.Name, "revert");
                _view.AllBadgesStale = true;
            },
            () =>
            {
                if (!Ai(bot).CanEditSain) return;
                BotLevels.ResetToOriginal(group, src);
                ConfigEditingTracker.Add(bot.Name, "original");
            },
            _view.CanRevert, _view.Ai.CanEditSain);
        GUI.enabled = _view.Ai.CanEditSain;
        if (GUILayout.Button(T("다른 봇에 복사", "Copy to other bots"), ZzapUI.Button, GUILayout.Height(28f)))
        {
            Write(() =>
            {
                _copyOpen = true;
                _copyTargets.Clear();
            });
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        GUILayout.Label(_view.TargetLine, _wrapSmall);

        AiRow(_view.Vanilla, _view.VanillaLock, vanilla =>
        {
            group.UseVanillaAI = vanilla;
            ConfigEditingTracker.Add(bot.Name, vanilla ? "vanilla" : "SAIN");
        });
        if (!_view.Ai.CanEditSain)
        {
            SlotsRow();
            GUILayout.Label(T("현재 적용: ", "Effective AI: ") + AiLabel(_view.Ai), _wrapSmall);
            GUILayout.Label(T("보관 중인 봇별 선택: ", "Stored per-bot choice: ") + (group.UseVanillaAI ? T("바닐라", "Vanilla") : "SAIN"), _wrapSmall);
            GUILayout.Label(T("SAIN 값은 보관됩니다. 전역 바닐라를 끄거나 이 봇을 SAIN으로 바꾸면 다시 편집·적용됩니다.", "Stored SAIN values resume when the vanilla override is removed."), _wrapSmall);
            return;
        }
        Color old = GUI.color;
        if (_view.Vanilla)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
        }
        GUILayout.Space(6f);
        LevelRow(level =>
        {
            BotLevels.Apply(level, group, src);
            ConfigEditingTracker.Add(bot.Name, level);
        });
        if (_view.AdjustedLine != null)
        {
            GUILayout.Label("<color=#facc15>" + _view.AdjustedLine + "</color>", _wrapSmall);
        }
        SlotsRow();
        TabbedContent();
        GUI.color = old;
    }

    // zzap (user 2026-11-04 "too many settings in one place - tabs, a ZZAP part per bot, personality right there"): quick knobs,
    // personality, ZZAP switches, per-map multipliers and the advanced numbers each on their own tab (one bot or a selection).
    private static void TabbedContent()
    {
        var groups = EditGroups().Where(g => g != null).ToList();
        BotTabsGUI.TabBar(Write, TabBadge);
        switch (BotTabsGUI.Current)
        {
            case BotTabsGUI.Tab.Quick:
                KnobHeading();
                foreach (string knobGroup in KnobGroups)
                {
                    var knobs = _view.Knobs.Where(k => k.Knob.Group == knobGroup && k.Knob.Id != PERSONALITY).ToList();
                    if (knobs.Count == 0) continue;
                    GUILayout.Space(8f);
                    GUILayout.Label(GroupName(knobGroup), ZzapUI.Section);
                    foreach (var knob in knobs) KnobRow(knob);
                }
                break;
            case BotTabsGUI.Tab.Personality:
                ZzapUI.StepHeading("P", T("성격 — 이 봇 종류에 정해 주기", "Personality — fix it for these bot types"),
                    T("지정 안 함 = 성격 확률대로 섞음", "Unassigned = mixed by the personality chances"));
                foreach (var knob in _view.Knobs.Where(k => k.Knob.Id == PERSONALITY)) KnobRow(knob);
                GUILayout.Label(T("성격마다 하는 행동(누가 다이아몬드 스텝·돌격을 하는지 등)은 '성격' 탭에서 바꿉니다. 난이도 4칸 모두에 적용됩니다.",
                    "What each personality does (who diamond-steps, pushes...) is on the Personalities tab. Applies to all four difficulty slots."), _wrapSmall);
                break;
            case BotTabsGUI.Tab.Zzap:
                BotTabsGUI.ZzapPanel(groups, Write, ConfigEditingTracker.Add);
                break;
            case BotTabsGUI.Tab.Maps:
                BotTabsGUI.MapsPanel(groups, Write, ConfigEditingTracker.Add);
                break;
            default:
                Advanced(false);
                break;
        }
    }

    private const string PERSONALITY = "personality";

    private static string TabBadge(BotTabsGUI.Tab tab)
    {
        var groups = EditGroups().Where(g => g != null).ToList();
        int n = tab switch
        {
            BotTabsGUI.Tab.Zzap => BotZzapSwitches.Changed(groups),
            BotTabsGUI.Tab.Maps => groups.SelectMany(BotMapModifiers.ChangedMaps).Distinct().Count(),
            _ => 0,
        };
        return n > 0 ? n.ToString() : null;
    }

    private static void KnobRow(KnobView view)
    {
        var knob = view.Knob;
        var group = _view.Group;
        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(LABEL_WIDTH));
        GUILayout.BeginHorizontal();
        GUILayout.Label(view.Label, ZzapUI.NameLine, GUILayout.Height(22f), GUILayout.ExpandWidth(false));
        ZzapUI.ChangeMark(view.CanReset);
        if (view.Mixed) GUILayout.Label(T("섞임", "mixed"), ZzapUI.Badge, GUILayout.ExpandWidth(false));
        GUILayout.EndHorizontal();
        GUILayout.Label(knob.Help(Korean), _wrapSmall);
        GUILayout.EndVertical();

        switch (knob.Kind)
        {
            case BotKnobKind.Toggle:
                GUILayout.FlexibleSpace();
                ToggleSwitch(view);
                break;
            case BotKnobKind.Personality:
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(_view.PoolText + "  ▾", ZzapUI.Button, GUILayout.Width(170f), GUILayout.Height(26f)))
                {
                    Write(() => _personalityOpen = !_personalityOpen);
                }
                break;
            default:
                if (knob.Kind == BotKnobKind.ToggleNumber)
                {
                    ToggleSwitch(view);
                }
                float position = ZzapUI.Slider(knob.ToSlider(view.Value), knob.Min, knob.Max, GUILayout.Height(24f), GUILayout.ExpandWidth(true));
                ZzapUI.DrawTick(GUILayoutUtility.GetLastRect(), Mathf.InverseLerp(knob.Min, knob.Max, knob.ToSlider(view.Reference)), ZzapUI.Yellow);
                // Only a moved slider writes: rounding an untouched value to the step would change presets on its own.
                if (Mathf.Abs(position - knob.ToSlider(view.Value)) > 0.0001f)
                {
                    float newValue = knob.Round(Mathf.Clamp(knob.FromSlider(position), knob.Min, knob.Max));
                    if (Mathf.Abs(newValue - view.Value) > 0.0001f)
                    {
                        Write(() =>
                        {
                            BotSelectionSettings.Apply(EditGroups(),knob,newValue);
                            ConfigEditingTracker.Add(knob.NameEn, newValue);
                        });
                    }
                }
                break;
        }

        GUILayout.BeginVertical(GUILayout.Width(KNOB_DELTA_WIDTH));
        GUILayout.Label(view.Mixed ? T("섞임", "mixed") : view.CurrentText, view.Mixed ? ZzapUI.Badge : ZzapUI.Value);
        GUILayout.Label(view.BaseText, ZzapUI.Count);
        GUILayout.EndVertical();
        GUI.enabled = view.CanReset;
        {
            if (GUILayout.Button(new GUIContent(T("원래대로", "Reset"), T("고른 난이도의 값으로", "Back to the chosen Level's value")), ZzapUI.Button, GUILayout.Width(RESET_WIDTH), GUILayout.Height(26f)))
            {
                var sources = _view.Src;
                Write(() =>
                {
                    BotSelectionSettings.Reset(EditGroups(),knob,g=>Sources(EditBots().First(b=>ReferenceEquals(Group(b),g))));
                    ConfigEditingTracker.Add(knob.NameEn, "reset");
                });
            }
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        if (knob.Kind == BotKnobKind.Personality && _personalityOpen)
        {
            PersonalityChoices(knob);
        }
        ZzapUI.Separator(2f);
    }

    private static void ToggleSwitch(KnobView view)
    {
        bool newOn = ZzapUI.Switch(view.On, 26f);
        if (newOn != view.On)
        {
            var knob = view.Knob;
            var group = _view.Group;
            Write(() =>
            {
                BotSelectionSettings.ApplyToggle(EditGroups(),knob,newOn);
                ConfigEditingTracker.Add(knob.NameEn, newOn);
            });
        }
    }

    private static string PoolLabel()
    {
        var pool = BotPersonalityPool.Candidates(_view.Group.WildSpawnType, BotLevels.NormalSlot(_view.Group)?.Mind);
        return pool.Count == 0 ? T("자동 배정", "Automatic") : string.Join(" / ", pool);
    }

    private static void PersonalityChoices(BotKnob knob)
    {
        GUILayout.Label(T("여러 개 선택하면 생성할 때 균등 무작위 · 한 개는 고정 · 전부 해제하면 자동", "Multiple = uniform random at spawn · one = fixed · none = automatic"), _wrapSmall);
        var pool = BotPersonalityPool.Candidates(_view.Group.WildSpawnType, BotLevels.NormalSlot(_view.Group)?.Mind);
        foreach (var personality in Personalities.Where(p => p != EPersonality.None))
        {
            bool selected = pool.Contains(personality);
            if (ZzapUI.ListItem((selected ? "☑ " : "☐ ") + PersonalityName(personality), null, selected, null, LABEL_WIDTH, 24f))
            {
                var captured = personality;
                Write(() =>
                {
                    var values = BotPersonalityPool.Candidates(_view.Group.WildSpawnType, BotLevels.NormalSlot(_view.Group)?.Mind).ToList();
                    if (!values.Remove(captured)) values.Add(captured);
                    foreach(var target in EditGroups()) BotPersonalityPool.Set(target, values);
                    ConfigEditingTracker.Add(knob.NameEn, string.Join(",", values));
                });
            }
        }
    }

    // ---------------------------------------------------------------- advanced: every number, per-difficulty editing

    private static string _advCategory; // null = every category (advanced tab: one category at a time is easier to read)

    private static void Advanced(bool foldable = true)
    {
        if (foldable)
        {
            if (ZzapUI.FoldHeading("03", _view.AdvancedTitle, _view.NewCount, _advancedOpen))
            {
                Write(() => _advancedOpen = !_advancedOpen);
            }
            if (!_advancedOpen)
            {
                return;
            }
        }
        else
        {
            ZzapUI.StepHeading("03", _view.AdvancedTitle, _view.NewCount);
        }
        GUILayout.BeginVertical(ZzapUI.Frame);
        GUILayout.Label(T("같은 숫자를 편집 대상 전체에 적용합니다. 각 항목의 원래대로는 봇마다 원래 값을 되살립니다.", "Numbers apply to all bots being edited. Reset restores each bot’s original value for that setting."), _wrapSmall);
        DrawFilters();
        GUILayout.BeginHorizontal();
        GUILayout.Label(T("난이도별로 따로 바꾸기", "Per-difficulty editing"), ZzapUI.Small, GUILayout.Width(140f), GUILayout.Height(28f));
        foreach (var (scope, text, seen) in _view.Pills)
        {
            bool on = _advScope == scope;
            Color old = GUI.color;
            if (!seen && !on)
            {
                GUI.color = new Color(old.r, old.g, old.b, old.a * 0.55f);
            }
            if (GUILayout.Button(text, on ? ZzapUI.Pill : ZzapUI.Button, GUILayout.Height(28f)))
            {
                var captured = scope;
                Write(() => _advScope = captured);
            }
            GUI.color = old;
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Label(_view.RecordLine, _wrapSmall);
        if (_view.SlotsLine != null)
        {
            GUILayout.Label(_view.SlotsLine, _wrapSmall);
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label(T("묶음", "Group"), ZzapUI.Small, GUILayout.Width(60f), GUILayout.Height(28f));
        int chips = 0;
        foreach (var head in _view.Rows.Where(r => r.Heading != null).Select(r => (r.Row.Group, r.Heading)).Prepend((null, T("전부", "All"))))
        {
            if (GUILayout.Button(head.Heading, _advCategory == head.Group ? ZzapUI.Pill : ZzapUI.Button, GUILayout.Height(26f), GUILayout.ExpandWidth(false)))
            {
                string picked = head.Group;
                Write(() => _advCategory = picked);
            }
            if (++chips % 8 == 0)
            {
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Space(64f);
            }
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        _entryConfig ??= new GUIEntryConfig();
        foreach (var view in _view.Rows)
        {
            if (_advCategory != null && view.Row.Group != _advCategory)
            {
                continue;
            }
            if (view.Heading != null)
            {
                GUILayout.Space(6f);
                GUILayout.Label(view.Heading, ZzapUI.Section);
            }
            var row = view.Row;
            object owner = row.Owner(_view.Slot);
            if (owner == null)
            {
                continue;
            }
            object value = row.Field.GetValue(owner);
            GUILayout.BeginHorizontal();
            GUILayout.Label(view.Label, ZzapUI.Name, GUILayout.Width(ZzapUI.NAME_WIDTH), GUILayout.MinHeight(AttributesGUI.RowHeight));
            ZzapUI.ChangeMark(view.Changed, AttributesGUI.RowHeight);
            if (view.New) GUILayout.Label("NEW", ZzapUI.NewTag, GUILayout.ExpandWidth(false));
            if (view.Mixed) GUILayout.Label(T("섞임", "mixed"), ZzapUI.Badge, GUILayout.ExpandWidth(false));
            object before = value;
            value = AttributesGUI.EditFloatBoolInt(ref value, owner, view.Info, _entryConfig, 0, out bool edited, false, false, showDefaultReset: false);
            if (view.RefText != null)
            {
                GUILayout.Label(view.RefText, ZzapUI.Small, GUILayout.Width(90f), GUILayout.Height(AttributesGUI.RowHeight));
            }
            var group = _view.Group;
            var scope = _advScope;
            GUI.enabled = view.Changed;
            {
                if (GUILayout.Button(new GUIContent(T("원래대로", "Reset"), T("zzap 값으로", "Back to the zzap original")), ZzapUI.Button, GUILayout.Width(RESET_WIDTH), GUILayout.Height(AttributesGUI.RowHeight)))
                {
                    var reference = _view.Reference;
                    Write(() =>
                    {
                        foreach(var target in EditGroups()) BotAdvancedEdit.Reset(target,BotSelectionSettings.Baseline(target,Sources(EditBots().First(b=>ReferenceEquals(Group(b),target)))),scope,row);
                        ConfigEditingTracker.Add(view.Info.Name, "reset");
                    });
                }
            }
            GUI.enabled = true;
            if (edited && !Equals(before, value))
            {
                var shown = _view.Shown;
                object newValue = value;
                Write(() =>
                {
                    if(IsBatch) BotSelectionSettings.ApplyField(EditGroups(),row,scope,newValue);
                    else BotAdvancedEdit.Apply(group, scope, shown, row, newValue);
                    ConfigEditingTracker.Add(view.Info.Name, newValue);
                });
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(view.Info.Description, _wrapSmall);
            ZzapUI.Separator(4f);
        }
        GUILayout.EndVertical();
    }

    private static void DrawFilters()
    {
        GUILayout.BeginHorizontal();
        bool next = ZzapUI.Switch(_newOnly, 26f, showState: false);
        if (next != _newOnly) Write(() => _newOnly = next);
        GUILayout.Label(T("새 항목만 보기", "New only"), ZzapUI.Small, GUILayout.ExpandWidth(false));
        bool changed = ZzapUI.Switch(_changedOnly, 26f, showState: false);
        if (changed != _changedOnly) Write(() => _changedOnly = changed);
        GUILayout.Label(T("바뀐 항목만 보기", "Changed only"), ZzapUI.Small, GUILayout.ExpandWidth(false));
        GUILayout.Label(T("기준: zzap 원본 · 두 필터는 함께 적용됩니다", "Compared with zzap original · both filters apply together"), ZzapUI.Count);
        GUILayout.EndHorizontal();
    }

    private static void KnobHeading() => ZzapUI.StepHeading("02", _view.KnobHeading,
        T("zzap 원본　<color=#38bdf8>오른쪽일수록 강하게 →</color>", "zzap original　<color=#38bdf8>Stronger to the right →</color>"), referenceMark: true);

    // ---------------------------------------------------------------- the explicit selection

    private static void SectionPanel()
    {
        string section = T("선택 묶음", "Selection");
        var bots = Targets();
        GUILayout.BeginHorizontal();
        GUILayout.Label(_view.TargetTitle, ZzapUI.Title, GUILayout.ExpandWidth(false));
        GUILayout.Label(T("함께 편집", "Together"), ZzapUI.Tag, GUILayout.ExpandWidth(false));
        GUILayout.FlexibleSpace();
        HeaderButtons(
            () =>
            {
                foreach (var bot in bots.Where(b => Ai(b).CanEditSain))
                {
                    BotLevels.RestoreSaved(Group(bot), _saved.TryGetValue(bot, out var saved) ? saved : null);
                }
                ConfigEditingTracker.Add(section, "revert");
                _view.AllBadgesStale = true;
            },
            () =>
            {
                foreach (var bot in bots.Where(b => Ai(b).CanEditSain))
                {
                    BotLevels.ResetToOriginal(Group(bot), Sources(bot));
                }
                ConfigEditingTracker.Add(section, "original");
                _view.AllBadgesStale = true;
            },
            bots.Any(b => _saved.ContainsKey(b)), bots.Any(b => Ai(b).CanEditSain));
        GUI.enabled = false;
        GUILayout.Button(T("다른 봇에 복사", "Copy to other bots"), ZzapUI.Button, GUILayout.Height(28f));
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        GUILayout.Label(_view.TargetLine, _wrapSmall);

        AiRow(_view.SectionVanilla, bots.Any(b => Ai(b).CanChooseAI) ? null : T("전부 바닐라로 잠겨 있습니다", "All bots have a vanilla override"), vanilla =>
        {
            foreach (var bot in bots)
            {
                var group = Group(bot);
                if (group != null && Ai(bot).CanChooseAI)
                {
                    group.UseVanillaAI = vanilla;
                }
            }
            ConfigEditingTracker.Add(section, vanilla ? "vanilla" : "SAIN");
            _view.AllBadgesStale = true;
        });
        GUILayout.Space(6f);
        LevelRow(level =>
        {
            BotSelectionSettings.ApplyLevel(EditGroups(),level,g=>Sources(EditBots().First(b=>ReferenceEquals(Group(b),g))));
            ConfigEditingTracker.Add(section, level);
            _view.AllBadgesStale = true;
        });
        SlotsRow();
        if(EditBots().Any())
        {
            TabbedContent();
        }

    }

    // ---------------------------------------------------------------- copy to other bots

    private static void CopyPanel()
    {
        ZzapUI.PanelHeading(T("복사할 봇 고르기", "Pick bots to copy to"),
            Korean ? $"{_selected.Name}의 난이도 4칸 값을 고른 봇에 덮어씁니다(성격 지정 포함)." : $"Overwrites the picked bots' four difficulty slots with {_selected.Name}'s (personality override included).");
        foreach (string section in Sections)
        {
            var bots = BotsBySection[section];
            if (bots.Count == 0 || (bots.Count == 1 && bots[0] == _selected))
            {
                continue;
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label(section, ZzapUI.Section);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(T("전부 선택", "Select all"), ZzapUI.Button, GUILayout.Height(24f)))
            {
                Write(() =>
                {
                    foreach (var b in bots)
                    {
                        if (b != _selected && Ai(b).CanEditSain)
                        {
                            _copyTargets.Add(b);
                        }
                    }
                });
            }
            GUILayout.EndHorizontal();
            foreach (var bot in bots)
            {
                if (bot == _selected || !Ai(bot).CanEditSain)
                {
                    continue;
                }
                var captured = bot;
                bool picked = _copyTargets.Contains(bot);
                if (ZzapUI.ListItem((picked ? "☑  " : "☐  ") + bot.Name, null, picked, null, 12f, 26f))
                {
                    Write(() =>
                    {
                        if (!_copyTargets.Remove(captured))
                        {
                            _copyTargets.Add(captured);
                        }
                    });
                }
            }
        }
        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(T("복사", "Copy") + $" ({_copyTargets.Count})", ZzapUI.ButtonPrimary, GUILayout.Height(30f), GUILayout.Width(140f)) && _copyTargets.Count > 0)
        {
            var source = _view.Group;
            Write(() =>
            {
                int n = CopyTo(source, _copyTargets.ToList());
                ConfigEditingTracker.Add("Copy bot settings", n);
                _copyOpen = false;
                _view.AllBadgesStale = true;
                Sounds.PlaySound(EUISoundType.ButtonBottomBarClick);
            });
        }
        if (GUILayout.Button(T("취소", "Cancel"), ZzapUI.Button, GUILayout.Height(30f), GUILayout.Width(100f)))
        {
            Write(() => _copyOpen = false);
        }
        GUILayout.EndHorizontal();
    }

    private static int CopyTo(SAINSettingsGroupClass source, List<BotType> targets)
    {
        int copied = 0;
        foreach (var bot in targets)
        {
            var target = Group(bot);
            if (!Ai(bot).CanEditSain || target?.Settings == null || ReferenceEquals(target, source))
            {
                continue;
            }
            foreach (var pair in source.Settings)
            {
                if (pair.Value != null && target.Settings.ContainsKey(pair.Key))
                {
                    target.Settings[pair.Key] = JsonConvert.DeserializeObject<SAINSettingsClass>(JsonConvert.SerializeObject(pair.Value));
                }
            }
            target.DifficultyModifier = source.DifficultyModifier;
            target.DifficultyLevel = source.DifficultyLevel;
            copied++;
        }
        return copied;
    }
}
