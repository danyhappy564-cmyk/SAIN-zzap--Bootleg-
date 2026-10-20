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
        TurnOffDevicesNotInHands();
        if (ShallStayDark(out string why))
        {
            // zzap light discipline: holding an angle with the enemy out of sight -> light AND laser off (both are one
            // tactical device toggle). 2026-09-29 field report: bots holding inside a room lit up the doorway and gave
            // themselves away. The moment the enemy shows up the normal logic turns it on to blind him.
            wantLightOn = false;
            if (IsLightEnabled && _nextDarkForce < Time.time)
            {
                // idle: once per 10 s at most - if EFT's own light logic wants it on there, no click loop
                _nextDarkForce = Time.time + (why == "idleOutOfCombat" ? 10f : 0.5f);
                setLight(false);
                if (SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.CountOn) SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count($"light.dark.{why}");
            }
            return;
        }
        updateLightToggle();
    }

    private float _nextDarkForce;
    private float _idleSince = -1f;
    private float _darkUntil;
    private string _darkWhy;

    /// <summary>Keep light and laser off for a while (a tactic holding an angle / ambushing asks every tick).</summary>
    public void RequestDark(float seconds, string why)
    {
        _darkUntil = Mathf.Max(_darkUntil, Time.time + seconds);
        _darkWhy = why;
    }

    private bool ShallStayDark(out string why)
    {
        why = null;
        var settings = SAIN.Preset.Shared.GlobalSettings.GlobalSettingsClass.Instance?.General?.CloseCombat;
        if (settings == null || !settings.LightDiscipline)
        {
            return false;
        }
        Enemy enemy = Bot.GoalEnemy;
        if (enemy != null && enemy.IsVisible)
        {
            return false;
        }
        // 10/14 sim F8 mark #1 (user: "camping with the laser on, no enemy in sight"): after a fight the bot went back to ORBIT
        // and stood still with the light/laser it had switched on in combat - updateLightToggle only runs while a SAIN layer
        // is active or an enemy is known, so nothing ever turned it off. Standing 2 s+ outside SAIN's layers -> off.
        if (!Bot.SAINLayersActive && Player.Velocity.magnitude < 0.5f)
        {
            if (_idleSince < 0f)
            {
                _idleSince = Time.time;
            }
            if (Time.time - _idleSince > 2f)
            {
                why = "idleOutOfCombat";
                return true;
            }
        }
        else
        {
            _idleSince = -1f;
        }
        if (Time.time < _darkUntil)
        {
            why = _darkWhy ?? "requested";
            return true;
        }
        // Holding still (or creeping) while an enemy is known nearby but out of sight = an ambush / angle hold.
        if (enemy != null && (enemy.Seen || enemy.Heard) && enemy.TimeSinceLastKnownUpdated < 60f && enemy.RealDistance < 60f
            && Player.Velocity.magnitude < 1.2f && Bot.Decision.CurrentCombatDecision != ECombatDecision.Search)
        {
            why = "holdingAngle";
            return true;
        }
        return false;
    }

    // zzap: how many 60s back-offs in a row this bot hit without the light ever coming on (a gun with no usable light
    // retried every minute all raid: 70-104 back-offs per raid in the 12th-14th sims, each one two more clicks).
    private int _lightBackoffRounds;

    private void updateLightToggle()
    {
        if (wantLightOn && AllDevicesOn)
        {
            _lightBackoffRounds = 0;
        }
        // on = every device on, off = every device off (a half-on gun - light off, laser on - is not "off")
        bool mismatch = wantLightOn ? !AllDevicesOn : AnyDeviceOn;
        if ((Bot.SAINLayersActive || Bot.HasEnemy) && mismatch && _nextLightChangeTime < Time.time)
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
            if (wantLightOn && _lastTurnOnTime > 0f && Time.time - _lastTurnOnTime > 0.8f && !AllDevicesOn)
            {
                _lightOnFailures++;
                if (_lightOnFailures >= 2)
                {
                    // 60s, then 120s, 240s, ... up to 10 min while it keeps failing.
                    _lightRetryAfter = Time.time + Mathf.Min(60f * (1 << Mathf.Min(_lightBackoffRounds, 4)), 600f);
                    _lightBackoffRounds++;
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

    /// <summary>Any tactical device on the gun in hands is on (light, laser, IR - anything on the rail).</summary>
    public bool IsLightEnabled
    {
        get { return AnyDeviceOn; }
    }

    // zzap (user 2026-10-20, F8 mark #3: "a laser stays on outside a fight - lasers, lights, everything on the rail should go
    // off/on together; it turns one off and on"): EFT's BotLight drives only the FIRST LightComponent on the gun
    // (FindLight: list.First(), IsEnable reads only that one), so a gun with a light and a laser kept the other one as it
    // spawned and SAIN's light discipline never saw it. BotLight.TurnOn / TurnOff are patched (BotLightTurnOnPatch /
    // BotLightTurnOffPatch) to switch every device here in one SetLightsState call - one click, all of them.
    private readonly System.Collections.Generic.List<EFT.InventoryLogic.LightComponent> _devices = new();
    private EFT.InventoryLogic.Item _devicesWeapon;
    private float _devicesCheckedAt = -10f;

    /// <summary>The tactical devices on the gun in hands (re-read when the gun changes, or every 5 s for a mod swap).</summary>
    public System.Collections.Generic.List<EFT.InventoryLogic.LightComponent> Devices
    {
        get
        {
            EFT.InventoryLogic.Weapon weapon = (Player?.HandsController as EFT.Player.FirearmController)?.Item as EFT.InventoryLogic.Weapon;
            if (weapon == null)
            {
                _devices.Clear();
                _devicesWeapon = null;
                return _devices;
            }
            if (!ReferenceEquals(weapon, _devicesWeapon) || Time.time - _devicesCheckedAt > 5f)
            {
                _devicesWeapon = weapon;
                _devicesCheckedAt = Time.time;
                _devices.Clear();
                // the same list EFT's BotLight.FindLight reads (it only keeps the first)
                foreach (EFT.InventoryLogic.Item item in weapon.Mods)
                {
                    var device = item.GetItemComponent<EFT.InventoryLogic.LightComponent>();
                    if (device != null)
                    {
                        _devices.Add(device);
                    }
                }
            }
            return _devices;
        }
    }

    public bool AnyDeviceOn
    {
        get
        {
            var devices = Devices;
            for (int i = 0; i < devices.Count; i++)
            {
                if (devices[i].IsActive)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public bool AllDevicesOn
    {
        get
        {
            var devices = Devices;
            if (devices.Count == 0)
            {
                return false;
            }
            for (int i = 0; i < devices.Count; i++)
            {
                if (!devices[i].IsActive)
                {
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>Every device on the gun to the same state in one SetLightsState call. False when nothing needed switching.</summary>
    public bool SetAllDevices(bool active)
    {
        var fc = Player?.HandsController as EFT.Player.FirearmController;
        var devices = Devices;
        if (fc == null || devices.Count == 0)
        {
            return false;
        }
        int n = 0;
        for (int i = 0; i < devices.Count; i++)
        {
            if (devices[i].IsActive != active)
            {
                n++;
            }
        }
        if (n == 0)
        {
            return false;
        }
        var states = new EFT.LightsState[n];
        int k = 0;
        for (int i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            if (device.IsActive != active)
            {
                states[k++] = new EFT.LightsState { Id = device.Item.Id, IsActive = active, LightMode = device.SelectedMode };
            }
        }
        fc.SetLightsState(states);
        if (SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.CountOn && devices.Count > 1)
        {
            SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count(active ? "light.allOn" : "light.allOff");
        }
        return true;
    }

    // zzap (user 2026-10-20, 2nd sim 224327 mark #6: "walks around with the laser on its pistol"): AGX held an RPD (no device)
    // while the TT in its holster had spawned with its laser on - nothing ever switched a gun it never drew. A carried gun not
    // in the hands has its devices off: every 5 s the device visuals on the body (TacticalComboVisualController, the beam
    // EFT draws from LightComponent.IsActive) are checked, and any lit one not on the gun in hands goes off.
    private float _nextNotInHandsCheck;

    private void TurnOffDevicesNotInHands()
    {
        float now = Time.time;
        if (now < _nextNotInHandsCheck || Player == null)
        {
            return;
        }
        _nextNotInHandsCheck = now + 5f;
        var inHands = Devices;
        var visuals = Player.GetComponentsInChildren<TacticalComboVisualController>(true);
        for (int i = 0; i < visuals.Length; i++)
        {
            var device = visuals[i].LightMod;
            if (device == null || !device.IsActive || inHands.Contains(device))
            {
                continue;
            }
            device.IsActive = false;
            visuals[i].UpdateBeams();
            SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count("light.offNotInHands");
        }
    }

    /// <summary>"2/3 on" style text for the F8 scene mark.</summary>
    public string DevicesText
    {
        get
        {
            var devices = Devices;
            int on = 0;
            for (int i = 0; i < devices.Count; i++)
            {
                if (devices[i].IsActive)
                {
                    on++;
                }
            }
            return devices.Count == 0 ? "none" : $"{on}/{devices.Count}on";
        }
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

            if (enemy.Seen && IsLightEnabled && enemy.TimeSinceSeen > randomizedTurnOffTime)
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
