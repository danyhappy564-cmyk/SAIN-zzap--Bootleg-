using System;
using System.Reflection;
using BepInEx.Bootstrap;
using SAIN.Helpers;
using UnityEngine;
using static SAIN.AssemblyInfoClass;
using static SAIN.Editor.SAINLayout;

namespace SAIN;

public static class ModDetection
{
    static ModDetection()
    {
        ModsCheckTimer = Time.time + 5f;
    }

    public static void ManualUpdate()
    {
        if (!ModsChecked && ModsCheckTimer < Time.time && ModsCheckTimer > 0)
        {
            ModsChecked = true;
            CheckPlugins();
        }
    }

    public static bool LootingBotsLoaded { get; private set; }
    public static bool RealismLoaded { get; private set; }
    public static bool QuestingBotsLoaded { get; private set; }
    public static bool ProjectFikaLoaded { get; private set; }
    public static bool ProjectFikaHeadlessLoaded { get; private set; }

    public static class FikaInterop
    {
        private static Assembly FikaAssembly { get; set; }
        private static Type FikaBackendUtils { get; set; }
        private static PropertyInfo IsServerProperty { get; set; }
        private static PropertyInfo IsClientProperty { get; set; }

        public static void InitializeInterop()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "Fika.Core")
                {
                    FikaAssembly = assembly;
                    break;
                }
            }

            if (FikaAssembly == null)
            {
                return;
            }

            FikaBackendUtils = FikaAssembly.GetType("Fika.Core.Main.Utils.FikaBackendUtils");
            IsServerProperty = FikaBackendUtils.GetProperty("IsServer", BindingFlags.Public | BindingFlags.Static);
            IsClientProperty = FikaBackendUtils.GetProperty("IsClient", BindingFlags.Public | BindingFlags.Static);
        }

        public static bool IsServer()
        {
            if (IsServerProperty == null)
            {
                return false;
            }

            return (bool)IsServerProperty.GetValue(null);
        }

        public static bool IsClient()
        {
            if (IsClientProperty == null)
            {
                return false;
            }

            return (bool)IsClientProperty.GetValue(null);
        }
    }

    public static void CheckPlugins()
    {
        if (Chainloader.PluginInfos.ContainsKey(FikaGUID))
        {
            ProjectFikaLoaded = true;

            FikaInterop.InitializeInterop();

            Logger.LogInfo($"SAIN: Project Fika Detected.");
        }

        if (Chainloader.PluginInfos.ContainsKey(FikaHeadlessGUID))
        {
            ProjectFikaHeadlessLoaded = true;
            Logger.LogInfo($"SAIN: Project Fika Headless Detected.");
        }

        if (Chainloader.PluginInfos.ContainsKey(QuestingBotsGUID))
        {
            QuestingBotsLoaded = true;
            Logger.LogInfo($"SAIN: Questing Bots Detected.");
        }
        if (Chainloader.PluginInfos.ContainsKey(LootingBotsGUID))
        {
            LootingBotsLoaded = true;
            Logger.LogInfo($"SAIN: Looting Bots Detected.");
        }
        if (Chainloader.PluginInfos.ContainsKey(RealismModKey))
        {
            RealismLoaded = true;
            Logger.LogInfo($"SAIN: Realism Detected.");
        }
        // zzap: which door mods (doordash / DualSideDoorBreach) are installed - logs + stay out of their way.
        DoorModInterop.Init();
    }

    public static void UpdateArmorClassCoef()
    {
        if (RealismLoaded)
        {
            EFTCoreSettings.UpdateArmorClassCoef(3.5f);
            Logger.LogInfo($"Realism Detected, updating armor class number to reflect new armor classes...");
        }
        else
        {
            EFTCoreSettings.UpdateArmorClassCoef(6f);
        }
    }

    public static void ModDetectionGUI()
    {
        BeginVertical();

        BeginHorizontal();
        IsDetected(LootingBotsLoaded, "Looting Bots");
        IsDetected(QuestingBotsLoaded, "Questing Bots");
        IsDetected(RealismLoaded, "Realism Mod");
        EndHorizontal();

        EndVertical();
    }

    private static void IsDetected(bool value, string name)
    {
        Label(name);
        Box(value ? "Detected" : "Not Detected");
    }

    private static readonly float ModsCheckTimer = -1f;
    private static bool ModsChecked = false;
}
