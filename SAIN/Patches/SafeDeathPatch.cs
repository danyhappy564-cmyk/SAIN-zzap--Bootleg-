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
        if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat($"[DeadBug] death handler {who} threw {inner.GetType().Name}: {inner.Message} - skipped, OnDead continues");
        if (_warned.Add(who))
        {
            Logger.LogWarning($"[SAIN zzap] death handler {who} threw (skipped so the body still ragdolls): {inner}");
        }
    }
}

/// <summary>
/// zzap fork: "standing corpses" again in the 10th sim (2026-09-30, Bot52 / Bot55): health hit 0 but there was no [Death]
/// line, no ragdoll, and the bot's brain kept switching layers for 60s (BotOwner.OnDied never ran either) until ORBIT
/// pushed the body around. Player.OnDead and BotOwner.OnDied are both subscribers of the health controller's DiedEvent,
/// fired from ActiveHealthController.Kill - one subscriber before them throwing (EFT can swallow that into its own log)
/// skips the rest. Same cure as inside OnDead: every subscriber is called on its own, a thrower is logged as [DeadBug].
/// </summary>
public class SafeDiedEventPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(EFT.HealthSystem.ActiveHealthController), nameof(EFT.HealthSystem.ActiveHealthController.Kill));
    }

    [PatchTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return SafeDeathEventsPatch.Transpiler(instructions);
    }
}

/// <summary>
/// zzap fork: the actual cause of the "standing corpses" found in the 13th sim (2026-10-01, Bot50, full stack in LogOutput):
/// LocalPlayer.OnDead first calls BotPlayerCulling.DisableCullingOnDead() and only THEN base.OnDead (ragdoll, controller off,
/// OnPlayerDead...). DisableCullingOnDead -> Disable -> SetMode -> OfflinePlayerCulling.ApplyVisibleState - Harmony-patched by
/// another mod (DMD frame) - threw a NullReferenceException, so base.OnDead never ran. Turning culling off for a corpse is
/// cosmetic; a failure there must not cost the whole death. Swallow it here (logged once per call site) so OnDead goes on.
/// </summary>
public class SafeCullingOnDeadPatch : ModulePatch
{
    private static bool _warned;

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BasePlayerCulling), nameof(BasePlayerCulling.DisableCullingOnDead));
    }

    [PatchFinalizer]
    public static Exception Finalizer(Exception __exception)
    {
        if (__exception == null)
        {
            return null;
        }
        TacticDiagnostics.Count("deadBug.cullingOnDeadThrew");
        if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat(
            $"[DeadBug] disabling culling on death threw {__exception.GetType().Name} (another mod's culling patch) - ignored, death handling continues");
        if (!_warned)
        {
            _warned = true;
            Logger.LogWarning($"[SAIN zzap] BasePlayerCulling.DisableCullingOnDead threw during a death (ignored so the body still ragdolls): {__exception}");
        }
        return null;
    }
}

/// <summary>
/// zzap fork: last line for a death EFT never processed (whatever the reason): a player whose health says dead but who has
/// no corpse after 1.5s gets Player.OnDead (ragdoll, controller off) and, for a bot, BotOwner.OnDied (brain off, bot
/// removed from its zone/group) run here. Checked every second over the alive-players list (an unprocessed death stays
/// in it) and whenever a dead player gets move input. Logs [DeadBug] ... death never processed.
/// </summary>
internal static class UnprocessedDeathRescue
{
    private static readonly Dictionary<int, float> _deadSince = new();
    private static readonly HashSet<int> _rescued = new();
    private static readonly List<Player> _scan = new();
    private static float _nextScan;

    public static void Tick()
    {
        float time = UnityEngine.Time.time;
        if (time < _nextScan)
        {
            return;
        }
        _nextScan = time + 1f;
        var world = Comfort.Common.Singleton<EFT.GameWorld>.Instance;
        if (world?.AllAlivePlayersList == null)
        {
            return;
        }
        _scan.Clear();
        _scan.AddRange(world.AllAlivePlayersList);
        foreach (Player player in _scan)
        {
            Check(player);
        }
        _scan.Clear();
    }

    internal static void Check(Player player)
    {
        if (player == null || player.HealthController == null || player.HealthController.IsAlive)
        {
            return;
        }
        // A switched-off body (ORBIT 2.x Ghost Mode puts far bots to sleep that way) can't build a ragdoll until it is
        // switched back on - ORBIT wakes a ghost before killing it, so don't force a death through on a sleeping body.
        if (player.gameObject == null || !player.gameObject.activeInHierarchy)
        {
            return;
        }
        int id = player.GetInstanceID();
        if (_rescued.Contains(id) || DeathRescuePatch.CorpseRef(player) != null)
        {
            return;
        }
        float time = UnityEngine.Time.time;
        if (!_deadSince.TryGetValue(id, out float since))
        {
            _deadSince[id] = time;
            return;
        }
        // Let EFT's own death handling finish first (it runs in the same frame as the killing hit).
        if (time - since < 1.5f)
        {
            return;
        }
        _rescued.Add(id);
        BotOwner bot = player.AIData?.BotOwner;
        TacticDiagnostics.Count("deadBug.unprocessedDeathRescued");
        string line = $"[DeadBug] {player.name} dead {time - since:0.0}s with no corpse - EFT's death handling never ran (a DiedEvent handler failed?) -> running it now"
            + (bot != null ? $" (bot brain still on: {!bot.IsDead})" : "");
        if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat(line);
        Logger.LogWarning($"[SAIN zzap] {line}");
        try
        {
            player.OnDead(EDamageType.Undefined);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[SAIN zzap] forced OnDead for {player.name} threw: {ex.Message}");
        }
        try
        {
            if (bot != null && !bot.IsDead)
            {
                bot.OnDied(EDamageType.Undefined);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[SAIN zzap] forced BotOwner.OnDied for {player.name} threw: {ex.Message}");
        }
    }

    public static void Clear()
    {
        _deadSince.Clear();
        _rescued.Clear();
        _scan.Clear();
        _nextScan = 0f;
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
        if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat(
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

/// <summary>
/// zzap fork: "a dead body walking sideways and jumping for a while" (9th sim screenshots, ragdoll was built - the 3s body
/// check said all 104 deaths were fine). Something kept feeding move/jump input to a dead player. Nothing should: a dead
/// player's Move/TryJump/TryVaulting are dropped here, and the first attempt per body logs who sent it as [DeadBug].
/// </summary>
internal static class DeadInputGuard
{
    private static readonly HashSet<int> _logged = new();

    internal static bool IsDead(Player player)
    {
        return player != null && player.HealthController != null && !player.HealthController.IsAlive;
    }

    internal static void Blocked(Player player, string what)
    {
        if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"deadBug.inputBlocked.{what}");
        UnprocessedDeathRescue.Check(player);
        if (player == null || !_logged.Add(player.GetInstanceID()))
        {
            return;
        }
        string caller = "?";
        try
        {
            var frames = new System.Diagnostics.StackTrace(2, false).GetFrames();
            var names = new List<string>();
            if (frames != null)
            {
                foreach (var f in frames)
                {
                    var m = f.GetMethod();
                    var t = m?.DeclaringType;
                    if (m == null || t == null || m.Name.StartsWith("DMD<") || t.Name.Contains("Patch") || t == typeof(DeadInputGuard))
                    {
                        continue;
                    }
                    names.Add($"{t.Name}.{m.Name}");
                    if (names.Count >= 4)
                    {
                        break;
                    }
                }
            }
            caller = names.Count > 0 ? string.Join(" <- ", names) : "?";
        }
        catch
        {
        }
        if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat($"[DeadBug] {player.name} is dead but got {what} input - dropped | from {caller}");
    }
}

public class DeadMovePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(Player), nameof(Player.Move), new[] { typeof(UnityEngine.Vector2) });
    }

    [PatchPrefix]
    public static bool PatchPrefix(Player __instance, UnityEngine.Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f || !DeadInputGuard.IsDead(__instance))
        {
            return true;
        }
        DeadInputGuard.Blocked(__instance, "move");
        return false;
    }
}

public class DeadJumpPatch : ModulePatch
{
    internal static readonly AccessTools.FieldRef<MovementContext, Player> PlayerRef = AccessTools.FieldRefAccess<MovementContext, Player>("_player");

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(MovementContext), nameof(MovementContext.TryJump));
    }

    [PatchPrefix]
    public static bool PatchPrefix(MovementContext __instance)
    {
        Player player = PlayerRef(__instance);
        if (!DeadInputGuard.IsDead(player))
        {
            return true;
        }
        DeadInputGuard.Blocked(player, "jump");
        return false;
    }
}

public class DeadVaultPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(MovementContext), nameof(MovementContext.TryVaulting));
    }

    [PatchPrefix]
    public static bool PatchPrefix(MovementContext __instance, ref bool __result)
    {
        Player player = DeadJumpPatch.PlayerRef(__instance);
        if (!DeadInputGuard.IsDead(player))
        {
            return true;
        }
        DeadInputGuard.Blocked(player, "vault");
        __result = false;
        return false;
    }
}

/// <summary>
/// zzap fork (sim 2026-10-10: "NullReferenceException in MuzzleSmoke.OnRenderObject" 401 / 709 / 1275 times on Factory /
/// Customs / Shoreline, first at t=459s / 1005s / 164s - long before any sim corpse was removed): EFT's muzzle smoke draws itself
/// every rendered frame while it has smoke points; once one of them throws it keeps throwing every frame (log spam, frame
/// cost). The throwing component is switched off and cleared (the next shot starts its smoke again), and the first one is
/// described once in the log - whose weapon, alive or not, material / camera present - to find the cause.
/// </summary>
public class SafeMuzzleSmokePatch : ModulePatch
{
    private static int _described;

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(MuzzleSmoke), nameof(MuzzleSmoke.OnRenderObject));
    }

    [PatchFinalizer]
    public static Exception Finalizer(Exception __exception, MuzzleSmoke __instance)
    {
        if (__exception == null)
        {
            return null;
        }
        TacticDiagnostics.Count("deadBug.muzzleSmokeThrew");
        try
        {
            if (_described < 3)
            {
                _described++;
                Player owner = __instance != null ? __instance.GetComponentInParent<Player>() : null;
                string who = owner == null ? "no player above it (dropped/loose weapon?)"
                    : $"{owner.Profile?.Nickname} ({owner.Profile?.Info?.Settings?.Role}) alive={owner.HealthController?.IsAlive} ai={owner.IsAI}";
                Logger.LogWarning(
                    $"[SAIN zzap] MuzzleSmoke.OnRenderObject threw {__exception.GetType().Name} - smoke switched off. On: {(__instance != null ? __instance.gameObject.name : "destroyed")}, "
                        + $"{who}, material {(__instance != null && __instance.Material != null ? "ok" : "MISSING")}, main camera {(EFT.CameraControl.CameraManager.Instance.Camera != null ? "ok" : "MISSING")}");
            }
            if (__instance != null)
            {
                __instance.Clear(); // empties the points and disables it
            }
        }
        catch
        {
        }
        return null;
    }
}

/// <summary>
/// zzap fork (sim 2026-10-10 Labs: 13x "NullReferenceException in Player.UpdateSourcePriority" from PlaySoundBank, first at
/// t=1723s): the prone sound (an animation event) uses the player's step sound source, which EFT takes from its audio source
/// pool when the player is created - with many bots in a long raid the pool can run dry and the player gets none (null).
/// Skip that one sound for such a player instead of throwing on every prone step.
/// </summary>
public class SafeProneSoundPatch : ModulePatch
{
    private static readonly AccessTools.FieldRef<Player, BetterSource> _stepSource = AccessTools.FieldRefAccess<Player, BetterSource>("NestedStepSoundSource");

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(Player), nameof(Player.PlaySoundBank));
    }

    [PatchPrefix]
    public static bool Prefix(Player __instance, string soundBank)
    {
        if (soundBank == "Prone" && __instance != null && _stepSource(__instance) == null)
        {
            TacticDiagnostics.Count("deadBug.proneSoundNoSource");
            return false;
        }
        return true;
    }
}

/// <summary>
/// zzap fork (sim 2026-10-10 Factory: 14x "NullReferenceException in BotWeaponManager.UpdateHandsController" from
/// BotWeaponSelector.OnWeaponTaken, first at t=1135s): a weapon draw that finishes after the bot's weapon manager was
/// disposed (killed mid-swap - Dispose sets Melee to null) still runs EFT's "weapon taken" callback, which uses it.
/// Nothing is left to update on a disposed bot: end the swap quietly.
/// </summary>
public class SafeWeaponTakenPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotWeaponSelector), nameof(BotWeaponSelector.OnWeaponTaken));
    }

    [PatchPrefix]
    public static bool Prefix(BotWeaponSelector __instance)
    {
        var wm = __instance?._owner?.WeaponManager;
        if (wm == null || wm._disposed || wm.Melee == null)
        {
            if (__instance != null)
            {
                __instance.IsChanging = false;
            }
            TacticDiagnostics.Count("deadBug.weaponTakenAfterDispose");
            return false;
        }
        return true;
    }
}
