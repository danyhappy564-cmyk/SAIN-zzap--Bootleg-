using SAIN.Components;
using SAIN.Helpers;
using SAIN.Models.Enums;
using SAIN.Preset.Shared.GlobalSettings.Categories.Look;
using SAIN.SAINComponent.Classes.EnemyClasses;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Sense;

public class BotFlashedClass : BotComponentClassBase
{
    private const float REMEMBERED_HEIGHT = 1.2f;

    private const float SEARCH_POINT_RANGE = 140f;

    public BotFlashedClass(BotComponent bot)
        : base(bot)
    {
        TickRequirement = ESAINTickState.OnlyNoSleep;
    }

    public bool IsFlashed
    {
        get { return _flashEndTime > Time.time; }
    }

    public float TimeRemaining
    {
        get { return Mathf.Max(0f, _flashEndTime - Time.time); }
    }

    public Vector3? LastSeenEnemyPoint { get; private set; }

    public bool BlindFireReady
    {
        get { return _blindFireTime < Time.time; }
    }

    private float GetDuration(float baseTime)
    {
        var settings = Settings;
        float duration = baseTime * settings.DurationMultiplier * Bot.Info.FileSettings.Look.FlashDurationMulti;
        if (BotOwner.NightVision.UsingNow)
        {
            duration *= settings.NightVisionMultiplier;
        }
        return Mathf.Min(duration, settings.MaxDuration);
    }

    /// <summary>
    /// Keeps an already-running flash going (continuous exposure, e.g. standing in CS gas) without
    /// re-running ApplyFlash's one-shot "just got blinded" side effects. Calling ApplyFlash on a timer
    /// instead reset the blind-fire delay every refresh (bot never fired while exposed), re-snapped
    /// LastSeenEnemyPoint to the enemy's live position (blind bot tracking the player like ESP), and
    /// re-added a group search point every refresh. Never shortens a longer flash already in effect.
    /// applyModifiers: false keeps the Flashed state/layer running without the fixed flashbang stat
    /// penalty - CS gas supplies its own intensity-scaled one through SetGasIntensity instead.
    /// </summary>
    public void RefreshFlash(float baseTime, bool applyModifiers = true)
    {
        float duration = GetDuration(baseTime);
        if (duration <= 0f)
        {
            return;
        }
        float newEnd = Time.time + duration;
        if (newEnd <= _flashEndTime)
        {
            return;
        }
        _flashEndTime = newEnd;
        if (applyModifiers)
        {
            ApplyModifiers(duration, Settings.RecoveryPoint);
        }
    }

    public void ApplyFlash(float baseTime, Vector3 position, bool applyModifiers = true)
    {
        var settings = Settings;

        float duration = GetDuration(baseTime);
        if (duration <= 0f)
        {
            return;
        }

        Enemy enemy = Bot.GoalEnemy;
        LastSeenEnemyPoint = enemy != null ? enemy.EnemyPosition + (Vector3.up * REMEMBERED_HEIGHT) : null;

        BotOwner.BotsGroup.AddPointToSearch(position, SEARCH_POINT_RANGE, BotOwner);

        _flashEndTime = Time.time + duration;
        _blindFireTime = Time.time + settings.BlindFireDelay;
        if (applyModifiers)
        {
            ApplyModifiers(duration, settings.RecoveryPoint);
        }
    }

    /// <summary>
    /// CS gas stat penalty, scaled by exposure intensity (0 = none, 1 = the same penalty a flashbang
    /// applies at full strength), so it builds up gradually while a bot stands in the cloud and fades
    /// out gradually after it leaves - GrenadeReactionClass.TickGasExposure owns the ramp itself.
    /// Kept separate from the flashbang's own two-stage modifiers so a real flashbang on top of gas
    /// stacks with it instead of one overwriting the other. TemporaryStatModifiers values are
    /// multipliers where 1 means "no change" (see FlashLightDazzleClass's neutral new(1,1,1,1,1)), so
    /// each value is interpolated between 1 and the flashbang value. Quantized to GAS_INTENSITY_STEPS
    /// so a full ramp only rebuilds/re-applies the modifier set a handful of times instead of every tick.
    /// Applied without a duration: it stays until the next step change, 0, or Dispose.
    /// </summary>
    public void SetGasIntensity(float intensity)
    {
        int step = Mathf.Clamp(Mathf.RoundToInt(intensity * GAS_INTENSITY_STEPS), 0, GAS_INTENSITY_STEPS);
        if (step == _gasStep)
        {
            return;
        }
        _gasStep = step;
        DismissGas();
        if (step == 0)
        {
            return;
        }
        float t = step / (float)GAS_INTENSITY_STEPS;
        var change = BotOwner.Settings.FileSettings.Change;
        _gasModifiers = new TemporaryStatModifiers(
            Mathf.Lerp(1f, change.FLASH_PRECICING, t),
            Mathf.Lerp(1f, change.FLASH_ACCURATY, t),
            Mathf.Lerp(1f, change.FLASH_GAIN_SIGHT, t),
            Mathf.Lerp(1f, change.FLASH_SCATTERING, t),
            Mathf.Lerp(1f, change.FLASH_SCATTERING, t),
            Mathf.Lerp(1f, change.FLASH_VISION_DIST, t),
            Mathf.Lerp(1f, change.FLASH_HEARING, t)
        );
        BotOwner.Settings.Current.Apply(_gasModifiers.Modifiers);
    }

    private const int GAS_INTENSITY_STEPS = 10;

    private void DismissGas()
    {
        if (_gasModifiers != null)
        {
            BotOwner.Settings.Current.Dismiss(_gasModifiers.Modifiers);
            _gasModifiers = null;
        }
    }

    private void ApplyModifiers(float duration, float recoveryPoint)
    {
        var change = BotOwner.Settings.FileSettings.Change;

        Dismiss();
        _firstStage = Build(change);
        _secondStage = Build(change);
        BotOwner.Settings.Current.Apply(_firstStage.Modifiers, duration);
        BotOwner.Settings.Current.Apply(_secondStage.Modifiers, duration * recoveryPoint);
    }

    private static TemporaryStatModifiers Build(BotGlobalsChangeSettings change)
    {
        return new TemporaryStatModifiers(
            change.FLASH_PRECICING,
            change.FLASH_ACCURATY,
            change.FLASH_GAIN_SIGHT,
            change.FLASH_SCATTERING,
            change.FLASH_SCATTERING,
            change.FLASH_VISION_DIST,
            change.FLASH_HEARING
        );
    }

    private void Dismiss()
    {
        if (_firstStage != null)
        {
            BotOwner.Settings.Current.Dismiss(_firstStage.Modifiers);
            _firstStage = null;
        }
        if (_secondStage != null)
        {
            BotOwner.Settings.Current.Dismiss(_secondStage.Modifiers);
            _secondStage = null;
        }
    }

    public override void ManualUpdate()
    {
        // LastSeenEnemyPoint check too: a gas-driven flash (applyModifiers: false) never creates
        // _firstStage, and would otherwise leave the remembered point set after the flash ends.
        if (!IsFlashed && (_firstStage != null || LastSeenEnemyPoint != null))
        {
            Dismiss();
            LastSeenEnemyPoint = null;
        }
        base.ManualUpdate();
    }

    public override void Dispose()
    {
        Dismiss();
        DismissGas();
        base.Dispose();
    }

    private static FlashbangSettings Settings
    {
        get { return SAINPlugin.LoadedPreset.GlobalSettings.Look.Flashbang; }
    }

    private float _flashEndTime;
    private float _blindFireTime;
    private TemporaryStatModifiers _firstStage;
    private TemporaryStatModifiers _secondStage;
    private TemporaryStatModifiers _gasModifiers;
    private int _gasStep;
}
