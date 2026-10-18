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
    private const float NAME_WIDTH = 330f;
    private const float SLIDER_WIDTH = 220f;
    private const float VALUE_WIDTH = 60f;
    private const float RESET_WIDTH = 74f;

    private static GUIStyle _card;
    private static GUIStyle _cardSelected;
    private static GUIStyle _cardLegacy;
    private static GUIStyle _panel;
    private static GUIStyle _panelBlue;
    private static GUIStyle _title;
    private static GUIStyle _name;
    private static GUIStyle _muted;
    private static GUIStyle _section;
    private static GUIStyle _value;
    private static GUIStyle _small;
    private static Texture2D _tickTexture;

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
            T("노란 눈금 = zzap 값 · 슬라이더는 오른쪽일수록 봇이 강함 · 위쪽 '저장'을 눌러야 게임과 봇에 적용됩니다. 나머지 설정은 '전역 설정 (전체)' 탭.",
              "Yellow tick = zzap's value · sliders make bots stronger to the right · press 'Save' at the top to apply to the game and bots. Everything else is on 'Global settings (all)'."),
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
            _showSainPresets = !_showSainPresets;
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
        _newName = GUILayout.TextField(_newName ?? string.Empty, 40, GUILayout.Height(28f));
        if (GUILayout.Button(new GUIContent(T("+ 만들기", "+ Create"), T("지금 프리셋(바꾼 값 포함)을 이 이름으로 복사", "Copy the current preset (with its changes) under this name")), GUILayout.Width(90f), GUILayout.Height(28f)))
        {
            CreateCopy();
        }
        GUILayout.EndHorizontal();
        GUILayout.Label(T("카드를 누르면 바로 그 프리셋으로 바뀝니다.", "Click a card to switch to that preset."), _muted);
    }

    private static void Card(SAINPresetDefinition def, ZzapPresetCard zzap)
    {
        bool selected = SAINPlugin.LoadedPreset.Info.Name == def.Name;
        string level = zzap == null ? string.Empty : "   " + new string('●', zzap.Level) + new string('○', 3 - zzap.Level);
        string tag = zzap?.TagKo == null ? string.Empty : $"  [{zzap.Tag(Korean)}]";
        string summary = zzap != null ? zzap.Summary(Korean) : Short(def.Description);
        var style = selected ? _cardSelected : zzap?.Legacy == true ? _cardLegacy : _card;
        string text = $"<b>{def.Name}</b>{tag}{level}" + (string.IsNullOrEmpty(summary) ? string.Empty : "\n<size=12>" + summary + "</size>");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent(text, def.Description), style, GUILayout.MinHeight(46f)) && !selected)
        {
            Sounds.PlaySound(EUISoundType.MenuCheckBox);
            ConfigEditingTracker.Clear();
            PresetHandler.ApplyDefinition(def);
        }
        bool deletable = def.IsCustom && zzap == null;
        if (deletable)
        {
            if (_pendingDelete == def.Name)
            {
                if (GUILayout.Button(new GUIContent(T("삭제!", "Delete!"), T("한 번 더 누르면 지웁니다", "Click again to delete")), GUILayout.Width(56f), GUILayout.MinHeight(46f)))
                {
                    Delete(def);
                }
            }
            else if (GUILayout.Button(new GUIContent("X", T("이 프리셋 삭제", "Delete this preset")), GUILayout.Width(28f), GUILayout.MinHeight(46f)))
            {
                _pendingDelete = def.Name;
            }
        }
        GUILayout.EndHorizontal();
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
        GUILayout.BeginVertical(on == total ? _panelBlue : _panel);
        GUILayout.BeginHorizontal();
        GUILayout.Label(
            "<b>" + T("저사양 PC — 한 번에 맞추기", "Low-spec PC — one switch") + "</b>  <size=12>"
                + T("난이도는 그대로, 프레임을 잡아먹는 계산만 줄임", "keeps the difficulty, only cuts the costly calculations") + "</size>",
            _title);
        GUILayout.FlexibleSpace();
        string state = on == total ? T("켬", "On") : on == 0 ? T("끔", "Off") : T("일부", "Partly");
        bool newValue = SAINLayout.Toggle(on == total, new GUIContent($"{state} ({on}/{total})"), EUISoundType.MenuCheckBox, GUILayout.Width(150f), GUILayout.Height(28f));
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

    private static void Group(string group, QuickPresetView view)
    {
        GUILayout.BeginVertical(_panel);
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

    private static void NumberRow(QuickSetting q, QuickPresetView view)
    {
        float value = q.Get(view);
        bool changed = Changed(q, value, out float saved);

        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(NAME_WIDTH));
        GUILayout.Label(new GUIContent(q.Name(Korean) + ChangedMark(changed), q.Help(Korean)), _name);
        GUILayout.Label(q.Text(value, Korean), _small);
        GUILayout.EndVertical();

        GUILayout.BeginVertical(GUILayout.Width(SLIDER_WIDTH));
        GUILayout.Space(12f);
        float position = SAINLayout.HorizontalSlider(q.ToSlider(value), q.Min, q.Max, null, GUILayout.Width(SLIDER_WIDTH), GUILayout.Height(18f));
        DrawZzapTick(q);
        GUILayout.EndVertical();

        GUILayout.Label(Format(value), _value, GUILayout.Width(VALUE_WIDTH), GUILayout.Height(36f));

        // Only a moved slider writes: rounding an untouched value to the slider step would change presets on its own
        // (e.g. easy's 0.33 spotting speed).
        float newValue = Mathf.Abs(position - q.ToSlider(value)) > 0.0001f ? q.Round(Mathf.Clamp(q.FromSlider(position), q.Min, q.Max)) : value;
        if (changed)
        {
            if (GUILayout.Button(new GUIContent(T("되돌리기", "Undo"), string.Format(T("저장된 값 {0}(으)로", "Back to the saved {0}"), Format(saved))), GUILayout.Width(RESET_WIDTH), GUILayout.Height(26f)))
            {
                newValue = saved;
            }
        }
        else
        {
            GUILayout.Space(RESET_WIDTH + 4f);
        }
        GUILayout.EndHorizontal();

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

        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(NAME_WIDTH + SLIDER_WIDTH));
        GUILayout.Label(new GUIContent(q.Name(Korean) + ChangedMark(changed), q.Help(Korean)), _name);
        GUILayout.Label(q.Help(Korean), _small);
        GUILayout.EndVertical();
        bool newOn = SAINLayout.Toggle(on, new GUIContent(on ? T("켬", "On") : T("끔", "Off")), EUISoundType.MenuCheckBox, GUILayout.Width(VALUE_WIDTH + RESET_WIDTH), GUILayout.Height(28f));
        GUILayout.EndHorizontal();

        if (newOn != on)
        {
            q.Set(view, newOn ? 1f : 0f);
            ConfigEditingTracker.Add(q.NameEn, newOn);
        }
    }

    // A thin yellow mark on the slider where zzap's own value sits.
    private static void DrawZzapTick(QuickSetting q)
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }
        Rect rect = GUILayoutUtility.GetLastRect();
        float t = Mathf.InverseLerp(q.Min, q.Max, q.ToSlider(q.ZzapValue));
        float x = rect.x + 6f + (rect.width - 12f) * t;
        GUI.DrawTexture(new Rect(x - 1f, rect.y - 3f, 2f, rect.height + 6f), _tickTexture);
    }

    private static string Format(float value)
    {
        return System.Math.Round(value, 2).ToString("0.##");
    }

    // ---------------------------------------------------------------- styles

    private static void EnsureStyles()
    {
        if (_card != null)
        {
            return;
        }
        _tickTexture = Solid(new Color(0.98f, 0.8f, 0.08f, 1f));

        _card = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true,
            richText = true,
            fontSize = 14,
            padding = new RectOffset(12, 10, 7, 7),
            margin = new RectOffset(0, 0, 3, 3),
        };
        SetBackground(_card, new Color(0.09f, 0.09f, 0.11f, 0.95f), new Color(0.14f, 0.14f, 0.17f, 0.98f));
        SetText(_card, new Color(0.89f, 0.89f, 0.91f));

        _cardSelected = new GUIStyle(_card);
        SetBackground(_cardSelected, new Color(0.05f, 0.25f, 0.33f, 0.95f), new Color(0.07f, 0.3f, 0.4f, 0.98f));
        SetText(_cardSelected, Color.white);

        _cardLegacy = new GUIStyle(_card);
        SetText(_cardLegacy, new Color(0.55f, 0.55f, 0.6f));

        _panel = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(12, 12, 8, 10),
            margin = new RectOffset(0, 0, 0, 0),
        };
        SetBackground(_panel, new Color(0.08f, 0.08f, 0.1f, 0.92f), new Color(0.08f, 0.08f, 0.1f, 0.92f));

        _panelBlue = new GUIStyle(_panel);
        SetBackground(_panelBlue, new Color(0.04f, 0.16f, 0.22f, 0.95f), new Color(0.04f, 0.16f, 0.22f, 0.95f));

        _title = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 15, alignment = TextAnchor.MiddleLeft };
        SetText(_title, Color.white);

        _name = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14, alignment = TextAnchor.LowerLeft };
        SetText(_name, new Color(0.93f, 0.93f, 0.95f));

        _small = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12, wordWrap = true, alignment = TextAnchor.UpperLeft };
        SetText(_small, new Color(0.63f, 0.63f, 0.67f));

        _muted = new GUIStyle(_small) { fontSize = 12 };

        _section = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        SetText(_section, new Color(0.98f, 0.8f, 0.08f));

        _value = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        SetText(_value, Color.white);
    }

    private static Texture2D Solid(Color color)
    {
        var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private static void SetBackground(GUIStyle style, Color normal, Color hover)
    {
        var normalTexture = Solid(normal);
        var hoverTexture = Solid(hover);
        style.normal.background = normalTexture;
        style.onNormal.background = normalTexture;
        style.hover.background = hoverTexture;
        style.onHover.background = hoverTexture;
        style.active.background = hoverTexture;
        style.onActive.background = hoverTexture;
        style.focused.background = normalTexture;
        style.onFocused.background = normalTexture;
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
