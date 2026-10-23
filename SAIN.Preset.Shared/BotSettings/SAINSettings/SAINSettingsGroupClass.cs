using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using SAIN.Preset.Shared.Attributes;
using SAIN.Preset.Shared.Enums;

namespace SAIN.Preset.Shared.BotSettings.SAINSettings;

[DataContract]
public class SAINSettingsGroupClass
{
    public SAINSettingsGroupClass() { }

    public SAINSettingsGroupClass(ESainBotDifficulty[] difficulties)
    {
        foreach (var difficulty in difficulties)
        {
            Settings.Add(difficulty, new SAINSettingsClass());
        }
    }

    public void Init()
    {
        foreach (var settings in Settings.Values)
        {
            settings.Init();
        }
    }

    public void UpdateDefaults()
    {
        foreach (var settings in Settings.Values)
        {
            settings.UpdateDefaults();
        }
    }

    [DataMember]
    public string Name;

    [DataMember]
    public ESainWildSpawnType WildSpawnType;

    [DataMember]
    [NameAndDescription(
        "Difficulty Modifier",
        "How much to improve this bot type's recoil handling, fire-rate, and full auto burst length, reaction time, general stats that are used in SAIN."
    )]
    [DefaultValue(0.5f)]
    [MinMax(0.01f, 1f)]
    public float DifficultyModifier = 0.5f;

    [DataMember]
    public Dictionary<ESainBotDifficulty, SAINSettingsClass> Settings = new();

    /// <summary>zzap (2026-10-23): the level picked on the bot type page (BotLevels); None = never picked.</summary>
    [DataMember]
    public EBotLevel DifficultyLevel = EBotLevel.None;

    /// <summary>zzap (2026-10-23): this bot type runs EFT's own AI instead of SAIN (applied at game start).</summary>
    [DataMember]
    public bool UseVanillaAI;

    /// <summary>
    /// zzap (2026-10-23): for a bot type with no reference preset entry (modded bots), the values the first level pick started
    /// from, so going between levels never drifts. Empty when a reference exists.
    /// </summary>
    [DataMember]
    public Dictionary<string, float> LevelBase = new();
}
