using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using SAIN.SAINComponent.Classes.Mover;

namespace SAIN;

/// <summary>
/// zzap fork (2026-10-13): which door mods are installed, for logs and to stay out of their way. Bots no longer use any of them -
/// the sprint ram is SAIN's own (BotDoorBreacher, ported from doordash + DualSideDoorBreach, works with neither installed).
///   - doordash (bmpq, com.tarkin.doordash): only logged. Its ram stays on the main player with its own config; SAIN never adds
///     it to bots any more (user: "the doordash config is the player's, bots get SAIN's values").
///   - DualSideDoorBreach (maschine34675, com.maschine.DualSideDoorBreach): its KickOpen prefix turns every kick away from the
///     kicker while its Enabled + AdjustOpenDirection are on. Only logged since 10/14: on SAIN's own bot rams SAIN sets the final
///     angle after it (its side test misreads doors kicked from beside the hinge - BotDoorBreacher.WillSwingToward).
/// Nothing here is referenced at compile time: found by BepInEx GUID once all plugins are loaded, members looked up by name;
/// anything unexpected is logged once and handled the safe way.
/// Door Overhaul support was dropped the same day (user: "it eats ~600 MB a minute").
/// </summary>
public static class DoorModInterop
{
    public const string DoorDashGUID = "com.tarkin.doordash";
    public const string DualSideGUID = "com.maschine.DualSideDoorBreach";

    public static bool DoorDashInstalled { get; private set; }
    public static bool DualSideInstalled { get; private set; }

    private static string _doorDashVersion = string.Empty;
    private static string _dualSideVersion = string.Empty;
    private static ConfigEntry<bool> _dsEnabled;
    private static ConfigEntry<bool> _dsAdjustOpenDirection;
    private static bool _dsBound;

    private const BindingFlags ANY_STATIC = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    /// <summary>Called once from ModDetection.CheckPlugins (a few seconds after the game starts, all plugins loaded).</summary>
    public static void Init()
    {
        DoorDashInstalled = Chainloader.PluginInfos.TryGetValue(DoorDashGUID, out var ddInfo) && ddInfo?.Instance != null;
        DualSideInstalled = Chainloader.PluginInfos.TryGetValue(DualSideGUID, out var dsInfo) && dsInfo?.Instance != null;
        _doorDashVersion = DoorDashInstalled ? ddInfo.Metadata.Version.ToString() : string.Empty;
        _dualSideVersion = DualSideInstalled ? dsInfo.Metadata.Version.ToString() : string.Empty;

        if (DoorDashInstalled)
        {
            Logger.LogWarning($"SAIN: doordash {_doorDashVersion} detected - it stays the player's; bots use SAIN's own sprint ram (F6 Door Tactics).");
        }
        if (DualSideInstalled)
        {
            _dsBound = BindDualSide(dsInfo.Instance.GetType().Assembly);
            Logger.LogWarning(_dsBound
                ? $"SAIN: DualSideDoorBreach {_dualSideVersion} detected - while its door direction setting is on, it turns bots' rammed doors (SAIN doesn't flip them twice)."
                : $"SAIN: DualSideDoorBreach {_dualSideVersion} detected but its settings were not found (different version?) - assuming it turns doors itself.");
        }
    }

    private static bool BindDualSide(Assembly asm)
    {
        try
        {
            Type plugin = asm.GetType("DualSideDoorBreach.Plugin");
            _dsEnabled = plugin?.GetField("Enabled", ANY_STATIC)?.GetValue(null) as ConfigEntry<bool>;
            _dsAdjustOpenDirection = plugin?.GetField("AdjustOpenDirection", ANY_STATIC)?.GetValue(null) as ConfigEntry<bool>;
            return _dsEnabled != null && _dsAdjustOpenDirection != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// DualSideDoorBreach will turn a bot's breach away from it by itself. Unreadable settings count as yes (its default):
    /// a door that ends up swinging into the bot is a smaller problem than one flipped twice (= back into the bot every time).
    /// </summary>
    public static bool DualSideHandlesSwing
    {
        get
        {
            if (!DualSideInstalled)
            {
                return false;
            }
            if (!_dsBound)
            {
                return true;
            }
            try
            {
                return _dsEnabled.Value && _dsAdjustOpenDirection.Value;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    /// <summary>New raid (TacticDiagnostics): put what was detected into the counters / journal.</summary>
    public static void OnNewRaid(Action<string> count, Action<string> line)
    {
        BotDoorBreacher.OnNewRaid();
        count(BotDoorBreacher.Active ? "doorram.on" : "doorram.off");
        if (DoorDashInstalled) count("doormod.present.doordash");
        if (DualSideInstalled) count(DualSideHandlesSwing ? "doormod.present.dualSide" : "doormod.present.dualSide.directionOff");
        line($"[DoorMod] bot sprint ram (SAIN): {(BotDoorBreacher.Active ? $"on, {BotDoorBreacher.MinSpeed:0.0} m/s, both sides {BotDoorBreacher.BothSides}" : "off")}"
            + $" | doordash: {(DoorDashInstalled ? _doorDashVersion + " (player only)" : "not installed")}"
            + $" | DualSideDoorBreach: {(DualSideInstalled ? _dualSideVersion + (DualSideHandlesSwing ? " (turns player kicks; bot rams set by SAIN)" : " (direction off)") : "not installed")}");
    }
}
