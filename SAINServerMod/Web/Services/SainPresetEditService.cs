using SAIN.Preset.Shared.Models.WS;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using SAIN.Preset.Shared.Preset;
using SAINServerMod.Generators;
using SAINServerMod.Models.Preset;
using SAINServerMod.Services;
using SAINServerMod.Utils;
using SAINServerMod.Ws;
using SPTarkov.DI.Annotations;

namespace SAINServerMod.Web.Services;

public sealed record PresetListItem(string Name, string? Description, bool IsCustom, string? BaseDifficulty, bool Disabled = false);

/// <summary>A preset opened for editing, plus whether saving it will create a brand-new custom preset.</summary>
public sealed record OpenedPreset(SAINPresetBundle Bundle, bool IsNewCustom, string? OriginalName);

[Injectable(InjectionType.Singleton)]
public sealed class SainPresetEditService(
    PresetService presetService,
    PresetGenerationService presetGeneration,
    SAINPresetWebSocketHandler webSocket,
    SainServerConfigService serverConfig
)
{
    public IReadOnlyList<PresetListItem> ListAll()
    {
        var items = new List<PresetListItem>();

        foreach (var name in presetService.ListCustom())
        {
            items.Add(new PresetListItem(name, Description: null, IsCustom: true, BaseDifficulty: null));
        }

        return items;
    }

    /// <summary>A deep copy (safe to read) of any preset - default or custom (the bundled zzap presets are custom files). Null if missing.</summary>
    public async Task<SAINPresetBundle?> ReadOnlyCopyAsync(string name)
    {
        var opened = await OpenAsync(name);
        return opened == null ? null : Clone(opened.Bundle);
    }

    public static SAINPresetBundle Clone(SAINPresetBundle bundle)
    {
        return SAINJsonUtil.Deserialize<SAINPresetBundle>(SAINJsonUtil.Serialize(bundle))!;
    }

    public static QuickPresetView View(SAINPresetBundle bundle)
    {
        return new QuickPresetView(
            bundle.GlobalSettings,
            bundle.BotSettings.Select(kv => new KeyValuePair<string, SAINSettingsGroupClass>(kv.Key.ToString(), kv.Value)).ToList()
        );
    }

    public bool CustomExists(string name)
    {
        return presetService.ListCustom().Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Opens a preset for editing. Custom presets load as-is, defaults are cloned into an editable copy.</summary>
    public async Task<OpenedPreset?> OpenAsync(string name)
    {
        var customJson = await presetService.GetCustomAsync(name);
        if (!string.IsNullOrWhiteSpace(customJson))
        {
            var bundle = SAINJsonUtil.Deserialize<SAINPresetBundle>(customJson);
            if (bundle != null)
            {
                bundle.Info ??= new SAINPresetDefinition { Name = name };
                bundle.Info.IsCustom = true;
                bundle.Info.CanEditName = true;
                return new OpenedPreset(presetGeneration.ComposeCustom(bundle), IsNewCustom: false, OriginalName: name);
            }
        }

        var generated = presetGeneration.Presets.FirstOrDefault(p => string.Equals(p.Info.Name, name, StringComparison.OrdinalIgnoreCase));
        if (generated == null)
        {
            return null;
        }

        var deep = SAINJsonUtil.Deserialize<SAINPresetBundle>(SAINJsonUtil.Serialize(generated.ToBundle()))!;
        deep.Info ??= new SAINPresetDefinition { Name = generated.Info.Name };
        deep.Info.CanEditName = true;
        return new OpenedPreset(deep, IsNewCustom: true, OriginalName: generated.Info.Name);
    }

    /// <summary>The server's forced preset: saving it keeps the name (the forced name is what loads), even for a bundled zzap preset.</summary>
    public bool IsForced(string name)
    {
        return string.Equals(serverConfig.Config.ForcedPresetName, name, StringComparison.Ordinal);
    }

    /// <param name="copyOf">The bundled zzap preset this save is a "[Modified]" copy of: the game switches to the copy if it uses
    /// that preset (SAINPresetSyncMessage.CopyOf).</param>
    public async Task SaveAsync(SAINPresetBundle bundle, string? renamedFrom = null, string? copyOf = null)
    {
        bundle.Info ??= new SAINPresetDefinition();
        string original = copyOf ?? renamedFrom ?? bundle.Info.Name;
        bundle.Info.Name = PresetSavePolicy.NameForSave(original, bundle.Info.Name, CustomExists);
        bundle.Info.IsCustom = true;
        bundle.Info.CanEditName = true;

        var json = SAINJsonUtil.Serialize(bundle);
        await presetService.SaveCustomAsync(bundle.Info.Name, json);
        // If the preset was renamed, drop the old custom file so we don't leave a stale duplicate.
        if (
            !string.IsNullOrWhiteSpace(renamedFrom)
            && !string.Equals(renamedFrom, bundle.Info.Name, StringComparison.Ordinal)
            && CustomExists(renamedFrom)
            && !PresetSavePolicy.IsProtected(renamedFrom)
        )
        {
            presetService.DeleteCustom(renamedFrom);
        }

        await webSocket.BroadcastPresetChanged(bundle.Info.Name, EPresetSyncChange.Saved, copyOf: copyOf);
    }

    public async Task<bool> DeleteAsync(string name)
    {
        var deleted = presetService.DeleteCustom(name);
        if (deleted)
        {
            // Deleting the last custom re-enables all defaults so players are never left with nothing.
            if (presetService.ListCustom().Count == 0 && serverConfig.ClearDisabledDefaults())
            {
                await serverConfig.SaveAsync();
            }
            await webSocket.BroadcastPresetChanged(name, EPresetSyncChange.Deleted);
        }
        return deleted;
    }

    public async Task<string?> DuplicateAsync(string sourceName, string newName)
    {
        var opened = await OpenAsync(sourceName);
        if (opened == null)
        {
            return null;
        }

        opened.Bundle.Info.Name = newName;
        opened.Bundle.Info.CanEditName = true;
        opened.Bundle.Info.IsCustom = true;
        await SaveAsync(opened.Bundle);
        return newName;
    }
}
