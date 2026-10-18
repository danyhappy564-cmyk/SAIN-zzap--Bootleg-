using System.Collections.Generic;
using EFT.UI;
using UnityEngine;

namespace SAIN.Editor;

/// <summary>
/// zzap fork (2026-10-18): the look of mockup 5 for the whole F6 editor - one palette, one set of styles and the few widgets
/// IMGUI lacks (an on/off switch, a visible slider, list rows, a search box). User after the first in-game test: "text is cut,
/// sliders are invisible, and only Quick settings looks like the mockup". Every tab draws through this.
/// Textures are made with HideAndDontSave so EFT's unused-asset cleanup between scenes cannot take them away.
/// </summary>
public static class ZzapUI
{
    public static readonly Color Bg = Hex(0x0e0e11);
    public static readonly Color Panel = Hex(0x17171c);
    public static readonly Color PanelHover = Hex(0x202027);
    public static readonly Color Field = Hex(0x0b0b0e);
    public static readonly Color Line = Hex(0x2a2a31);
    public static readonly Color Control = Hex(0x26262d);
    public static readonly Color ControlHover = Hex(0x33333c);
    public static readonly Color Text = Hex(0xe4e4e7);
    public static readonly Color Muted = Hex(0xa1a1aa);
    public static readonly Color Dim = Hex(0x71717a);
    public static readonly Color Blue = Hex(0x38bdf8);
    public static readonly Color BlueLight = Hex(0x7dd3fc);
    public static readonly Color BlueDark = Hex(0x0c4a6e);
    public static readonly Color BluePanel = Hex(0x0a2430);
    public static readonly Color Yellow = Hex(0xfacc15);
    public static readonly Color YellowHover = Hex(0xfde047);
    public static readonly Color YellowTint = Hex(0x2b250d);
    public static readonly Color Track = Hex(0x3f3f46);
    public static readonly Color Green = Hex(0x4ade80);

    public const float ROW_HEIGHT = 30f;
    public const float PANEL_HEIGHT = 900f;
    public const float SIDE_WIDTH = 320f;
    public const float NAME_WIDTH = 380f;
    public const float VALUE_WIDTH = 80f;
    public const float RESET_WIDTH = 72f;

    public static GUIStyle Title;
    public static GUIStyle Name;
    public static GUIStyle Small;
    public static GUIStyle Section;
    public static GUIStyle Value;
    public static GUIStyle Panel1;
    public static GUIStyle PanelBlue;
    public static GUIStyle ListText;
    public static GUIStyle ListTextSelected;
    public static GUIStyle Count;
    public static GUIStyle FieldStyle;
    public static GUIStyle Placeholder;
    public static GUIStyle Button;
    public static GUIStyle ButtonPrimary;
    public static GUIStyle Pill;
    public static GUIStyle Badge;
    public static GUIStyle TabText;
    public static GUIStyle TabTextSelected;
    public static GUIStyle SwitchText;
    public static GUIStyle SwitchTextOn;
    public static GUIStyle Header;

    private static readonly Dictionary<Color, Texture2D> _textures = new();
    private static Texture2D _circle;
    private static bool _ready;

    private static Color Hex(int rgb)
    {
        return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }

    public static Texture2D Tex(Color color)
    {
        if (_textures.TryGetValue(color, out var texture) && texture != null)
        {
            return texture;
        }
        texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixel(0, 0, color);
        texture.Apply();
        _textures[color] = texture;
        return texture;
    }

    // A white anti-aliased disc, tinted when drawn (slider thumb, switch knob and the switch's round ends).
    private static Texture2D Circle
    {
        get
        {
            if (_circle != null)
            {
                return _circle;
            }
            const int size = 32;
            _circle = new Texture2D(size, size, TextureFormat.ARGB32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            float radius = size / 2f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;
                    float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            _circle.SetPixels(pixels);
            _circle.Apply();
            return _circle;
        }
    }

    public static void Fill(Rect rect, Color color)
    {
        GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, 0);
    }

    public static void Disc(Rect rect, Color color)
    {
        GUI.DrawTexture(rect, Circle, ScaleMode.StretchToFill, true, 0, color, 0, 0);
    }

    // Round-ended bar: two discs and the rectangle between them.
    public static void PillRect(Rect rect, Color color)
    {
        float h = rect.height;
        Disc(new Rect(rect.x, rect.y, h, h), color);
        Disc(new Rect(rect.xMax - h, rect.y, h, h), color);
        Fill(new Rect(rect.x + h / 2f, rect.y, Mathf.Max(0f, rect.width - h), h), color);
    }

    public static void EnsureStyles()
    {
        if (_ready && Panel1?.normal.background != null)
        {
            return;
        }
        _ready = true;

        Title = Label(15, Color.white, FontStyle.Bold);
        Name = Label(14, Text);
        Name.wordWrap = true;
        Small = Label(12, Muted);
        Small.wordWrap = true;
        Small.alignment = TextAnchor.UpperLeft;
        Section = Label(12, Yellow, FontStyle.Bold);
        Value = Label(15, Color.white, FontStyle.Bold);
        Value.alignment = TextAnchor.MiddleRight;
        ListText = Label(14, Text);
        ListText.clipping = TextClipping.Clip;
        ListTextSelected = Label(14, Yellow, FontStyle.Bold);
        ListTextSelected.clipping = TextClipping.Clip;
        Count = Label(12, Dim);
        Count.alignment = TextAnchor.MiddleRight;
        Placeholder = Label(13, Dim);
        TabText = Label(14, Muted);
        TabText.alignment = TextAnchor.MiddleCenter;
        TabTextSelected = Label(14, Yellow, FontStyle.Bold);
        TabTextSelected.alignment = TextAnchor.MiddleCenter;
        SwitchText = Label(13, Muted);
        SwitchTextOn = Label(13, Blue, FontStyle.Bold);
        Header = Label(14, Text);
        Header.padding = new RectOffset(12, 8, 0, 0);
        Header.clipping = TextClipping.Clip;

        Panel1 = new GUIStyle(GUI.skin.box) { padding = new RectOffset(14, 14, 10, 12), margin = new RectOffset(0, 0, 0, 0) };
        Background(Panel1, Panel, Panel);
        PanelBlue = new GUIStyle(Panel1);
        Background(PanelBlue, BluePanel, BluePanel);

        FieldStyle = new GUIStyle(GUI.skin.textField)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(8, 8, 4, 4),
            margin = new RectOffset(4, 4, 2, 2),
        };
        Background(FieldStyle, Field, Field);
        TextColor(FieldStyle, Color.white);

        Button = new GUIStyle(GUI.skin.button)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(10, 10, 4, 4),
            margin = new RectOffset(4, 4, 2, 2),
        };
        Background(Button, Control, ControlHover);
        TextColor(Button, Text);

        ButtonPrimary = new GUIStyle(Button) { fontStyle = FontStyle.Bold };
        Background(ButtonPrimary, Yellow, YellowHover);
        TextColor(ButtonPrimary, Hex(0x1a1a1a));

        Pill = new GUIStyle(Button) { fontSize = 12, fontStyle = FontStyle.Bold, padding = new RectOffset(10, 10, 2, 2) };
        Background(Pill, YellowTint, YellowTint);
        TextColor(Pill, Yellow);

        Badge = new GUIStyle(Pill) { fontSize = 11, padding = new RectOffset(6, 6, 1, 1), margin = new RectOffset(2, 6, 6, 6) };
    }

    private static GUIStyle Label(int size, Color color, FontStyle fontStyle = FontStyle.Normal)
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            richText = true,
            fontSize = size,
            fontStyle = fontStyle,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
            padding = new RectOffset(2, 2, 1, 1),
            margin = new RectOffset(2, 2, 1, 1),
        };
        style.normal.background = null;
        style.hover.background = null;
        TextColor(style, color);
        return style;
    }

    public static void Background(GUIStyle style, Color normal, Color hover)
    {
        var normalTexture = Tex(normal);
        var hoverTexture = Tex(hover);
        style.normal.background = normalTexture;
        style.onNormal.background = normalTexture;
        style.focused.background = normalTexture;
        style.onFocused.background = normalTexture;
        style.hover.background = hoverTexture;
        style.onHover.background = hoverTexture;
        style.active.background = hoverTexture;
        style.onActive.background = hoverTexture;
    }

    public static void TextColor(GUIStyle style, Color color)
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

    // ---------------------------------------------------------------- deferred clicks

    private static System.Action _deferred;

    /// <summary>
    /// Run a click that changes what is drawn (another group, another preset, a folded list) at the start of the next frame.
    /// IMGUI lays a frame out first and draws it after; if the controls change in between it logs errors and can draw garbage.
    /// </summary>
    public static void Defer(System.Action action)
    {
        _deferred += action;
    }

    public static void RunDeferred()
    {
        if (_deferred == null || Event.current.type != EventType.Layout)
        {
            return;
        }
        var actions = _deferred;
        _deferred = null;
        try
        {
            actions();
        }
        catch (System.Exception ex)
        {
            Logger.LogError(ex);
        }
    }

    // ---------------------------------------------------------------- widgets

    /// <summary>An on/off switch (blue when on) with "On"/"Off" next to it. Rows pass their height so it sits centred.</summary>
    public static bool Switch(bool value, float height = ROW_HEIGHT, string onText = null, string offText = null)
    {
        EnsureStyles();
        Rect rect = GUILayoutUtility.GetRect(44f, height, GUILayout.Width(44f), GUILayout.Height(height));
        if (Event.current.type == EventType.Repaint)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            var track = new Rect(rect.x + 2f, rect.center.y - 10f, 40f, 20f);
            PillRect(track, value ? (hover ? BlueLight : Blue) : hover ? ControlHover : Track);
            float knobX = value ? track.xMax - 18f : track.x + 2f;
            Disc(new Rect(knobX, track.y + 2f, 16f, 16f), value ? Color.white : Hex(0xd4d4d8));
        }
        bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        string text = value ? onText ?? EditorText.UI("On") : offText ?? EditorText.UI("Off");
        GUILayout.Label(text, value ? SwitchTextOn : SwitchText, GUILayout.Height(height), GUILayout.ExpandWidth(false));
        if (clicked)
        {
            Sounds.PlaySound(EUISoundType.MenuCheckBox);
            return !value;
        }
        return value;
    }

    /// <summary>A slider with a visible track: grey line, blue fill, round thumb. Same input handling as SAIN's slider.</summary>
    public static float Slider(float value, float min, float max, params GUILayoutOption[] options)
    {
        float newValue = SAINLayout.HorizontalSlider(value, min, max, null, options);
        DrawSlider(GUILayoutUtility.GetLastRect(), max > min ? Mathf.InverseLerp(min, max, newValue) : 0f);
        return newValue;
    }

    public static void DrawSlider(Rect rect, float progress)
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }
        progress = Mathf.Clamp01(progress);
        bool hover = rect.Contains(Event.current.mousePosition);
        const float thumb = 16f;
        float left = rect.x + thumb / 2f;
        float width = Mathf.Max(1f, rect.width - thumb);
        var track = new Rect(left, rect.center.y - 2f, width, 4f);
        Fill(track, Track);
        Fill(new Rect(track.x, track.y, width * progress, track.height), Blue);
        float size = hover ? thumb + 2f : thumb;
        Disc(new Rect(left + width * progress - size / 2f, rect.center.y - size / 2f, size, size), hover ? Color.white : Blue);
    }

    /// <summary>A thin mark on a slider (the zzap value on Quick settings).</summary>
    public static void DrawTick(Rect sliderRect, float progress, Color color)
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }
        const float thumb = 16f;
        float x = sliderRect.x + thumb / 2f + Mathf.Max(1f, sliderRect.width - thumb) * Mathf.Clamp01(progress);
        Fill(new Rect(x - 1f, sliderRect.center.y - 8f, 2f, 16f), color);
    }

    /// <summary>One row of a left-hand list: yellow bar and tint when selected, a count on the right.</summary>
    public static bool ListItem(string text, string right, bool selected, string tooltip = null, float indent = 0f, float height = 30f)
    {
        EnsureStyles();
        Rect rect = GUILayoutUtility.GetRect(10f, height, GUILayout.Height(height), GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint)
        {
            if (selected)
            {
                Fill(rect, YellowTint);
                Fill(new Rect(rect.x, rect.y, 3f, rect.height), Yellow);
            }
            else if (rect.Contains(Event.current.mousePosition))
            {
                Fill(rect, PanelHover);
            }
        }
        float rightWidth = string.IsNullOrEmpty(right) ? 0f : 64f;
        GUI.Label(new Rect(rect.x + 10f + indent, rect.y, rect.width - 14f - indent - rightWidth, rect.height), text, selected ? ListTextSelected : ListText);
        if (rightWidth > 0f)
        {
            GUI.Label(new Rect(rect.xMax - rightWidth - 6f, rect.y, rightWidth, rect.height), right, Count);
        }
        bool clicked = GUI.Button(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none);
        if (clicked)
        {
            Sounds.PlaySound(EUISoundType.MenuDropdown);
        }
        return clicked;
    }

    /// <summary>A search box with a grey hint while empty and a clear button (always drawn, so typing never changes the layout).</summary>
    public static string Search(string value, string placeholder, float height = 32f)
    {
        EnsureStyles();
        GUILayout.BeginHorizontal();
        value = GUILayout.TextField(value ?? string.Empty, FieldStyle, GUILayout.Height(height), GUILayout.ExpandWidth(true));
        if (string.IsNullOrEmpty(value) && Event.current.type == EventType.Repaint)
        {
            Rect rect = GUILayoutUtility.GetLastRect();
            GUI.Label(new Rect(rect.x + 10f, rect.y, rect.width - 12f, rect.height), placeholder, Placeholder);
        }
        if (GUILayout.Button(new GUIContent("×", EditorText.UI("Clear")), Button, GUILayout.Width(32f), GUILayout.Height(height)))
        {
            Sounds.PlaySound(EUISoundType.MenuContextMenu);
            value = string.Empty;
        }
        GUILayout.EndHorizontal();
        return value;
    }

    public static void Separator(float space = 0f)
    {
        Rect rect = GUILayoutUtility.GetRect(1f, 1f + space, GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint)
        {
            Fill(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Line);
        }
    }

    /// <summary>Panel heading: title, optional description under it in grey.</summary>
    public static void PanelHeading(string title, string description = null)
    {
        EnsureStyles();
        GUILayout.Label(title, Title);
        if (!string.IsNullOrEmpty(description))
        {
            GUILayout.Label(description, Small);
        }
        GUILayout.Space(4f);
        Separator(4f);
        GUILayout.Space(4f);
    }
}
