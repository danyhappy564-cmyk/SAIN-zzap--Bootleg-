using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using EFT;
using EFT.Interactive;
using UnityEngine;

namespace SAIN;

/// <summary>
/// zzap fork (2026-10-13, user: "door tactics that use doordash - only while that mod is installed; it gets added and removed,
/// so detect it by itself"): optional bridge to doordash (bmpq, com.tarkin.doordash) - sprinting into a shut door that swings
/// away bursts it open (Breach). Only the main player gets doordash's RaycastBreacher component; we add it to a bot for the
/// length of one ram. Nothing here is referenced at compile time: the mod is found by its BepInEx GUID when SAIN checks plugins
/// (every game start, so adding/removing it takes effect on the next launch) and its members are looked up by name. A missing
/// mod, a renamed member (mod update) or an exception at runtime switches the bridge off, with one log line.
/// Door Overhaul support was dropped the same day (user: "it eats ~600 MB a minute").
/// </summary>
public static class DoorModInterop
{
    public const string DoorDashGUID = "com.tarkin.doordash";

    public static bool DoorDashInstalled { get; private set; }

    /// <summary>Installed and every member we use was found and nothing has thrown since.</summary>
    public static bool DoorDashBridgeOk { get; private set; }

    // ---- doordash members
    private static Type _ddBreacherType;          // tarkin.doordash.RaycastBreacher
    private static ConfigEntry<bool> _ddEnabled;  // Plugin.Enabled
    private static ConfigEntry<float> _ddVelocityThresholdSqr;

    private const BindingFlags ANY_STATIC = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    /// <summary>Called once from ModDetection.CheckPlugins (a few seconds after the game starts, all plugins loaded).</summary>
    public static void Init()
    {
        DoorDashInstalled = Chainloader.PluginInfos.TryGetValue(DoorDashGUID, out var ddInfo) && ddInfo?.Instance != null;

        if (DoorDashInstalled)
        {
            DoorDashBridgeOk = BindDoorDash(ddInfo.Instance.GetType().Assembly, out string missing);
            Logger.LogWarning(DoorDashBridgeOk
                ? $"SAIN: doordash {ddInfo.Metadata.Version} detected - bots may sprint-ram shut doors (room clear, retreat, rush, search)."
                : $"SAIN: doordash {ddInfo.Metadata.Version} detected but '{missing}' was not found (different version?) - the ram stays off.");
        }
    }

    private static bool BindDoorDash(Assembly asm, out string missing)
    {
        missing = null;
        try
        {
            _ddBreacherType = asm.GetType("tarkin.doordash.RaycastBreacher");
            Type plugin = asm.GetType("tarkin.doordash.Plugin");
            if (_ddBreacherType == null || !typeof(MonoBehaviour).IsAssignableFrom(_ddBreacherType)) { missing = "RaycastBreacher"; return false; }
            if (plugin == null) { missing = "Plugin"; return false; }
            _ddEnabled = plugin.GetField("Enabled", ANY_STATIC)?.GetValue(null) as ConfigEntry<bool>;
            _ddVelocityThresholdSqr = plugin.GetField("VelocityThresholdSqr", ANY_STATIC)?.GetValue(null) as ConfigEntry<float>;
            if (_ddEnabled == null) { missing = "Plugin.Enabled"; return false; }
            return true;
        }
        catch (Exception ex)
        {
            missing = ex.GetType().Name;
            return false;
        }
    }

    /// <summary>One bad call (mod updated mid-way, unexpected state) turns the bridge off for the rest of the session -
    /// never an exception every frame.</summary>
    private static void FailDoorDash(string where, Exception ex)
    {
        if (!DoorDashBridgeOk) return;
        DoorDashBridgeOk = false;
        Logger.LogWarning($"SAIN: doordash bridge turned off after an error in {where}: {ex.GetType().Name}: {ex.Message}");
    }

    // ------------------------------------------------------------------ doordash

    /// <summary>doordash present, bridge ok and its own Enabled switch on.</summary>
    public static bool RamActive
    {
        get
        {
            if (!DoorDashBridgeOk) return false;
            try
            {
                return _ddEnabled.Value;
            }
            catch (Exception ex)
            {
                FailDoorDash("RamActive", ex);
                return false;
            }
        }
    }

    /// <summary>doordash's "Velocity Threshold" (squared m/s, default 20 = 4.5 m/s) - the ram only fires above it.</summary>
    public static float RamSpeedSqr
    {
        get
        {
            try
            {
                return _ddVelocityThresholdSqr != null ? _ddVelocityThresholdSqr.Value : 20f;
            }
            catch (Exception)
            {
                return 20f;
            }
        }
    }

    /// <summary>
    /// doordash's own "would this door swing into the player" test (RaycastBreacher.WillDoorSwingTowardsPlayer, copied so the
    /// bot's choice matches what doordash will accept): such a door is never breached, the bot would just bounce off it.
    /// </summary>
    public static bool WillSwingToward(Door door, Vector3 position)
    {
        try
        {
            Vector3 doorPoint = door.transform.TransformPoint(door.viewTarget1);
            Vector2 toTarget = new Vector2(position.x - doorPoint.x, position.z - doorPoint.z);
            Vector3 axis = WorldInteractiveObject.GetRotationAxis(door.DoorForward, door.transform);
            Vector3 shutForward = door.GetDoorRotation(door.GetAngle(EDoorState.Shut)) * axis;
            Vector3 openForward = door.GetDoorRotation(door.GetAngle(EDoorState.Open)) * axis;
            Vector3 bisector = shutForward + openForward;
            return Vector2.Dot(toTarget.normalized, new Vector2(bisector.x, bisector.z).normalized) > 0f;
        }
        catch (Exception ex)
        {
            FailDoorDash("WillSwingToward", ex);
            return true;
        }
    }

    private struct RamJob
    {
        public Player Player;
        public Component Breacher;
        public float Until;
    }

    private const int MAX_RAM_JOBS = 16;
    private static readonly List<RamJob> _ramJobs = new();

    /// <summary>
    /// Gives this bot doordash's ram for <paramref name="seconds"/> (a sprint into a shut door bursts it open, with
    /// doordash's arm damage / stun for the rammer). Always taken away again: by StopRam, when the time runs out (ticked
    /// globally, even if the tactic that asked for it is gone), on death (doordash removes it itself) and at raid end.
    /// </summary>
    public static bool StartRam(Player player, float seconds)
    {
        if (player == null || !RamActive || player.IsYourPlayer)
        {
            return false;
        }
        try
        {
            for (int i = 0; i < _ramJobs.Count; i++)
            {
                if (ReferenceEquals(_ramJobs[i].Player, player))
                {
                    var job = _ramJobs[i];
                    job.Until = Time.time + seconds;
                    _ramJobs[i] = job;
                    return true;
                }
            }
            if (_ramJobs.Count >= MAX_RAM_JOBS || player.gameObject.GetComponent(_ddBreacherType) != null)
            {
                return false;
            }
            Component c = player.gameObject.AddComponent(_ddBreacherType);
            if (c == null)
            {
                return false;
            }
            _ramJobs.Add(new RamJob { Player = player, Breacher = c, Until = Time.time + seconds });
            return true;
        }
        catch (Exception ex)
        {
            FailDoorDash("StartRam", ex);
            return false;
        }
    }

    public static void StopRam(Player player)
    {
        for (int i = _ramJobs.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_ramJobs[i].Player, player))
            {
                RemoveRam(i);
            }
        }
    }

    private static void RemoveRam(int index)
    {
        Component c = _ramJobs[index].Breacher;
        _ramJobs.RemoveAt(index);
        if (c != null)
        {
            UnityEngine.Object.Destroy(c);
        }
    }

    // ------------------------------------------------------------------ global tick / raid

    /// <summary>Every frame from DoorHandler.ManualUpdate. The list is tiny and capped; the loop only removes.</summary>
    public static void Tick(float time)
    {
        for (int i = _ramJobs.Count - 1; i >= 0; i--)
        {
            RamJob job = _ramJobs[i];
            if (job.Breacher == null || job.Player == null || time > job.Until || job.Player.HealthController?.IsAlive != true)
            {
                RemoveRam(i);
            }
        }
    }

    /// <summary>New raid (TacticDiagnostics): drop leftovers and put the detection into the counters / journal.</summary>
    public static void OnNewRaid(Action<string> count, Action<string> line)
    {
        for (int i = _ramJobs.Count - 1; i >= 0; i--)
        {
            RemoveRam(i);
        }
        if (DoorDashInstalled)
        {
            count(DoorDashBridgeOk ? (RamActive ? "doormod.present.doordash" : "doormod.present.doordash.disabled") : "doormod.present.doordash.bridgeFailed");
        }
        line($"[DoorMod] doordash: {(DoorDashInstalled ? (DoorDashBridgeOk ? (RamActive ? "on" : "installed, disabled") : "installed, bridge FAILED") : "not installed")}");
    }
}
