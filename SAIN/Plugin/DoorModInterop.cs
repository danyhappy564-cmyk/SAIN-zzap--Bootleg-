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
/// zzap fork (2026-10-13, user: "door tactics that use Door Overhaul / doordash - only while those mods are installed; they
/// get added and removed, so detect it by itself"): optional bridge to two door mods. Nothing here is referenced at compile
/// time - both mods are found by their BepInEx GUID when SAIN checks plugins (every game start, so adding/removing a mod
/// takes effect on the next launch) and their members are looked up by name. A missing mod, a renamed member (mod update)
/// or an exception at runtime switches only that bridge off, with one log line - SAIN's own door code never depends on it.
///
///   Door Overhaul (SageSPT, com.sage.dooroverhaul): destructible doors. Bullets punch through a door when their penetration
///     is at least the door material's threshold (F6 of that mod), grenades blow doors apart, a destroyed leaf is taken over
///     (DoorState forced Open, Operatable off). Player peeks leave doors standing partly open (state Open, small angle).
///   doordash (bmpq, com.tarkin.doordash): sprinting into a shut door bursts it open (Breach) - only the main player gets the
///     RaycastBreacher component; we add it to a bot for the length of one ram.
/// </summary>
public static class DoorModInterop
{
    public const string DoorOverhaulGUID = "com.sage.dooroverhaul";
    public const string DoorDashGUID = "com.tarkin.doordash";

    public static bool DoorOverhaulInstalled { get; private set; }
    public static bool DoorDashInstalled { get; private set; }

    /// <summary>Installed and every member we use was found and nothing has thrown since.</summary>
    public static bool DoorOverhaulBridgeOk { get; private set; }
    public static bool DoorDashBridgeOk { get; private set; }

    // ---- Door Overhaul members
    private static MethodInfo _doTryGet;          // DoorDestructionRegistry.TryGet(Door, out DoorEntry)
    private static FieldInfo _doEntryEligible;    // DoorEntry.Eligible
    private static FieldInfo _doEntryProfile;     // DoorEntry.Profile (MaterialProfile)
    private static FieldInfo _doEntryTakeover;    // DoorEntry.Takeover
    private static FieldInfo _doProfilePen;       // MaterialProfile.ThroughPenetration
    private static MethodInfo _doHasHole;         // DestructionVisuals.HasHole(Door)
    private static PropertyInfo _doFikaPresent;   // DoorDestructionRegistry.FikaPresent
    private static ConfigEntry<bool> _doDestructionEnabled;
    private static PropertyInfo _doPeekActive;    // PeekDriver.IsActive (optional)
    private static PropertyInfo _doPeekDoor;      // PeekDriver.Door (optional)

    // ---- doordash members
    private static Type _ddBreacherType;          // tarkin.doordash.RaycastBreacher
    private static ConfigEntry<bool> _ddEnabled;  // Plugin.Enabled
    private static ConfigEntry<float> _ddVelocityThresholdSqr;

    private const BindingFlags ANY_STATIC = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags ANY_INSTANCE = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>Called once from ModDetection.CheckPlugins (a few seconds after the game starts, all plugins loaded).</summary>
    public static void Init()
    {
        DoorOverhaulInstalled = Chainloader.PluginInfos.TryGetValue(DoorOverhaulGUID, out var doInfo) && doInfo?.Instance != null;
        DoorDashInstalled = Chainloader.PluginInfos.TryGetValue(DoorDashGUID, out var ddInfo) && ddInfo?.Instance != null;

        if (DoorOverhaulInstalled)
        {
            DoorOverhaulBridgeOk = BindDoorOverhaul(doInfo.Instance.GetType().Assembly, out string missing);
            Logger.LogWarning(DoorOverhaulBridgeOk
                ? $"SAIN: Door Overhaul {doInfo.Metadata.Version} detected - door mod tactics can use destructible doors."
                : $"SAIN: Door Overhaul {doInfo.Metadata.Version} detected but '{missing}' was not found (different version?) - SAIN's Door Overhaul tactics stay off.");
        }
        if (DoorDashInstalled)
        {
            DoorDashBridgeOk = BindDoorDash(ddInfo.Instance.GetType().Assembly, out string missing);
            Logger.LogWarning(DoorDashBridgeOk
                ? $"SAIN: doordash {ddInfo.Metadata.Version} detected - bots may sprint-ram doors in a room clear."
                : $"SAIN: doordash {ddInfo.Metadata.Version} detected but '{missing}' was not found (different version?) - the ram stays off.");
        }
    }

    private static bool BindDoorOverhaul(Assembly asm, out string missing)
    {
        missing = null;
        try
        {
            Type registry = asm.GetType("DoorOverhaul.Destruction.DoorDestructionRegistry");
            Type entry = asm.GetType("DoorOverhaul.Destruction.DoorEntry");
            Type profile = asm.GetType("DoorOverhaul.Destruction.Core.MaterialProfile");
            Type visuals = asm.GetType("DoorOverhaul.Destruction.DestructionVisuals");
            Type settings = asm.GetType("DoorOverhaul.Settings");
            Type peek = asm.GetType("DoorOverhaul.Doors.PeekDriver");
            if (registry == null) { missing = "DoorDestructionRegistry"; return false; }
            if (entry == null) { missing = "DoorEntry"; return false; }
            if (profile == null) { missing = "MaterialProfile"; return false; }
            if (visuals == null) { missing = "DestructionVisuals"; return false; }
            if (settings == null) { missing = "Settings"; return false; }

            _doTryGet = registry.GetMethod("TryGet", ANY_STATIC, null, new[] { typeof(Door), entry.MakeByRefType() }, null);
            _doFikaPresent = registry.GetProperty("FikaPresent", ANY_STATIC);
            _doEntryEligible = entry.GetField("Eligible", ANY_INSTANCE);
            _doEntryProfile = entry.GetField("Profile", ANY_INSTANCE);
            _doEntryTakeover = entry.GetField("Takeover", ANY_INSTANCE);
            _doProfilePen = profile.GetField("ThroughPenetration", ANY_INSTANCE);
            _doHasHole = visuals.GetMethod("HasHole", ANY_STATIC, null, new[] { typeof(Door) }, null);
            _doDestructionEnabled = settings.GetField("DestructionEnabled", ANY_STATIC)?.GetValue(null) as ConfigEntry<bool>;
            _doPeekActive = peek?.GetProperty("IsActive", ANY_STATIC);
            _doPeekDoor = peek?.GetProperty("Door", ANY_STATIC);

            if (_doTryGet == null) { missing = "DoorDestructionRegistry.TryGet"; return false; }
            if (_doEntryEligible == null) { missing = "DoorEntry.Eligible"; return false; }
            if (_doEntryProfile == null) { missing = "DoorEntry.Profile"; return false; }
            if (_doEntryTakeover == null) { missing = "DoorEntry.Takeover"; return false; }
            if (_doProfilePen == null) { missing = "MaterialProfile.ThroughPenetration"; return false; }
            if (_doHasHole == null) { missing = "DestructionVisuals.HasHole"; return false; }
            if (_doDestructionEnabled == null) { missing = "Settings.DestructionEnabled"; return false; }
            return true;
        }
        catch (Exception ex)
        {
            missing = ex.GetType().Name;
            return false;
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

    /// <summary>One bad call (mod updated mid-way, unexpected state) turns that bridge off for the rest of the session -
    /// never an exception every frame.</summary>
    private static void FailDoorOverhaul(string where, Exception ex)
    {
        if (!DoorOverhaulBridgeOk) return;
        DoorOverhaulBridgeOk = false;
        Logger.LogWarning($"SAIN: Door Overhaul bridge turned off after an error in {where}: {ex.GetType().Name}: {ex.Message}");
    }

    private static void FailDoorDash(string where, Exception ex)
    {
        if (!DoorDashBridgeOk) return;
        DoorDashBridgeOk = false;
        Logger.LogWarning($"SAIN: doordash bridge turned off after an error in {where}: {ex.GetType().Name}: {ex.Message}");
    }

    // ------------------------------------------------------------------ Door Overhaul

    /// <summary>Door destruction is live right now (mod present, bridge ok, its own switch on, no Fika - it disables itself there).</summary>
    public static bool DestructionActive
    {
        get
        {
            if (!DoorOverhaulBridgeOk) return false;
            try
            {
                if (!_doDestructionEnabled.Value) return false;
                if (_doFikaPresent != null && (bool)_doFikaPresent.GetValue(null)) return false;
                return true;
            }
            catch (Exception ex)
            {
                FailDoorOverhaul("DestructionActive", ex);
                return false;
            }
        }
    }

    /// <summary>
    /// The door can be shot through / blown apart by Door Overhaul: registered, eligible (not an exit / special door), not
    /// already taken over. <paramref name="throughPen"/> = the penetration a bullet needs to punch through it.
    /// </summary>
    public static bool IsDestructible(Door door, out float throughPen)
    {
        throughPen = 0f;
        if (door == null || !DestructionActive) return false;
        try
        {
            object[] args = { door, null };
            if (!(bool)_doTryGet.Invoke(null, args) || args[1] == null) return false;
            object entry = args[1];
            if (!(bool)_doEntryEligible.GetValue(entry)) return false;
            if (_doEntryTakeover.GetValue(entry) != null) return false;
            object profile = _doEntryProfile.GetValue(entry);
            if (profile == null) return false;
            throughPen = (float)_doProfilePen.GetValue(profile);
            return true;
        }
        catch (Exception ex)
        {
            FailDoorOverhaul("IsDestructible", ex);
            return false;
        }
    }

    /// <summary>The leaf was destroyed / torn off (Door Overhaul took it over: state Open, not operatable, no collider in the frame).</summary>
    public static bool IsDestroyed(Door door)
    {
        if (door == null || !DoorOverhaulBridgeOk) return false;
        try
        {
            object[] args = { door, null };
            return (bool)_doTryGet.Invoke(null, args) && args[1] != null && _doEntryTakeover.GetValue(args[1]) != null;
        }
        catch (Exception ex)
        {
            FailDoorOverhaul("IsDestroyed", ex);
            return false;
        }
    }

    public static bool HasHole(Door door)
    {
        if (door == null || !DoorOverhaulBridgeOk) return false;
        try
        {
            return (bool)_doHasHole.Invoke(null, new object[] { door });
        }
        catch (Exception ex)
        {
            FailDoorOverhaul("HasHole", ex);
            return false;
        }
    }

    /// <summary>The main player is holding this door in a Door Overhaul peek right now (it drives the angle every frame).</summary>
    public static bool PlayerPeekingDoor(WorldInteractiveObject door)
    {
        if (door == null || !DoorOverhaulBridgeOk || _doPeekActive == null || _doPeekDoor == null) return false;
        try
        {
            return (bool)_doPeekActive.GetValue(null) && ReferenceEquals(_doPeekDoor.GetValue(null), door);
        }
        catch (Exception ex)
        {
            FailDoorOverhaul("PlayerPeekingDoor", ex);
            return false;
        }
    }

    // ---- partly open doors (Door Overhaul peeks leave them at 8-33 % - DoorState Open, leaf still across the doorway)

    /// <summary>How far open a door stands, 0 = shut angle, 1 = fully open angle (same math as Door Overhaul's DoorMath).</summary>
    public static float OpenFraction(WorldInteractiveObject door)
    {
        float travel = Mathf.DeltaAngle(door.CloseAngle, door.OpenAngle);
        if (Mathf.Abs(travel) < 1f)
        {
            return door.DoorState == EDoorState.Open ? 1f : 0f;
        }
        return Mathf.Clamp01(Mathf.DeltaAngle(door.CloseAngle, door.CurrentAngle) / travel);
    }

    public const float PARTLY_OPEN_MAX = 0.6f;

    /// <summary>State Open but the leaf still stands mostly across the doorway. Vanilla never leaves a door like this, so
    /// this is only ever true with Door Overhaul (player peek / quiet open). Sliding doors and switches never count.</summary>
    public static bool IsPartlyOpen(Door door)
    {
        if (door == null || !DoorOverhaulInstalled || door.DoorState != EDoorState.Open || door is SlidingDoor || !door.Operatable)
        {
            return false;
        }
        if (Mathf.Abs(Mathf.DeltaAngle(door.CloseAngle, door.OpenAngle)) < 1f)
        {
            return false;
        }
        return OpenFraction(door) < PARTLY_OPEN_MAX;
    }

    private struct PushJob
    {
        public Door Door;
        public float From;
        public float StartTime;
    }

    private const float PUSH_TIME = 0.45f;
    private const int MAX_PUSH_JOBS = 32;
    private static readonly List<PushJob> _pushJobs = new();

    /// <summary>
    /// A bot pushes a partly open door the rest of the way (0.45s swing, squeak). The door keeps its Open state - only the
    /// angle moves, like Door Overhaul's own body push. Skipped while the player is holding that door in a peek.
    /// </summary>
    public static bool RequestPushOpen(Door door, out bool started)
    {
        started = false;
        if (!IsPartlyOpen(door) || PlayerPeekingDoor(door))
        {
            return false;
        }
        for (int i = 0; i < _pushJobs.Count; i++)
        {
            if (ReferenceEquals(_pushJobs[i].Door, door))
            {
                return true;
            }
        }
        if (_pushJobs.Count >= MAX_PUSH_JOBS)
        {
            return false;
        }
        _pushJobs.Add(new PushJob { Door = door, From = OpenFraction(door), StartTime = Time.time });
        started = true;
        try
        {
            if (door.SqueakSound != null && door.SqueakSound.Length > 0)
            {
                AudioClip clip = door.SqueakSound[UnityEngine.Random.Range(0, door.SqueakSound.Length)];
                if (clip != null)
                {
                    door.PlaySoundAtPoint(door.GetSoundPos(), clip, BetterAudio.AudioSourceGroupType.InteractiveObjects, door._squeakSoundRolloff, 0.5f);
                }
            }
        }
        catch (Exception)
        {
            // sound only
        }
        return true;
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

    /// <summary>Every frame from DoorHandler.ManualUpdate. Both lists are tiny and capped; the loops only remove.</summary>
    public static void Tick(float time)
    {
        for (int i = _pushJobs.Count - 1; i >= 0; i--)
        {
            PushJob job = _pushJobs[i];
            Door door = job.Door;
            if (door == null || door.DoorState != EDoorState.Open || !door.Operatable || PlayerPeekingDoor(door))
            {
                _pushJobs.RemoveAt(i);
                continue;
            }
            float t = Mathf.Clamp01((time - job.StartTime) / PUSH_TIME);
            float fraction = Mathf.Lerp(job.From, 1f, t * t * (3f - 2f * t));
            try
            {
                door.CurrentAngle = door.CloseAngle + Mathf.DeltaAngle(door.CloseAngle, door.OpenAngle) * fraction;
            }
            catch (Exception)
            {
                _pushJobs.RemoveAt(i);
                continue;
            }
            if (t >= 1f)
            {
                _pushJobs.RemoveAt(i);
            }
        }
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
        _pushJobs.Clear();
        for (int i = _ramJobs.Count - 1; i >= 0; i--)
        {
            RemoveRam(i);
        }
        if (DoorOverhaulInstalled)
        {
            count(DoorOverhaulBridgeOk ? (DestructionActive ? "doormod.present.DoorOverhaul" : "doormod.present.DoorOverhaul.destructionOff") : "doormod.present.DoorOverhaul.bridgeFailed");
        }
        if (DoorDashInstalled)
        {
            count(DoorDashBridgeOk ? (RamActive ? "doormod.present.doordash" : "doormod.present.doordash.disabled") : "doormod.present.doordash.bridgeFailed");
        }
        line($"[DoorMod] Door Overhaul: {(DoorOverhaulInstalled ? (DoorOverhaulBridgeOk ? (DestructionActive ? "on" : "installed, destruction off") : "installed, bridge FAILED") : "not installed")}"
            + $" / doordash: {(DoorDashInstalled ? (DoorDashBridgeOk ? (RamActive ? "on" : "installed, disabled") : "installed, bridge FAILED") : "not installed")}");
    }
}
