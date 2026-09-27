using EFT;
using SAIN.Components;
using SAIN.Preset.Shared.Enums;
using SAIN.SAINComponent.Classes.EnemyClasses;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.WeaponFunction;

public class BotLightController : BotComponentClassBase
{
    public BotLightController(BotComponent sain)
        : base(sain)
    {
        TickRequirement = ESAINTickState.OnlyNoSleep;
    }

    public override void ManualUpdate()
    {
        base.ManualUpdate();
        if (BotOwner?.BotLight == null)
        {
            return;
        }
        updateLightToggle();
    }

    private void updateLightToggle()
    {
        if ((Bot.SAINLayersActive || Bot.HasEnemy) && IsLightEnabled != wantLightOn && _nextLightChangeTime < Time.time)
        {
            // zzap: a bot whose weapon has no usable light (laser only, IR, or nothing that turns on) never reaches
            // IsEnable == true, so SAIN re-sent TurnOn every ~1s and the player heard a tactical-device click loop
            // from bots with no visible light. After 2 TurnOn calls that didn't take, stop trying for 60s.
            if (wantLightOn && _lightOnFailures >= 2)
            {
                if (Time.time < _lightRetryAfter)
                {
                    return;
                }
                _lightOnFailures = 0;
            }
            _nextLightChangeTime = Time.time + (wantLightOn ? 0.25f : _changelightFreq * UnityEngine.Random.Range(0.66f, 1.33f));
            if (wantLightOn && _lastTurnOnTime > 0f && Time.time - _lastTurnOnTime > 0.8f && !IsLightEnabled)
            {
                _lightOnFailures++;
                if (_lightOnFailures >= 2)
                {
                    _lightRetryAfter = Time.time + 60f;
                    SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count("light.turnOnFailedBackoff");
                    return;
                }
            }
            if (wantLightOn)
            {
                _lastTurnOnTime = Time.time;
            }
            else
            {
                _lastTurnOnTime = 0f;
                _lightOnFailures = 0;
            }
            setLight(wantLightOn);
        }
    }

    public bool IsLightEnabled
    {
        get { return BotOwner?.BotLight?.IsEnable == true; }
    }

    private float _nextLightChangeTime;
    private float _lastTurnOnTime;
    private int _lightOnFailures;
    private float _lightRetryAfter;
    private float _changelightFreq = 1f;

    private void setLight(bool value)
    {
        try
        {
            if (value)
            {
                BotOwner.BotLight.TurnOn(true);
            }
            else
            {
                BotOwner.BotLight.TurnOff(false, true);
            }
        }
        catch
        { // eft code go burr
        }
    }

    public void ToggleLight(bool value)
    {
        wantLightOn = value;
    }

    private bool wantLightOn;

    public void ToggleLaser(bool value) { }

    public void HandleLightForSearch(float distanceToCurrentCornerSqr)
    {
        const float DISTANCE_TO_CORNER_TURN_LIGHT_ON = 15f;
        const float TURN_LIGHT_OFF_AFTER = 1f;
        if (distanceToCurrentCornerSqr < DISTANCE_TO_CORNER_TURN_LIGHT_ON * DISTANCE_TO_CORNER_TURN_LIGHT_ON)
        {
            _timeWithinDistanceSearch = Time.time;
            ToggleLight(true);
        }
        else if (_timeWithinDistanceSearch + TURN_LIGHT_OFF_AFTER < Time.time)
        {
            ToggleLight(false);
        }
    }

    private float _timeWithinDistanceSearch;

    public void HandleLightForEnemy(Enemy enemy)
    {
        if (Bot.Decision.CurrentCombatDecision == ECombatDecision.Search)
        {
            return;
        }
        if (BotOwner.ShootData.Shooting)
        {
            return;
        }
        if (enemy != null)
        {
            if (!enemy.Seen)
            {
                ToggleLight(false);
                return;
            }

            float maxTurnOnrange = 40f;
            ECombatDecision decision = Bot.Decision.CurrentCombatDecision;

            if (enemy.EnemyNotLooking && enemy.RealDistance <= maxTurnOnrange * 0.9f)
            {
                ToggleLight(true);
                return;
            }

            // zzap (reference clip 2): light off while closing in, on the moment the target is in sight to blind them.
            if (enemy.IsVisible && Time.time - enemy.Vision.VisibleStartTime > 0.1f)
            {
                if (enemy.RealDistance <= maxTurnOnrange * 0.9f)
                {
                    ToggleLight(true);
                }
                else if (enemy.RealDistance > maxTurnOnrange)
                {
                    ToggleLight(false);
                }
                return;
            }

            if (enemy.Seen && BotOwner.BotLight?.IsEnable == true && enemy.TimeSinceSeen > randomizedTurnOffTime)
            {
                ToggleLight(false);
                return;
            }
        }
    }

    private float randomizedTurnOffTime
    {
        get
        {
            if (_nextRandomTime < Time.time)
            {
                _nextRandomTime = Time.time + _randomFreq * UnityEngine.Random.Range(0.66f, 1.33f);
                _randomTime = UnityEngine.Random.Range(_minRandom, _maxRandom);
            }
            return _randomTime;
        }
    }

    private float _nextRandomTime;
    private float _randomFreq = 2f;
    private float _randomTime;
    private float _minRandom = 1.5f;
    private float _maxRandom = 6f;
}
