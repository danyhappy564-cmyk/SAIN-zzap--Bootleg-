using SAIN.Plugin;
using UnityEngine;

namespace SAIN.Editor;

public static class RectLayout
{
    public static Vector2 ScaledPivot
    {
        get { return GetScaling(); }
    }

    private static float ReferenceResX
    {
        get { return 1920 * PresetHandler.EditorDefaults.ConfigScaling; }
    }

    private static float ReferenceResY
    {
        get { return 1080 * PresetHandler.EditorDefaults.ConfigScaling; }
    }

    private static Vector2 GetScaling()
    {
        float scaling = Mathf.Min(Screen.width / ReferenceResX, Screen.height / ReferenceResY);
        return new Vector2(scaling, scaling);
    }

    public static Rect MainWindow = new(0, 0, 1920, 1080);

    // zzap fork (2026-10-18, mockup 5): a taller header, the buttons inset from the edges with gaps, a wide yellow Save button.
    private const float RectHeight = 44f;
    private const float ButtonHeight = 32f;
    private const float ButtonY = (RectHeight - ButtonHeight) / 2f;
    private const float Gap = 8f;
    private const float ExitWidth = 40f;
    private const float SaveAllWidth = 260f;
    private const float AdvWidth = 170f;
    private const float LangWidth = 84f;

    private static readonly float ExitStartX = MainWindow.width - ExitWidth - 12f;
    private static readonly float SaveAllStartX = ExitStartX - SaveAllWidth - Gap;
    private static readonly float AdvRectStartX = SaveAllStartX - AdvWidth - Gap;
    private static readonly float LangRectStartX = AdvRectStartX - LangWidth - Gap;
    private static readonly float DragWidth = LangRectStartX - Gap;

    public static Rect ExitRect = new(ExitStartX, ButtonY, ExitWidth, ButtonHeight);
    public static Rect DragRect = new(0, 0, DragWidth, RectHeight);
    public static Rect SaveAllRect = new(SaveAllStartX, ButtonY, SaveAllWidth, ButtonHeight);
    public static Rect AdvRect = new(AdvRectStartX, ButtonY, AdvWidth, ButtonHeight);
    public static Rect LangRect = new(LangRectStartX, ButtonY, LangWidth, ButtonHeight);
    public static Rect HeaderRect = new(0, 0, MainWindow.width, RectHeight);
}
