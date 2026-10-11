using System.Collections.Generic;
using System.Reflection;
using System;
using HarmonyLib;
using SAIN.SAINComponent.Classes.Tactics;
using SPT.Reflection.Patching;

namespace SAIN.Patches.Generic;

/// <summary>
/// zzap fork, 2026-10-12 (sim crash at 03:59, "NRE then the game closed"): TextureDecalsPainter (blood/hit marks painted
/// on bodies) works its queue by drawing entry [0] and only then removing it. If that entry's renderer was destroyed - the
/// sim corpse delete destroys the whole body, vanilla only returns bodies to the pool - DrawDecal throws before the
/// Remove, so the same entry throws again every frame and the queue never moves. In the sim that was ~45 exceptions a
/// second from 03:57:19 (40 ms after a corpse delete) on, native memory went 33 -> 58 GB in 2 minutes and the game ran
/// out of memory. This drops queue entries whose renderer is gone before the painter runs (CorpseCleanup also removes
/// a deleted body's queued marks right before deleting it; its textures are freed by CorpseDecalRelease).
/// </summary>
public class DecalPainterGuardPatch : ModulePatch
{
    public static readonly AccessTools.FieldRef<TextureDecalsPainter, List<TextureDecalsPainter.DecalDescription>> Queue =
        AccessTools.FieldRefAccess<TextureDecalsPainter, List<TextureDecalsPainter.DecalDescription>>("_decalDescriptions");

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(TextureDecalsPainter), nameof(TextureDecalsPainter.Update));
    }

    [PatchPrefix]
    public static void Prefix(TextureDecalsPainter __instance)
    {
        var queue = Queue(__instance);
        if (queue == null || queue.Count == 0)
        {
            return;
        }
        for (int i = queue.Count - 1; i >= 0; i--)
        {
            if (queue[i].Renderer == null)
            {
                queue.RemoveAt(i);
                TacticDiagnostics.Count("sim.decalDeadRendererDropped");
            }
        }
    }

    // Second line for any other cause: if Update still throws, the entry it was drawing ([0], never removed) would throw
    // again next frame forever - drop it so the queue keeps moving, and log only the first one per game session.
    private static bool _logged;

    [PatchFinalizer]
    public static Exception Finalizer(TextureDecalsPainter __instance, Exception __exception)
    {
        if (__exception == null || __instance == null)
        {
            return __exception;
        }
        var queue = Queue(__instance);
        if (queue != null && queue.Count > 0)
        {
            queue.RemoveAt(0);
        }
        TacticDiagnostics.Count("sim.decalPainterThrew");
        if (!_logged)
        {
            _logged = true;
            Logger.LogWarning($"[SAIN zzap] blood decal painter threw, dropped the stuck mark (shown once): {__exception}");
        }
        return null;
    }
}
