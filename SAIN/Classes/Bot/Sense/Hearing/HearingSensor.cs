using System;
using SAIN.Components;
using SAIN.Components.PlayerComponentSpace;
using SAIN.Helpers;
using SAIN.SAINComponent;
using SAIN.SAINComponent.Classes.EnemyClasses;
using UnityEngine;

namespace SAIN.Classes.Bot.Sense.Hearing;

public class HearingSensor : BotComponentClassBase
{
    public event Action<AISoundData, Enemy> OnEnemySoundHeard;

    public HearingInput HearingInput { get; }
    public HearingAnalysis HearingAnalysis { get; }
    public HearingDispersion HearingDispersion { get; }

    public HearingSensor(BotComponent sain)
        : base(sain)
    {
        TickRequirement = ESAINTickState.OnlyNoSleep;
        HearingInput = new HearingInput(this);
        HearingAnalysis = new HearingAnalysis(this);
        HearingDispersion = new HearingDispersion(this);
    }

    public override void Init()
    {
        HearingInput.Init();
        HearingAnalysis.Init();
        HearingDispersion.Init();
        base.Init();
    }

    public override void ManualUpdate()
    {
        HearingInput.ManualUpdate();
        HearingAnalysis.ManualUpdate();
        HearingDispersion.ManualUpdate();
        base.ManualUpdate();
    }

    public override void Dispose()
    {
        HearingInput.Dispose();
        HearingAnalysis.Dispose();
        HearingDispersion.Dispose();
        base.Dispose();
    }

    public void ReactToBulletFlyBy(AISoundData sound, float FlyByDistance)
    {
        bool underFire = FlyByDistance <= SAINPlugin.LoadedPreset.GlobalSettings.Mind.MaxUnderFireDistance;
        if (HearingInput.IsIgnoringSounds(underFire))
        {
            return;
        }

        Vector3 EstimatedPosition = HearingDispersion.CalcRandomizedPosition(sound, 1f);
        ReactToBulletFlyBy(sound, FlyByDistance, EstimatedPosition, underFire);
        OnEnemySoundHeard?.Invoke(sound, sound.Enemy);
    }

    public void ReactToHeardSound(AISoundData sound)
    {
        Vector3 EstimatedPosition;
        if (HearingAnalysis.CheckIfSoundHeard(sound))
        {
            if (sound.IsGunShot && !ShallReactToGunshot(sound))
            {
                return;
            }
            EstimatedPosition = HearingDispersion.CalcRandomizedPosition(sound, 1f);
            Bot.Squad.SquadInfo?.AddPointToSearch(sound.Enemy, EstimatedPosition, sound, Bot);
            OnEnemySoundHeard?.Invoke(sound, sound.Enemy);
        }
    }

    // zzap remake5 (user 2026-10-11): distant gunfire in three bands. Before, a "chases distant gunshots" personality (GigaChad,
    // Chad, Wreckless) turned every shot it heard - up to 500 m - into a known enemy and a search, which also pulls the bot out of
    // ORBIT into SAIN's combat layer for nothing; other personalities simply ignored everything past 100 m.
    //   <= AudioStraightDistanceToIgnore (100 m)  react as before (search the spot)
    //   up to 250 m                               glance toward it (no enemy registered, ORBIT keeps the bot); a chaser personality
    //                                             that isn't fighting goes for it at most once a minute
    //   beyond 250 m                              ignored
    // A shooter whose bullets came at this bot (fly-by / hit in the last 5 s) is always reacted to, whatever the distance -
    // user: "long-range snipers / DMRs shooting at the bot itself must be excluded".
    private const float GUN_GLANCE_DISTANCE = 250f;
    private const float CHASE_COOLDOWN = 60f;
    private float _nextChaseTime;
    private float _nextGlanceTime;

    private bool ShallReactToGunshot(AISoundData sound)
    {
        float distance = sound.PlayerDistance;
        var searchSettings = Bot.Info.PersonalitySettings.Search;
        bool countOn = SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.CountOn;
        var status = sound.Enemy?.Status;
        float time = Time.time;
        bool atMe = status != null
            && ((status.TimeLastShotAtMe > 0f && time - status.TimeLastShotAtMe < 5f) || (status.TimeLastShotMe > 0f && time - status.TimeLastShotMe < 5f));
        if (distance <= searchSettings.AudioStraightDistanceToIgnore)
        {
            if (countOn) SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count("sound.gun.near");
            return true;
        }
        if (atMe)
        {
            if (countOn) SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count("sound.gun.shotAtMe");
            return true;
        }
        if (distance > GUN_GLANCE_DISTANCE)
        {
            if (countOn) SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count("sound.gun.far");
            return false;
        }
        bool fighting = Bot.HasEnemy && Bot.GoalEnemy != null && Bot.GoalEnemy.EnemyProfileId != sound.Enemy?.EnemyProfileId;
        if (searchSettings.WillChaseDistantGunshots && !fighting && time >= _nextChaseTime)
        {
            _nextChaseTime = time + CHASE_COOLDOWN;
            if (countOn) SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count("sound.gun.chase");
            return true;
        }
        if (countOn) SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count("sound.gun.mid");
        Glance(sound, fighting);
        return false;
    }

    /// <summary>Turn the head toward a 100-250 m gunshot without making the shooter a known enemy. Only when not fighting and
    /// not running (ORBIT steers a running bot; a glance at a walk is what a player does).</summary>
    private void Glance(AISoundData sound, bool fighting)
    {
        float time = Time.time;
        if (fighting || time < _nextGlanceTime || Player.IsSprintEnabled)
        {
            return;
        }
        _nextGlanceTime = time + 8f;
        try
        {
            Vector3 at = sound.Sound.Position;
            BotOwner.Steering?.LookToPoint(at + Vector3.up * 1.2f);
            SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count("sound.gun.glance");
        }
        catch (Exception)
        {
            // a bot being removed - nothing to do
        }
    }

    private void ReactToBulletFlyBy(AISoundData sound, float ProjectionPointDistance, Vector3 EstimatedPosition, bool UnderFire)
    {
        Enemy enemy = sound.Enemy;
        if (UnderFire)
        {
            BotOwner?.HearingSensor?.OnEnemySounHearded?.Invoke(EstimatedPosition, sound.PlayerDistance, sound.SoundType.Convert());
            Bot.Memory.SetUnderFire(enemy, EstimatedPosition);
            enemy.SetEnemyAsSniper(enemy.RealDistance > Bot.Info.PersonalitySettings.General.ENEMYSNIPER_DISTANCE);
        }
        Bot.Suppression.CheckAddSuppression(enemy, ProjectionPointDistance);
        enemy.Status.RegisterEnemyFlyBy();
        Bot.Squad.SquadInfo?.AddPointToSearch(enemy, EstimatedPosition, sound, Bot);
    }
}
