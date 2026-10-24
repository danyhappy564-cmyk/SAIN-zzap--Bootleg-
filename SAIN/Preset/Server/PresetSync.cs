using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SAIN.Extensions;
using SAIN.Plugin;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.Models.WS;
using SAIN.Preset.Shared.Preset;
using SharedWildSpawnType = SAIN.Preset.Shared.Enums.ESainWildSpawnType;

namespace SAIN.Preset.Server;

public static class PresetSync
{
    private static readonly JsonSerializerSettings _settings = new()
    {
        Converters = { new StringEnumConverter() },
        Formatting = Formatting.Indented,
    };

    private static bool InRaid
    {
        get { return SAIN.Components.GameWorldComponent.Instance != null; }
    }

    private static List<SAINPresetBundle> _serverDefaults;
    private static bool _serverDefaultsFetched;
    private static string _serverDefaultsJson;
    private static List<SAINPresetBundle> _savedServerDefaults;
    private static readonly Dictionary<string, (string Json, SAINPresetBundle Bundle)> _savedCustomBundles = new(StringComparer.Ordinal);
    private static bool _liveReapplyPending;
    private static bool _configReapplyPending;
    private static SAINPresetDefinition _pendingConfigTarget;
    private static SAINPresetDefinition _pendingSwitchTarget;

    private static readonly Dictionary<string, SAINPresetBundle> _customBundles = new(StringComparer.Ordinal);

    // zzap fork (2026-10-18): each custom preset's JSON as last pulled or saved. The editor edits the cached bundle's objects
    // directly, so values moved and never saved stayed in memory after switching presets and came back looking saved (user's
    // screenshot: 'zzap 보통' sight 4.86, zzap fire rate 2.35). RevertCustom rebuilds the bundle from this copy.
    private static readonly Dictionary<string, string> _customJson = new(StringComparer.Ordinal);

    public static IEnumerable<SAINPresetBundle> CustomBundles
    {
        get { return _customBundles.Values; }
    }

    public static bool TryGetCustomBundle(string name, out SAINPresetBundle bundle)
    {
        return _customBundles.TryGetValue(name, out bundle);
    }

    /// <summary>A working copy with no shared bot/global/personality/gear objects; restores the active static globals.</summary>
    public static SAINPresetBundle CopyForEditing(SAINPresetBundle bundle)
    {
        if (bundle == null) return null;
        try
        {
            return JsonConvert.DeserializeObject<SAINPresetBundle>(JsonConvert.SerializeObject(bundle, _settings), _settings);
        }
        finally
        {
            PresetHandler.LoadedPreset?.MakeCurrent();
        }
    }

    /// <summary>Last pulled/saved values for editor references; never the objects the editor changes.</summary>
    public static bool TryGetSavedCustomBundle(string name, out SAINPresetBundle bundle)
    {
        bundle = null;
        if (name == null || !_customJson.TryGetValue(name, out string json))
        {
            return false;
        }
        if (_savedCustomBundles.TryGetValue(name, out var cached) && string.Equals(cached.Json, json, StringComparison.Ordinal))
        {
            bundle = cached.Bundle;
            return true;
        }
        try
        {
            bundle = JsonConvert.DeserializeObject<SAINPresetBundle>(json, _settings);
            if (bundle?.Info == null)
            {
                return false;
            }
            _savedCustomBundles[name] = (json, bundle);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[SAIN] Could not read saved reference '{name}': {ex.Message}");
            bundle = null;
            return false;
        }
        finally
        {
            // Deserializing globals/debug settings changes their static Instance fields.
            PresetHandler.LoadedPreset?.MakeCurrent();
        }
    }

    /// <summary>Generated defaults as received from the server, independent of the loaded default's edits.</summary>
    public static IReadOnlyList<SAINPresetBundle> SavedServerDefaults()
    {
        ServerDefaults();
        if (_savedServerDefaults != null)
        {
            return _savedServerDefaults;
        }
        try
        {
            _savedServerDefaults = string.IsNullOrEmpty(_serverDefaultsJson)
                ? [] : JsonConvert.DeserializeObject<List<SAINPresetBundle>>(_serverDefaultsJson, _settings) ?? [];
        }
        catch (Exception ex)
        {
            Logger.LogError($"[SAIN] Could not read saved default references: {ex.Message}");
            _savedServerDefaults = [];
        }
        finally
        {
            PresetHandler.LoadedPreset?.MakeCurrent();
        }
        return _savedServerDefaults;
    }

    public static List<SAINPresetBundle> ServerDefaults()
    {
        if (_serverDefaultsFetched)
        {
            return _serverDefaults ?? [];
        }
        _serverDefaultsFetched = true;
        try
        {
            string json = ServerPresetClient.GetGeneratedDefaults();
            if (!string.IsNullOrEmpty(json) && json != "null")
            {
                _serverDefaults = JsonConvert.DeserializeObject<List<SAINPresetBundle>>(json, _settings);
                _serverDefaultsJson = json;
                _savedServerDefaults = null;
                PresetHandler.LoadedPreset?.MakeCurrent();
                Logger.LogInfo($"[SAIN] Fetched {_serverDefaults?.Count ?? 0} server-generated default presets.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[SAIN] Failed to fetch server-generated default presets: {ex.Message}");
        }
        return _serverDefaults ?? [];
    }

    public static void RefreshServerDefaults()
    {
        _serverDefaults = null;
        _serverDefaultsJson = null;
        _savedServerDefaults = null;
        _serverDefaultsFetched = false;
        ServerDefaults();
    }

    public static bool TryGetServerDefault(SAINDifficulty difficulty, out SAINPresetBundle bundle)
    {
        bundle = null;
        foreach (var b in ServerDefaults())
        {
            if (b?.Info != null && b.Info.BaseSAINDifficulty == difficulty)
            {
                bundle = b;
                return true;
            }
        }
        return false;
    }

    public static void PullCustomPresets()
    {
        try
        {
            _customBundles.Clear();
            _customJson.Clear();
            _savedCustomBundles.Clear();
            foreach (string name in ServerPresetClient.ListCustom())
            {
                try
                {
                    string json = ServerPresetClient.GetCustom(name);
                    if (string.IsNullOrEmpty(json) || json == "null")
                    {
                        continue;
                    }
                    var bundle = JsonConvert.DeserializeObject<SAINPresetBundle>(json, _settings);
                    if (bundle?.Info == null)
                    {
                        continue;
                    }
                    _customBundles[bundle.Info.Name] = bundle;
                    _customJson[bundle.Info.Name] = json;
                    Logger.LogInfo($"[SAIN] Pulled custom preset '{name}' from server.");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[SAIN] Failed to pull custom preset '{name}': {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"[SAIN] Custom preset pull skipped: {ex.Message}");
        }
        PresetHandler.LoadedPreset?.MakeCurrent();
    }

    public static void ProcessDeferred()
    {
        if (InRaid)
        {
            return;
        }

        if (_configReapplyPending)
        {
            _configReapplyPending = false;
            var target = _pendingConfigTarget;
            _pendingConfigTarget = null;
            Logger.LogInfo("[SAIN] Applying the server preset change from during the raid.");
            PresetHandler.ApplyDefinition(target);
        }

        if (_pendingSwitchTarget != null)
        {
            var copy = _pendingSwitchTarget;
            _pendingSwitchTarget = null;
            _liveReapplyPending = false;
            Logger.LogInfo($"[SAIN] Switching to '{copy.Name}', saved on the web page during the raid.");
            PresetHandler.InitPresetFromDefinition(copy);
            return;
        }

        if (!_liveReapplyPending)
        {
            return;
        }
        _liveReapplyPending = false;

        string name = PresetHandler.LoadedPreset?.Info?.Name;
        if (name != null && TryGetCustomBundle(name, out var bundle))
        {
            Logger.LogInfo($"[SAIN] Applying preset '{name}' that changed during the raid.");
            PresetHandler.InitPresetFromDefinition(bundle.Info);
        }
        else
        {
            Logger.LogInfo("[SAIN] The preset in use was removed during the raid, loading a default.");
            PresetHandler.InitPresetFromDefinition(null);
        }
    }

    /// <summary>Re-reads the admin server config live and, if a preset is forced or presets are off, applies it.</summary>
    public static void ApplyRemoteConfigChange()
    {
        ServerConfigClient.Fetch();
        RefreshServerDefaults();
        Logger.LogInfo("[SAIN] Server config changed.");

        SAINPresetDefinition target;
        if (ServerConfigClient.ForcedActive)
        {
            target = PresetHandler.FindDefinitionByName(ServerConfigClient.ForcedPresetName);
        }
        else
        {
            // Not forced: only step in if the loaded default was just hidden, then move to the nearest one.
            var loaded = PresetHandler.LoadedPreset?.Info;
            if (loaded == null || loaded.IsCustom || PresetHandler.IsDefaultAvailable(loaded.Name))
            {
                return;
            }
            target = PresetHandler.NearestAvailableDefault(loaded.BaseSAINDifficulty);
            Logger.LogInfo($"[SAIN] Preset '{loaded.Name}' was hidden by the server, falling back to '{target?.Name ?? "a default"}'.");
        }

        string currentName = PresetHandler.LoadedPreset?.Info?.Name;
        if (string.Equals(currentName, target?.Name, StringComparison.Ordinal))
        {
            return;
        }

        if (InRaid)
        {
            _configReapplyPending = true;
            _pendingConfigTarget = target;
            Logger.LogInfo("[SAIN] A server preset change will apply when the raid ends.");
            return;
        }

        PresetHandler.ApplyDefinition(target);
    }

    private static void ReapplyLivePreset(SAINPresetDefinition info)
    {
        if (InRaid)
        {
            _liveReapplyPending = true;
            Logger.LogInfo($"[SAIN] Preset '{info?.Name}' changed, but a raid is running — applying when it ends.");
            return;
        }
        PresetHandler.InitPresetFromDefinition(info);
    }

    public static void ResyncCustomPresets()
    {
        string loadedName = PresetHandler.LoadedPreset?.Info?.Name;
        bool loadedIsCustom = PresetHandler.LoadedPreset?.Info?.IsCustom == true;

        PullCustomPresets();
        PresetHandler.LoadCustomPresetOptions();

        if (loadedIsCustom && loadedName != null && TryGetCustomBundle(loadedName, out var bundle))
        {
            ReapplyLivePreset(bundle.Info);
        }
    }

    public static void ApplyRemoteChange(SAINPresetSyncMessage message)
    {
        string name = message.PresetName;
        bool isLoaded = string.Equals(PresetHandler.LoadedPreset?.Info?.Name, name, StringComparison.Ordinal);

        if (message.Change == EPresetSyncChange.Deleted)
        {
            _customBundles.Remove(name);
            _customJson.Remove(name);
            _savedCustomBundles.Remove(name);
            PresetHandler.LoadCustomPresetOptions();
            Logger.LogInfo($"[SAIN] Custom preset '{name}' was deleted on the server.");

            if (isLoaded)
            {
                ReapplyLivePreset(null);
            }
            return;
        }

        string json = ServerPresetClient.GetCustom(name);
        if (string.IsNullOrEmpty(json) || json == "null")
        {
            return;
        }

        if (
            _customBundles.TryGetValue(name, out var existing)
            && string.Equals(JsonConvert.SerializeObject(existing, _settings), json, StringComparison.Ordinal)
        )
        {
            // A web save can equal the live unsaved edits while still advancing the saved baseline.
            _customJson[name] = json;
            return;
        }

        var bundle = JsonConvert.DeserializeObject<SAINPresetBundle>(json, _settings);
        PresetHandler.LoadedPreset?.MakeCurrent();
        if (bundle?.Info == null)
        {
            return;
        }

        _customBundles[bundle.Info.Name] = bundle;
        _customJson[bundle.Info.Name] = json;
        PresetHandler.LoadCustomPresetOptions();
        Logger.LogInfo($"[SAIN] Custom preset '{name}' was updated on the server.");

        if (isLoaded)
        {
            ReapplyLivePreset(bundle.Info);
        }
        else if (UsesCopySource(message.CopyOf))
        {
            // zzap (user 2026-10-20: "the web save makes a copy and tells me to pick it in game - make it apply in game too"):
            // the web page saved its "[Modified]" copy of the zzap preset this game is on (or of the one an earlier copy came
            // from) - use the new copy. Only that family: a game on any other preset keeps it (REGRESSIONS P13).
            if (InRaid)
            {
                _pendingSwitchTarget = bundle.Info;
                Logger.LogInfo($"[SAIN] '{name}' saved on the web page as a copy of '{message.CopyOf}' - switching to it when the raid ends.");
                return;
            }
            Logger.LogInfo($"[SAIN] '{name}' saved on the web page as a copy of '{message.CopyOf}' - switching to it.");
            PresetHandler.InitPresetFromDefinition(bundle.Info);
        }
    }

    /// <summary>The loaded preset is <paramref name="source"/> itself or one of its "[Modified]" / "[Modified N]" copies.</summary>
    private static bool UsesCopySource(string source)
    {
        string loaded = PresetHandler.LoadedPreset?.Info?.Name;
        if (string.IsNullOrEmpty(source) || loaded == null)
        {
            return false;
        }
        return string.Equals(loaded, source, StringComparison.Ordinal) || loaded.StartsWith(source + " [Modified", StringComparison.Ordinal);
    }

    /// <summary>Throw away unsaved edits of a custom preset: rebuild its cached bundle from the last pulled/saved JSON.</summary>
    public static bool RevertCustom(string name)
    {
        if (name == null || !_customJson.TryGetValue(name, out string json))
        {
            return false;
        }
        try
        {
            var bundle = JsonConvert.DeserializeObject<SAINPresetBundle>(json, _settings);
            PresetHandler.LoadedPreset?.MakeCurrent();
            if (bundle?.Info == null)
            {
                return false;
            }
            _customBundles[name] = bundle;
            Logger.LogInfo($"[SAIN] Discarded unsaved changes of preset '{name}'.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[SAIN] Could not restore preset '{name}': {ex.Message}");
            return false;
        }
    }

    public static void DeleteCustomPreset(string name)
    {
        if (PresetSavePolicy.IsProtected(name))
        {
            Logger.LogWarning($"[SAIN] Bundled preset '{name}' can't be deleted - only your copies can.");
            return;
        }
        _customBundles.Remove(name);
        _customJson.Remove(name);
        _savedCustomBundles.Remove(name);
        try
        {
            ServerPresetClient.DeleteCustom(name);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[SAIN] Failed to delete custom preset '{name}' from server: {ex.Message}");
        }
    }

    public static bool PushCustomPreset(SAINPresetClass preset)
    {
        if (preset?.Info == null || !preset.Info.IsCustom)
        {
            return false;
        }
        return PublishCustom(preset, preset.Info);
    }

    public static bool PublishCustom(SAINPresetClass preset, SAINPresetDefinition info)
    {
        if (info == null || PresetSavePolicy.IsProtected(info.Name))
        {
            Logger.LogWarning("[SAIN] Bundled preset originals are read-only. Save a Modified copy.");
            return false;
        }
        if (!ServerConfigClient.EditingAllowed)
        {
            Logger.LogWarning("[SAIN] Preset editing is disabled by the server. Changes were not saved.");
            return false;
        }

        try
        {
            var bundle = BuildBundle(preset);
            bundle.Info = info;
            string json = JsonConvert.SerializeObject(bundle, _settings);
            ServerPresetClient.SaveCustom(info.Name, json);
            // Detach the stored object too: editing after a successful save must not alter the cache.
            _customBundles[info.Name] = CopyForEditing(bundle);
            _customJson[info.Name] = json;
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[SAIN] Failed to push custom preset to server: {ex.Message}");
            return false;
        }
    }

    private static SAINPresetBundle BuildBundle(SAINPresetClass preset)
    {
        var bot = new Dictionary<SharedWildSpawnType, SAINSettingsGroupClass>();
        foreach (var kv in preset.BotSettings.SAINSettings)
        {
            bot[kv.Key.ToESain()] = kv.Value;
        }

        return new SAINPresetBundle
        {
            Info = preset.Info,
            GlobalSettings = preset.GlobalSettings,
            BotSettings = bot,
            Personalities = preset.PersonalityManager.PersonalityDictionary,
            GearStealthValues = preset.GearStealthValuesClass.ItemStealthValues,
        };
    }
}
