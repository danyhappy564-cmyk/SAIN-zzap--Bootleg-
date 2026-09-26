global using EFTMath = MyExtensions;
using BepInEx;
using BepInEx.Configuration;
using SAIN.Components.BotController;
using SAIN.Editor;
using SAIN.Helpers;
using SAIN.Plugin;
using SAIN.Preset;
using SAIN.Preset.Server;
using SAIN.Preset.Shared;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.GlobalSettings.Categories.General;
using SPT.Reflection.Patching;
using UnityEngine;
using static SAIN.AssemblyInfoClass;

namespace SAIN;

[BepInPlugin(SAINGUID, SAINName, SAINVersionInfo.SAINVersion)]
[BepInDependency(BigBrainGUID, BigBrainVersion)]
//[BepInDependency(SPTGUID, SPTVersion)]
[BepInProcess(EscapeFromTarkov)]
[BepInIncompatibility("com.dvize.BushNoESP")]
[BepInIncompatibility("com.dvize.NoGrenadeESP")]
public class SAINPlugin : BaseUnityPlugin
{
    private PatchManager _patchManager;

    public static DebugSettings DebugSettings
    {
        get { return LoadedPreset.GlobalSettings.General.Debug; }
    }

    public static bool DebugMode
    {
        get { return DebugSettings.Logs.GlobalDebugMode; }
    }

    public static bool ProfilingMode
    {
        get { return DebugSettings.Logs.GlobalProfilingToggle; }
    }

    public static bool DrawDebugGizmos
    {
        get { return DebugSettings.Gizmos.DrawDebugGizmos; }
    }

    public static PresetEditorDefaults EditorDefaults
    {
        get { return PresetHandler.EditorDefaults; }
    }

    public static ECombatDecision ForceSoloDecision = ECombatDecision.None;

    public static ESquadDecision ForceSquadDecision = ESquadDecision.None;

    public static ESelfActionType ForceSelfDecision = ESelfActionType.None;

    public void Awake()
    {
        LogBuildStamp();
        _patchManager = new(this, true);

        if (!PresetHandler.Init() || !EFTCoreSettings.Load())
        {
            Logger.LogError(
                "[SAIN] Startup aborted: could not load the presets and core overrides from the SAIN server mod, re-install SAIN correctly and restart."
            );

            return;
        }
        BotSpawnController.LoadExclusionList();
        BindConfigs();
        _patchManager.EnablePatches();
        BigBrainHandler.Init();
    }

    /// <summary>
    /// Writes the loaded SAIN DLL's file timestamp to the log, so a field log can be matched to the
    /// exact build that produced it (2026-09-24: a log was misjudged as coming from an older build
    /// because nothing in it identified which DLL was actually loaded).
    /// </summary>
    private void LogBuildStamp()
    {
        try
        {
            string dllPath = typeof(SAINPlugin).Assembly.Location;
            Logger.LogInfo(
                $"[SAIN] Loaded DLL {System.IO.Path.GetFileName(dllPath)} built/modified at "
                    + $"{System.IO.File.GetLastWriteTime(dllPath):yyyy-MM-dd HH:mm:ss} (local time)"
            );
        }
        catch (System.Exception ex)
        {
            Logger.LogWarning($"[SAIN] Could not read DLL timestamp: {ex.Message}");
        }
    }

    private void BindConfigs()
    {
        string category = "SAIN Editor";
        OpenEditorButton = Config.Bind(category, "Open Editor", false, "Opens the Editor on press");
        OpenEditorConfigEntry = Config.Bind(
            category,
            "Open Editor Shortcut",
            new KeyboardShortcut(KeyCode.F6),
            "The keyboard shortcut that toggles editor"
        );
    }

    public static ConfigEntry<bool> OpenEditorButton { get; private set; }

    public static ConfigEntry<KeyboardShortcut> OpenEditorConfigEntry { get; private set; }

    public static SAINPresetClass LoadedPreset
    {
        get { return PresetHandler.LoadedPreset; }
    }

    public void Update()
    {
        PresetSyncWebSocket.Resync();

        ModDetection.ManualUpdate();
        SAINEditor.ManualUpdate();
        DebugGizmos.ManualUpdate();
    }

    public void Start()
    {
        SAINEditor.Init();
    }

    public void OnDestroy()
    {
        PresetSyncWebSocket.Instance?.Close();
    }

    public void LateUpdate()
    {
        SAINEditor.LateUpdate();
    }

    public void OnGUI()
    {
        SAINEditor.OnGUI();
    }
}
