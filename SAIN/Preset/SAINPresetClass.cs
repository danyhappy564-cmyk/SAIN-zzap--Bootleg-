using System;
using EFT.Communications;
using SAIN.Extensions;
using SAIN.Plugin;
using SAIN.Preset.BotSettings;
using SAIN.Preset.GearStealthValues;
using SAIN.Preset.Personalities;
using SAIN.Preset.Server;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.Preset;

namespace SAIN.Preset;

public class SAINPresetClass
{
    public static SAINPresetClass Instance { get; private set; }
    public SAINPresetDefinition Info { get; private set; }
    public GlobalSettingsClass GlobalSettings { get; private set; }
    public SAINBotSettingsClass BotSettings { get; private set; }
    public PersonalityManagerClass PersonalityManager { get; private set; }
    public GearStealthValuesClass GearStealthValuesClass { get; private set; }

    public SAINPresetClass(SAINPresetBundle bundle)
    {
        bundle = PresetSync.CopyForEditing(bundle);
        Instance = this;
        if (bundle.Info.IsCustom)
        {
            SAINPlugin.EditorDefaults.SelectedDefaultPreset = SAINDifficulty.none;
            SAINPlugin.EditorDefaults.SelectedCustomPreset = bundle.Info.Name;
        }
        else
        {
            SAINPlugin.EditorDefaults.SelectedCustomPreset = string.Empty;
            SAINPlugin.EditorDefaults.SelectedDefaultPreset = bundle.Info.BaseSAINDifficulty;
        }

        Info = bundle.Info;
        GlobalSettings = bundle.GlobalSettings ?? new GlobalSettingsClass();

        var serverSettings = new System.Collections.Generic.Dictionary<EFT.WildSpawnType, SAINSettingsGroupClass>();
        if (bundle.BotSettings != null)
        {
            foreach (var kv in bundle.BotSettings)
            {
                serverSettings[kv.Key.ToEft()] = kv.Value;
            }
        }

        BotSettings = new SAINBotSettingsClass(this, serverSettings);
        PersonalityManager = new PersonalityManagerClass(this, bundle.Personalities);
        GearStealthValuesClass = new GearStealthValuesClass(bundle.GearStealthValues);
        MakeCurrent();
    }

    /// <summary>
    /// zzap: make this preset the one every bot reads. <see cref="GlobalSettingsClass"/>'s constructor sets the static
    /// Instance, so whichever preset JSON was deserialized LAST won - e.g. pulling all custom presets reads "zzap TEST"
    /// then "zzap", and bots ran on "zzap"'s global settings while "zzap TEST" was loaded (7th sim: mistakes/adapt on in a
    /// preset that has them off). Called after every load and every deserialization.
    /// </summary>
    public void MakeCurrent()
    {
        Instance = this;
        if (GlobalSettings != null)
        {
            GlobalSettingsClass.Instance = GlobalSettings;
            GlobalSettings.General?.Debug?.MakeCurrent();
        }
    }

    public void Init()
    {
        GlobalSettings.Init();
        BotSettings.Init();
        PersonalityManager.Init();
    }

    public void UpdateDefaults(SAINPresetClass preset = null)
    {
        GlobalSettings.UpdateDefaults(preset?.GlobalSettings);
        BotSettings.UpdateDefaults(preset?.BotSettings);
        PersonalityManager.UpdateDefaults(preset?.PersonalityManager);
    }

    public static void ExportAll(SAINPresetClass preset)
    {
        if (!PresetHandler.CanEditCurrentPreset)
        {
            Logger.LogWarning("[SAIN] Preset editing is locked by the server. Your changes were not saved.");
            return;
        }
        // zzap remake5: during a sim raid with the zzap cross-check the behaviour sections hold zzap's values, not this preset's -
        // saving now would write them into the preset. Save after the raid (the sim preset's own values are back then).
        if (SAIN.SimLab.SimLabZzapCheck.Applied)
        {
            Logger.LogWarning("[SAIN] Sim raid with the zzap cross-check running - preset not saved (save again after the raid).");
            return;
        }

        // Shipped zzap presets always save to a detached Modified copy.
        bool bundledZzap = preset.Info.IsCustom && QuickSettings.FindZzap(preset.Info.Name) != null;

        if (preset.Info.IsCustom == false || bundledZzap)
        {
            SAINPresetDefinition newPreset = preset.Info.Clone();

            if (bundledZzap)
            {
                newPreset.Name = PresetSavePolicy.NameForSave(preset.Info.Name, preset.Info.Name, name => PresetSync.TryGetCustomBundle(name, out _));
            }
            else
            {
                newPreset.Name += " [Modified]";
            }
            newPreset.Creator = "user";
            newPreset.Description = "[Modified] " + newPreset.Description;
            newPreset.DateCreated = DateTime.Today.ToString();

            if (!PresetSync.PublishCustom(preset, newPreset)) return;
            PresetHandler.SavePresetDefinition(newPreset);
            PresetHandler.InitPresetFromDefinition(newPreset);
            ConfigEditingTracker.Clear();
            PresetHandler.UpdateExistingBots();
            if (bundledZzap)
            {
                Logger.LogInfo($"[SAIN] Bundled preset '{preset.Info.Name}' kept as shipped - your changes were saved as '{newPreset.Name}'.");
                NotifyCopy(string.Format(
                    SAIN.Editor.EditorText.Korean ? "'{0}'은(는) 그대로 두고 '{1}'(으)로 저장했습니다." : "'{0}' left as shipped - saved as '{1}'.",
                    preset.Info.Name, newPreset.Name));
            }
            return;
        }

        if (!PresetSync.PushCustomPreset(preset)) return;
        ConfigEditingTracker.Clear();
        PresetHandler.UpdateExistingBots();
    }

    private static void NotifyCopy(string message)
    {
        try
        {
            NotificationManager.DisplayMessageNotification(message, ENotificationDurationType.Long, ENotificationIconType.Default, null);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[SAIN] Could not show the saved-copy message: {ex.Message}");
        }
    }
}

public abstract class BasePreset
{
    public BasePreset(SAINPresetClass presetClass)
    {
        Preset = presetClass;
        Info = presetClass.Info;
    }

    public readonly SAINPresetClass Preset;
    public readonly SAINPresetDefinition Info;
}
