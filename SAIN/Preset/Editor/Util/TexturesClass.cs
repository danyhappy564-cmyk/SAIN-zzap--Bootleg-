using System.Collections.Generic;
using SAIN.Editor.Util;
using UnityEngine;

namespace SAIN.Editor;

public static class TexturesClass
{
    public static void CreateCache()
    {
        if (ColorTextures.Count == 0)
        {
            foreach (var color in ColorsClass.ColorSchemeDictionary)
            {
                if (color.Key == ColorNames.Clear)
                {
                    ColorTextures.Add(color.Key.ToString(), null);
                }
                else
                {
                    ColorTextures.Add(color.Key.ToString(), NewTexture(color.Value));
                }
            }
            foreach (var color in ColorsClass.GrayColorScheme)
            {
                ColorTextures.Add(color.Key.ToString(), NewTexture(color.Value));
            }
        }
    }

    public static Texture2D GetRandomGray(string key)
    {
        if (!RandomColors.ContainsKey(key))
        {
            var texture = NewTexture(ColorsClass.GetRandomColor(key));
            RandomColors.Add(key, texture);
        }
        return RandomColors[key];
    }

    private static readonly Dictionary<string, Texture2D> RandomColors = new();

    public static Texture2D GetTexture<T>(T name)
    {
        if (ColorTextures.TryGetValue(name.ToString(), out var texture))
        {
            return texture;
        }
        return Texture2D.redTexture;
    }

    public static Texture2D GetCustom(ColorNames name)
    {
        if (CustomTextures.TryGetValue(name.ToString(), out var texture))
        {
            return texture;
        }
        return Texture2D.redTexture;
    }

    public static readonly Dictionary<string, Texture2D> ColorTextures = new();

    public static readonly Dictionary<string, Texture2D> CustomTextures = new();

    public static Texture2D NewTexture(Color color, int width = 2, int height = 2)
    {
        Texture2D texture = new(width, height) { hideFlags = HideFlags.HideAndDontSave };
        Color[] colorApply = new Color[texture.width * texture.height];
        for (int i = 0; i < colorApply.Length; i++)
        {
            colorApply[i] = color;
        }
        texture.SetPixels(colorApply);
        texture.Apply();
        return texture;
    }

    // zzap fork (2026-10-18): every SAIN slider is drawn by ZzapUI now (grey track, blue fill, round thumb) - the red bars
    // that filled the whole row are gone (user: "sliders are invisible / nothing like the mockup").
    public static Rect DrawSliderBackGrounds(float progressRatio, Rect lastRect)
    {
        ZzapUI.DrawSlider(lastRect, progressRatio);
        return lastRect;
    }
}
