using System;
using System.Collections.Generic;
using EFT.UI;
using SAIN.Attributes;
using SAIN.Editor.Util;
using SAIN.Extensions;
using SAIN.Plugin;
using SAIN.Preset;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using UnityEngine;
using static SAIN.Editor.SAINLayout;

namespace SAIN.Editor;

public static class BotSelectionClass
{
    static BotSelectionClass()
    {
        List<string> sections = new();
        foreach (var type in BotTypeDefinitions.BotTypes.Values)
        {
            if (!sections.Contains(type.Section))
            {
                sections.Add(type.Section);
            }
        }

        Sections = sections.ToArray();
        SectionOpens = new bool[Sections.Length];
        if (SectionOpens.Length > 0)
        {
            SectionOpens[0] = true;
        }
    }

    private static bool[] SectionOpens;
    private static UnityEngine.Vector2 _botScroll;
    private static UnityEngine.Vector2 _groupScroll;
    private static UnityEngine.Vector2 _valueScroll;
    private static string _selectedCategory;
    private const float GROUP_WIDTH = 270f;
    private const float COMBO_WIDTH = 240f;

    // zzap fork (2026-10-18, mockup 5): three columns - bot types and difficulties | setting groups | the values. The old page
    // was a row of section buttons, a difficulty row, then every group and every setting behind its own fold-out button, and
    // showed "no bot types selected" until all of that was clicked through. Save is in the header now.
    public static void Menu()
    {
        ZzapUI.EnsureStyles();
        var container = SettingsContainers.GetContainer(typeof(SAINSettingsClass), "Select Options to Edit");
        string search = container.SearchPattern ?? string.Empty;

        BeginHorizontal();
        BotColumn();
        Space(12f);
        GroupColumn(container, search.Length > 0);
        Space(12f);
        ValueColumn(container, search.ToLowerInvariant());
        EndHorizontal();
    }

    private static void BotColumn()
    {
        GUILayout.BeginVertical(ZzapUI.Panel1, Width(ZzapUI.SIDE_WIDTH), Height(ZzapUI.PANEL_HEIGHT));
        Label(EditorText.UI("BOT TYPES"), ZzapUI.Section);
        Label(EditorText.UI("Pick one or more. Values on the right change for every bot type and difficulty picked here."), ZzapUI.Small);
        Space(4f);
        _botScroll = BeginScrollView(_botScroll, GUILayout.ExpandHeight(true));
        for (int i = 0; i < Sections.Length; i++)
        {
            string section = Sections[i];
            int picked = 0;
            foreach (var bot in SelectedBotTypes)
            {
                if (bot.Section == section)
                {
                    picked++;
                }
            }
            int index = i;
            if (ZzapUI.ListItem((SectionOpens[i] ? "▾  " : "▸  ") + section, picked > 0 ? string.Format(EditorText.UI("{0} picked"), picked) : null, false))
            {
                ZzapUI.Defer(() => SectionOpens[index] = !SectionOpens[index]);
            }
            if (!SectionOpens[i])
            {
                continue;
            }
            foreach (var bot in BotTypeDefinitions.BotTypes.Values)
            {
                if (bot.Section != section)
                {
                    continue;
                }
                bool selected = SelectedBotTypes.Contains(bot);
                var captured = bot;
                if (ZzapUI.ListItem(bot.Name, null, selected, bot.Description, 22f, 28f))
                {
                    ZzapUI.Defer(() =>
                    {
                        if (!SelectedBotTypes.Remove(captured))
                        {
                            SelectedBotTypes.Add(captured);
                        }
                    });
                }
            }
        }
        EndScrollView();

        Space(8f);
        Label(EditorText.UI("DIFFICULTY"), ZzapUI.Section);
        BeginHorizontal();
        foreach (var difficulty in BotDifficultyOptions)
        {
            bool selected = SelectedDifficulties.Contains(difficulty);
            var captured = difficulty;
            if (GUILayout.Button(EditorText.Value(difficulty), selected ? ZzapUI.Pill : ZzapUI.Button, Height(30f)))
            {
                Sounds.PlaySound(EUISoundType.MenuCheckBox);
                ZzapUI.Defer(() =>
                {
                    if (!SelectedDifficulties.Remove(captured))
                    {
                        SelectedDifficulties.Add(captured);
                    }
                });
            }
        }
        EndHorizontal();
        Space(4f);
        if (GUILayout.Button(new GUIContent(EditorText.UI("Clear selection"), EditorText.UI("Clear all selected bot types and difficulties")), ZzapUI.Button, Height(28f)))
        {
            Sounds.PlaySound(EUISoundType.ButtonBottomBarClick);
            ZzapUI.Defer(() =>
            {
                SelectedBotTypes.Clear();
                SelectedDifficulties.Clear();
            });
        }
        GUILayout.EndVertical();
    }

    private static void GroupColumn(SettingsContainer container, bool searching)
    {
        GUILayout.BeginVertical(ZzapUI.Panel1, Width(GROUP_WIDTH), Height(ZzapUI.PANEL_HEIGHT));
        container.SearchPattern = ZzapUI.Search(container.SearchPattern, EditorText.UI("Search settings"));
        Space(8f);
        if (_selectedCategory == null && container.Categories.Count > 0)
        {
            _selectedCategory = container.Categories[0].CategoryInfo.EnglishName;
        }
        _groupScroll = BeginScrollView(_groupScroll, GUILayout.ExpandHeight(true));
        foreach (var category in container.Categories)
        {
            int count = category.OptionCount(out _);
            if (count == 0)
            {
                continue;
            }
            string key = category.CategoryInfo.EnglishName;
            bool selected = !searching && key == _selectedCategory;
            if (ZzapUI.ListItem(category.CategoryInfo.Name, count.ToString(), selected, category.CategoryInfo.Description))
            {
                ZzapUI.Defer(() =>
                {
                    _selectedCategory = key;
                    container.SearchPattern = string.Empty;
                    _valueScroll = UnityEngine.Vector2.zero;
                });
            }
        }
        EndScrollView();
        GUILayout.EndVertical();
    }

    private static void ValueColumn(SettingsContainer container, string search)
    {
        GUILayout.BeginVertical(ZzapUI.Panel1, Height(ZzapUI.PANEL_HEIGHT), GUILayout.ExpandWidth(true));
        if (SelectedBotTypes.Count == 0 || SelectedDifficulties.Count == 0)
        {
            ZzapUI.PanelHeading(
                EditorText.UI("Pick bot types and difficulties"),
                SelectedBotTypes.Count == 0
                    ? EditorText.UI("No bot type picked yet - pick at least one on the left.")
                    : EditorText.UI("No difficulty picked yet - pick at least one on the left, under the bot types.")
            );
            Label(
                EditorText.UI("1. Open a section on the left (PMC, Scav...) and click the bot types. 2. Click one or more difficulties under them. 3. Pick a group in the middle; its values show here, one row per bot type and difficulty."),
                ZzapUI.Small
            );
            FlexibleSpace();
            GUILayout.EndVertical();
            return;
        }

        bool searching = search.Length > 0;
        Category shown = null;
        if (!searching)
        {
            foreach (var category in container.Categories)
            {
                if (category.CategoryInfo.EnglishName == _selectedCategory)
                {
                    shown = category;
                }
            }
            shown ??= container.Categories.Count > 0 ? container.Categories[0] : null;
        }
        string combos = string.Format(EditorText.UI("{0} bot types × {1} difficulties"), SelectedBotTypes.Count, SelectedDifficulties.Count);
        if (searching)
        {
            ZzapUI.PanelHeading(string.Format(EditorText.UI("Search: '{0}'"), search), combos);
        }
        else if (shown != null)
        {
            ZzapUI.PanelHeading(shown.CategoryInfo.Name, string.IsNullOrEmpty(shown.CategoryInfo.Description) ? combos : shown.CategoryInfo.Description + "   ·   " + combos);
        }

        _valueScroll = BeginScrollView(_valueScroll, GUILayout.ExpandHeight(true));
        try
        {
            foreach (var category in container.Categories)
            {
                if (searching ? false : category != shown)
                {
                    continue;
                }
                foreach (var fieldAtt in category.FieldAttributesList)
                {
                    if (fieldAtt.DoNotShowGUI)
                    {
                        continue;
                    }
                    if (searching && !fieldAtt.Name.ToLower().Contains(search) && fieldAtt.EnglishName?.ToLower().Contains(search) != true)
                    {
                        continue;
                    }
                    EditField(category, fieldAtt);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
        EndScrollView();
        GUILayout.EndVertical();
    }

    // One setting: its name, then one row per picked bot type × difficulty (a single row when only one is picked).
    private static void EditField(Category category, ConfigInfoClass fieldAtt)
    {
        entryConfig ??= new GUIEntryConfig();
        bool single = SelectedBotTypes.Count * SelectedDifficulties.Count == 1;
        if (!single)
        {
            Space(6f);
            Label(new GUIContent(fieldAtt.Name, fieldAtt.Description), ZzapUI.Name);
            if (!string.IsNullOrEmpty(fieldAtt.Description))
            {
                Label(fieldAtt.Description, ZzapUI.Small);
            }
        }
        for (int k = 0; k < SelectedBotTypes.Count; k++)
        {
            var bot = SelectedBotTypes[k];
            if (!SAINPlugin.LoadedPreset.BotSettings.SAINSettings.TryGetValue(bot.WildSpawnType, out var settings))
            {
                continue;
            }
            for (int t = 0; t < SelectedDifficulties.Count; t++)
            {
                var difficulty = SelectedDifficulties[t];
                if (!settings.Settings.TryGetValue(difficulty.ToESain(), out var SAINSettings))
                {
                    continue;
                }
                object categoryValue = category.GetValue(SAINSettings);
                object value = fieldAtt.GetValue(categoryValue);
                BeginHorizontal(single ? 8f : 24f);
                if (single)
                {
                    Label(new GUIContent(fieldAtt.Name, fieldAtt.Description), ZzapUI.Name, Width(ZzapUI.NAME_WIDTH), GUILayout.MinHeight(AttributesGUI.RowHeight));
                }
                else
                {
                    Label($"{bot.Name} · {EditorText.Value(difficulty)}", ZzapUI.Small, Width(COMBO_WIDTH), Height(AttributesGUI.RowHeight));
                }
                value = AttributesGUI.EditFloatBoolInt(ref value, categoryValue, fieldAtt, entryConfig, 0, out bool newEdit, false, false);
                if (newEdit)
                {
                    ConfigEditingTracker.Add(fieldAtt.Name, value);
                }
                fieldAtt.SetValue(categoryValue, value);
                EndHorizontal(8f);
            }
        }
        ZzapUI.Separator(4f);
    }

    public static readonly string[] Sections;

    private static readonly List<BotType> SelectedBotTypes = new();

    public static readonly BotDifficulty[] BotDifficultyOptions =
    [
        BotDifficulty.easy,
        BotDifficulty.normal,
        BotDifficulty.hard,
        BotDifficulty.impossible,
    ];
    public static readonly List<BotDifficulty> SelectedDifficulties = new();

    public static bool BotSettingsWereEdited;

    private static GUIEntryConfig entryConfig;
}
