using SAIN.Components;
using SAIN.Extensions;
using SAIN.Helpers;
using SAIN.Preset;
using SAIN.Preset.Shared;
using SAIN.Preset.Shared.Preset;

namespace SAIN.SAINComponent.Classes;

public class BotDifficultyClass : BotBase
{
    public TemporaryStatModifiers GlobalDifficultyModifiers { get; }
    public TemporaryStatModifiers BotDifficultyModifiers { get; }
    public TemporaryStatModifiers PersonalityDifficultyModifiers { get; }
    public TemporaryStatModifiers LocationDifficultyModifiers { get; }

    public float AggressionModifier { get; private set; } = 1f;
    public float HearingDistanceModifier { get; private set; } = 1f;

    public BotDifficultyClass(BotComponent sain)
        : base(sain)
    {
        GlobalDifficultyModifiers = new TemporaryStatModifiers();
        BotDifficultyModifiers = new TemporaryStatModifiers();
        PersonalityDifficultyModifiers = new TemporaryStatModifiers();
        LocationDifficultyModifiers = new TemporaryStatModifiers();
    }

    public override void Dispose()
    {
        dismiss();
        base.Dispose();
    }

    public void UpdateSettings(SAINPresetClass preset)
    {
        dismiss();
        applyGlobal(preset);
        applyBot(preset);
        applyLocation(preset);
        applyPersonality(preset);
        apply();

        createCustomMods(preset);
    }

    private void createCustomMods(SAINPresetClass preset)
    {
        var globalSettings = preset.GlobalSettings.Difficulty;
        var botSettings = Bot.Info.FileSettings.Difficulty;
        var personalitySettings = Bot.Info.PersonalitySettingsClass.Difficulty;

        HearingDistanceModifier =
            1f * globalSettings.HearingDistanceCoef * botSettings.HearingDistanceCoef * personalitySettings.HearingDistanceCoef;

        AggressionModifier = 1f * globalSettings.AggressionCoef * botSettings.AggressionCoef * personalitySettings.AggressionCoef;

        var locationSettings = preset.GlobalSettings.Location.Current();

        if (locationSettings == null)
        {
            return;
        }

        HearingDistanceModifier *= locationSettings.HearingDistanceCoef;
        AggressionModifier *= locationSettings.AggressionCoef;

        var botMap = BotMapSettings(preset);
        if (botMap != null)
        {
            HearingDistanceModifier *= botMap.HearingDistanceCoef;
            AggressionModifier *= botMap.AggressionCoef;
        }
    }

    /// <summary>zzap (2026-11-04): this bot type's own multipliers for the current map (bot settings), null when it has none.</summary>
    private DifficultySettings BotMapSettings(SAINPresetClass preset)
    {
        var map = GameWorldComponent.Instance?.Location?.Location;
        if (map == null || !preset.BotSettings.SAINSettings.TryGetValue(Bot.Info.Profile.WildSpawnType, out var group))
        {
            return null;
        }
        return BotMapModifiers.Get(group, map.Value);
    }

    private void applyGlobal(SAINPresetClass preset)
    {
        var globalSettings = preset.GlobalSettings.Difficulty;
        var mods = GlobalDifficultyModifiers.Modifiers;

        mods.AccuratySpeedCoef = globalSettings.ACCURACY_SPEED_COEF;
        mods.PrecicingSpeedCoef = globalSettings.PRECISION_SPEED_COEF;
        mods.VisibleDistCoef = globalSettings.VisibleDistCoef;
        mods.ScatteringCoef = globalSettings.ScatteringCoef;
        //mods.PriorityScatteringCoef = globalSettings.PriorityScatteringCoef;
        mods.RuntimeVisionEffectK = globalSettings.GainSightCoef;
        mods.HearingDistCoef = globalSettings.HearingDistanceCoef;
    }

    private void applyBot(SAINPresetClass preset)
    {
        var botSettings = Bot.Info.FileSettings.Difficulty;
        var mods = BotDifficultyModifiers.Modifiers;

        mods.AccuratySpeedCoef = botSettings.ACCURACY_SPEED_COEF;
        mods.PrecicingSpeedCoef = botSettings.PRECISION_SPEED_COEF;
        mods.VisibleDistCoef = botSettings.VisibleDistCoef;
        mods.ScatteringCoef = botSettings.ScatteringCoef;
        //mods.PriorityScatteringCoef = botSettings.PriorityScatteringCoef;
        mods.RuntimeVisionEffectK = botSettings.GainSightCoef;
        mods.HearingDistCoef = botSettings.HearingDistanceCoef;
    }

    private void applyLocation(SAINPresetClass preset)
    {
        var locationSettings = preset.GlobalSettings.Location.Current();

        if (locationSettings == null)
        {
            return;
        }
        var mods = LocationDifficultyModifiers.Modifiers;
        // The map's own bot-type multipliers (bot settings) go on top of the map's global ones; 1x when none.
        var botMap = BotMapSettings(preset);

        mods.AccuratySpeedCoef = locationSettings.ACCURACY_SPEED_COEF * (botMap?.ACCURACY_SPEED_COEF ?? 1f);
        mods.PrecicingSpeedCoef = locationSettings.PRECISION_SPEED_COEF * (botMap?.PRECISION_SPEED_COEF ?? 1f);
        mods.VisibleDistCoef = locationSettings.VisibleDistCoef * (botMap?.VisibleDistCoef ?? 1f);
        mods.ScatteringCoef = locationSettings.ScatteringCoef * (botMap?.ScatteringCoef ?? 1f);
        //mods.PriorityScatteringCoef = locationSettings.PriorityScatteringCoef;
        mods.RuntimeVisionEffectK = locationSettings.GainSightCoef * (botMap?.GainSightCoef ?? 1f);
        mods.HearingDistCoef = locationSettings.HearingDistanceCoef * (botMap?.HearingDistanceCoef ?? 1f);
    }

    private void applyPersonality(SAINPresetClass preset)
    {
        var personalitySettings = Bot.Info.PersonalitySettingsClass.Difficulty;
        var mods = PersonalityDifficultyModifiers.Modifiers;

        mods.AccuratySpeedCoef = personalitySettings.ACCURACY_SPEED_COEF;
        mods.PrecicingSpeedCoef = personalitySettings.PRECISION_SPEED_COEF;
        mods.VisibleDistCoef = personalitySettings.VisibleDistCoef;
        mods.ScatteringCoef = personalitySettings.ScatteringCoef;
        //mods.PriorityScatteringCoef = personalitySettings.PriorityScatteringCoef;
        mods.RuntimeVisionEffectK = personalitySettings.GainSightCoef;
        mods.HearingDistCoef = personalitySettings.HearingDistanceCoef;
    }

    private void apply()
    {
        var current = BotOwner.Settings.Current;
        current.Apply(GlobalDifficultyModifiers.Modifiers);
        current.Apply(BotDifficultyModifiers.Modifiers);
        current.Apply(PersonalityDifficultyModifiers.Modifiers);
        current.Apply(LocationDifficultyModifiers.Modifiers);
    }

    private void dismiss()
    {
        var current = BotOwner.Settings.Current;
        current.Dismiss(GlobalDifficultyModifiers.Modifiers);
        current.Dismiss(BotDifficultyModifiers.Modifiers);
        current.Dismiss(PersonalityDifficultyModifiers.Modifiers);
        current.Dismiss(LocationDifficultyModifiers.Modifiers);
    }
}
