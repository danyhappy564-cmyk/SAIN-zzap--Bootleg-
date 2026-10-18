using System.Collections.Generic;
using SAIN.Editor.Util;
using SAIN.Helpers;
using UnityEngine;
using static SAIN.Editor.Util.ApplyToStyle;
using Color = UnityEngine.Color;

namespace SAIN.Editor;

public static class StylesClass
{
    public static void CreateCache()
    {
        if (Styles.Count == 0)
        {
            CreateStyles();
        }
    }

    private static readonly Dictionary<Style, GUIStyle> Styles = new();

    public static GUIStyle GetStyle(Style key)
    {
        if (!Styles.ContainsKey(key))
        {
            Styles.Add(key, new GUIStyle(GUI.skin.box));
        }
        return Styles[key];
    }

    public static GUIStyle GetFontStyleDynamic(Style key, bool active)
    {
        if (!DynamicStyles.ContainsKey(key))
        {
            var originalStyle = GetStyle(key);
            var normalStyle = new GUIStyle(originalStyle) { fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter };
            var activeStyle = new GUIStyle(originalStyle) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            var gold = ColorsClass.GetColor(ColorNames.Gold);
            TextColorAllStates(Color.white, normalStyle);
            TextColorAllStates(gold, activeStyle);

            DynamicStyles.Add(key, new DynamicStyle { Normal = normalStyle, Active = activeStyle });
        }

        DynamicStyle dynamicStyle = DynamicStyles[key];
        return active ? dynamicStyle.Active : dynamicStyle.Normal;
    }

    private static readonly Dictionary<Style, DynamicStyle> DynamicStyles = new();

    private static void CreateStyles()
    {
        GUIStyle LabelStyle = new(GUI.skin.label)
        {
            padding = new RectOffset(4, 4, 0, 0),
            margin = new RectOffset(4, 4, 4, 4),
            border = new RectOffset(4, 4, 0, 0),
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Normal,
        };

        GUIStyle ButtonStyle = new(GUI.skin.button)
        {
            padding = LabelStyle.padding,
            margin = LabelStyle.margin,
            border = LabelStyle.border,
            overflow = LabelStyle.overflow,
            contentOffset = LabelStyle.contentOffset,
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Normal,
        };

        GUIStyle BoxStyle = new(GUI.skin.box)
        {
            padding = LabelStyle.padding,
            margin = LabelStyle.margin,
            border = LabelStyle.border,
            overflow = LabelStyle.overflow,
            contentOffset = LabelStyle.contentOffset,
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Normal,
        };

        GUIStyle ToggleStyle = new(GUI.skin.toggle)
        {
            padding = LabelStyle.padding,
            margin = LabelStyle.margin,
            border = LabelStyle.border,
            overflow = LabelStyle.overflow,
            contentOffset = LabelStyle.contentOffset,
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Normal,
        };

        GUIStyle TextAreaStyle = new(GUI.skin.textArea)
        {
            padding = LabelStyle.padding,
            margin = LabelStyle.margin,
            border = LabelStyle.border,
            overflow = LabelStyle.overflow,
            contentOffset = LabelStyle.contentOffset,
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Normal,
        };

        GUIStyle TextFieldStyle = new(GUI.skin.textField)
        {
            padding = LabelStyle.padding,
            margin = LabelStyle.margin,
            border = LabelStyle.border,
            overflow = LabelStyle.overflow,
            contentOffset = LabelStyle.contentOffset,
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Normal,
        };

        GUIStyle ScrollViewStyle = new(GUI.skin.scrollView)
        {
            padding = LabelStyle.padding,
            margin = LabelStyle.margin,
            border = LabelStyle.border,
            overflow = LabelStyle.overflow,
            contentOffset = LabelStyle.contentOffset,
        };

        GUIStyle WindowStyle = new(GUI.skin.window);

        GUIStyle VerticalScrollbarDownButtonStyle = new(GUI.skin.verticalScrollbarDownButton);

        GUIStyle VerticalScrollbarStyle = new(GUI.skin.verticalScrollbar);

        GUIStyle VerticalScrollbarThumbStyle = new(GUI.skin.verticalScrollbarThumb);

        GUIStyle VerticalScrollbarUpButtonStyle = new(GUI.skin.verticalScrollbarUpButton);

        GUIStyle HorizontalSliderStyle = new(GUI.skin.horizontalSlider)
        {
            padding = LabelStyle.padding,
            margin = LabelStyle.margin,
            border = LabelStyle.border,
            overflow = LabelStyle.overflow,
            contentOffset = LabelStyle.contentOffset,
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Normal,
        };

        GUIStyle HorizontalSliderThumbStyle = new(GUI.skin.horizontalSliderThumb)
        {
            padding = LabelStyle.padding,
            margin = LabelStyle.margin,
            border = LabelStyle.border,
            overflow = LabelStyle.overflow,
            contentOffset = LabelStyle.contentOffset,
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Normal,
        };

        GUIStyle VerticalSliderStyle = new(GUI.skin.verticalSlider);

        GUIStyle VerticalSliderThumbStyle = new(GUI.skin.verticalSliderThumb);

        GUIStyle ListStyle = new(GUI.skin.toggle)
        {
            padding = LabelStyle.padding,
            margin = LabelStyle.margin,
            border = LabelStyle.border,
            overflow = LabelStyle.overflow,
            contentOffset = LabelStyle.contentOffset,
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Normal,
        };

        GUIStyle ToolTipStyle = new(GUI.skin.box)
        {
            padding = new RectOffset(4, 4, 4, 4),
            border = new RectOffset(4, 4, 4, 4),
            wordWrap = true,
            clipping = TextClipping.Clip,
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Normal,
        };

        GUIStyle BlankBackgroundStyle = new(LabelStyle) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Normal };

        GUIStyle AlertStyle = new(GUI.skin.box)
        {
            padding = LabelStyle.padding,
            margin = LabelStyle.margin,
            border = LabelStyle.border,
            overflow = LabelStyle.overflow,
            contentOffset = LabelStyle.contentOffset,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
        };

        // zzap fork (2026-10-18, mockup 5): dark panels, yellow tint + yellow text for "selected", grey controls - no more red
        // blocks (user after the first in-game test: "nothing like the mockup outside Quick settings").
        Texture2D texControl = ZzapUI.Tex(ZzapUI.Control);
        Texture2D texControlHover = ZzapUI.Tex(ZzapUI.ControlHover);
        Texture2D texSelected = ZzapUI.Tex(ZzapUI.YellowTint);
        Texture2D texPanel = ZzapUI.Tex(ZzapUI.Panel);
        Texture2D texPanelHover = ZzapUI.Tex(ZzapUI.PanelHover);
        Texture2D texField = ZzapUI.Tex(ZzapUI.Field);
        Texture2D texWindow = ZzapUI.Tex(ZzapUI.Bg);
        Texture2D texTrack = ZzapUI.Tex(ZzapUI.Track);

        Color text = ZzapUI.Text;
        Color selectedText = ZzapUI.Yellow;

        TextColorAllStates(text, selectedText, WindowStyle, ListStyle, ButtonStyle, ToggleStyle, TextFieldStyle, TextAreaStyle);
        TextColorHover(Color.white, selectedText, ListStyle, ButtonStyle, ToggleStyle);
        TextColorHover(Color.white, TextFieldStyle, TextAreaStyle, WindowStyle);

        TextColorAllStates(selectedText, AlertStyle);
        BackgroundAllStates(texSelected, AlertStyle);

        BackgroundAllStates(null, BlankBackgroundStyle, LabelStyle);
        TextColorAllStates(text, BlankBackgroundStyle, ToolTipStyle, BoxStyle, LabelStyle);
        TextColorHover(Color.white, BlankBackgroundStyle, ToolTipStyle, BoxStyle, LabelStyle);

        GUIStyle[] ToggleStyles = [ToggleStyle, ButtonStyle, ListStyle];
        BackgroundNormal(texControl, texSelected, ToggleStyles);
        BackgroundActive(texControlHover, texSelected, ToggleStyles);
        BackgroundHover(texControlHover, texSelected, ToggleStyles);
        BackgroundFocused(texControl, texSelected, ToggleStyles);

        BackgroundAllStates(texField, TextFieldStyle, TextAreaStyle);

        BackgroundAllStates(null, HorizontalSliderStyle, HorizontalSliderThumbStyle, ScrollViewStyle);

        BackgroundAllStates(texWindow, WindowStyle);

        BackgroundAllStates(texPanel, VerticalScrollbarStyle, VerticalScrollbarUpButtonStyle, VerticalScrollbarDownButtonStyle, BoxStyle);
        BackgroundHover(texPanelHover, VerticalScrollbarStyle, VerticalScrollbarUpButtonStyle, VerticalScrollbarDownButtonStyle, BoxStyle);
        BackgroundAllStates(texControl, ToolTipStyle);

        BackgroundAllStates(texTrack, VerticalScrollbarThumbStyle);

        GUIStyle selectGridStyle = new(ToggleStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Normal };

        LabelStyle.margin = BoxStyle.margin;
        LabelStyle.padding = BoxStyle.margin;

        GUIStyle dragBarStyle = new(BlankBackgroundStyle) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 3, 3) };

        GUIStyle botTypeGridStyle = new(ToggleStyle) { fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleLeft };

        GUIStyle SelectionListStyle = new(ToggleStyle) { fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleLeft };

        GUIStyle botTypeSectionStyle = new(ToggleStyle) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Normal };

        Styles.Add(Style.botTypeSection, botTypeSectionStyle);
        Styles.Add(Style.scrollView, ScrollViewStyle);
        Styles.Add(Style.selectionList, SelectionListStyle);
        Styles.Add(Style.alert, AlertStyle);
        Styles.Add(Style.botTypeGrid, botTypeGridStyle);
        Styles.Add(Style.dragBar, dragBarStyle);
        Styles.Add(Style.selectionGrid, selectGridStyle);
        Styles.Add(Style.horizontalSliderThumb, HorizontalSliderThumbStyle);
        Styles.Add(Style.button, ButtonStyle);
        Styles.Add(Style.box, BoxStyle);
        Styles.Add(Style.toggle, ToggleStyle);
        Styles.Add(Style.textField, TextFieldStyle);
        Styles.Add(Style.textArea, TextAreaStyle);
        Styles.Add(Style.window, WindowStyle);
        Styles.Add(Style.verticalScrollbarUpButton, VerticalScrollbarUpButtonStyle);
        Styles.Add(Style.verticalScrollbarThumb, VerticalScrollbarThumbStyle);
        Styles.Add(Style.verticalScrollbar, VerticalScrollbarStyle);
        Styles.Add(Style.verticalScrollbarDownButton, VerticalScrollbarDownButtonStyle);
        Styles.Add(Style.horizontalSlider, HorizontalSliderStyle);
        Styles.Add(Style.label, LabelStyle);
        Styles.Add(Style.list, ListStyle);
        Styles.Add(Style.verticalSlider, VerticalSliderStyle);
        Styles.Add(Style.verticalSliderThumb, VerticalSliderThumbStyle);
        Styles.Add(Style.blankbox, BlankBackgroundStyle);
        Styles.Add(Style.tooltip, ToolTipStyle);

        foreach (var style in EnumValues.GetEnum<Style>())
        {
            if (!Styles.ContainsKey(style))
            {
                //Logger.LogWarning(style);
            }
        }
    }

    private sealed class DynamicStyle
    {
        public GUIStyle Normal;
        public GUIStyle Active;
    }
}
