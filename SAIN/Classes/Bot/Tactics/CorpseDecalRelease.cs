using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using HarmonyLib;
using SAIN.Preset.Shared.GlobalSettings;
using Systems.Effects;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork: frees the blood / bullet-hole decal textures of dead characters (corpses stay, only their decals go).
/// EFT's TextureDecalsPainter gives every character mesh that gets hit its own RenderTexture (a pool of 128 made up front,
/// more created on demand) and keeps it in a Renderer -> texture dictionary until the raid ends - corpses included. In a
/// 20-min bot-vs-bot sim RenderTextures went 281 -> 1072 (431 -> 1254 MPix, ~1 MPix = ~4 MB each) with 63 deaths, several
/// GB of the RAM/VRAM growth (user report 2026-09-30, fix8 RAM 90%). Here: a character dead for the set delay (or a mesh
/// that was destroyed) has its decal switched off, its texture cleared and handed back to EFT's pool, so later hits reuse
/// it instead of creating new ones. Pool copies above EFT's own 128 are destroyed.
/// </summary>
public static class CorpseDecalRelease
{
    private const float SCAN_INTERVAL = 5f;
    private const int POOL_KEEP = 128;

    // Resolved on first use; if EFT renamed a field the feature switches itself off with one warning instead of throwing every tick.
    private static AccessTools.FieldRef<TextureDecalsPainter, Dictionary<Renderer, RenderTexture>> _renderersRef;
    private static AccessTools.FieldRef<TextureDecalsPainter, ObjectPool<RenderTexture>> _poolRef;
    private static bool _resolved;
    private static bool _broken;

    private static bool Resolve()
    {
        if (_resolved)
        {
            return !_broken;
        }
        _resolved = true;
        try
        {
            _renderersRef = AccessTools.FieldRefAccess<TextureDecalsPainter, Dictionary<Renderer, RenderTexture>>("_renderers");
            _poolRef = AccessTools.FieldRefAccess<TextureDecalsPainter, ObjectPool<RenderTexture>>("_texturesPool");
        }
        catch (System.Exception ex)
        {
            _broken = true;
            Logger.LogWarning($"[SAIN zzap] corpse decal cleanup off - EFT's decal painter changed: {ex.Message}");
        }
        return !_broken;
    }

    private static readonly int _decalsTex = Shader.PropertyToID("_DecalsTex");
    private static readonly int _decalPower = Shader.PropertyToID("_DecalPower");

    // Renderer instance id -> owning Player (null = none found); dead-since times per Player instance id.
    private static readonly Dictionary<int, Player> _owners = new();
    private static readonly Dictionary<int, float> _deadSince = new();
    private static readonly List<Renderer> _release = new();

    private static float _nextScan;

    public static int Released { get; private set; }
    public static int Destroyed { get; private set; }

    public static void Tick()
    {
        float time = Time.time;
        if (time < _nextScan)
        {
            return;
        }
        _nextScan = time + SCAN_INTERVAL;

        var settings = GlobalSettingsClass.Instance?.General?.Performance;
        if (settings == null || !settings.FreeCorpseDecals || !Singleton<Effects>.Instantiated || !Resolve())
        {
            return;
        }
        TextureDecalsPainter painter = Singleton<Effects>.Instance.TexDecals;
        if (painter == null)
        {
            return;
        }
        Dictionary<Renderer, RenderTexture> renderers = _renderersRef(painter);
        ObjectPool<RenderTexture> pool = _poolRef(painter);
        if (renderers == null || pool == null || renderers.Count == 0)
        {
            return;
        }

        try
        {
            Scan(renderers, pool, time, settings.FreeCorpseDecalsDelay);
        }
        catch (System.Exception ex)
        {
            _broken = true;
            Logger.LogWarning($"[SAIN zzap] corpse decal cleanup stopped for this session: {ex}");
        }
    }

    private static void Scan(Dictionary<Renderer, RenderTexture> renderers, ObjectPool<RenderTexture> pool, float time, float delay)
    {
        _release.Clear();
        foreach (var kv in renderers)
        {
            Renderer renderer = kv.Key;
            if (renderer == null || ShouldRelease(renderer, time, delay))
            {
                _release.Add(renderer);
            }
        }
        foreach (Renderer renderer in _release)
        {
            if (!renderers.TryGetValue(renderer, out RenderTexture texture))
            {
                continue;
            }
            renderers.Remove(renderer);
            if (renderer != null)
            {
                _owners.Remove(renderer.GetInstanceID());
                TurnOffDecal(renderer);
            }
            GiveBack(pool, texture);
        }
        if (_release.Count > 0)
        {
            TacticDiagnostics.Count("perf.corpseDecalsFreed");
        }
        _release.Clear();
    }

    private static bool ShouldRelease(Renderer renderer, float time, float delay)
    {
        int id = renderer.GetInstanceID();
        if (!_owners.TryGetValue(id, out Player owner))
        {
            owner = renderer.GetComponentInParent<Player>();
            _owners[id] = owner;
        }
        if (owner == null)
        {
            // Not part of a character we can read: only a mesh that now sits on a corpse object is freed.
            return renderer.GetComponentInParent<Corpse>() != null && DeadLongEnough(id, time, delay);
        }
        if (owner.HealthController?.IsAlive != false)
        {
            return false;
        }
        return DeadLongEnough(owner.GetInstanceID(), time, delay);
    }

    private static bool DeadLongEnough(int key, float time, float delay)
    {
        if (!_deadSince.TryGetValue(key, out float since))
        {
            _deadSince[key] = time;
            return delay <= 0f;
        }
        return time - since >= delay;
    }

    private static void TurnOffDecal(Renderer renderer)
    {
        try
        {
            // The painter already gave this renderer its own material instance (renderer.material) - no new copy here.
            Material material = renderer.sharedMaterial;
            if (material != null)
            {
                material.SetFloat(_decalPower, 0f);
                material.SetTexture(_decalsTex, Clean);
            }
        }
        catch
        {
        }
    }

    // Same as a freshly cleared decal texture: transparent black = no marks.
    private static Texture2D _clean;

    private static Texture2D Clean
    {
        get
        {
            if (_clean == null)
            {
                _clean = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "zzap clean decal" };
                _clean.SetPixel(0, 0, new Color(0f, 0f, 0f, 0f));
                _clean.Apply(false, true);
            }
            return _clean;
        }
    }

    private static void GiveBack(ObjectPool<RenderTexture> pool, RenderTexture texture)
    {
        if (texture == null)
        {
            return;
        }
        if (pool._pool != null && pool._pool.Count >= POOL_KEEP)
        {
            texture.Release();
            Object.Destroy(texture);
            Destroyed++;
            return;
        }
        // The painter blends a new decal into what the texture already holds - wipe the old body's marks first.
        RenderTexture active = RenderTexture.active;
        RenderTexture.active = texture;
        GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));
        RenderTexture.active = active;
        pool.Return(texture);
        Released++;
    }

    public static void Clear()
    {
        _owners.Clear();
        _deadSince.Clear();
        _release.Clear();
        _nextScan = 0f;
        Released = 0;
        Destroyed = 0;
    }
}
