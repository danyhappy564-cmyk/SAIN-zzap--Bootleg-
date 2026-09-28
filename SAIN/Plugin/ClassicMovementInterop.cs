using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace SAIN;

/// <summary>
/// zzap: detects Boogle's Classic Movement (com.boogle.classicmovement) and reads its server config through reflection
/// (static field OldTarkovMovement.Plugin.ModConfig, filled from the server in the plugin's Awake). It's a common mod and
/// it changes things SAIN tuning depends on:
///  - NoInertia: inertia-less movement states for the player; for bots only if BotsUseOldMovement (isAI check).
///  - QuickTilting: fast lean for EVERY player, bots included (no isAI check in that patch).
///  - DoesAimingSlowYouDown / DoBushesSlowYouDown: also for everyone.
/// Read once per raid (Refresh) and logged as [ClassicMovement].
/// </summary>
public static class ClassicMovementInterop
{
    public const string GUID = "com.boogle.classicmovement";

    public static bool Checked { get; private set; }
    public static bool Installed { get; private set; }
    public static bool ConfigRead { get; private set; }
    public static string Version { get; private set; } = "-";
    public static bool NoInertia { get; private set; }
    public static bool QuickTilting { get; private set; }
    public static bool BotsUseOldMovement { get; private set; }
    public static bool AimingSlowsDown { get; private set; } = true;
    public static bool BushesSlowDown { get; private set; } = true;

    /// <summary>The human player moves without inertia (instant direction changes).</summary>
    public static bool PlayerNoInertia
    {
        get { Ensure(); return Installed && NoInertia; }
    }

    /// <summary>Bots also move without inertia (mod setting BotsUseOldMovement).</summary>
    public static bool BotsNoInertia
    {
        get { Ensure(); return Installed && NoInertia && BotsUseOldMovement; }
    }

    /// <summary>Bots lean fast (the quick-tilt patch applies to every player).</summary>
    public static bool BotsQuickTilt
    {
        get { Ensure(); return Installed && QuickTilting; }
    }

    private static void Ensure()
    {
        if (!Checked)
        {
            Refresh();
        }
    }

    public static void Refresh()
    {
        Checked = true;
        Installed = false;
        ConfigRead = false;
        try
        {
            if (!Chainloader.PluginInfos.TryGetValue(GUID, out var info) || info == null)
            {
                return;
            }
            Installed = true;
            Version = info.Metadata?.Version?.ToString() ?? "?";
            // Mod defaults (its settings.jsonc) in case the config can't be read.
            NoInertia = true;
            QuickTilting = true;
            BotsUseOldMovement = true;
            AimingSlowsDown = true;
            BushesSlowDown = false;

            Type pluginType = info.Instance != null ? info.Instance.GetType() : null;
            object config = pluginType?.GetField("ModConfig", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (config == null)
            {
                return;
            }
            NoInertia = Read(config, "NoInertia", NoInertia);
            QuickTilting = Read(config, "QuickTilting", QuickTilting);
            BotsUseOldMovement = Read(config, "BotsUseOldMovement", BotsUseOldMovement);
            AimingSlowsDown = Read(config, "DoesAimingSlowYouDown", AimingSlowsDown);
            BushesSlowDown = Read(config, "DoBushesSlowYouDown", BushesSlowDown);
            ConfigRead = true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[ClassicMovement] detection failed: {ex.Message}");
        }
    }

    private static bool Read(object config, string name, bool fallback)
    {
        PropertyInfo prop = config.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.PropertyType == typeof(bool))
        {
            return (bool)prop.GetValue(config);
        }
        return fallback;
    }

    public static string Describe()
    {
        Ensure();
        if (!Installed)
        {
            return "not installed";
        }
        return $"installed v{Version} (config {(ConfigRead ? "read" : "NOT read, mod defaults assumed")}): "
            + $"player no-inertia={PlayerNoInertia}, bots no-inertia={BotsNoInertia} (BotsUseOldMovement={BotsUseOldMovement}), "
            + $"quick lean for everyone incl. bots={QuickTilting}, aiming slows={AimingSlowsDown}, bushes slow={BushesSlowDown}";
    }
}
