using System;
using System.Collections.Generic;
using SAIN.Preset.Shared.GlobalSettings;
using UnityEngine;
using UnityEngine.Profiling;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap (2026-10-31, game sim RamCleaner: SAIN made 70-80% of the mods' managed garbage on Reserve, 5 MB/min at minute 3 rising
/// to 50-131 MB/min near the end; the 1st stage only reproduces a twentieth of it, so the source is in parts it fakes): EFT turns
/// the GC off for the raid, so Mono's used heap only grows - its growth across one bot class's tick is what that class allocated.
/// Sim mode only (F6 SimLab on), one frame in SAMPLE_EVERY, scaled back up. Counters alloc.&lt;class&gt; in KB, written every
/// FLUSH_SECONDS. An estimate (Codex review 10/31): a sample a GC ran through (RamCleaner's) is dropped (alloc.discardedGC), and
/// another managed thread allocating during a tick is charged to that class - rank the classes, don't sum them to the byte.
/// </summary>
public static class AllocMeter
{
    private const int SAMPLE_EVERY = 8;
    private const float FLUSH_SECONDS = 10f;

    private static readonly Dictionary<Type, long> _bytes = new();
    private static int _frame = -1;
    private static bool _sampling;
    private static float _nextFlush;

    /// <summary>Whether this frame's class ticks are measured (checked once per tick group; cheap after the first call a frame).</summary>
    public static bool Sampling
    {
        get
        {
            int frame = Time.frameCount;
            if (frame != _frame)
            {
                _frame = frame;
                _sampling = GlobalSettingsClass.Instance?.General?.SimLab?.Enabled == true && frame % SAMPLE_EVERY == 0;
                if (_sampling && Time.time >= _nextFlush)
                {
                    _nextFlush = Time.time + FLUSH_SECONDS;
                    Flush();
                }
            }
            return _sampling;
        }
    }

    public static long Now() => Profiler.GetMonoUsedSizeLong();

    /// <summary>GC count before a measured tick; Add drops the sample when it changed.</summary>
    public static int Collections() => GC.CollectionCount(0);

    public static void Add(Type type, long bytes, int collectionsBefore)
    {
        if (GC.CollectionCount(0) != collectionsBefore)
        {
            _discarded++;
            return;
        }
        if (bytes <= 0)
        {
            return;
        }
        _bytes.TryGetValue(type, out long was);
        _bytes[type] = was + bytes;
    }

    private static int _discarded;

    /// <summary>Pending samples into the counters (also at raid end, before Clear).</summary>
    public static void Flush()
    {
        if (_discarded > 0)
        {
            TacticDiagnostics.Add("alloc.discardedGC", _discarded);
            _discarded = 0;
        }
        foreach (var kv in _bytes)
        {
            long kb = kv.Value * SAMPLE_EVERY / 1024;
            if (kb > 0)
            {
                TacticDiagnostics.Add($"alloc.{kv.Key.Name}", (int)Math.Min(int.MaxValue, kb));
            }
        }
        _bytes.Clear();
    }

    public static void Clear()
    {
        Flush();
        _bytes.Clear();
        _discarded = 0;
        _frame = -1;
        _nextFlush = 0f;
    }
}
