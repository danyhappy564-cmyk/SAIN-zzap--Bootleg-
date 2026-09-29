using System;
using System.Collections.Generic;
using System.Reflection;
using EFT;
using EFT.Interactive;
using HarmonyLib;
using SAIN.SAINComponent.Classes.Tactics;
using SPT.Reflection.Patching;

namespace SAIN.Patches.Generic;

/// <summary>
/// zzap fork: "corpses walking around" (user report, 3rd time 2026-09-29). Player.OnDead fires OnPlayerDead /
/// OnPlayerDeadStatic / OnPlayerDeadOrUnspawn / OnIPlayerDeadOrUnspawn first and only AFTER that turns off the animators and
/// the character controller and builds the ragdoll corpse. A multicast delegate stops at the first subscriber that throws,
/// and the exception leaves OnDead - so one faulty death handler anywhere (any mod) left the body standing with its
/// controller still on, sliding around. This calls every subscriber on its own (one throwing no longer stops the others
/// or OnDead) and logs which one threw as [DeadBug].
/// </summary>
public class SafeDeathEventsPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(Player), nameof(Player.OnDead));
    }

    [PatchTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var ins in instructions)
        {
            if (ins.opcode == System.Reflection.Emit.OpCodes.Callvirt && ins.operand is MethodInfo mi && mi.Name == "Invoke"
                && typeof(Delegate).IsAssignableFrom(mi.DeclaringType))
            {
                var ps = mi.GetParameters();
                MethodInfo safe = null;
                if (ps.Length == 1)
                {
                    safe = AccessTools.Method(typeof(SafeDeathEventsPatch), nameof(Safe1)).MakeGenericMethod(mi.DeclaringType, ps[0].ParameterType);
                }
                else if (ps.Length == 4)
                {
                    safe = AccessTools.Method(typeof(SafeDeathEventsPatch), nameof(Safe4))
                        .MakeGenericMethod(mi.DeclaringType, ps[0].ParameterType, ps[1].ParameterType, ps[2].ParameterType, ps[3].ParameterType);
                }
                if (safe != null)
                {
                    yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, safe).MoveLabelsFrom(ins).MoveBlocksFrom(ins);
                    continue;
                }
            }
            yield return ins;
        }
    }

    public static void Safe1<TD, T1>(TD handlers, T1 a)
        where TD : Delegate
    {
        foreach (Delegate h in handlers.GetInvocationList())
        {
            try
            {
                h.DynamicInvoke(a);
            }
            catch (Exception ex)
            {
                Report(h, ex);
            }
        }
    }

    public static void Safe4<TD, T1, T2, T3, T4>(TD handlers, T1 a, T2 b, T3 c, T4 d)
        where TD : Delegate
    {
        foreach (Delegate h in handlers.GetInvocationList())
        {
            try
            {
                h.DynamicInvoke(a, b, c, d);
            }
            catch (Exception ex)
            {
                Report(h, ex);
            }
        }
    }

    private static readonly HashSet<string> _warned = new();

    internal static void Report(Delegate h, Exception ex)
    {
        Exception inner = ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
        string who = h?.Method == null ? "?" : $"{h.Method.DeclaringType?.FullName}.{h.Method.Name}";
        TacticDiagnostics.Count("deadBug.handlerThrew");
        TacticDiagnostics.LogCloseCombat($"[DeadBug] death handler {who} threw {inner.GetType().Name}: {inner.Message} - skipped, OnDead continues");
        if (_warned.Add(who))
        {
            Logger.LogWarning($"[SAIN zzap] death handler {who} threw (skipped so the body still ragdolls): {inner}");
        }
    }
}

/// <summary>
/// zzap fork: second line for the same bug - if OnDead still throws somewhere else (a step we don't wrap) and the corpse was
/// never built, finish the parts that stop a "walking corpse": animators and character controller off, ragdoll corpse,
/// death coroutine. Logs [DeadBug] with the exception.
/// </summary>
public class DeathRescuePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(Player), nameof(Player.OnDead));
    }

    [PatchFinalizer]
    public static Exception Finalizer(Player __instance, Exception __exception)
    {
        if (__exception == null || __instance == null)
        {
            return __exception;
        }
        TacticDiagnostics.Count("deadBug.onDeadThrew");
        bool hadCorpse = CorpseRef(__instance) != null;
        TacticDiagnostics.LogCloseCombat(
            $"[DeadBug] {__instance.name} OnDead threw {__exception.GetType().Name}: {__exception.Message} | corpse built: {hadCorpse}"
                + (hadCorpse ? "" : " -> rescuing (animators/controller off, ragdoll)")
        );
        Logger.LogWarning($"[SAIN zzap] {__instance.name} OnDead threw: {__exception}");
        if (hadCorpse)
        {
            return null;
        }
        Try(() => __instance.EnabledAnimators = 0);
        Try(() => DisableAnimator(__instance, "BodyAnimatorCommon"));
        Try(() => DisableAnimator(__instance, "ArmsAnimatorCommon"));
        Try(() => __instance._characterController.isEnabled = false);
        Try(() => CorpseRef(__instance) = __instance.CreateCorpse());
        Try(() => __instance.ApplyCorpseImpulse());
        Try(() => __instance.StartCoroutine(__instance.OnDeadCoroutine()));
        TacticDiagnostics.Count(CorpseRef(__instance) != null ? "deadBug.rescued" : "deadBug.rescueFailed");
        return null;
    }

    internal static readonly AccessTools.FieldRef<Player, Corpse> CorpseRef = AccessTools.FieldRefAccess<Player, Corpse>("Corpse");

    // The animator type lives in AnimationSystem.Types, which SAIN doesn't reference - set "enabled" by reflection.
    private static void DisableAnimator(Player player, string property)
    {
        object animator = AccessTools.Property(typeof(Player), property)?.GetValue(player);
        animator?.GetType().GetProperty("enabled")?.SetValue(animator, false);
    }

    private static void Try(Action step)
    {
        try
        {
            step();
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[SAIN zzap] death rescue step failed: {ex.Message}");
        }
    }
}
