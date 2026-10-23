using System.Collections.Generic;
using SAIN.Plugin;
using SAIN.Preset;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SAIN.Preset.Shared.Personalities.BasePersonality;
using static SAIN.Attributes.AttributesGUI;

namespace SAIN.Editor.GUISections;

public static class BotPersonalityEditor
{
    private static UnityEngine.Vector2 _scroll;

    // zzap fork (2026-10-18, mockup 5): personalities listed on the left, the picked one's settings on the right (was a grid
    // of buttons with an empty page until one was clicked, and a second Save button - Save is in the header now).
    public static void PersonalityMenu()
    {
        ZzapUI.EnsureStyles();
        if (_options.Count == 0)
        {
            _options.AddRange(SAINPresetClass.Instance.PersonalityManager.PersonalityDictionary.Keys);
        }
        if (_options.Count == 0)
        {
            return;
        }
        if (_selected == EPersonality.None || !_options.Contains(_selected))
        {
            _selected = _options[0];
        }
        EPersonality current = _selected;

        UnityEngine.GUILayout.BeginHorizontal();
        UnityEngine.GUILayout.BeginVertical(ZzapUI.Panel1, UnityEngine.GUILayout.Width(ZzapUI.SIDE_WIDTH), UnityEngine.GUILayout.Height(ZzapUI.PANEL_HEIGHT));
        UnityEngine.GUILayout.Label(EditorText.UI("PERSONALITIES"), ZzapUI.Section);
        UnityEngine.GUILayout.Label(
            EditorText.UI("Modify Individual Personality settings for how they are assigned to bots, and what each personality does for a bot's behavior."),
            ZzapUI.Small
        );
        UnityEngine.GUILayout.Space(6f);
        foreach (var personality in _options)
        {
            var captured = personality;
            if (ZzapUI.ListItem(EditorText.Value(personality), null, personality == current))
            {
                ZzapUI.Defer(() =>
                {
                    _selected = captured;
                    _scroll = UnityEngine.Vector2.zero;
                });
            }
        }
        UnityEngine.GUILayout.FlexibleSpace();
        UnityEngine.GUILayout.EndVertical();

        UnityEngine.GUILayout.Space(12f);

        UnityEngine.GUILayout.BeginVertical(ZzapUI.Panel1, UnityEngine.GUILayout.Height(ZzapUI.PANEL_HEIGHT), UnityEngine.GUILayout.ExpandWidth(true));
        ZzapUI.PanelHeading(EditorText.Value(current), EditorText.Setting(PersonalitySettingsClass.DescriptionOf(current)));
        _scroll = SAINLayout.BeginScrollView(_scroll, UnityEngine.GUILayout.ExpandHeight(true));
        if (SAINPresetClass.Instance.PersonalityManager.PersonalityDictionary.TryGetValue(current, out var settings))
        {
            EditAllValuesInObj(settings, out bool newEdit);
        }
        SAINLayout.EndScrollView();
        UnityEngine.GUILayout.EndVertical();
        UnityEngine.GUILayout.EndHorizontal();
    }

    public static EPersonality SelectPersonality(EPersonality selected, float height, int optionsPerLine)
    {
        if (_options.Count == 0)
        {
            _options.AddRange(SAINPresetClass.Instance.PersonalityManager.PersonalityDictionary.Keys);
        }
        return BuilderClass.SelectionGrid(selected, height, optionsPerLine, _options);
    }

    private static EPersonality _selected = EPersonality.None;
    public static bool PersonalitiesWereEdited
    {
        get { return ConfigEditingTracker.UnsavedChanges; }
    }

    private static List<EPersonality> _options = new();

    private static readonly Dictionary<string, bool> OpenPersMenus = new();
}
