using System;
using BepInEx;
using EFT.Console.Core;
using EFT.UI;
using SAIN.Editor.Util;
using SAIN.Plugin;
using SAIN.Preset;
using SAIN.Preset.Shared;
using SAIN.Preset.Shared.Preset;
using UnityEngine;
using static SAIN.Editor.RectLayout;
using static SAIN.Editor.SAINLayout;
using static SAIN.Editor.Sounds;
using ColorsClass = SAIN.Editor.Util.ColorsClass;

namespace SAIN.Editor;

public static class SAINEditor
{
    private static string T(string ko, string en) => EditorText.Korean ? ko : en;
    static SAINEditor()
    {
        ConsoleScreen.Processor.RegisterCommand("saineditor", new Action(ToggleGUI));
    }

    public static void Init()
    {
        CursorSettings.InitCursor();
    }

    public static bool AdvancedBotConfigs
    {
        get { return PresetHandler.EditorDefaults.AdvancedBotConfigs; }
    }

    [ConsoleCommand("Toggle SAIN GUI Editor")]
    private static void ToggleGUI()
    {
        if (!DisplayingWindow && !PresetHandler.CanEditCurrentPreset)
        {
            return;
        }
        DisplayingWindow = !DisplayingWindow;
    }

    private static float CheckKeyLimiter;
    public static bool ShiftKeyPressed;
    public static bool CtrlKeyPressed;
    private static bool ToggleKeyPressed;
    private static bool EscapeKeyPressed;

    private static void CheckKeys()
    {
        if (CheckKeyLimiter < Time.time)
        {
            CheckKeyLimiter = Time.time + 0.1f;
            ShiftKeyPressed = Input.GetKey(KeyCode.LeftShift);
            CtrlKeyPressed = Input.GetKey(KeyCode.LeftControl);
            ToggleKeyPressed = Input.GetKeyDown(SAINPlugin.OpenEditorConfigEntry.Value.MainKey);
            EscapeKeyPressed = Input.GetKeyDown(KeyCode.Escape);
        }
    }

    public static void ManualUpdate()
    {
        // If the server locks editing (forced default, presets off, or access revoked) while the panel is open, close it.
        if (DisplayingWindow && !PresetHandler.CanEditCurrentPreset)
        {
            DisplayingWindow = false;
        }

        if (DisplayingWindow)
        {
            CursorSettings.SetUnlockCursor(0, true);
            MouseFunctions.Update();
        }
        else
        {
            CheckKeys();
        }

        if ((SAINPlugin.OpenEditorConfigEntry.Value.IsDown() && !DisplayingWindow) || SAINPlugin.OpenEditorButton.Value)
        {
            if (SAINPlugin.OpenEditorButton.Value)
            {
                SAINPlugin.OpenEditorButton.BoxedValue = false;
                SAINPlugin.OpenEditorButton.Value = false;
            }
            ToggleGUI();
        }
    }

    public static void LateUpdate()
    {
        if (DisplayingWindow)
        {
            CursorSettings.SetUnlockCursor(0, true);
        }
    }

    public static void OnGUI()
    {
        if (DisplayingWindow)
        {
            if (!CacheCreated)
            {
                CacheCreated = true;
                ColorsClass.CreateCache();
                TexturesClass.CreateCache();
                StylesClass.CreateCache();
            }

            CursorSettings.SetUnlockCursor(0, true);
            GUIUtility.ScaleAroundPivot(ScaledPivot, Vector2.zero);
            MainWindow = GUI.Window(0, MainWindow, MainWindowFunc, EditorText.UI("SAIN AI Settings Editor"), GetStyle(Style.window));
            UnityInput.Current.ResetInputAxes();
            ConfigEditingTracker.Update();
        }
    }

    private static bool CacheCreated;

    private static void MainWindowFunc(int TWCWindowID)
    {
        GUI.FocusWindow(TWCWindowID);
        ZzapUI.RunDeferred();
        CheckKeys();
        if (ToggleKeyPressed || EscapeKeyPressed)
        {
            ToggleGUI();
            return;
        }
        // Keep Layout/Repaint intact, but route input exclusively to an open overlay.
        EventType inputType = Event.current.type;
        bool overlay = BotSelectionClass.HelpOpen || BotSlotsGUI.OverlayOpen;
        if (overlay && inputType != EventType.Layout && inputType != EventType.Repaint) Event.current.type = EventType.Ignore;
        CreateDragBar();
        CreateTopBarOptions();
        EEditorTab selectedTab = EditTabsClass.TabSelectMenu(35f, 3f, 0.5f);
        float space = DragRect.height + EditTabsClass.TabMenuRect.height;
        Space(space + 10f);
        GUITabs.CreateTabs(selectedTab);
        if (overlay && inputType != EventType.Layout && inputType != EventType.Repaint) Event.current.type = inputType;
        if (BotSelectionClass.HelpOpen) BotSelectionClass.DrawHelp();
        else if (BotSlotsGUI.OverlayOpen) BotSlotsGUI.DrawOverlay();
        else DrawTooltip();
    }

    // zzap fork (2026-10-18, mockup 5): header = name, preset in yellow, a yellow "N unsaved changes" pill; on the right the
    // language, advanced and a wide yellow Save button.
    private static void CreateDragBar()
    {
        ZzapUI.EnsureStyles();
        if (Event.current.type == EventType.Repaint)
        {
            ZzapUI.Fill(HeaderRect, ZzapUI.Panel);
            ZzapUI.Fill(new Rect(0, HeaderRect.yMax - 1f, HeaderRect.width, 1f), ZzapUI.Line);
        }
        string title =
            $"<b>{EditorText.UI("SAIN zzap settings (F6)")}</b>  <color=#facc15>{SAINVersionInfo.ZzapRelease}</color>   |   "
            + $"{EditorText.UI("Preset")}: <color=#facc15><b>{QuickSettings.DisplayName(SAINPlugin.LoadedPreset.Info.Name, EditorText.Korean)}</b></color>";
        var content = new GUIContent(title);
        float titleWidth = Mathf.Min(ZzapUI.Header.CalcSize(content).x + 4f, DragRect.width - 230f);
        GUI.Label(new Rect(DragRect.x, DragRect.y, titleWidth, DragRect.height), content, ZzapUI.Header);
        if (Event.current.type == EventType.Layout && (_unsavedCount != ConfigEditingTracker.EditedConfigValues.Count || _unsavedKorean != EditorText.Korean))
        {
            _unsavedCount = ConfigEditingTracker.EditedConfigValues.Count;
            _unsavedKorean = EditorText.Korean;
            _unsavedText = T($"저장 안 한 변경 {_unsavedCount}개", $"{_unsavedCount} unsaved changes");
        }
        GUI.Label(new Rect(SaveAllRect.x, DragRect.y, 154f, DragRect.height), _unsavedText, ZzapUI.Count);
        GUI.DragWindow(DragRect);
    }

    public static string ExceptionString = string.Empty;
    private static string _unsavedText;
    private static int _unsavedCount = -1;
    private static bool _unsavedKorean;

    private static readonly GUIContent SaveContent = new(
        "Save All Changes",
        $"Save all changes to the SAIN server mod for preset '{SAINPlugin.LoadedPreset.Info.Name}'"
    );

    private static void CreateTopBarOptions()
    {
        SaveContent.text = T("저장", "Save");
        SaveContent.tooltip = ConfigEditingTracker.UnsavedChanges ? ConfigEditingTracker.GetUnsavedValuesString() : EditorText.UI("Nothing to save yet.");

        bool advancedEnabled = PresetHandler.EditorDefaults.AdvancedBotConfigs;
        // No "[OFF]"/"[Off]": the Nalasmalssami translation mod rewrites a bracketed off on screen to "[꺼짐]" even in English (2026-11-03).
        var advContent = new GUIContent(
            advancedEnabled ? T("고급 설정: 켬", "Advanced settings: On") : T("고급 설정: 끔", "Advanced settings: Off"),
            EditorText.UI("Also show the settings marked 'Advanced' (for experienced users).")
        );
        if (GUI.Button(AdvRect, advContent, advancedEnabled ? ZzapUI.Pill : ZzapUI.Button))
        {
            PlaySound(EUISoundType.MenuEscape);
            ZzapUI.Defer(() => { PresetHandler.EditorDefaults.AdvancedBotConfigs = !advancedEnabled; PresetHandler.ExportEditorDefaults(); });
        }

        // zzap fork: Korean / English switch for the whole editor.
        bool korean = EditorText.Korean;
        var langContent = new GUIContent(korean ? "[한] / EN" : "한 / [EN]", EditorText.UI("Language: Korean / English"));
        if (GUI.Button(LangRect, langContent, ZzapUI.Button))
        {
            PlaySound(EUISoundType.MenuEscape);
            ZzapUI.Defer(() => { PresetHandler.EditorDefaults.KoreanUI = !korean; PresetHandler.ExportEditorDefaults(); });
        }

        GUI.enabled = PresetHandler.CanEditCurrentPreset;
        var compactSave = new Rect(SaveAllRect.x + 162f, SaveAllRect.y, 56f, SaveAllRect.height);
        if (GUI.Button(compactSave, SaveContent, ZzapUI.ButtonPrimary))
        {
            PlaySound(EUISoundType.InsuranceInsured);
            ZzapUI.Defer(() => SAINPresetClass.ExportAll(SAINPlugin.LoadedPreset));
        }
        GUI.enabled = true;

        if (GUI.Button(new Rect(compactSave.xMax + 6f, compactSave.y, 30f, compactSave.height), "?", ZzapUI.Button)) BotSelectionClass.OpenHelp();

        if (GUI.Button(ExitRect, new GUIContent("X", EditorText.UI("Close (F6)")), ZzapUI.Button))
        {
            PlaySound(EUISoundType.MenuEscape);
            ToggleGUI();
        }
    }

    private static void DrawTooltip()
    {
        if (string.IsNullOrEmpty(GUI.tooltip))
        {
            //var sb = new StringBuilder();
            //
            //sb.AppendLine(Event.current.rawType.ToString());
            //sb.AppendLine(Event.current.type.ToString());
            //sb.AppendLine(Event.current.control.ToString());
            //sb.AppendLine(Event.current.commandName.ToString());
            //GUI.tooltip = sb.ToString();
            return;
        }

        const int width = 250;
        var x = Event.current.mousePosition.x;
        var y = Event.current.mousePosition.y + 15;
        if (x > Screen.width / 3)
        {
            x -= width;
        }

        var ToolTipStyle = GetStyle(Style.tooltip);
        var height = ToolTipStyle.CalcHeight(new GUIContent(GUI.tooltip), width) + 10;
        GUI.Box(new Rect(x, y, width, height), GUI.tooltip, ToolTipStyle);
    }

    public static bool DisplayingWindow
    {
        get { return CursorSettings.DisplayingWindow; }
        set { CursorSettings.DisplayingWindow = value; }
    }

    public static Rect OpenTabRect = new(0, 0, MainWindow.width, 1000f);

}
