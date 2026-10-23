using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
/// Knob math, the spawn record and the advanced list live in SAIN.Preset.Shared, shared with the web page.
/// </summary>
public static class BotSelectionClass
{
    private const float KNOB_DELTA_WIDTH = 330f;
    private const float RESET_WIDTH = 74f;

    private static readonly ESainBotDifficulty[] Difficulties = { ESainBotDifficulty.easy, ESainBotDifficulty.normal, ESainBotDifficulty.hard, ESainBotDifficulty.impossible };

    public static readonly string[] Sections = BotTypeDefinitions.BotTypes.Values.Select(b => b.Section).Distinct().ToArray();

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

    // Spawn record: read once in the background when the page first opens.
    private static volatile List<SpawnRaidCounts> _record;
    private static bool _recordRequested;

    // Per bot type text for the list, rebuilt when the record arrives or once a second (the changed dot).
    private static readonly Dictionary<BotType, string> _badges = new();
    private static readonly Dictionary<BotType, bool> _dots = new();
    private static float _nextDotRefresh;
    private static List<SpawnRaidCounts> _badgesFor;

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
        RequestRecord();
        RefreshListText();
        _selected ??= BotTypeDefinitions.BotTypes.Values.FirstOrDefault();

        GUILayout.BeginHorizontal();
        BotList();
        GUILayout.Space(12f);
        GUILayout.BeginVertical(ZzapUI.Panel1, GUILayout.Height(ZzapUI.PANEL_HEIGHT), GUILayout.ExpandWidth(true));
        if (_selected != null && Group(_selected) is SAINSettingsGroupClass group)
        {
            _rightScroll = GUILayout.BeginScrollView(_rightScroll, GUILayout.ExpandHeight(true));
            if (_copyOpen)
            {
                CopyPanel(group);
            }
            else
            {
                BotPanel(_selected, group);
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

    // ---------------------------------------------------------------- data

    private static SAINSettingsGroupClass Group(BotType bot)
    {
        var all = SAINPlugin.LoadedPreset?.BotSettings?.SAINSettings;
        return all != null && all.TryGetValue(bot.WildSpawnType, out var group) ? group : null;
    }

    private static SAINSettingsGroupClass Reference(BotType bot)
    {
        return PresetSync.TryGetCustomBundle("zzap", out var bundle) && bundle?.BotSettings != null
            && bundle.BotSettings.TryGetValue(bot.WildSpawnType.ToESain(), out var group) ? group : null;
    }

    private static void RequestRecord()
    {
        if (_recordRequested)
        {
            return;
        }
        _recordRequested = true;
        Task.Run(() => _record = SpawnDifficultyRecorder.Load());
    }

    private static Dictionary<ESainBotDifficulty, int> Totals(BotType bot)
    {
        return SpawnDifficultyRecord.Totals(_record, bot.WildSpawnType.ToESain());
    }

    private static void RefreshListText()
    {
        var record = _record;
        if (!ReferenceEquals(record, _badgesFor))
        {
            _badgesFor = record;
            _badges.Clear();
            foreach (var bot in BotTypeDefinitions.BotTypes.Values)
            {
                var totals = SpawnDifficultyRecord.Totals(record, bot.WildSpawnType.ToESain());
                _badges[bot] = totals.Count == 0
                    ? T("기록 없음", "no record")
                    : string.Join("·", Difficulties.Where(totals.ContainsKey).Select(d => EditorText.Value(d)));
            }
        }
        if (Time.unscaledTime >= _nextDotRefresh)
        {
            _nextDotRefresh = Time.unscaledTime + 1f;
            foreach (var bot in BotTypeDefinitions.BotTypes.Values)
            {
                _dots[bot] = BotQuickSettings.AnyChanged(Group(bot), Reference(bot));
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
            bool headed = false;
            foreach (var bot in BotTypeDefinitions.BotTypes.Values)
            {
                if (bot.Section != section || (search.Length > 0 && bot.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    continue;
                }
                if (!headed)
                {
                    headed = true;
                    GUILayout.Label(section, ZzapUI.Section);
                }
                _badges.TryGetValue(bot, out string badge);
                _dots.TryGetValue(bot, out bool dot);
                var captured = bot;
                if (ZzapUI.ListItem(bot.Name + (dot ? "  ●" : string.Empty), badge, bot == _selected, bot.Description, 12f, 28f))
                {
                    ZzapUI.Defer(() =>
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

    private static void BotPanel(BotType bot, SAINSettingsGroupClass group)
    {
        var reference = Reference(bot);
        var totals = Totals(bot);
        var shown = _scope ?? SpawnDifficultyRecord.ShownDifficulty(totals);

        GUILayout.BeginHorizontal();
        GUILayout.Label("<b>" + bot.Name + "</b>", ZzapUI.Title);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(T("이 봇 설정을 다른 봇에 복사", "Copy this bot to other bots"), ZzapUI.Button, GUILayout.Height(28f)))
        {
            ZzapUI.Defer(() =>
            {
                _copyOpen = true;
                _copyTargets.Clear();
            });
        }
        GUILayout.EndHorizontal();
        GUILayout.Label(RecordLine(totals), ZzapUI.Small);
        GUILayout.Space(6f);

        GUILayout.BeginHorizontal();
        GUILayout.Label(T("바꾸면 적용할 곳", "Apply changes to"), ZzapUI.Small, GUILayout.Width(110f), GUILayout.Height(28f));
        ScopePill(null, T("난이도 4칸 전부", "All 4 difficulties"), true);
        foreach (var d in Difficulties)
        {
            bool seen = totals.ContainsKey(d);
            ScopePill(d, EditorText.Value(d) + (seen || totals.Count == 0 ? string.Empty : T(" (안 나옴)", " (not seen)")), seen || totals.Count == 0);
        }
        GUILayout.FlexibleSpace();
        GUILayout.Label(reference != null ? T("기준: zzap 프리셋", "Reference: zzap preset") : T("기준 없음 (zzap 프리셋에 이 봇이 없음)", "No reference (not in the zzap preset)"), ZzapUI.Small, GUILayout.Height(28f));
        GUILayout.EndHorizontal();
        ZzapUI.Separator(4f);

        if (!group.Settings.TryGetValue(shown, out var slot) || slot == null)
        {
            slot = group.Settings.Values.FirstOrDefault(s => s != null);
            if (slot == null)
            {
                return;
            }
        }
        SAINSettingsClass refSlot = reference != null && reference.Settings.TryGetValue(shown, out var r) ? r : null;

        foreach (string knobGroup in new[] { "aim", "sense", "mind" })
        {
            GUILayout.Space(8f);
            GUILayout.Label(GroupName(knobGroup), ZzapUI.Section);
            foreach (var knob in BotQuickSettings.All)
            {
                if (knob.Group == knobGroup)
                {
                    KnobRow(knob, group, reference, slot, refSlot, shown);
                }
            }
        }

        GUILayout.Space(10f);
        Advanced(group, reference, slot, refSlot, shown);
    }

    private static string RecordLine(Dictionary<ESainBotDifficulty, int> totals)
    {
        if (totals.Count == 0)
        {
            return T("아직 기록 없음 — 레이드를 한 판 하면 나옵니다", "No record yet - play a raid and it shows here");
        }
        int raids = _record?.Count ?? 0;
        string parts = string.Join(" · ", Difficulties.Where(totals.ContainsKey).Select(d => Korean ? $"{EditorText.Value(d)} {totals[d]}명" : $"{d} {totals[d]}"));
        return Korean ? $"최근 레이드 {raids}판: {parts}" : $"Last {raids} raids: {parts}";
    }

    private static void ScopePill(ESainBotDifficulty? scope, string text, bool seen)
    {
        bool on = _scope == scope;
        var style = on ? ZzapUI.Pill : ZzapUI.Button;
        Color old = GUI.color;
        if (!seen && !on)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
        }
        if (GUILayout.Button(text, style, GUILayout.Height(28f)))
        {
            Sounds.PlaySound(EUISoundType.MenuCheckBox);
            ZzapUI.Defer(() => _scope = scope);
        }
        GUI.color = old;
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

    private static string DeltaColor(int direction)
    {
        return direction > 0 ? "#f87171" : direction < 0 ? "#4ade80" : "#71717a";
    }

    private static void KnobRow(BotKnob knob, SAINSettingsGroupClass group, SAINSettingsGroupClass reference, SAINSettingsClass slot, SAINSettingsClass refSlot, ESainBotDifficulty shown)
    {
        bool changed = BotQuickSettings.IsChanged(knob, group, reference);
        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(220f));
        GUILayout.Label(new GUIContent("<b>" + knob.Name(Korean) + "</b>" + (changed ? "  <color=#facc15>●</color>" : string.Empty), knob.Help(Korean)), ZzapUI.Name, GUILayout.Height(22f));
        GUILayout.Label(knob.Help(Korean), ZzapUI.Small);
        GUILayout.EndVertical();

        switch (knob.Kind)
        {
            case BotKnobKind.Toggle:
                ToggleCells(knob, group, slot, refSlot);
                break;
            case BotKnobKind.Personality:
                PersonalityCells(knob, group, slot, refSlot);
                break;
            default:
                NumberCells(knob, group, slot, refSlot, shown);
                break;
        }

        if (changed && reference != null)
        {
            if (GUILayout.Button(T("되돌리기", "Reset"), ZzapUI.Button, GUILayout.Width(RESET_WIDTH), GUILayout.Height(26f)))
            {
                BotQuickSettings.Reset(knob, group, reference, _scope);
                ConfigEditingTracker.Add(knob.NameEn, "reset");
            }
        }
        else
        {
            GUILayout.Space(RESET_WIDTH + 4f);
        }
        GUILayout.EndHorizontal();
        if (knob.Kind == BotKnobKind.Personality && _personalityOpen)
        {
            PersonalityChoices(knob, group);
        }
        ZzapUI.Separator(2f);
    }

    private static void NumberCells(BotKnob knob, SAINSettingsGroupClass group, SAINSettingsClass slot, SAINSettingsClass refSlot, ESainBotDifficulty shown)
    {
        if (knob.Kind == BotKnobKind.ToggleNumber)
        {
            bool on = knob.GetOn(slot);
            bool newOn = ZzapUI.Switch(on, 26f);
            if (newOn != on)
            {
                BotQuickSettings.ApplyToggle(knob, group, _scope, newOn);
                ConfigEditingTracker.Add(knob.NameEn, newOn);
            }
        }
        float value = knob.Get(slot);
        float reference = refSlot != null ? knob.Get(refSlot) : value;
        int direction = knob.Direction(value, reference);
        float position = ZzapUI.DiffSlider(knob.ToSlider(value), knob.Min, knob.Max, knob.ToSlider(reference), direction, GUILayout.Height(24f), GUILayout.ExpandWidth(true));
        string delta = refSlot == null
            ? $"<b>{knob.Format(value, Korean)}</b>"
            : $"<color=#71717a>{knob.Format(reference, Korean)}</color> → <b>{knob.Format(value, Korean)}</b>   <color={DeltaColor(direction)}><b>{knob.DeltaText(value, reference, Korean)}</b></color>";
        GUILayout.Label(delta, ZzapUI.Small, GUILayout.Width(KNOB_DELTA_WIDTH), GUILayout.Height(24f));
        // Only a moved slider writes: rounding an untouched value to the step would change presets on its own.
        if (Mathf.Abs(position - knob.ToSlider(value)) > 0.0001f)
        {
            float newValue = knob.Round(Mathf.Clamp(knob.FromSlider(position), knob.Min, knob.Max));
            if (Mathf.Abs(newValue - value) > 0.0001f)
            {
                BotQuickSettings.Apply(knob, group, _scope, shown, newValue);
                ConfigEditingTracker.Add(knob.NameEn, newValue);
            }
        }
    }

    private static void ToggleCells(BotKnob knob, SAINSettingsGroupClass group, SAINSettingsClass slot, SAINSettingsClass refSlot)
    {
        bool on = knob.GetOn(slot);
        GUILayout.FlexibleSpace();
        GUILayout.Label(ReferenceNote(refSlot == null || knob.GetOn(refSlot) == on, refSlot != null && knob.GetOn(refSlot) ? T("켬", "on") : T("끔", "off")), ZzapUI.Small, GUILayout.Height(26f));
        bool newOn = ZzapUI.Switch(on, 26f);
        if (newOn != on)
        {
            BotQuickSettings.ApplyToggle(knob, group, _scope, newOn);
            ConfigEditingTracker.Add(knob.NameEn, newOn);
        }
    }

    private static void PersonalityCells(BotKnob knob, SAINSettingsGroupClass group, SAINSettingsClass slot, SAINSettingsClass refSlot)
    {
        var personality = knob.GetPersonality(slot);
        GUILayout.FlexibleSpace();
        var refPersonality = refSlot != null ? knob.GetPersonality(refSlot) : personality;
        GUILayout.Label(ReferenceNote(refPersonality == personality, PersonalityName(refPersonality)), ZzapUI.Small, GUILayout.Height(26f));
        if (GUILayout.Button(PersonalityName(personality) + "  ▾", ZzapUI.Button, GUILayout.Width(170f), GUILayout.Height(26f)))
        {
            ZzapUI.Defer(() => _personalityOpen = !_personalityOpen);
        }
    }

    private static void PersonalityChoices(BotKnob knob, SAINSettingsGroupClass group)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(220f);
        foreach (EPersonality personality in Enum.GetValues(typeof(EPersonality)))
        {
            var captured = personality;
            if (GUILayout.Button(PersonalityName(personality), ZzapUI.Button, GUILayout.Height(26f)))
            {
                ZzapUI.Defer(() =>
                {
                    BotQuickSettings.ApplyPersonality(knob, group, _scope, captured);
                    ConfigEditingTracker.Add(knob.NameEn, captured);
                    _personalityOpen = false;
                });
            }
        }
        GUILayout.EndHorizontal();
    }

    private static string PersonalityName(EPersonality personality)
    {
        return personality == EPersonality.None ? T("지정 안 함", "Unassigned") : personality.ToString();
    }

    private static string ReferenceNote(bool same, string referenceText)
    {
        return same ? $"<color=#71717a>{T("기준과 같음", "Same as reference")}</color>" : $"<color=#71717a>{T("기준", "Reference")} {referenceText} →</color>";
    }

    // ---------------------------------------------------------------- advanced: every number

    private static void Advanced(SAINSettingsGroupClass group, SAINSettingsGroupClass reference, SAINSettingsClass slot, SAINSettingsClass refSlot, ESainBotDifficulty shown)
    {
        var fields = BotAdvancedFields.List();
        int changedCount = 0;
        if (refSlot != null)
        {
            foreach (var (_, category, field) in fields)
            {
                if (!Same(field.GetValue(category.GetValue(slot)), field.GetValue(category.GetValue(refSlot))))
                {
                    changedCount++;
                }
            }
        }
        string title = Korean ? $"고급 — 숫자 전부 보기 ({fields.Count}개 · 바꾼 값 {changedCount}개)" : $"Advanced - every number ({fields.Count} · {changedCount} changed)";
        if (ZzapUI.ListItem((_advancedOpen ? "▾  " : "▸  ") + title, null, false))
        {
            ZzapUI.Defer(() => _advancedOpen = !_advancedOpen);
        }
        if (!_advancedOpen)
        {
            return;
        }
        _entryConfig ??= new GUIEntryConfig();
        string lastGroup = null;
        foreach (var (groupName, category, field) in fields)
        {
            var info = AttributesGUI.GetAttributeInfo(field);
            if (info == null || info.DoNotShowGUI)
            {
                continue;
            }
            if (groupName != lastGroup)
            {
                lastGroup = groupName;
                GUILayout.Space(6f);
                GUILayout.Label(groupName == "vanilla" ? T("게임 기본값", "EFT's own values") : EditorText.UI(category.Name), ZzapUI.Section);
            }
            object categoryValue = category.GetValue(slot);
            object value = info.GetValue(categoryValue);
            object refValue = refSlot != null ? field.GetValue(category.GetValue(refSlot)) : value;
            bool changed = refSlot != null && !Same(value, refValue);

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(info.Name + (changed ? "  <color=#facc15>●</color>" : string.Empty), info.Description), ZzapUI.Name, GUILayout.Width(ZzapUI.NAME_WIDTH), GUILayout.MinHeight(AttributesGUI.RowHeight));
            object before = value;
            value = AttributesGUI.EditFloatBoolInt(ref value, categoryValue, info, _entryConfig, 0, out bool edited, false, false);
            if (refSlot != null)
            {
                GUILayout.Label($"<color=#71717a>{T("기준", "ref")} {refValue}</color>", ZzapUI.Small, GUILayout.Width(90f), GUILayout.Height(AttributesGUI.RowHeight));
            }
            if (changed && GUILayout.Button(T("되돌리기", "Reset"), ZzapUI.Button, GUILayout.Width(RESET_WIDTH), GUILayout.Height(AttributesGUI.RowHeight)))
            {
                SetField(group, category, field, reference, shown, before, refValue, isReset: true);
                ConfigEditingTracker.Add(info.Name, refValue);
            }
            else if (edited && !Same(before, value))
            {
                SetField(group, category, field, reference, shown, before, value, isReset: false);
                ConfigEditingTracker.Add(info.Name, value);
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(info.Description, ZzapUI.Small);
        }
    }

    private static bool Same(object a, object b)
    {
        return a is float fa && b is float fb ? Mathf.Abs(fa - fb) <= 1e-4f : Equals(a, b);
    }

    // One advanced field: scope slot directly; all slots = numbers by the ratio of the shown slot's change, others alike.
    // Reset copies each slot's own reference value.
    private static void SetField(SAINSettingsGroupClass group, FieldInfo category, FieldInfo field, SAINSettingsGroupClass reference, ESainBotDifficulty shown, object before, object value, bool isReset)
    {
        foreach (var pair in group.Settings)
        {
            if (pair.Value == null || (_scope.HasValue && pair.Key != _scope.Value))
            {
                continue;
            }
            object target = category.GetValue(pair.Value);
            if (isReset)
            {
                if (reference != null && reference.Settings.TryGetValue(pair.Key, out var from) && from != null)
                {
                    field.SetValue(target, field.GetValue(category.GetValue(from)));
                }
                continue;
            }
            if (_scope.HasValue || pair.Key == shown)
            {
                field.SetValue(target, value);
            }
            else if (value is float fv && before is float fb && Mathf.Abs(fb) > 1e-6f)
            {
                field.SetValue(target, (float)field.GetValue(target) * (fv / fb));
            }
            else
            {
                field.SetValue(target, value);
            }
        }
    }

    // ---------------------------------------------------------------- copy to other bots

    private static void CopyPanel(SAINSettingsGroupClass source)
    {
        ZzapUI.PanelHeading(T("복사할 봇 고르기", "Pick bots to copy to"),
            Korean ? $"{_selected.Name}의 난이도 4칸 값을 고른 봇에 덮어씁니다(성격 지정 포함)." : $"Overwrites the picked bots' four difficulty slots with {_selected.Name}'s (personality override included).");
        foreach (string section in Sections)
        {
            var bots = BotTypeDefinitions.BotTypes.Values.Where(b => b.Section == section && b != _selected).ToList();
            if (bots.Count == 0)
            {
                continue;
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label(section, ZzapUI.Section);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(T("전부 선택", "Select all"), ZzapUI.Button, GUILayout.Height(24f)))
            {
                ZzapUI.Defer(() => bots.ForEach(b => _copyTargets.Add(b)));
            }
            GUILayout.EndHorizontal();
            foreach (var bot in bots)
            {
                var captured = bot;
                if (ZzapUI.ListItem((_copyTargets.Contains(bot) ? "☑  " : "☐  ") + bot.Name, null, _copyTargets.Contains(bot), null, 12f, 26f))
                {
                    ZzapUI.Defer(() =>
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
            var targets = _copyTargets.ToList();
            ZzapUI.Defer(() =>
            {
                int n = CopyTo(source, targets);
                ConfigEditingTracker.Add("Copy bot settings", n);
                _copyOpen = false;
                Sounds.PlaySound(EUISoundType.ButtonBottomBarClick);
            });
        }
        if (GUILayout.Button(T("취소", "Cancel"), ZzapUI.Button, GUILayout.Height(30f), GUILayout.Width(100f)))
        {
            ZzapUI.Defer(() => _copyOpen = false);
        }
        GUILayout.EndHorizontal();
    }

    private static int CopyTo(SAINSettingsGroupClass source, List<BotType> targets)
    {
        int copied = 0;
        foreach (var bot in targets)
        {
            var target = Group(bot);
            if (target?.Settings == null)
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
