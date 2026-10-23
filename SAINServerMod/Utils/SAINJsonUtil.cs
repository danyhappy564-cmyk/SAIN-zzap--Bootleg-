using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using SAIN.Preset.Shared.BotSettings.SAINSettings.Categories;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.Preset;
using SPTarkov.Server.Core.Models.Spt.Servers;

namespace SAINServerMod.Utils;

public static class SAINJsonUtil
{
    public static readonly JsonSerializerOptions Indented = new()
    {
        IncludeFields = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { DataContractTypeInfoResolver } },
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(object value)
    {
        return JsonSerializer.Serialize(value, Indented);
    }

    public static T? Deserialize<T>(string json)
    {
        var value = JsonSerializer.Deserialize<T>(json, Indented);
        if (value is SAINPresetBundle bundle)
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("BotSettings", out var botSettings))
            {
                foreach (var bot in botSettings.EnumerateObject())
                {
                    if (
                        Enum.TryParse<ESainWildSpawnType>(bot.Name, true, out var type)
                        && bundle.BotSettings.TryGetValue(type, out var settingsGroup)
                    )
                    {
                        ApplyMissingPersonalityOverrides(settingsGroup, bot.Value);
                    }
                }
            }
        }
        else if (value is SAINSettingsGroupClass standaloneGroup)
        {
            using var document = JsonDocument.Parse(json);
            ApplyMissingPersonalityOverrides(standaloneGroup, document.RootElement);
        }
        return value;
    }

    // zzap (2026-10-23): fill only absent fields on every preset read, preserving an explicit None in older presets too.
    private static void ApplyMissingPersonalityOverrides(SAINSettingsGroupClass group, JsonElement saved)
    {
        if (!saved.TryGetProperty("Settings", out var settings))
        {
            return;
        }
        foreach (var difficulty in settings.EnumerateObject())
        {
            if (
                Enum.TryParse<ESainBotDifficulty>(difficulty.Name, true, out var key)
                && group.Settings.TryGetValue(key, out var entry)
                && (
                    !difficulty.Value.TryGetProperty("Mind", out var mind)
                    || !mind.TryGetProperty(nameof(SAINMindSettings.PersonalityOverride), out _)
                )
            )
            {
                entry.Mind.PersonalityOverride = SAINMindSettings.DefaultPersonalityOverride(group.WildSpawnType);
            }
        }
    }

    public static StreamedJsonBody StreamRaw(string? json)
    {
        return new StreamedJsonBody(string.IsNullOrEmpty(json) ? null : JsonNode.Parse(json));
    }

    private static void DataContractTypeInfoResolver(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        if (typeInfo.Type.GetCustomAttribute<DataContractAttribute>() is null)
        {
            return;
        }

        foreach (JsonPropertyInfo property in typeInfo.Properties.ToList())
        {
            var member = property.AttributeProvider as MemberInfo;

            if (member?.GetCustomAttribute<DataMemberAttribute>() is null)
            {
                typeInfo.Properties.Remove(property);
            }
        }
    }
}
