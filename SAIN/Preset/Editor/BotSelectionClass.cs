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
/// zzap (2026-10-23, bot type settings redesign, user-approved mockup A): one bot type at a time - the difficulty it really
/// spawned at in the last raids, nine plain knobs that apply to all four difficulty slots by default, and every number folded
/// under "Advanced". Replaces the three-column page (bot types x difficulties x groups) where values multiplied per pick and
/// nothing said which difficulty slot a bot actually uses (user: "can't tell whether a setting took").
/// Knob math, advanced edits, the spawn record and the field list live in SAIN.Preset.Shared, shared with the web page.
/// Everything drawn comes from a view rebuilt on Layout events only and every write goes through ZzapUI.Defer, so a value
/// that flips a row to "changed" can't add a control between Layout and Repaint (final review 10/23).
/// </summary>
public static class BotSelectionClass
{
    private const float KNOB_DELTA_WIDTH = 330f;
    private const float RESET_WIDTH = 74f;
    private const float LABEL_WIDTH = 220f;

    private static readonly ESainBotDifficulty[] Difficulties = { ESainBotDifficulty.easy, ESainBotDifficulty.normal, ESainBotDifficulty.hard, ESainBotDifficulty.impossible };
    private static readonly string[] KnobGroups = { "aim", "sense", "mind" };
    private static readonly EPersonality[] Personalities = (EPersonality[])Enum.GetValues(typeof(EPersonality));

    public static readonly string[] Sections = BotTypeDefinitions.BotTypes.Values.Select(b => b.Section).Distinct().ToArray();
    private static readonly Dictionary<string, List<BotType>> BotsBySection = Sections.ToDictionary(s => s, s => BotTypeDefinitions.BotTypes.Values.Where(b => b.Section == s).ToList());

    private static BotType _selected;
    private static ESainBotDifficulty? _scope; // null = all four slots
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
        _wrapSmall ??= new GUIStyle(ZzapUI.Small) { wordWrap = true };
        if (Time.frameCount - _lastMenuFrame > 2)
        {
            // The page was not drawn last frames: it was (re)opened - fetch the record again and recheck every bot's dot.
            Task.Run(() => _loaded = SpawnDifficultyRecorder.Load());
            _view.AllDotsStale = true;
            _dirty = true;
        }
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
        if (_view.Group != null)
        {
            _rightScroll = GUILayout.BeginScrollView(_rightScroll, GUILayout.ExpandHeight(true));
            if (_copyOpen)
            {
                CopyPanel();
            }
            else
            {
                BotPanel();
            }
            GUILayout.EndScrollView();
        }
        else
        {
            ZzapUI.PanelHeading(T("봇 종류를 고르세요", "Pick a bot type"), T("이 프리셋에 이 봇 종류가 없습니다.", "This preset has no entry for that bot type."));
        }
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
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

    private static SAINSettingsGroupClass Reference(BotType bot)
    {
        return bot != null && PresetSync.TryGetCustomBundle("zzap", out var bundle) && bundle?.BotSettings != null
            && bundle.BotSettings.TryGetValue(bot.WildSpawnType.ToESain(), out var group) ? group : null;
    }

    private static string DeltaColor(int direction)
    {
        return direction > 0 ? "#f87171" : direction < 0 ? "#4ade80" : "#71717a";
    }

    private static string PersonalityName(EPersonality personality)
    {
        return personality == EPersonality.None ? T("지정 안 함", "Unassigned") : personality.ToString();
    }

    private static string ReferenceNote(bool same, string referenceText)
    {
        return same ? $"<color=#71717a>{T("기준과 같음", "Same as reference")}</color>" : $"<color=#71717a>{T("기준", "Reference")} {referenceText} →</color>";
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
        public string SlotText;
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

    /// <summary>What the page shows, rebuilt on Layout when something changed (selection, scope, record, a write).</summary>
    private sealed class View
    {
        public SAINSettingsGroupClass Group;
        public SAINSettingsGroupClass Reference;
        public SAINSettingsClass Slot;
        public ESainBotDifficulty Shown;
        public Dictionary<ESainBotDifficulty, int> Totals = new();
        public string RecordLine;
        public string ReferenceLine;
        public string SlotsLine;
        public string ScopeLine;
        public string AdvancedTitle;
        public readonly List<(ESainBotDifficulty? Scope, string Text, bool Seen)> Pills = new();
        public readonly List<KnobView> Knobs = new();
        public readonly List<RowView> Rows = new();
        public readonly Dictionary<BotType, string> Badges = new();
        public readonly Dictionary<BotType, bool> Dots = new();
        public bool AllDotsStale = true;

        private List<SpawnRaidCounts> _record;
        private bool _korean;
        private BotType _bot;
        private ESainBotDifficulty? _scope;
        private readonly Dictionary<BotFieldRow, ConfigInfoClass> _infos = new();

        public void Update()
        {
            var record = _loaded;
            bool recordChanged = !ReferenceEquals(record, _record) || _korean != Korean;
            if (!recordChanged && !_dirty && !AllDotsStale && _bot == _selected && _scope == BotSelectionClass._scope)
            {
                return;
            }
            _record = record;
            _korean = Korean;
            _bot = _selected;
            _scope = BotSelectionClass._scope;
            _dirty = false;

            if (recordChanged)
            {
                Badges.Clear();
                foreach (var bot in BotTypeDefinitions.BotTypes.Values)
                {
                    var totals = SpawnDifficultyRecord.Totals(record, bot.WildSpawnType.ToESain());
                    Badges[bot] = totals.Count == 0 ? T("기록 없음", "no record") : string.Join("·", Difficulties.Where(totals.ContainsKey).Select(d => EditorText.Value(d)));
                }
            }
            if (AllDotsStale)
            {
                AllDotsStale = false;
                foreach (var bot in BotTypeDefinitions.BotTypes.Values)
                {
                    Dots[bot] = BotQuickSettings.AnyChanged(Group(bot), Reference(bot));
                }
            }

            Group = Group(_bot);
            Reference = Reference(_bot);
            Knobs.Clear();
            Rows.Clear();
            Pills.Clear();
            if (Group == null)
            {
                return;
            }
            Dots[_bot] = BotQuickSettings.AnyChanged(Group, Reference);
            Totals = SpawnDifficultyRecord.Totals(record, _bot.WildSpawnType.ToESain());
            Shown = _scope ?? SpawnDifficultyRecord.ShownDifficulty(Totals);
            if (!Group.Settings.TryGetValue(Shown, out Slot) || Slot == null)
            {
                Slot = Group.Settings.Values.FirstOrDefault(s => s != null);
                Shown = Group.Settings.FirstOrDefault(p => p.Value == Slot).Key;
            }
            SAINSettingsClass refSlot = Reference != null && Reference.Settings.TryGetValue(Shown, out var r) ? r : null;

            RecordLine = Totals.Count == 0
                ? T("아직 기록 없음 — 레이드를 한 판 하면 나옵니다", "No record yet - play a raid and it shows here")
                : (Korean ? $"최근 레이드 {record?.Count ?? 0}판: " : $"Last {record?.Count ?? 0} raids: ")
                    + string.Join(" · ", Difficulties.Where(Totals.ContainsKey).Select(d => Korean ? $"{EditorText.Value(d)} {Totals[d]}명" : $"{d} {Totals[d]}"));
            // zzap (user 10/23: "picking easy/normal/hard, can't tell anything changed"): which slot is shown and what a change hits.
            string shownName = EditorText.Value(Shown);
            ScopeLine = _scope.HasValue
                ? (Korean ? $"지금 보는 칸: {shownName} · 바꾸면 {shownName} 칸만 바뀝니다" : $"Showing: {Shown} · changes hit the {Shown} slot only")
                : (Korean ? $"지금 보는 칸: {shownName}{(Totals.Count > 0 ? " (최근 레이드에서 가장 많이 나옴)" : " (기록이 없어 보통)")} · 바꾸면 4칸 모두 같은 비율로 바뀝니다"
                    : $"Showing: {Shown}{(Totals.Count > 0 ? " (most seen lately)" : " (no record - normal)")} · changes move all four slots by the same ratio");
            SlotsLine = BotQuickSettings.AllSlotsSame(Group)
                ? T("이 봇은 난이도 4칸 값이 모두 같습니다 — 어느 칸을 골라도 숫자는 같습니다.", "This bot has the same values in all four difficulty slots - every slot shows the same numbers.")
                : null;
            ReferenceLine = Reference != null ? T("기준: zzap 프리셋", "Reference: zzap preset") : T("기준 없음 (zzap 프리셋에 이 봇이 없음)", "No reference (not in the zzap preset)");
            Pills.Add((null, T("난이도 4칸 전부", "All 4 difficulties"), true));
            foreach (var d in Difficulties)
            {
                if (!Group.Settings.ContainsKey(d))
                {
                    continue;
                }
                bool seen = Totals.Count == 0 || Totals.ContainsKey(d);
                Pills.Add((d, EditorText.Value(d) + (seen ? string.Empty : T(" (안 나옴)", " (not seen)")), seen));
            }

            if (Slot == null)
            {
                return;
            }
            foreach (var knob in BotQuickSettings.All)
            {
                bool changed = BotQuickSettings.IsChanged(knob, Group, Reference);
                bool differs = BotQuickSettings.DiffersBySlot(knob, Group);
                string bySlot = differs ? "  <color=#a1a1aa><size=11>" + T("칸마다 다름", "differs by slot") + "</size></color>" : string.Empty;
                var view = new KnobView
                {
                    Knob = knob,
                    Label = new GUIContent("<b>" + knob.Name(Korean) + "</b>" + bySlot + (changed ? "  <color=#facc15>●</color>" : string.Empty), knob.Help(Korean)),
                    CanReset = changed && Reference != null,
                    Value = knob.Get(Slot),
                    On = knob.GetOn(Slot),
                    Personality = knob.GetPersonality(Slot),
                };
                if (differs)
                {
                    view.SlotText = string.Join(" · ", BotQuickSettings.SlotValues(knob, Group, Korean)
                        .Select(p => p.Difficulty == Shown ? $"<b><color=#e4e4e7>{EditorText.Value(p.Difficulty)} {p.Text}</color></b>" : $"{EditorText.Value(p.Difficulty)} {p.Text}"));
                }
                view.Reference = refSlot != null ? knob.Get(refSlot) : view.Value;
                view.Direction = knob.Direction(view.Value, view.Reference);
                view.Delta = refSlot == null
                    ? $"<b>{knob.Format(view.Value, Korean)}</b>"
                    : $"<color=#71717a>{knob.Format(view.Reference, Korean)}</color> → <b>{knob.Format(view.Value, Korean)}</b>   <color={DeltaColor(view.Direction)}><b>{knob.DeltaText(view.Value, view.Reference, Korean)}</b></color>";
                if (knob.Kind == BotKnobKind.Toggle)
                {
                    bool refOn = refSlot != null && knob.GetOn(refSlot);
                    view.Note = ReferenceNote(refSlot == null || refOn == view.On, refOn ? T("켬", "on") : T("끔", "off"));
                }
                else if (knob.Kind == BotKnobKind.Personality)
                {
                    var refPersonality = refSlot != null ? knob.GetPersonality(refSlot) : view.Personality;
                    view.Note = ReferenceNote(refPersonality == view.Personality, PersonalityName(refPersonality));
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
            AdvancedTitle = Korean ? $"고급 — 숫자 전부 보기 ({Rows.Count}개 · 바꾼 값 {changedCount}개)" : $"Advanced - every number ({Rows.Count} · {changedCount} changed)";
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
            bool headed = false;
            foreach (var bot in BotsBySection[section])
            {
                if (search.Length > 0 && bot.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                if (!headed)
                {
                    headed = true;
                    GUILayout.Label(section, ZzapUI.Section);
                }
                _view.Badges.TryGetValue(bot, out string badge);
                _view.Dots.TryGetValue(bot, out bool dot);
                var captured = bot;
                if (ZzapUI.ListItem(dot ? bot.Name + "  ●" : bot.Name, badge, bot == _selected, bot.Description, 12f, 28f))
                {
                    Write(() =>
                    {
                        _selected = captured;
                        _copyOpen = false;
                        _personalityOpen = false;
                        _rightScroll = Vector2.zero;
                    });
                }
            }
        }
        GUILayout.EndScrollView();
        GUILayout.Label(T("오른쪽 글자 = 최근 레이드에서 실제로 나온 난이도 · ● = 기준과 다른 값이 있음", "Right = difficulty it really spawned at lately · ● = differs from the reference"), ZzapUI.Small);
        GUILayout.EndVertical();
    }

    // ---------------------------------------------------------------- right: one bot

    private static void BotPanel()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("<b>" + _selected.Name + "</b>", ZzapUI.Title);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(T("이 봇 설정을 다른 봇에 복사", "Copy this bot to other bots"), ZzapUI.Button, GUILayout.Height(28f)))
        {
            Write(() =>
            {
                _copyOpen = true;
                _copyTargets.Clear();
            });
        }
        GUILayout.EndHorizontal();
        GUILayout.Label(_view.RecordLine, ZzapUI.Small);
        GUILayout.Space(6f);

        GUILayout.BeginHorizontal();
        GUILayout.Label(T("바꾸면 적용할 곳", "Apply changes to"), ZzapUI.Small, GUILayout.Width(110f), GUILayout.Height(28f));
        foreach (var (scope, text, seen) in _view.Pills)
        {
            ScopePill(scope, text, seen);
        }
        GUILayout.FlexibleSpace();
        GUILayout.Label(_view.ReferenceLine, ZzapUI.Small, GUILayout.Height(28f));
        GUILayout.EndHorizontal();
        GUILayout.Label(_view.ScopeLine, ZzapUI.Small);
        if (_view.SlotsLine != null)
        {
            GUILayout.Label(_view.SlotsLine, ZzapUI.Small);
        }
        ZzapUI.Separator(4f);
        if (_view.Slot == null)
        {
            return;
        }

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
    }

    private static void ScopePill(ESainBotDifficulty? scope, string text, bool seen)
    {
        bool on = _scope == scope;
        Color old = GUI.color;
        if (!seen && !on)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
        }
        if (GUILayout.Button(text, on ? ZzapUI.Pill : ZzapUI.Button, GUILayout.Height(28f)))
        {
            Sounds.PlaySound(EUISoundType.MenuCheckBox);
            Write(() => _scope = scope);
        }
        GUI.color = old;
    }

    private static void KnobRow(KnobView view)
    {
        var knob = view.Knob;
        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(LABEL_WIDTH));
        GUILayout.Label(view.Label, ZzapUI.Name, GUILayout.Height(22f));
        GUILayout.Label(knob.Help(Korean), _wrapSmall);
        if (view.SlotText != null)
        {
            GUILayout.Label(view.SlotText, _wrapSmall);
        }
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
                        var group = _view.Group;
                        var scope = _scope;
                        var shown = _view.Shown;
                        Write(() =>
                        {
                            BotQuickSettings.Apply(knob, group, scope, shown, newValue);
                            ConfigEditingTracker.Add(knob.NameEn, newValue);
                        });
                    }
                }
                break;
        }

        if (view.CanReset)
        {
            if (GUILayout.Button(T("되돌리기", "Reset"), ZzapUI.Button, GUILayout.Width(RESET_WIDTH), GUILayout.Height(26f)))
            {
                var group = _view.Group;
                var reference = _view.Reference;
                var scope = _scope;
                Write(() =>
                {
                    BotQuickSettings.Reset(knob, group, reference, scope);
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
            var scope = _scope;
            Write(() =>
            {
                BotQuickSettings.ApplyToggle(knob, group, scope, newOn);
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
                var scope = _scope;
                Write(() =>
                {
                    BotQuickSettings.ApplyPersonality(knob, group, scope, captured);
                    ConfigEditingTracker.Add(knob.NameEn, captured);
                    _personalityOpen = false;
                });
            }
        }
        GUILayout.EndHorizontal();
    }

    // ---------------------------------------------------------------- advanced: every number

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
            var scope = _scope;
            if (view.Changed)
            {
                if (GUILayout.Button(T("되돌리기", "Reset"), ZzapUI.Button, GUILayout.Width(RESET_WIDTH), GUILayout.Height(AttributesGUI.RowHeight)))
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
            GUILayout.Label(view.Info.Description, ZzapUI.Small);
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
                _view.AllDotsStale = true;
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
            copied++;
        }
        return copied;
    }
}
