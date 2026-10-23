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
/// Clicking a faction name edits every bot in it at once (level, AI, revert, original); the arrow folds it.
/// Everything drawn comes from a view rebuilt on Layout events only and every write goes through ZzapUI.Defer, so a value
/// that flips a row can't add a control between Layout and Repaint (v1 final review).
/// </summary>
public static class BotSelectionClass
{
    private const float KNOB_DELTA_WIDTH = 330f;
    private const float RESET_WIDTH = 74f;
    private const float LABEL_WIDTH = 220f;

    private static readonly ESainBotDifficulty[] Difficulties = { ESainBotDifficulty.easy, ESainBotDifficulty.normal, ESainBotDifficulty.hard, ESainBotDifficulty.impossible };
    private static readonly EBotLevel[] Levels = { EBotLevel.Easy, EBotLevel.Normal, EBotLevel.Hard, EBotLevel.Impossible };
    private static readonly string[] KnobGroups = { "aim", "sense", "mind" };
    private static readonly EPersonality[] Personalities = (EPersonality[])Enum.GetValues(typeof(EPersonality));

    public static readonly string[] Sections = BotTypeDefinitions.BotTypes.Values.Select(b => b.Section).Distinct().ToArray();
    private static readonly Dictionary<string, List<BotType>> BotsBySection = Sections.ToDictionary(s => s, s => BotTypeDefinitions.BotTypes.Values.Where(b => b.Section == s).ToList());

    private static BotType _selected;
    private static string _selectedSection; // a faction is selected instead of a bot
    private static readonly HashSet<string> _folded = new();
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
    private static GUIStyle _levelButton;
    private static GUIStyle _levelButtonOn;

    // Spawn record: read in the background each time the page is opened again (a raid may have ended since).
    private static volatile List<SpawnRaidCounts> _loaded;
    private static int _lastMenuFrame = -10;

    // "Revert" copies, taken when the page opens and again after a save.
    private static readonly Dictionary<BotType, SAINSettingsGroupClass> _saved = new();
    private static bool _lastUnsaved;

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
        if (Time.frameCount - _lastMenuFrame > 2 || (_lastUnsaved && !unsaved))
        {
            // (Re)opened, or just saved: fetch the record again, take fresh revert copies, recheck every badge.
            Task.Run(() => _loaded = SpawnDifficultyRecorder.Load());
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
        GUILayout.BeginVertical(ZzapUI.Panel1, GUILayout.Height(ZzapUI.PANEL_HEIGHT), GUILayout.ExpandWidth(true));
        _rightScroll = GUILayout.BeginScrollView(_rightScroll, GUILayout.ExpandHeight(true));
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
        if (_levelButton == null)
        {
            _levelButton = new GUIStyle(ZzapUI.Button) { richText = true, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            _levelButtonOn = new GUIStyle(ZzapUI.Pill) { richText = true, wordWrap = true, alignment = TextAnchor.MiddleCenter };
        }
    }

    /// <summary>Queue a write for after this GUI pass and rebuild the view on the next Layout.</summary>
    private static void Write(Action action)
    {
        ZzapUI.Defer(() =>
        {
            action();
            _dirty = true;
        });
    }

    // ---------------------------------------------------------------- data

    private static SAINSettingsGroupClass Group(BotType bot)
    {
        var all = SAINPlugin.LoadedPreset?.BotSettings?.SAINSettings;
        return bot != null && all != null && all.TryGetValue(bot.WildSpawnType, out var group) ? group : null;
    }

    private static SAINPresetBundle Bundle(string name)
    {
        return PresetSync.TryGetCustomBundle(name, out var bundle) ? bundle : null;
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
        foreach (var bundle in PresetSync.ServerDefaults())
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
            Current = LevelGlobals.From(SAINPlugin.LoadedPreset?.GlobalSettings),
            Easy = LevelGlobals.From(easy),
            Normal = LevelGlobals.From(normal),
            Hard = LevelGlobals.From(hard),
            EasyBot = BotIn(easy, bot),
            NormalBot = BotIn(normal, bot),
            HardBot = BotIn(hard, bot),
        };
    }

    private static void TakeSnapshots()
    {
        _saved.Clear();
        foreach (var bot in BotTypeDefinitions.BotTypes.Values)
        {
            var copy = BotLevels.Snapshot(Group(bot));
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
        public bool CanReset;
    }

    private sealed class RowView
    {
        public BotFieldRow Row;
        public ConfigInfoClass Info;
        public string Heading;
        public GUIContent Label;
        public string RefText;
        public bool Changed;
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
        public bool Vanilla;
        public string VanillaLock;
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

        private List<SpawnRaidCounts> _record;
        private bool _korean;
        private BotType _bot;
        private string _section;
        private ESainBotDifficulty? _scope;
        private readonly Dictionary<BotFieldRow, ConfigInfoClass> _infos = new();

        public void Update()
        {
            var record = _loaded;
            bool languageChanged = _korean != Korean;
            if (ReferenceEquals(record, _record) && !languageChanged && !_dirty && !AllBadgesStale && _bot == _selected && _section == _selectedSection && _scope == _advScope)
            {
                return;
            }
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
                    Badges[bot] = BadgeText(Group(bot), Sources(bot));
                }
            }
            SectionCounts.Clear();
            foreach (var section in Sections)
            {
                SectionCounts[section] = (Korean ? $"{BotsBySection[section].Count}종" : $"{BotsBySection[section].Count}") + (_folded.Contains(section) ? T(" · 접힘", " · folded") : string.Empty);
            }

            Knobs.Clear();
            Rows.Clear();
            Pills.Clear();
            LevelButtons.Clear();
            SectionRows.Clear();
            SectionName = _section;
            if (_section != null)
            {
                UpdateSection();
                return;
            }
            Group = Group(_bot);
            if (Group == null)
            {
                return;
            }
            Src = Sources(_bot);
            Reference = Src.Reference;
            Badges[_bot] = Badge = BadgeText(Group, Src);
            Vanilla = Group.UseVanillaAI;
            VanillaLock = BigBrainHandler.BrainAssignment.VanillaByGroupSetting(_bot.WildSpawnType, out string setting)
                ? (Korean ? $"전역 설정 '{EditorText.Setting(setting)}'이 켜져 있어 이 봇은 바닐라로 고정됩니다" : $"Global setting '{setting}' is on - this bot stays vanilla")
                : null;
            CanRevert = _saved.ContainsKey(_bot);
            var shownLevel = Group.DifficultyLevel == EBotLevel.None ? EBotLevel.Hard : Group.DifficultyLevel;
            LevelValues = BotLevels.Compute(shownLevel, Group, Src);
            foreach (var level in Levels)
            {
                bool on = Group.DifficultyLevel == level;
                bool available = BotLevels.Available(level, Src);
                string text = $"<b>{LevelName(level)}</b>\n<size=11>{LevelHint(level)}{(on ? T(" · 지금", " · now") : string.Empty)}</size>";
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
            ReferenceLine = Reference != null ? T("기준: zzap 프리셋", "Reference: zzap preset") : T("기준 없음 (zzap 프리셋에 이 봇이 없음)", "No reference (not in the zzap preset)");
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
                bool changed = affected && BotLevels.KnobDiffers(knob, Group, LevelValues);
                var view = new KnobView
                {
                    Knob = knob,
                    Label = new GUIContent("<b>" + knob.Name(Korean) + "</b>" + (changed ? "  <color=#facc15>●</color>" : string.Empty), knob.Help(Korean)),
                    CanReset = changed,
                    Value = knob.Get(normalSlot),
                    On = knob.GetOn(normalSlot),
                    Personality = knob.GetPersonality(normalSlot),
                };
                view.Reference = affected && LevelValues != null ? knob.Get(LevelValues) : view.Value;
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
                Knobs.Add(view);
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
                bool changed = BotAdvancedEdit.Changed(Group, Reference, _scope, row);
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
                    Label = new GUIContent(name + (changed ? "  <color=#facc15>●</color>" : string.Empty), info.Description),
                    RefText = refSlot != null ? $"<color=#71717a>{T("기준", "ref")} {row.Get(refSlot)}</color>" : null,
                    Changed = changed && Reference != null,
                });
            }
            AdvancedTitle = Korean
                ? $"고급 — 숫자 전부 보기 · 난이도별로 따로 바꾸기 ({Rows.Count}개 · zzap과 다른 값 {changedCount}개)"
                : $"Advanced - every number · per-difficulty editing ({Rows.Count} · {changedCount} differ from zzap)";
        }

        private void UpdateSection()
        {
            Group = null;
            var bots = BotsBySection.TryGetValue(_section, out var list) ? list : new List<BotType>();
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
                if (group.UseVanillaAI)
                {
                    anyVanilla = true;
                }
                else
                {
                    anySain = true;
                }
                SectionRows.Add(new SectionRow { Name = bot.Name, Badge = BadgeText(group, src), Ai = group.UseVanillaAI ? T("바닐라", "vanilla") : "SAIN" });
            }
            SectionVanilla = anyVanilla && anySain ? null : anyVanilla;
            foreach (var level in Levels)
            {
                counts.TryGetValue(level, out int n);
                string text = $"<b>{LevelName(level)}</b>\n<size=11>{LevelHint(level)}</size>\n<size=11>{(Korean ? $"{n}종" : $"{n}")}</size>";
                LevelButtons.Add(new LevelView { Level = level, Content = new GUIContent(text), On = false, Available = true });
            }
        }
    }

    // ---------------------------------------------------------------- left: bot list

    private static void BotList()
    {
        GUILayout.BeginVertical(ZzapUI.Panel1, GUILayout.Width(ZzapUI.SIDE_WIDTH), GUILayout.Height(ZzapUI.PANEL_HEIGHT));
        _search = ZzapUI.Search(_search, T("봇 이름 검색", "Search bot types"));
        GUILayout.Space(6f);
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
            if (GUILayout.Button(folded ? "▸" : "▾", ZzapUI.Button, GUILayout.Width(26f), GUILayout.Height(28f)))
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
            if (ZzapUI.ListItem("<b>" + section + "</b>", count, _selectedSection == section, T("누르면 이 팩션 봇 전체를 한꺼번에", "Click to edit every bot in this faction"), 0f, 28f))
            {
                string captured = section;
                Write(() =>
                {
                    _selectedSection = captured;
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
                if (ZzapUI.ListItem(bot.Name, badge, _selectedSection == null && bot == _selected, bot.Description, 26f, 28f))
                {
                    Write(() =>
                    {
                        _selected = captured;
                        _selectedSection = null;
                        _copyOpen = false;
                        _personalityOpen = false;
                        _rightScroll = Vector2.zero;
                    });
                }
            }
        }
        GUILayout.EndScrollView();
        GUILayout.Label(T("이름 옆 = 이 봇의 난이도 · (조정됨) = 고른 뒤 더 만짐 · 직접 설정 = 난이도를 고른 적 없고 zzap과 다름", "Next to the name = the bot's level · (adjusted) = changed since · custom = never picked, differs from zzap"), _wrapSmall);
        GUILayout.EndVertical();
    }

    // ---------------------------------------------------------------- right: one bot

    private static void HeaderButtons(Action revert, Action original, bool canRevert)
    {
        GUI.enabled = canRevert;
        if (GUILayout.Button(new GUIContent(T("되돌리기", "Revert"), T("마지막으로 저장한 값으로 (저장 안 한 변경 취소)", "Back to the last saved values (drops unsaved changes)")), ZzapUI.Button, GUILayout.Height(28f)))
        {
            Write(revert);
        }
        GUI.enabled = true;
        if (GUILayout.Button(new GUIContent(T("원본 사용", "Use original"), T("zzap 프리셋의 원래 값으로, 난이도 선택도 지움 (AI는 SAIN으로)", "The zzap preset's values, no level picked, SAIN AI")), ZzapUI.Button, GUILayout.Height(28f)))
        {
            Write(original);
        }
    }

    private static void AiRow(bool? vanilla, string lockReason, Action<bool> set)
    {
        GUILayout.BeginHorizontal(ZzapUI.Panel1);
        GUILayout.Label("<b>" + T("이 봇의 AI", "This bot's AI") + "</b>", ZzapUI.Name, GUILayout.Width(110f), GUILayout.Height(28f));
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
        string help = lockReason ?? (vanilla == null ? T("섞임 · 누르면 전부 그 AI로", "mixed · click to set all") : T("게임 재시작 후 적용", "Applies after a game restart"));
        GUILayout.Label(help, _wrapSmall, GUILayout.Height(28f));
        GUILayout.EndHorizontal();
    }

    private static void LevelRow(Action<EBotLevel> pick)
    {
        GUILayout.BeginHorizontal();
        foreach (var lv in _view.LevelButtons)
        {
            GUI.enabled = lv.Available;
            if (GUILayout.Button(lv.Content, lv.On ? _levelButtonOn : _levelButton, GUILayout.Height(52f), GUILayout.ExpandWidth(true)))
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
        GUILayout.Label("<b>" + bot.Name + "</b>   <size=12><color=#a1a1aa>" + _view.Badge + "</color></size>", ZzapUI.Title);
        GUILayout.FlexibleSpace();
        HeaderButtons(
            () =>
            {
                BotLevels.RestoreSaved(group, _saved.TryGetValue(bot, out var saved) ? saved : null);
                ConfigEditingTracker.Add(bot.Name, "revert");
                _view.AllBadgesStale = true;
            },
            () =>
            {
                BotLevels.ResetToOriginal(group, src);
                ConfigEditingTracker.Add(bot.Name, "original");
            },
            _view.CanRevert);
        if (GUILayout.Button(T("다른 봇에 복사", "Copy to other bots"), ZzapUI.Button, GUILayout.Height(28f)))
        {
            Write(() =>
            {
                _copyOpen = true;
                _copyTargets.Clear();
            });
        }
        GUILayout.EndHorizontal();
        GUILayout.Label(T("난이도를 고르면 아래 숫자가 그 단계 값으로 바뀝니다 · ", "Picking a level writes that level's values below · ") + _view.ReferenceLine, _wrapSmall);

        AiRow(_view.Vanilla, _view.VanillaLock, vanilla =>
        {
            group.UseVanillaAI = vanilla;
            ConfigEditingTracker.Add(bot.Name, vanilla ? "vanilla" : "SAIN");
        });
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
        GUILayout.Label(T("행동(문 전술 등)은 성격과 전역 설정을 따릅니다.", "Behaviour (door tactics etc.) follows the personality and global settings."), _wrapSmall);
        ZzapUI.Separator(4f);

        foreach (string knobGroup in KnobGroups)
        {
            GUILayout.Space(8f);
            GUILayout.Label(GroupName(knobGroup), ZzapUI.Section);
            foreach (var knob in _view.Knobs)
            {
                if (knob.Knob.Group == knobGroup)
                {
                    KnobRow(knob);
                }
            }
        }

        GUILayout.Space(10f);
        Advanced();
        GUI.color = old;
    }

    private static void KnobRow(KnobView view)
    {
        var knob = view.Knob;
        var group = _view.Group;
        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(LABEL_WIDTH));
        GUILayout.Label(view.Label, ZzapUI.Name, GUILayout.Height(22f));
        GUILayout.Label(knob.Help(Korean), _wrapSmall);
        GUILayout.EndVertical();

        switch (knob.Kind)
        {
            case BotKnobKind.Toggle:
                GUILayout.FlexibleSpace();
                GUILayout.Label(view.Note, ZzapUI.Small, GUILayout.Height(26f));
                ToggleSwitch(view);
                break;
            case BotKnobKind.Personality:
                GUILayout.FlexibleSpace();
                GUILayout.Label(view.Note, ZzapUI.Small, GUILayout.Height(26f));
                if (GUILayout.Button(PersonalityName(view.Personality) + "  ▾", ZzapUI.Button, GUILayout.Width(170f), GUILayout.Height(26f)))
                {
                    Write(() => _personalityOpen = !_personalityOpen);
                }
                break;
            default:
                if (knob.Kind == BotKnobKind.ToggleNumber)
                {
                    ToggleSwitch(view);
                }
                float position = ZzapUI.DiffSlider(knob.ToSlider(view.Value), knob.Min, knob.Max, knob.ToSlider(view.Reference), view.Direction, GUILayout.Height(24f), GUILayout.ExpandWidth(true));
                GUILayout.Label(view.Delta, ZzapUI.Small, GUILayout.Width(KNOB_DELTA_WIDTH), GUILayout.Height(24f));
                // Only a moved slider writes: rounding an untouched value to the step would change presets on its own.
                if (Mathf.Abs(position - knob.ToSlider(view.Value)) > 0.0001f)
                {
                    float newValue = knob.Round(Mathf.Clamp(knob.FromSlider(position), knob.Min, knob.Max));
                    if (Mathf.Abs(newValue - view.Value) > 0.0001f)
                    {
                        Write(() =>
                        {
                            BotQuickSettings.Apply(knob, group, null, ESainBotDifficulty.normal, newValue);
                            ConfigEditingTracker.Add(knob.NameEn, newValue);
                        });
                    }
                }
                break;
        }

        if (view.CanReset)
        {
            if (GUILayout.Button(new GUIContent(T("되돌리기", "Reset"), T("고른 난이도의 값으로", "Back to the picked level's value")), ZzapUI.Button, GUILayout.Width(RESET_WIDTH), GUILayout.Height(26f)))
            {
                var values = _view.LevelValues;
                Write(() =>
                {
                    BotLevels.ResetKnob(knob, group, values);
                    ConfigEditingTracker.Add(knob.NameEn, "reset");
                });
            }
        }
        else
        {
            GUILayout.Space(RESET_WIDTH + 4f);
        }
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
                BotQuickSettings.ApplyToggle(knob, group, null, newOn);
                ConfigEditingTracker.Add(knob.NameEn, newOn);
            });
        }
    }

    private static void PersonalityChoices(BotKnob knob)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(LABEL_WIDTH);
        foreach (EPersonality personality in Personalities)
        {
            var captured = personality;
            if (GUILayout.Button(PersonalityName(personality), ZzapUI.Button, GUILayout.Height(26f)))
            {
                var group = _view.Group;
                Write(() =>
                {
                    BotQuickSettings.ApplyPersonality(knob, group, null, captured);
                    ConfigEditingTracker.Add(knob.NameEn, captured);
                    _personalityOpen = false;
                });
            }
        }
        GUILayout.EndHorizontal();
    }

    // ---------------------------------------------------------------- advanced: every number, per-difficulty editing

    private static void Advanced()
    {
        if (ZzapUI.ListItem((_advancedOpen ? "▾  " : "▸  ") + _view.AdvancedTitle, null, false))
        {
            Write(() => _advancedOpen = !_advancedOpen);
        }
        if (!_advancedOpen)
        {
            return;
        }
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

        _entryConfig ??= new GUIEntryConfig();
        foreach (var view in _view.Rows)
        {
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
            object before = value;
            value = AttributesGUI.EditFloatBoolInt(ref value, owner, view.Info, _entryConfig, 0, out bool edited, false, false, showDefaultReset: false);
            if (view.RefText != null)
            {
                GUILayout.Label(view.RefText, ZzapUI.Small, GUILayout.Width(90f), GUILayout.Height(AttributesGUI.RowHeight));
            }
            var group = _view.Group;
            var scope = _advScope;
            if (view.Changed)
            {
                if (GUILayout.Button(new GUIContent(T("되돌리기", "Reset"), T("zzap 값으로", "Back to zzap's value")), ZzapUI.Button, GUILayout.Width(RESET_WIDTH), GUILayout.Height(AttributesGUI.RowHeight)))
                {
                    var reference = _view.Reference;
                    Write(() =>
                    {
                        BotAdvancedEdit.Reset(group, reference, scope, row);
                        ConfigEditingTracker.Add(view.Info.Name, "reset");
                    });
                }
            }
            else
            {
                GUILayout.Space(RESET_WIDTH + 4f);
            }
            if (edited && !Equals(before, value))
            {
                var shown = _view.Shown;
                object newValue = value;
                Write(() =>
                {
                    BotAdvancedEdit.Apply(group, scope, shown, row, newValue);
                    ConfigEditingTracker.Add(view.Info.Name, newValue);
                });
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(view.Info.Description, _wrapSmall);
        }
    }

    // ---------------------------------------------------------------- a whole faction

    private static void SectionPanel()
    {
        string section = _view.SectionName;
        var bots = BotsBySection.TryGetValue(section, out var list) ? list : new List<BotType>();
        GUILayout.BeginHorizontal();
        GUILayout.Label("<b>" + (Korean ? $"{section} 전체 ({bots.Count}종)" : $"All of {section} ({bots.Count})") + "</b>", ZzapUI.Title);
        GUILayout.FlexibleSpace();
        HeaderButtons(
            () =>
            {
                foreach (var bot in bots)
                {
                    BotLevels.RestoreSaved(Group(bot), _saved.TryGetValue(bot, out var saved) ? saved : null);
                }
                ConfigEditingTracker.Add(section, "revert");
                _view.AllBadgesStale = true;
            },
            () =>
            {
                foreach (var bot in bots)
                {
                    BotLevels.ResetToOriginal(Group(bot), Sources(bot));
                }
                ConfigEditingTracker.Add(section, "original");
                _view.AllBadgesStale = true;
            },
            true);
        GUILayout.EndHorizontal();
        GUILayout.Label(T("여기서 바꾸면 이 팩션 봇 모두에 한꺼번에 적용됩니다.", "Changes here apply to every bot in this faction."), _wrapSmall);

        AiRow(_view.SectionVanilla, null, vanilla =>
        {
            foreach (var bot in bots)
            {
                var group = Group(bot);
                if (group != null && !BigBrainHandler.BrainAssignment.VanillaByGroupSetting(bot.WildSpawnType, out _))
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
            foreach (var bot in bots)
            {
                BotLevels.Apply(level, Group(bot), Sources(bot));
            }
            ConfigEditingTracker.Add(section, level);
            _view.AllBadgesStale = true;
        });
        GUILayout.Label(T("버튼 아래 = 어느 zzap 프리셋 수준인지 · 지금 그 난이도인 봇 수", "Under each button = which zzap preset level · bots at that level now"), _wrapSmall);
        GUILayout.Label(T("행동(문 전술 등)은 성격과 전역 설정을 따릅니다. 손잡이(조준 속도 등)는 봇을 하나 골라서.", "Behaviour follows the personality and global settings. Knobs: pick one bot."), _wrapSmall);
        ZzapUI.Separator(4f);
        foreach (var row in _view.SectionRows)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(row.Name, ZzapUI.Name, GUILayout.Width(LABEL_WIDTH));
            GUILayout.Label(row.Badge, ZzapUI.Small, GUILayout.Width(160f));
            GUILayout.Label(row.Ai, ZzapUI.Small);
            GUILayout.EndHorizontal();
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
                        if (b != _selected)
                        {
                            _copyTargets.Add(b);
                        }
                    }
                });
            }
            GUILayout.EndHorizontal();
            foreach (var bot in bots)
            {
                if (bot == _selected)
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
            if (target?.Settings == null || ReferenceEquals(target, source))
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
