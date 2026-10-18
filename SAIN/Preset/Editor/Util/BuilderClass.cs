using System.Collections.Generic;
using System.Linq;
using EFT.UI;
using SAIN.Attributes;
using SAIN.Editor.Util;
using SAIN.Helpers;
using SAIN.Plugin;
using UnityEngine;
using static SAIN.Editor.SAINLayout;

namespace SAIN.Editor;

public static class BuilderClass
{
    private static float ExpandMenuWidth
    {
        get { return 250f; }
    }

    public static string SearchBox(SettingsContainer container, float height = 30, SearchParams config = null)
    {
        container.SearchPattern = SearchBox(container.SearchPattern, height, config);
        return container.SearchPattern;
    }

    public static string SearchBox(string search, float height = 30, SearchParams config = null)
    {
        config = config ?? new SearchParams { optionHeight = height };
        config.Start();

        Label(EditorText.UI("Search"), config.Label);

        config.Spacing();

        search = TextField(search, null, config.TextField);

        config.Spacing();

        if (Button(EditorText.UI("Clear"), EUISoundType.MenuContextMenu, config.Clear))
        {
            search = string.Empty;
        }

        config.End();
        return search;
    }

    public sealed class SearchParams : GUIParams
    {
        public SearchParams(float height = 30)
            : base(height)
        {
            Options = [EGUIConfig.horizontal, EGUIConfig.startFlexSpace];
        }

        public float labelWidth = 150;

        public GUILayoutOption[] Label
        {
            get { return [GUILayout.Width(labelWidth), Height]; }
        }

        public float textFieldWidth = 250;

        public GUILayoutOption[] TextField
        {
            get { return [GUILayout.Width(textFieldWidth), Height]; }
        }

        public float clearWidth = 75;

        public GUILayoutOption[] Clear
        {
            get { return [GUILayout.Width(labelWidth), Height]; }
        }
    }

    public class GUIParams
    {
        public GUIParams(float height)
        {
            this.optionHeight = height;
        }

        public EGUIConfig[] Options = [EGUIConfig.beginHorizontal, EGUIConfig.endHorizontal];

        public float optionHeight = 30;
        public float optionSpacing = 5;

        public void Spacing()
        {
            GUILayout.Space(optionSpacing);
        }

        public GUILayoutOption Height
        {
            get { return GUILayout.Height(optionHeight); }
        }

        public bool StartFlexSpace
        {
            get { return Options.Contains(EGUIConfig.startFlexSpace); }
        }

        public bool EndFlexSpace
        {
            get { return Options.Contains(EGUIConfig.endFlexSpace); }
        }

        public bool Horizontal
        {
            get { return Options.Contains(EGUIConfig.horizontal); }
        }

        public bool Vertical
        {
            get { return Options.Contains(EGUIConfig.vertical); }
        }

        public bool FixedSpace
        {
            get { return Options.Contains(EGUIConfig.fixedSpace); }
        }

        public float FixedSpaceWidth = 10;

        public void Start()
        {
            if (Horizontal)
            {
                GUILayout.BeginHorizontal();
            }
            else if (Vertical)
            {
                GUILayout.BeginVertical();
            }

            if (FixedSpace)
            {
                GUILayout.Space(FixedSpaceWidth);
            }
            else if (StartFlexSpace)
            {
                GUILayout.FlexibleSpace();
            }
        }

        public void End()
        {
            if (FixedSpace)
            {
                GUILayout.Space(FixedSpaceWidth);
            }
            else if (EndFlexSpace)
            {
                GUILayout.FlexibleSpace();
            }

            if (Horizontal)
            {
                GUILayout.EndHorizontal();
            }
            else if (Vertical)
            {
                GUILayout.EndVertical();
            }
        }
    }

    public enum EGUIConfig
    {
        startFlexSpace,
        endFlexSpace,
        beginHorizontal,
        endHorizontal,
        fixedSpace,
        horizontal,
        vertical,
    }

    public static bool SaveChanges(string toolTip, float height = 35)
    {
        BeginHorizontal();

        bool result = false;
        if (Button(EditorText.UI("Save and Export"), toolTip, EUISoundType.InsuranceInsured, Height(height), Width(500)))
        {
            result = true;
        }

        const float alertWidth = 250f;

        if (ConfigEditingTracker.UnsavedChanges)
        {
            Alert(
                EditorText.UI("Click Save to export changes, and send changes to bots if in-game"),
                EditorText.UI("YOU HAVE UNSAVED CHANGES"),
                height,
                alertWidth,
                ColorNames.LightRed
            );
        }
        else
        {
            Alert(null, null, height, alertWidth);
        }

        EndHorizontal();

        return result;
    }

    public static void Alert(string toolTip, string text = null, float height = 25, float width = 25, ColorNames? colorName = null)
    {
        if (toolTip.IsNullOrEmpty() && text.IsNullOrEmpty())
        {
            Box(string.Empty, Height(height));
            return;
        }

        GUIStyle style = GetStyle(Style.alert);
        text = text ?? "";
        var content = new GUIContent(text, toolTip);
        Box(content, style, Height(height), Width(width));
    }

    public static void Alert(string toolTip, string text = null, float height = 25, ColorNames? colorName = null)
    {
        if (toolTip.IsNullOrEmpty() && text.IsNullOrEmpty())
        {
            Box(string.Empty, Height(height));
            return;
        }

        GUIStyle style = GetStyle(Style.alert);
        text = text ?? string.Empty;
        var content = new GUIContent(text, toolTip);
        Box(content, style, Height(height));
    }

    public static void MinValueBox(object value, params GUILayoutOption[] options)
    {
        if (value == null)
        {
            return;
        }

        Box(value.ToString(), EditorText.UI("Minimum"), options);
    }

    public static void MaxValueBox(object value, params GUILayoutOption[] options)
    {
        if (value == null)
        {
            return;
        }

        Box(value.ToString(), EditorText.UI("Maximum"), options);
    }

    public static object ResultBox(object value, params GUILayoutOption[] options)
    {
        if (value != null)
        {
            Box(value.ToString(), EditorText.UI("The Rounding this option is set to"), options);
            string dirtyString = TextField(value.ToString(), null, options);
            value = CleanString(dirtyString, value);
        }
        return value;
    }

    public static object CleanString(string input, object currentValue)
    {
        if (currentValue is float floatValue)
        {
            currentValue = CleanString(input, floatValue);
        }
        if (currentValue is int intValue)
        {
            currentValue = CleanString(input, intValue);
        }
        if (currentValue is bool boolValue)
        {
            currentValue = CleanString(input, boolValue);
        }
        return currentValue;
    }

    public static float CleanString(string input, float currentValue)
    {
        if (float.TryParse(input, out float result))
        {
            return result;
        }
        return currentValue;
    }

    public static int CleanString(string input, int currentValue)
    {
        if (int.TryParse(input, out int result))
        {
            return result;
        }
        else if (float.TryParse(input, out float floatResult))
        {
            return Mathf.RoundToInt(floatResult);
        }
        return currentValue;
    }

    public static bool CleanString(string input, bool currentValue)
    {
        if (input == true.ToString() || input == false.ToString())
        {
            currentValue = bool.Parse(input);
        }
        return currentValue;
    }

    public static T SelectionGrid<T>(T value, float height, int optionsPerLine, params T[] valueOptions)
    {
        if (valueOptions.Length == 0)
        {
            return value;
        }
        int count = StartSelection(optionsPerLine, height, out GUILayoutOption[] options);
        for (var i = 0; i < valueOptions.Length; i++)
        {
            value = CheckToggle(value, valueOptions[i], options);
            count = HorizontalSpacing(count, optionsPerLine);
        }
        EndHorizontal();
        return value;
    }

    public static T SelectionGrid<T>(T value, params T[] valueOptions)
    {
        return SelectionGrid(value, 25, 3, valueOptions);
    }

    public static T SelectionGrid<T>(T value, List<T> list)
    {
        return SelectionGrid(value, 25, 3, list);
    }

    public static T SelectionGrid<T>(T value, float height, int optionsPerLine, List<T> list)
    {
        int count = StartSelection(optionsPerLine, height, out GUILayoutOption[] options);
        for (var i = 0; i < list.Count; i++)
        {
            value = CheckToggle(value, list[i], options);
            count = HorizontalSpacing(count, optionsPerLine);
        }
        EndHorizontal();
        return value;
    }

    public static T SelectionGrid<T>(T value, List<T> list, float height = 25, int optionsPerLine = 3)
    {
        int count = StartSelection(optionsPerLine, height, out GUILayoutOption[] options);
        for (var i = 0; i < list.Count; i++)
        {
            value = CheckToggle(value, list[i], options);
            count = HorizontalSpacing(count, optionsPerLine);
        }
        EndHorizontal();
        return value;
    }

    private static int StartSelection(int optionsCount, float height, out GUILayoutOption[] options)
    {
        // zzap fork (2026-10-18): options share the row's width (was 1850 px / count, wider than the new side panels).
        BeginHorizontalSpace();
        options = [Height(height), GUILayout.MinWidth(80f), GUILayout.ExpandWidth(true)];
        return 0;
    }

    private static int HorizontalSpacing(int count, int max)
    {
        count++;
        if (count >= max)
        {
            count = 0;
            EndHorizontal();
            BeginHorizontalSpace();
        }
        return count;
    }

    private static readonly EUISoundType SelectionSound = EUISoundType.MenuCheckBox;

    private static T CheckToggle<T>(T value, T newValue, params GUILayoutOption[] options)
    {
        string listValueString = newValue.ToString();
        bool selected = listValueString == value.ToString();

        if (Toggle(selected, EditorText.Value(newValue), SelectionSound, options))
        {
            value = newValue;
        }
        return value;
    }

    public static string SelectionGridExpandHeight(
        Rect menuRect,
        string[] options,
        string selectedOption,
        Rect[] optionRects,
        float min = 15f,
        float incPerFrame = 3f,
        float closeMulti = 0.66f,
        string[] toolTips = null
    )
    {
        BeginGroup(menuRect);

        string tooltip = string.Empty;

        for (int i = 0; i < options.Length; i++)
        {
            if (toolTips != null)
            {
                tooltip = toolTips[i];
            }

            string option = options[i];
            bool selected = selectedOption == option;

            optionRects[i] = AnimateHeight(optionRects[i], selected, menuRect.height, out bool hovering, min, incPerFrame, closeMulti);

            GUIStyle style = StyleHandler(selected, hovering);

            bool toggleActivated = GUI.Button(optionRects[i], new GUIContent(option, tooltip), style);
            if (toggleActivated && selected)
            {
                Sounds.PlaySound(EUISoundType.ButtonClick);
                selectedOption = "None";
            }
            if (toggleActivated && !selected)
            {
                Sounds.PlaySound(EUISoundType.ButtonClick);
                selectedOption = option;
            }
        }
        EndGroup();
        return selectedOption;
    }

    public static GUIStyle StyleHandler(bool selected, bool hovering)
    {
        var style = StylesClass.GetFontStyleDynamic(Style.selectionGrid, selected);
        Color color = selected ? ZzapUI.YellowTint : hovering ? ZzapUI.ControlHover : ZzapUI.Control;
        ApplyToStyle.BackgroundAllStates(ZzapUI.Tex(color), style);
        return style;
    }

    public static string SelectionGridExpandWidth(
        Rect menuRect,
        string[] options,
        string selectedOption,
        Rect[] optionRects,
        float min = 15f,
        float incPerFrame = 3f,
        float closeMulti = 0.66f
    )
    {
        BeginGroup(menuRect);
        for (int i = 0; i < options.Length; i++)
        {
            string option = options[i];
            bool selected = selectedOption == option;

            optionRects[i] = AnimateWidth(optionRects[i], selected, menuRect.width, out bool hovering, min, incPerFrame, closeMulti);

            GUIStyle style = StyleHandler(selected, hovering);
            bool toggleActivated = GUI.Button(optionRects[i], option, style);
            if (toggleActivated && selected)
            {
                Sounds.PlaySound(EUISoundType.ButtonClick);
                selectedOption = "None";
            }
            if (toggleActivated && !selected)
            {
                Sounds.PlaySound(EUISoundType.ButtonClick);
                selectedOption = option;
            }
        }
        EndGroup();
        return selectedOption;
    }

    public static void SelectionGridExpandWidth(
        Rect menuRect,
        string[] options,
        List<string> selectedList,
        Rect[] optionRects,
        float min = 15f,
        float incPerFrame = 3f,
        float closeMulti = 0.66f
    )
    {
        for (int i = 0; i < options.Length; i++)
        {
            string option = options[i];
            bool selected = selectedList.Contains(option);

            optionRects[i] = AnimateWidth(optionRects[i], selected, menuRect.width, out bool hovering, min, incPerFrame, closeMulti);

            GUIStyle style = StyleHandler(selected, hovering);

            bool toggleActivated = GUI.Toggle(optionRects[i], selected, option, style);
            if (toggleActivated != selected)
            {
                Sounds.PlaySound(EUISoundType.MenuDropdownSelect);
                if (selected)
                {
                    selectedList.Remove(option);
                }
                else
                {
                    selectedList.Add(option);
                }
            }
        }
    }

    public static Rect[] VerticalGridRects(Rect MenuRect, int count, float startWidth)
    {
        Rect[] rects = new Rect[count];

        float optionHeight = MenuRect.height / count;
        float X = 0;

        for (int i = 0; i < rects.Length; i++)
        {
            float Y = optionHeight * i;
            rects[i] = new Rect
            {
                x = X,
                y = Y,
                width = startWidth,
                height = optionHeight,
            };
        }
        return rects;
    }

    public static Rect[] HorizontalGridRects(Rect MenuRect, int count, float startHeight)
    {
        Rect[] rects = new Rect[count];

        float optionWidth = MenuRect.width / count;
        float Y = 0;

        for (int i = 0; i < rects.Length; i++)
        {
            float X = optionWidth * i;
            rects[i] = new Rect
            {
                x = X,
                y = Y,
                width = optionWidth,
                height = startHeight,
            };
        }
        return rects;
    }

    private static Rect AnimateHeight(
        Rect rect,
        bool selected,
        float max,
        out bool hovering,
        float min = 15f,
        float incPerFrame = 3f,
        float closeMulti = 0.66f
    )
    {
        Rect detectRect = rect;
        detectRect.height = max;
        hovering = MouseFunctions.IsMouseInside(detectRect);
        rect.height = Animate(rect.height, hovering, selected, max, min, incPerFrame, closeMulti);
        return rect;
    }

    private static Rect AnimateWidth(
        Rect rect,
        bool selected,
        float max,
        out bool hovering,
        float min = 15f,
        float incPerFrame = 3f,
        float closeMulti = 0.66f
    )
    {
        Rect detectRect = rect;
        detectRect.width = max;
        hovering = MouseFunctions.IsMouseInside(detectRect);
        rect.width = Animate(rect.width, hovering, selected, max, min, incPerFrame, closeMulti);
        return rect;
    }

    private static float Animate(
        float current,
        bool mouseHover,
        bool selected,
        float max,
        float min = 15f,
        float incPerFrame = 3f,
        float closeMulti = 0.66f
    )
    {
        if (mouseHover || selected)
        {
            current += incPerFrame;
        }
        else
        {
            current -= incPerFrame * closeMulti;
        }
        current = Mathf.Clamp(current, min, max);
        return current;
    }

    // zzap fork (2026-10-18, mockup 5): one full-width row "▸ name" (was a "+" button and a red name bar).
    public static bool ExpandableMenu(string name, bool value, string description = null, float height = 20f)
    {
        ZzapUI.EnsureStyles();
        height = Mathf.Max(height, 28f);
        Rect rect = GUILayoutUtility.GetRect(10f, height, GUILayout.Height(height), GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint)
        {
            ZzapUI.Fill(rect, rect.Contains(Event.current.mousePosition) ? ZzapUI.PanelHover : ZzapUI.Panel);
            if (value)
            {
                ZzapUI.Fill(new Rect(rect.x, rect.y, 3f, rect.height), ZzapUI.Yellow);
            }
        }
        GUI.Label(new Rect(rect.x + 10f, rect.y, rect.width - 14f, rect.height), (value ? "▾  " : "▸  ") + name, value ? ZzapUI.ListTextSelected : ZzapUI.ListText);
        if (GUI.Button(rect, new GUIContent(string.Empty, description), GUIStyle.none))
        {
            Sounds.PlaySound(EUISoundType.MenuDropdown);
            value = !value;
        }
        return value;
    }

    public static float CreateSlider(float value, float min, float max, float rounding, params GUILayoutOption[] options)
    {
        float oldValue = value;
        value = HorizontalSlider(oldValue, min, max, null, options);
        Backgrounds(value, min, max);
        if (!MouseFunctions.MouseIsMoving)
        {
            value = value.Round(rounding);
        }
        return value;
    }

    public static float CreateSlider(float value, ConfigInfoClass info, GUIEntryConfig config)
    {
        float oldValue = value;
        value = HorizontalSlider(value, info.Min, info.Max, null, config.Toggle);
        Backgrounds(value, info.Min, info.Max);
        Box(value.Round(info.Rounding).ToString(), config.Result);
        if (!MouseFunctions.MouseIsMoving)
        {
            value = value.Round(info.Rounding);
        }
        return value;
    }

    private static void Backgrounds(float value, float min, float max)
    {
        var LastRect = GUILayoutUtility.GetLastRect();
        float progress = (value - min) / (max - min);
        TexturesClass.DrawSliderBackGrounds(progress, LastRect);
    }
}
