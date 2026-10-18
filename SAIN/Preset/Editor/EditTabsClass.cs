using System.Collections.Generic;
using UnityEngine;
using static SAIN.Editor.RectLayout;
using static SAIN.Editor.SAINLayout;

namespace SAIN.Editor;

internal class EditTabsClass
{
    static EditTabsClass()
    {
        TabClasses = new Dictionary<EEditorTab, TabClass>
        {
            // zzap fork (2026-10-18, mockup 5): quick settings first, the full global settings next.
            {
                EEditorTab.Quick,
                new TabClass { Name = "★ Quick Settings", ToolTip = "Pick a preset, the low-spec switch and the most changed values in plain words." }
            },
            {
                EEditorTab.Home,
                new TabClass { Name = "Global Settings (all)", ToolTip = "Every global SAIN setting, one group at a time (pick a group on the left or search)." }
            },
            {
                EEditorTab.BotSettings,
                new TabClass
                {
                    Name = "Bot Settings",
                    ToolTip =
                        "Modify Settings that are unique to particular bot types for individual difficulties. Difficulty is determined on spawn by EFT, and is changed by selecting the Difficulty value when starting a raid. As Online is a mix of all difficulties.",
                }
            },
            {
                EEditorTab.Personalities,
                new TabClass
                {
                    Name = "Personalities",
                    ToolTip =
                        "Modify Individual Personality settings for how they are assigned to bots, and what each personality does for a bot's behavior.",
                }
            },
            {
                EEditorTab.EquipmentStealth,
                new TabClass { Name = "Equipment Stealth", ToolTip = "Modify the stealth value that certain pieces of equipment provide." }
            },
            {
                EEditorTab.Advanced,
                new TabClass
                {
                    Name = "Advanced Options",
                    ToolTip = "Edit at your own risk. Enable additional advanced config options here",
                }
            },
        };

        List<string> names = new();
        List<string> tooltips = new();
        foreach (var tab in TabClasses)
        {
            names.Add(tab.Value.Name);
            tooltips.Add(tab.Value.ToolTip);
        }
        Tabs = names.ToArray();
        TabTooltips = tooltips.ToArray();
    }

    private const float TabMenuHeight = 42f;

    // zzap fork (2026-10-18, mockup 5): flat tabs sized to their text, the selected one in yellow with a yellow underline
    // (was equal-width red blocks that grew on hover). A click switches on the next layout pass, so the old tab and the new
    // one never mix in one frame (IMGUI complains when a frame's controls change between its layout and draw passes).
    public static EEditorTab TabSelectMenu(float minHeight = 30, float speed = 3, float closeSpeedMulti = 0.66f)
    {
        ZzapUI.EnsureStyles();
        if (_pendingTab.HasValue && Event.current.type == EventType.Layout)
        {
            SelectedTab = _pendingTab.Value;
            _pendingTab = null;
        }
        TabMenuRect = new Rect(0, HeaderRect.height, MainWindow.width, TabMenuHeight);

        // zzap fork: tab names/tooltips go through EditorText so the Korean/English switch applies right away.
        bool korean = EditorText.Korean;
        if (_localizedTabs == null || _localizedFor != korean)
        {
            _localizedFor = korean;
            _localizedTabs = new string[Tabs.Length];
            _localizedTooltips = new string[TabTooltips.Length];
            for (int i = 0; i < Tabs.Length; i++)
            {
                _localizedTabs[i] = EditorText.UI(Tabs[i]);
                _localizedTooltips[i] = EditorText.UI(TabTooltips[i]);
            }
        }

        bool repaint = Event.current.type == EventType.Repaint;
        if (repaint)
        {
            ZzapUI.Fill(TabMenuRect, ZzapUI.Bg);
            ZzapUI.Fill(new Rect(TabMenuRect.x, TabMenuRect.yMax - 1f, TabMenuRect.width, 1f), ZzapUI.Line);
        }

        float x = 16f;
        int index = 0;
        foreach (var tab in TabClasses)
        {
            bool selected = tab.Key == SelectedTab;
            var style = selected ? ZzapUI.TabTextSelected : ZzapUI.TabText;
            var label = new GUIContent(_localizedTabs[index]);
            float width = style.CalcSize(label).x + 40f;
            var rect = new Rect(x, TabMenuRect.y, width, TabMenuRect.height);
            if (repaint)
            {
                if (selected)
                {
                    ZzapUI.Fill(rect, ZzapUI.Panel);
                    ZzapUI.Fill(new Rect(rect.x, rect.yMax - 3f, rect.width, 3f), ZzapUI.Yellow);
                }
                else if (rect.Contains(Event.current.mousePosition))
                {
                    ZzapUI.Fill(rect, ZzapUI.PanelHover);
                }
            }
            GUI.Label(rect, label, style);
            if (GUI.Button(rect, new GUIContent(string.Empty, _localizedTooltips[index]), GUIStyle.none) && !selected)
            {
                Sounds.PlaySound(EFT.UI.EUISoundType.ButtonClick);
                _pendingTab = tab.Key;
            }
            x += width + 2f;
            index++;
        }
        return SelectedTab;
    }

    private static EEditorTab? _pendingTab;
    private static string[] _localizedTabs;
    private static string[] _localizedTooltips;
    private static bool _localizedFor;
    public static Rect TabMenuRect = new(0, 44f, 1920f, TabMenuHeight);

    public static void BeginScrollView()
    {
        TabClasses[SelectedTab].Scroll = SAINLayout.BeginScrollView(TabClasses[SelectedTab].Scroll, MainWindow.width - 20f);
        BeginVertical();
    }

    public static void EndScrollView()
    {
        EndVertical();
        SAINLayout.EndScrollView();
    }

    public static bool IsTabSelected(EEditorTab tab)
    {
        return SelectedTab == tab;
    }

    public static EEditorTab SelectedTab = EEditorTab.Quick;
    public static readonly string[] Tabs;
    public static readonly string[] TabTooltips;
    public static readonly Dictionary<EEditorTab, TabClass> TabClasses;
}

public sealed class TabClass
{
    public string Name;
    public string ToolTip;
    public Vector2 Scroll = Vector2.zero;
}

public enum EEditorTab
{
    Quick,
    Home,
    BotSettings,
    Personalities,
    EquipmentStealth,
    Advanced,
}
