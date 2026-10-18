using System.Collections.Generic;
using System.Linq;
using EFT.UI;
using SAIN.Plugin;
using SAIN.Preset;
using SAIN.Preset.Server;
using SAIN.Preset.Shared;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using SAIN.Preset.Shared.Preset;
using UnityEngine;

namespace SAIN.Editor.GUISections;

/// <summary>
/// zzap fork (2026-10-18, user approved mockup 5): the F6 "Quick settings" tab - preset cards on the left, the low-spec switch
/// and the most changed values on the right. Same list as the web page (SAIN.Preset.Shared QuickSettings); every slider makes
/// bots stronger to the right and each value is read out in plain words (user: "the numbers are too hard to read").
/// </summary>
public static class QuickSettingsGUI
{
    private const float LEFT_WIDTH = 360f;
    private const float RESET_WIDTH = 74f;

    private static GUIStyle _cardTitle;
    private static GUIStyle _cardTitleLegacy;
    private static GUIStyle _title;
    private static GUIStyle _name;
    private static GUIStyle _muted;
    private static GUIStyle _section;
    private static GUIStyle _value;
    private static GUIStyle _small;
    private static GUIStyle _delta;

    // The values as last saved (or as loaded): a value differing from these is "changed" and gets a reset button.
    private static readonly Dictionary<string, float> _saved = new();
    private static SAINPresetClass _savedFor;

    private static bool _showSainPresets;
    private static string _newName = string.Empty;
    private static string _pendingDelete;

    private static bool Korean
    {
        get { return EditorText.Korean; }
    }

    private static string T(string korean, string english)
    {
        return Korean ? korean : english;
    }

    public static void Draw()
    {
        var preset = SAINPlugin.LoadedPreset;
        if (preset == null)
        {
            return;
        }
        EnsureStyles();
        var view = View(preset);
        RememberSaved(preset, view);

        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical(GUILayout.Width(LEFT_WIDTH));
        PresetCards();
        GUILayout.EndVertical();

        GUILayout.Space(14f);

        GUILayout.BeginVertical();
        LowSpec(view);
        GUILayout.Space(10f);
        _refValues = ReferenceValues(preset);
        ReferenceBar();
        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        Group("aim", view);
        GUILayout.Space(10f);
        Group("tactics", view);
        GUILayout.EndVertical();
        GUILayout.Space(10f);
        GUILayout.BeginVertical();
        Group("sense", view);
        GUILayout.Space(10f);
        Group("log", view);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.Space(6f);
        GUILayout.Label(
            T("위쪽 '저장'을 눌러야 게임과 봇에 적용됩니다. 나머지 설정은 '전역 설정 (전체)' 탭.",
              "Press 'Save' at the top to apply to the game and bots. Everything else is on 'Global settings (all)'."),
            _muted);
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }

    private static QuickPresetView View(SAINPresetClass preset)
    {
        var bots = preset.BotSettings?.SAINSettings?.Select(kv => new KeyValuePair<string, SAINSettingsGroupClass>(kv.Key.ToString(), kv.Value))
            ?? Enumerable.Empty<KeyValuePair<string, SAINSettingsGroupClass>>();
        return new QuickPresetView(preset.GlobalSettings, bots.ToList());
    }

    private static void RememberSaved(SAINPresetClass preset, QuickPresetView view)
    {
        if (ReferenceEquals(_savedFor, preset) && ConfigEditingTracker.UnsavedChanges)
        {
            return;
        }
        _savedFor = preset;
        _saved.Clear();
        foreach (var q in QuickSettings.All)
        {
            _saved[q.Id] = q.Get(view);
        }
    }

    // ---------------------------------------------------------------- preset cards

    private static void PresetCards()
    {
        GUILayout.Label(T("ZZAP 프리셋 — 난이도 순", "ZZAP PRESETS — BY DIFFICULTY"), _section);
        if (ServerConfigClient.PresetLocked)
        {
            GUILayout.Label(
                string.Format(T("서버가 프리셋 '{0}'을(를) 고정했습니다. 고르기는 잠겨 있고, 값은 바꿀 수 있습니다.", "The server forced the preset '{0}'. Picking is locked; values can still be changed."),
                    SAINPlugin.LoadedPreset.Info.Name),
                _muted);
            return;
        }
        GUILayout.Label(T("고쳐서 저장하면 '이름 [Modified]' 복사본이 생기고, 원본은 그대로 남습니다(모드 업데이트 때 새 값으로 바뀜).",
            "Saving changes makes a 'name [Modified]' copy; the original stays as shipped (mod updates refresh it)."), _muted);

        var customs = PresetHandler.CustomPresetOptions;
        foreach (var zzap in QuickSettings.ZzapPresets)
        {
            var def = customs.FirstOrDefault(d => d.Name == zzap.Name);
            if (def != null)
            {
                Card(def, zzap);
            }
        }

        GUILayout.Space(6f);
        var defaults = PresetHandler.DefaultPresetOptions();
        if (GUILayout.Button(T($"원본 SAIN 프리셋 ({defaults.Count}) ", $"SAIN's original presets ({defaults.Count}) ") + (_showSainPresets ? "▾" : "▸"), _section))
        {
            Sounds.PlaySound(EUISoundType.MenuDropdown);
            ZzapUI.Defer(() => _showSainPresets = !_showSainPresets);
        }
        if (_showSainPresets)
        {
            foreach (var def in defaults)
            {
                Card(def, null);
            }
        }

        GUILayout.Space(6f);
        GUILayout.Label(T("내 프리셋", "MY PRESETS"), _section);
        bool any = false;
        foreach (var def in customs)
        {
            if (QuickSettings.FindZzap(def.Name) != null)
            {
                continue;
            }
            any = true;
            Card(def, null);
        }
        if (!any)
        {
            GUILayout.Label(T("아직 없음 — 아래에서 지금 프리셋을 복사해 만들 수 있습니다.", "None yet - copy the current preset below."), _muted);
        }

        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        _newName = GUILayout.TextField(_newName ?? string.Empty, 40, ZzapUI.FieldStyle, GUILayout.Height(30f));
        if (GUILayout.Button(new GUIContent(T("+ 만들기", "+ Create"), T("지금 프리셋(바꾼 값 포함)을 이 이름으로 복사", "Copy the current preset (with its changes) under this name")), ZzapUI.ButtonPrimary, GUILayout.Width(90f), GUILayout.Height(30f)))
        {
            ZzapUI.Defer(CreateCopy);
        }
        GUILayout.EndHorizontal();
        GUILayout.Label(T("카드를 누르면 바로 그 프리셋으로 바뀝니다.", "Click a card to switch to that preset."), _muted);
    }

    // zzap fork (2026-10-18): drawn by hand so the card grows with its text (the first version was a button with a fixed
    // minimum height, and "zzap 저사양 [테스트]" spilled out of it in game). Level = three small bars at the top right.
    private static void Card(SAINPresetDefinition def, ZzapPresetCard zzap)
    {
        bool selected = SAINPlugin.LoadedPreset.Info.Name == def.Name;
        bool deletable = def.IsCustom && zzap == null;
        string tagText = zzap?.TagKo == null ? null : zzap.Tag(Korean);
        bool showTag = tagText != null && def.Name.IndexOf("[" + tagText + "]", System.StringComparison.Ordinal) < 0;
        string summary = zzap != null ? zzap.Summary(Korean) : Short(def.Description);
        bool legacy = zzap?.Legacy == true;

        float width = LEFT_WIDTH - (deletable ? (_pendingDelete == def.Name ? 62f : 34f) : 0f);
        float summaryHeight = string.IsNullOrEmpty(summary) ? 0f : _small.CalcHeight(new GUIContent(summary), width - 24f);
        float height = 34f + summaryHeight + 8f;

        GUILayout.BeginHorizontal();
        Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
        if (Event.current.type == EventType.Repaint)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            ZzapUI.Fill(rect, selected ? ZzapUI.YellowTint : hover ? ZzapUI.PanelHover : ZzapUI.Panel);
            Color border = selected ? ZzapUI.Yellow : ZzapUI.Line;
            ZzapUI.Fill(new Rect(rect.x, rect.y, rect.width, 1f), border);
            ZzapUI.Fill(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), border);
            ZzapUI.Fill(new Rect(rect.x, rect.y, 1f, rect.height), border);
            ZzapUI.Fill(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), border);
            if (zzap != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    ZzapUI.Fill(new Rect(rect.xMax - 12f - (3 - i) * 20f, rect.y + 15f, 16f, 5f), i < zzap.Level ? ZzapUI.Yellow : ZzapUI.Track);
                }
            }
        }
        string title = "<b>" + def.Name + "</b>" + (showTag ? "  <color=#facc15>[" + tagText + "]</color>" : string.Empty);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 6f, rect.width - 24f - (zzap != null ? 70f : 0f), 24f), title, legacy ? _cardTitleLegacy : _cardTitle);
        if (summaryHeight > 0f)
        {
            GUI.Label(new Rect(rect.x + 12f, rect.y + 32f, rect.width - 24f, summaryHeight), summary, _small);
        }
        if (GUI.Button(rect, new GUIContent(string.Empty, def.Description), GUIStyle.none) && !selected)
        {
            Sounds.PlaySound(EUISoundType.MenuCheckBox);
            var picked = def;
            ZzapUI.Defer(() =>
            {
                ConfigEditingTracker.Clear();
                PresetHandler.ApplyDefinition(picked);
            });
        }
        if (deletable)
        {
            if (_pendingDelete == def.Name)
            {
                if (GUILayout.Button(new GUIContent(T("삭제!", "Delete!"), T("한 번 더 누르면 지웁니다", "Click again to delete")), ZzapUI.ButtonPrimary, GUILayout.Width(56f), GUILayout.Height(height)))
                {
                    var target = def;
                    ZzapUI.Defer(() => Delete(target));
                }
            }
            else if (GUILayout.Button(new GUIContent("X", T("이 프리셋 삭제", "Delete this preset")), ZzapUI.Button, GUILayout.Width(28f), GUILayout.Height(height)))
            {
                string name = def.Name;
                ZzapUI.Defer(() => _pendingDelete = name);
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(4f);
    }

    private static string Short(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }
        return text.Length <= 70 ? text : text.Substring(0, 70) + "…";
    }

    private static void CreateCopy()
    {
        string name = System.Text.RegularExpressions.Regex.Replace((_newName ?? string.Empty).Trim(), @"[^\w \-\[\]]", "");
        if (name.Length == 0 || PresetHandler.CustomPresetOptions.Any(d => d.Name == name))
        {
            Sounds.PlaySound(EUISoundType.ErrorMessage);
            return;
        }
        SAINPresetDefinition copy = SAINPlugin.LoadedPreset.Info.Clone();
        copy.Name = name;
        copy.Creator = "user";
        copy.SAINVersion = SAINVersionInfo.SAINPresetVersion;
        copy.DateCreated = System.DateTime.Today.ToString();
        PresetHandler.SavePresetDefinition(copy);
        PresetHandler.InitPresetFromDefinition(copy, true);
        PresetHandler.LoadCustomPresetOptions();
        ConfigEditingTracker.Clear();
        Sounds.PlaySound(EUISoundType.InsuranceInsured);
        _newName = string.Empty;
    }

    private static void Delete(SAINPresetDefinition def)
    {
        _pendingDelete = null;
        if (SAINPlugin.LoadedPreset.Info.Name == def.Name)
        {
            PresetHandler.loadDefault();
        }
        PresetSelection.DeletePreset(def);
        PresetHandler.LoadCustomPresetOptions();
        Sounds.PlaySound(EUISoundType.MalfunctionExamined);
    }

    // ---------------------------------------------------------------- low-spec switch

    private static void LowSpec(QuickPresetView view)
    {
        int on = QuickSettings.LowSpecCount(view);
        int total = QuickSettings.LowSpec.Length;
        GUILayout.BeginVertical(on == total ? ZzapUI.PanelBlue : ZzapUI.Panel1);
        GUILayout.BeginHorizontal();
        GUILayout.Label(
            "<b>" + T("저사양 PC — 한 번에 맞추기", "Low-spec PC — one switch") + "</b>  <size=12>"
                + T("난이도는 그대로, 프레임을 잡아먹는 계산만 줄임", "keeps the difficulty, only cuts the costly calculations") + "</size>",
            _title);
        GUILayout.FlexibleSpace();
        string state = on == total ? T("켬", "On") : on == 0 ? T("끔", "Off") : T("일부", "Partly");
        bool newValue = ZzapUI.Switch(on == total, 30f, $"{state} ({on}/{total})", $"{state} ({on}/{total})");
        if (newValue != (on == total))
        {
            if (newValue)
            {
                QuickSettings.ApplyLowSpec(view);
            }
            else
            {
                QuickSettings.RestoreLowSpec(view, null);
            }
            ConfigEditingTracker.Add("Low-spec PC", newValue);
        }
        GUILayout.EndHorizontal();

        var items = QuickSettings.LowSpec;
        for (int i = 0; i < items.Length; i += 3)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < i + 3 && j < items.Length; j++)
            {
                bool itemOn = items[j].IsLowSpec(view);
                GUILayout.Label((itemOn ? "<color=#4ade80>● " : "<color=#a1a1aa>○ ") + items[j].Name(Korean) + "</color>", _small, GUILayout.Width(400f));
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
    }

    // ---------------------------------------------------------------- value rows

    // ---------------------------------------------------------------- comparison reference (mockup 7)

    private const string SAVED = "";
    private static readonly string[] _referenceNames = { "zzap", "zzap 쉬움", "zzap 보통", SAVED };
    private static string _reference = "zzap";
    private static Dictionary<string, float> _refValues = new();

    private static string ReferenceLabel(string name)
    {
        if (name == SAVED)
        {
            return T("저장된 값", "Saved");
        }
        return name switch
        {
            "zzap 쉬움" => T("쉬움", "Easy"),
            "zzap 보통" => T("보통", "Normal"),
            _ => name,
        };
    }

    // The reference preset's quick values. The preset being edited and "Saved" both use the snapshot taken when it was
    // loaded or last saved, so the bars show exactly what changed since then.
    private static int _refFrame = -1;
    private static string _refFor;

    private static Dictionary<string, float> ReferenceValues(SAINPresetClass preset)
    {
        // Several GUI events run per frame; read the reference preset once per frame.
        if (_refFrame == Time.frameCount && _refFor == _reference)
        {
            return _refValues;
        }
        _refFrame = Time.frameCount;
        _refFor = _reference;
        if (_reference == SAVED || _reference == preset.Info.Name || !PresetSync.TryGetCustomBundle(_reference, out var bundle) || bundle?.GlobalSettings == null)
        {
            return _saved;
        }
        var bots = bundle.BotSettings.Select(kv => new KeyValuePair<string, SAINSettingsGroupClass>(kv.Key.ToString(), kv.Value)).ToList();
        var view = new QuickPresetView(bundle.GlobalSettings, bots);
        var values = new Dictionary<string, float>();
        foreach (var q in QuickSettings.All)
        {
            values[q.Id] = q.Get(view);
        }
        return values;
    }

    private static float RefValue(QuickSetting q, float fallback)
    {
        return _refValues.TryGetValue(q.Id, out float value) ? value : fallback;
    }

    private static void ReferenceBar()
    {
        GUILayout.BeginHorizontal(ZzapUI.Panel1);
        GUILayout.Label(T("비교 기준", "Compare with"), _section, GUILayout.Height(28f));
        foreach (string name in _referenceNames)
        {
            if (name != SAVED && !PresetSync.TryGetCustomBundle(name, out _))
            {
                continue;
            }
            bool selected = _reference == name;
            string tip = name == SAVED ? T("불러오거나 저장한 뒤 바꾼 것만 막대로 보기", "Bars show only what changed since the preset was loaded or saved") : T("이 프리셋 값과 비교", "Compare with this preset's values");
            if (GUILayout.Button(new GUIContent(ReferenceLabel(name), tip), selected ? ZzapUI.Pill : ZzapUI.Button, GUILayout.Height(28f), GUILayout.MinWidth(70f)))
            {
                Sounds.PlaySound(EUISoundType.MenuCheckBox);
                string picked = name;
                ZzapUI.Defer(() => _reference = picked);
            }
        }
        GUILayout.Space(16f);
        Swatch(ZzapUI.Yellow, 3f, 16f);
        GUILayout.Label(T("기준 위치", "reference"), _muted, GUILayout.Height(28f));
        Swatch(ZzapUI.Red, 30f, 6f);
        GUILayout.Label(T("기준보다 봇 강함 = 어려워짐", "bots stronger = harder"), _muted, GUILayout.Height(28f));
        Swatch(ZzapUI.Green, 30f, 6f);
        GUILayout.Label(T("봇 약함 = 쉬워짐", "bots weaker = easier"), _muted, GUILayout.Height(28f));
        GUILayout.Label(T("· 슬라이더는 오른쪽 = 봇 강함", "· right = stronger bots"), _muted, GUILayout.Height(28f));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    private static void Swatch(Color color, float width, float height)
    {
        Rect rect = GUILayoutUtility.GetRect(width + 8f, 28f, GUILayout.Width(width + 8f), GUILayout.Height(28f));
        if (Event.current.type == EventType.Repaint)
        {
            ZzapUI.Fill(new Rect(rect.x + 4f, rect.center.y - height / 2f, width, height), color);
        }
    }

    private static string DeltaColor(int direction)
    {
        return direction > 0 ? "#f87171" : direction < 0 ? "#4ade80" : "#71717a";
    }

    private static void Group(string group, QuickPresetView view)
    {
        GUILayout.BeginVertical(ZzapUI.Panel1);
        GUILayout.BeginHorizontal();
        GUILayout.Label("<b>" + QuickSettings.GroupName(group, Korean) + "</b>", _title);
        GUILayout.FlexibleSpace();
        if (group != "log")
        {
            GUILayout.Label(T("← 봇 약하게 · 강하게 →", "← weaker bots · stronger →"), _small);
        }
        GUILayout.EndHorizontal();
        foreach (var q in QuickSettings.All)
        {
            if (q.Group != group)
            {
                continue;
            }
            if (q.Kind == QuickKind.Toggle)
            {
                ToggleRow(q, view);
            }
            else
            {
                NumberRow(q, view);
            }
        }
        GUILayout.EndVertical();
    }

    private static bool Changed(QuickSetting q, float value, out float saved)
    {
        return _saved.TryGetValue(q.Id, out saved) && Mathf.Abs(saved - value) > 0.0001f;
    }

    private static string ChangedMark(bool changed)
    {
        return changed ? "  <color=#facc15><size=11>● " + T("바꿈", "changed") + "</size></color>" : string.Empty;
    }

    // Mockup 7: name and "reference → now  ▼ weaker · 25% slower" on top, the difference slider under it, the meaning below.
    private static void NumberRow(QuickSetting q, QuickPresetView view)
    {
        float value = q.Get(view);
        bool changed = Changed(q, value, out float saved);
        float reference = RefValue(q, q.ZzapValue);
        int direction = q.Direction(value, reference);

        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("<b>" + q.Name(Korean) + "</b>" + ChangedMark(changed), q.Help(Korean)), _name, GUILayout.Height(24f));
        GUILayout.FlexibleSpace();
        string delta = $"<color=#71717a>{ReferenceLabel(_reference)} {q.Format(reference, Korean)}</color>  →  <b>{q.Format(value, Korean)}</b>   "
            + $"<color={DeltaColor(direction)}><b>{q.DeltaText(value, reference, Korean)}</b></color>";
        GUILayout.Label(delta, _delta, GUILayout.Height(24f));
        float newValue = value;
        if (changed)
        {
            if (GUILayout.Button(new GUIContent(T("되돌리기", "Undo"), string.Format(T("저장된 값 {0}(으)로", "Back to the saved {0}"), q.Format(saved, Korean))), ZzapUI.Button, GUILayout.Width(RESET_WIDTH), GUILayout.Height(24f)))
            {
                newValue = saved;
            }
        }
        else
        {
            GUILayout.Space(RESET_WIDTH + 8f);
        }
        GUILayout.EndHorizontal();

        float position = ZzapUI.DiffSlider(q.ToSlider(value), q.Min, q.Max, q.ToSlider(reference), direction, GUILayout.Height(24f), GUILayout.ExpandWidth(true));
        GUILayout.Label(q.Text(value, Korean), _small);
        GUILayout.Space(4f);
        ZzapUI.Separator();

        // Only a moved slider writes: rounding an untouched value to the slider step would change presets on its own
        // (e.g. easy's 0.33 spotting speed).
        if (newValue == value && Mathf.Abs(position - q.ToSlider(value)) > 0.0001f)
        {
            newValue = q.Round(Mathf.Clamp(q.FromSlider(position), q.Min, q.Max));
        }
        if (Mathf.Abs(newValue - value) > 0.0001f)
        {
            q.Set(view, newValue);
            ConfigEditingTracker.Add(q.NameEn, newValue);
        }
    }

    private static void ToggleRow(QuickSetting q, QuickPresetView view)
    {
        float value = q.Get(view);
        bool on = value >= 0.5f;
        bool changed = Changed(q, value, out _);
        bool referenceOn = RefValue(q, q.ZzapValue) >= 0.5f;

        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        GUILayout.Label(new GUIContent("<b>" + q.Name(Korean) + "</b>" + ChangedMark(changed), q.Help(Korean)), _name, GUILayout.Height(24f));
        GUILayout.Label(q.Help(Korean), _small);
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        string state = referenceOn ? T("켬", "on") : T("끔", "off");
        string note = referenceOn == on
            ? $"<color=#71717a>{ReferenceLabel(_reference)}{T("과 같음", ": same")}</color>"
            : $"<color=#71717a>{ReferenceLabel(_reference)} {state} →</color>";
        GUILayout.Label(note, _delta, GUILayout.Height(36f));
        bool newOn = ZzapUI.Switch(on, 36f);
        GUILayout.EndHorizontal();
        GUILayout.Space(4f);
        ZzapUI.Separator();

        if (newOn != on)
        {
            q.Set(view, newOn ? 1f : 0f);
            ConfigEditingTracker.Add(q.NameEn, newOn);
        }
    }

    // ---------------------------------------------------------------- styles

    private static void EnsureStyles()
    {
        ZzapUI.EnsureStyles();
        if (_cardTitle != null)
        {
            return;
        }
        _cardTitle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 15, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
        SetText(_cardTitle, new Color(0.93f, 0.93f, 0.95f));
        _cardTitleLegacy = new GUIStyle(_cardTitle);
        SetText(_cardTitleLegacy, new Color(0.55f, 0.55f, 0.6f));

        _title = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 15, alignment = TextAnchor.MiddleLeft };
        SetText(_title, Color.white);

        _name = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14, alignment = TextAnchor.MiddleLeft };
        SetText(_name, new Color(0.93f, 0.93f, 0.95f));

        _small = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12, wordWrap = true, alignment = TextAnchor.UpperLeft };
        SetText(_small, new Color(0.63f, 0.63f, 0.67f));

        _muted = new GUIStyle(_small) { fontSize = 12 };

        _section = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        SetText(_section, new Color(0.98f, 0.8f, 0.08f));

        _delta = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13, alignment = TextAnchor.MiddleRight, wordWrap = false };
        SetText(_delta, new Color(0.89f, 0.89f, 0.91f));

        _value = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        SetText(_value, Color.white);
    }

    private static void SetText(GUIStyle style, Color color)
    {
        style.normal.textColor = color;
        style.onNormal.textColor = color;
        style.hover.textColor = color;
        style.onHover.textColor = color;
        style.active.textColor = color;
        style.onActive.textColor = color;
        style.focused.textColor = color;
        style.onFocused.textColor = color;
    }
}
