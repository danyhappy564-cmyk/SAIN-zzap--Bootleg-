using System.Text;
using EFT;
using SAIN.Components;
using SAIN.Models.Enums;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.GlobalSettings.Categories.General;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SAIN.SAINComponent.Classes.WeaponFunction;
using SAIN.Preset.Shared.Models.Enums;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.Classes.Search;
using SAIN.SAINComponent.Classes.Tactics;
using UnityEngine;
using UnityEngine.AI;

namespace SAIN.SAINComponent.Classes.Decision;

public class EnemyDecisionClass : BotBase
{
    private static readonly float RushEnemyMaxPathDistance = 10f;
    private static readonly float RushEnemyMaxPathDistanceSprint = 20f;
    private static readonly float RushEnemyLowAmmoRatio = 0.5f;
    private const float MOVE_TO_ENGAGE_MAX_TIME_SINCE_KNOWN = 60f;

    public SearchReasonsStruct DebugSearchReasons { get; private set; }
    public float FrozenDuration { get; private set; }
    public float TimeToUnfreeze { get; private set; }
    public StringBuilder DecisionReasons { get; } = new StringBuilder();
    public bool ShiftCoverComplete { get; set; }
    public bool? DebugShallSearch { get; set; }

    public Vector3? FiringPosition
    {
        get { return _firingPositionFinder.Position; }
    }

    public EnemyDecisionClass(BotComponent sain)
        : base(sain)
    {
        _firingPositionFinder = new FiringPositionFinder(sain);
    }

    private readonly FiringPositionFinder _firingPositionFinder;

    public bool GetDecision(out ECombatDecision result, Enemy enemy, EnemyList knownEnemies)
    {
        if (enemy == null)
        {
            result = ECombatDecision.None;
            return false;
        }
#if DEBUG
        if (SAINPlugin.DebugMode)
        {
            DecisionReasons.Clear();
        }
#endif

        Bot.Reposition.Observe(enemy);

        BotWeaponManager weaponManager = BotOwner.WeaponManager;
        if (weaponManager == null || !weaponManager.HaveBullets || weaponManager.Reload.Reloading)
        {
            result = ECombatDecision.Retreat;
            return true;
        }

        string reason = string.Empty;
#if DEBUG
        if (SAINPlugin.DebugMode)
        {
            DecisionReasons.AppendLine($"1. I've Got Bullets.");
        }
#endif

        if (Bot.DoorTactic.ShallEmergencyRetreat(enemy, out reason) || Bot.Reposition.ShallPullBack(out reason))
        {
            result = ECombatDecision.Retreat;
            return true;
        }

        if (Bot.Reposition.ShallUse(enemy, knownEnemies, out reason))
        {
            result = ECombatDecision.Reposition;
            return true;
        }

        bool canTakeAggressiveAction = CanBeAggressive(ref reason);
#if DEBUG
        if (SAINPlugin.DebugMode)
        {
            DecisionReasons.AppendLine($"2. CanTakeAggroActions?: [{canTakeAggressiveAction}, {reason}]");
        }
#endif

        bool shallShoot = shallStandAndShoot(enemy, out reason, knownEnemies);
#if DEBUG
        if (SAINPlugin.DebugMode)
        {
            DecisionReasons.AppendLine($"2. Shall Shoot: [{shallShoot}, {reason}]");
        }
#endif
        if (shallShoot)
        {
            if (Bot.Decision.CurrentCombatDecision != ECombatDecision.StandAndShoot)
            {
                Bot.Info.CalcHoldGroundDelay();
            }
            result = ECombatDecision.StandAndShoot;
            return true;
        }
        bool shallShootDistant = shallShootDistantEnemy(enemy, out reason);
#if DEBUG
        if (SAINPlugin.DebugMode)
        {
            DecisionReasons.AppendLine($"3. Shall Shoot Distant: [{shallShootDistant}, {reason}]");
        }
#endif
        if (shallShootDistant)
        {
            result = ECombatDecision.ShootDistantEnemy;
            return true;
        }

        if (canTakeAggressiveAction)
        {
            bool shallRush = shallRushEnemy(enemy, out reason);
#if DEBUG
            if (SAINPlugin.DebugMode)
            {
                DecisionReasons.AppendLine($"4. Shall Rush: [{shallRush}, {reason}]");
            }
#endif
            if (shallRush)
            {
                if (Bot.DoorTactic.Active)
                {
                    Bot.DoorTactic.End("rushInstead");
                }
                result = ECombatDecision.RushEnemy;
                return true;
            }

            bool doorTactic = Bot.DoorTactic.ShallUse(enemy, out reason);
#if DEBUG
            if (SAINPlugin.DebugMode)
            {
                DecisionReasons.AppendLine($"4b. Door Tactic: [{doorTactic}, {reason}]");
            }
#endif
            if (doorTactic)
            {
                result = ECombatDecision.DoorTactic;
                return true;
            }

            bool squadTactic = Bot.SquadCombat.ShallUse(enemy, out reason);
#if DEBUG
            if (SAINPlugin.DebugMode)
            {
                DecisionReasons.AppendLine($"4c. Squad Combat: [{squadTactic}, {reason}]");
            }
#endif
            if (squadTactic)
            {
                result = ECombatDecision.SquadTactic;
                return true;
            }

            bool shallThrowNade = shallThrowGrenade(enemy, out reason);
#if DEBUG
            if (SAINPlugin.DebugMode)
            {
                DecisionReasons.AppendLine($"5. Shall Throw Nade: [{shallThrowNade}, {reason}]");
            }
#endif
            if (shallThrowNade)
            {
                result = ECombatDecision.ThrowGrenade;
                return true;
            }

            bool moveToEngage = ShallMoveToEngage(enemy, out reason);
#if DEBUG
            if (SAINPlugin.DebugMode)
            {
                DecisionReasons.AppendLine($"6. Shall Move To Engage: [{moveToEngage}, {reason}]");
            }
#endif
            if (moveToEngage)
            {
                result = ECombatDecision.MoveToEngage;
                return true;
            }

            bool search = shallSearch(enemy, out reason);
#if DEBUG
            if (SAINPlugin.DebugMode)
            {
                DecisionReasons.AppendLine($"6. Shall Search: [{search}, {reason}]");
            }
#endif
            if (search)
            {
                if (Bot.Decision.CurrentCombatDecision != ECombatDecision.Search)
                {
                    enemy.Status.NumberOfSearchesStarted++;
                }
                result = ECombatDecision.Search;
                return true;
            }
        }

        bool freeze = shallFreezeAndWait(enemy, knownEnemies, out reason);
#if DEBUG
        if (SAINPlugin.DebugMode)
        {
            DecisionReasons.AppendLine($"7. Shall Freeze: [{freeze}, {reason}]");
        }
#endif
        if (freeze)
        {
            result = ECombatDecision.Freeze;
            return true;
        }

        bool shift = shallShiftCover(enemy, out reason);
#if DEBUG
        if (SAINPlugin.DebugMode)
        {
            DecisionReasons.AppendLine($"8. Shall Shift Cover: [{shift}, {reason}]");
        }
#endif
        if (shift)
        {
            result = ECombatDecision.ShiftCover;
            return true;
        }

#if DEBUG
        if (SAINPlugin.DebugMode)
        {
            DecisionReasons.AppendLine($"8. Seek Cover: [{true}, {Bot.Cover.CoverSeekingState}]");
        }
#endif
        result = ECombatDecision.SeekCover;
        return true;
    }

    private bool CanBeAggressive(ref string reason)
    {
        bool canTakeAggressiveAction = true;
        var suppState = Bot.Suppression.CurrentState;
        switch (suppState)
        {
            case ESuppressionState.Extreme:
            case ESuppressionState.Heavy:
                canTakeAggressiveAction = false;
#if DEBUG
                reason = $"Suppressed [{suppState}]";
#endif
                break;

            default:
                break;
        }

        return canTakeAggressiveAction;
    }

    private bool shallFreezeAndWait(Enemy enemy, EnemyList knownEnemies, out string reason)
    {
        // zzap: limits come from F6 General > Freeze Ambush (zzap); SAIN had them hardcoded (70m, indoors only, 240s, 80s, 10-120s).
        var freeze = GlobalSettings.General.FreezeAmbush;
        if (Bot.Info.PersonalitySettings.Search.HeardFromPeaceBehavior != EHeardFromPeaceBehavior.Freeze)
        {
            reason = "wontFreeze";
            return false;
        }
        if (!enemy.Hearing.EnemyHeardFromPeace)
        {
            reason = "notHeardFromPeace";
            return false;
        }
        if (!freeze.AllowOutdoors && !Bot.Memory.Location.IsIndoors)
        {
            reason = "outside";
            return false;
        }
        if (enemy.Seen && enemy.TimeSinceSeen < freeze.MinTimeSinceSeen)
        {
            reason = "seenRecent";
            return false;
        }
        if (enemy.TimeSinceLastKnownUpdated > freeze.MaxTimeSinceHeard)
        {
            reason = "haventHeard";
            return false;
        }
        if (enemy.KnownPlaces.BotDistanceFromLastKnown > freeze.MaxDistance)
        {
            reason = "tooFar";
            return false;
        }
        // zzap: SAIN never broke the freeze when the bot got shot. A frozen bot (every personality but Wreckless
        // freezes in TwitchPlayers) hit from an angle it wasn't watching stood still until the timer ran out
        // (up to 120s) and died standing. Being shot at / hit / another enemy in sight ends it.
        if (FreezeBroken(enemy, knownEnemies, out string broken))
        {
            if (Bot.Decision.CurrentCombatDecision == ECombatDecision.Freeze)
            {
                TacticDiagnostics.Count($"freeze.broken.{broken}");
                if (freeze.DiagnosticLogs)
                {
                    Logger.LogWarning($"[Freeze] [{Bot.name}] [{Bot.Info.Personality}] BREAK ambush: {broken}");
                }
            }
            TimeToUnfreeze = 0f;
            reason = broken;
            return false;
        }

        if (Bot.Decision.CurrentCombatDecision != ECombatDecision.Freeze)
        {
            float min = freeze.MinDuration;
            float max = Mathf.Max(freeze.MaxDuration, min);
            float timeToFreeze = UnityEngine.Random.Range(min, max) / Bot.Info.AggressionMultiplier;
            FrozenDuration = timeToFreeze;
            TimeToUnfreeze = Time.time + timeToFreeze;
            TacticDiagnostics.Count($"freeze.start.{Bot.Info.Personality}.{(Bot.Memory.Location.IsIndoors ? "indoors" : "outdoors")}");
            if (freeze.DiagnosticLogs)
            {
                Logger.LogWarning(
                    $"[Freeze] [{Bot.name}] [{Bot.Info.Personality}] START ambush {timeToFreeze:0}s "
                        + $"enemyDist={enemy.KnownPlaces.BotDistanceFromLastKnown:0}m indoors={Bot.Memory.Location.IsIndoors} "
                        + $"heard={enemy.TimeSinceLastKnownUpdated:0.0}s ago corner={(enemy.VisiblePathPoint != null ? "yes" : "no")}"
                );
            }
        }

        if (TimeToUnfreeze < Time.time)
        {
            reason = "frozenTooLong";
            return false;
        }
        reason = "timeForFreeze";
        return true;
    }

    private bool FreezeBroken(Enemy enemy, EnemyList knownEnemies, out string why)
    {
        if (BotOwner.Memory.IsUnderFire)
        {
            why = "underFire";
            return true;
        }
        var status = enemy.Status;
        if (status.ShotMeRecently || status.ShotAtMeRecently)
        {
            why = "shotAt";
            return true;
        }
        if (knownEnemies != null)
        {
            foreach (Enemy other in knownEnemies)
            {
                if (other != null && other != enemy && (other.IsVisible || other.Status.ShotMeRecently || other.Status.ShotAtMeRecently))
                {
                    why = "otherEnemy";
                    return true;
                }
            }
        }
        why = string.Empty;
        return false;
    }

    private bool shallThrowGrenade(Enemy enemy, out string reason)
    {
        return Bot.Grenade.GrenadeThrowDecider.GetDecision(enemy, out reason);
    }

    private bool shallRushEnemy(Enemy enemy, out string reason)
    {
        var health = Bot.Memory.Health.HealthStatus;
        if (health == ETagStatus.Dying)
        {
            reason = "imDying";
            return false;
        }
        if (enemy.Path.PathToEnemyStatus != UnityEngine.AI.NavMeshPathStatus.PathComplete)
        {
            reason = "incompletePath";
            return false;
        }
        if (Bot.Decision.SelfActionDecisions.LowOnAmmo(RushEnemyLowAmmoRatio))
        {
            reason = "lowAmmo";
            return false;
        }
        if (!checkInRangeForRush(enemy))
        {
            reason = "outOfRange";
            return false;
        }
        if (
            enemy.Hearing.EnemyHeardFromPeace
            && Bot.Info.PersonalitySettings.Search.HeardFromPeaceBehavior == EHeardFromPeaceBehavior.Charge
        )
        {
            reason = "heardFromPeaceCharge";
            return true;
        }
        if (!Bot.Info.PersonalitySettings.Rush.CanRushEnemyReloadHeal)
        {
            reason = "cantRush";
            return false;
        }

        if (enemy.Status.VulnerableAction != EEnemyAction.None)
        {
            reason = "enemyVulnerable";
            return true;
        }
        ETagStatus enemyHealth = enemy.EnemyPlayer.HealthStatus;
        if (enemyHealth == ETagStatus.Dying)
        {
            reason = "enemyHurtBad";
            return true;
        }
        if (enemyHealth == ETagStatus.BadlyInjured && enemy.EnemyPlayer.IsInPronePose)
        {
            reason = "enemyHurtAndProne";
            return true;
        }
        reason = "notGoodTimeTo";
        return false;
    }

    private bool checkInRangeForRush(Enemy enemy)
    {
        EEnemyAction vulnerableAction = enemy.Status.VulnerableAction;
        float modifier = vulnerableAction == EEnemyAction.UsingSurgery ? 2f : 1f;
        if (enemy.Path.PathLength < RushEnemyMaxPathDistance * modifier)
        {
            return true;
        }
        if (enemy.Path.PathLength < RushEnemyMaxPathDistanceSprint * modifier && BotOwner.CanSprintPlayer)
        {
            return true;
        }
        return false;
    }

    private bool shallShiftCover(Enemy enemy, out string reason)
    {
        if (Bot.Info.PersonalitySettings.Cover.CanShiftCoverPosition == false)
        {
            reason = "cantShift";
            return false;
        }
        if (Bot.Suppression.IsSuppressed)
        {
            reason = "suppressed";
            return false;
        }

        if (ContinueShiftCover())
        {
            reason = "continueShift";
            return true;
        }

        if (
            Bot.Cover.CoverInUse != null
            && Bot.Info.PersonalitySettings.Cover.CanShiftCoverPosition
            && Bot.Decision.TimeSinceChangeDecision > ShiftCoverChangeDecisionTime
            && TimeForNewShift < Time.time
        )
        {
            if (enemy != null)
            {
                if (enemy.Seen && !enemy.IsVisible && enemy.TimeSinceSeen > ShiftCoverTimeSinceSeen)
                {
                    TimeForNewShift = Time.time + ShiftCoverNewCoverTime;
                    ShiftResetTimer = Time.time + ShiftCoverResetTime;
                    reason = "enemyNotSeen";
                    return true;
                }
                if (!enemy.Seen && enemy.KnownPlaces.TimeSinceLastKnownUpdated > ShiftCoverTimeSinceEnemyCreated)
                {
                    TimeForNewShift = Time.time + ShiftCoverNewCoverTime;
                    ShiftResetTimer = Time.time + ShiftCoverResetTime;
                    reason = "lastKnownNotUpdated";
                    return true;
                }
            }
            if (enemy == null && Bot.Decision.TimeSinceChangeDecision > ShiftCoverNoEnemyResetTime)
            {
                TimeForNewShift = Time.time + ShiftCoverNewCoverTime;
                ShiftResetTimer = Time.time + ShiftCoverResetTime;
                reason = "timeDecisionMade";
                return true;
            }
        }

        reason = "dontWantTo";
        ShiftResetTimer = -1f;
        return false;
    }

    private bool ContinueShiftCover()
    {
        var CurrentDecision = Bot.Decision.CurrentCombatDecision;
        if (CurrentDecision == ECombatDecision.ShiftCover)
        {
            if (ShiftResetTimer > 0f && ShiftResetTimer < Time.time)
            {
                ShiftResetTimer = -1f;
                return false;
            }
            if (!Bot.Mover.Moving)
            {
                return false;
            }
            if (!ShiftCoverComplete)
            {
                return true;
            }
        }
        return false;
    }

    private bool ShallMoveToEngage(Enemy enemy, out string reason)
    {
        if (enemy.IsVisible)
        {
            reason = "enemyVisible";
            return false;
        }
        if (Bot.Memory.Health.HealthStatus == ETagStatus.Dying)
        {
            reason = "imDying";
            return false;
        }
        if (enemy.KnownPlaces.TimeSinceLastKnownUpdated > MOVE_TO_ENGAGE_MAX_TIME_SINCE_KNOWN)
        {
            reason = "knownTooOld";
            return false;
        }

        bool unreachable = enemy.Path.PathToEnemyStatus != NavMeshPathStatus.PathComplete;
        if (!unreachable && !enemy.IsSniper)
        {
            reason = "canWalkToEnemy";
            return false;
        }

        if (!_firingPositionFinder.Find(enemy))
        {
            reason = "noFiringPosition";
            return false;
        }

        reason = unreachable ? "cantReachEnemy" : "sniperOutOfReach";
        return true;
    }

    private bool shallShootDistantEnemy(Enemy enemy, out string reason)
    {
        if (
            _endShootDistTargetTime > Time.time
            && Bot.Decision.CurrentCombatDecision == ECombatDecision.ShootDistantEnemy
            && Bot.Memory.Health.HealthStatus != ETagStatus.Dying
        )
        {
            reason = "shootingDistantEnemy";
            return true;
        }
        if (
            _nextShootDistTargetTime < Time.time
            && enemy.RealDistance > Bot.Info.FileSettings.Shoot.MaxPointFireDistance
            && enemy.IsVisible
            && enemy.CanShoot
            && (Bot.Memory.Health.HealthStatus == ETagStatus.Healthy || Bot.Memory.Health.HealthStatus == ETagStatus.Injured)
        )
        {
            float timeAdd = 6f * UnityEngine.Random.Range(0.75f, 1.25f);
            _nextShootDistTargetTime = Time.time + timeAdd;
            _endShootDistTargetTime = Time.time + timeAdd / 3f;
            reason = "shootingDistantEnemy";
            return true;
        }
        reason = string.Empty;
        return false;
    }

    private bool shallSearch(Enemy enemy, out string reason)
    {
        bool shallSearch = Bot.Search.Decider.ShallStartSearch(enemy, out SearchReasonsStruct reasons);
        DebugSearchReasons = reasons;
        DebugShallSearch = shallSearch;
        if (shallSearch)
        {
            reason = "wantToSearch";
        }
        else
        {
            reason = "cantSearch";
        }
        return shallSearch;
    }

    /// <summary>
    /// zzap (2026-09-28 log: 15 of 42 deaths were bots walking to cover with the enemy visible 1-7m away): at point-blank
    /// range, walking away is just getting shot in the back of the head slowly. Keep fighting (StandAndShoot -> diamond
    /// step) while the enemy is this close and visible, the gun has rounds and the bot isn't healing/reloading.
    /// Cowards still run. F6 General > Close Combat (zzap) > Fight Close Instead Of Cover.
    /// </summary>
    private bool ShallFightCloseInsteadOfCover(Enemy enemy)
    {
        var settings = GlobalSettingsClass.Instance?.General?.CloseCombat;
        if (settings == null || !settings.CloseFight)
        {
            return false;
        }
        if (settings.PmcOnly && !Bot.Info.Profile.IsPMC)
        {
            return false;
        }
        if (Bot.Info.Personality == EPersonality.Coward || enemy.RealDistance > settings.CloseFightDistance)
        {
            return false;
        }
        if (Bot.Decision.CurrentSelfDecision != ESelfActionType.None)
        {
            return false;
        }
        if (SAINBotSuppressClass.CalcAmmoRatio(BotOwner, out _) < 0.2f)
        {
            return false;
        }
        if (Bot.Decision.CurrentCombatDecision != ECombatDecision.StandAndShoot)
        {
            TacticDiagnostics.Count("close.fightInsteadOfCover");
        }
        return true;
    }

    private bool shallStandAndShoot(Enemy enemy, out string reason, EnemyList KnownEnemies)
    {
        if (!enemy.IsVisible)
        {
            reason = "cantSeeEnemy";
            return false;
        }
        if (!enemy.CanShoot)
        {
            reason = "cantShootEnemy";
            return false;
        }
        if (BotOwner.WeaponManager?.HaveBullets == false)
        {
            reason = "noBullets";
            return false;
        }
        if (enemy.RealDistance > Bot.Info.WeaponInfo.EffectiveWeaponDistance * 1.25f)
        {
            reason = "outOfRange";
            return false;
        }
        if (enemy.IsZombie)
        {
            bool hasShooterContact = false;
            foreach (var knownEnemy in KnownEnemies)
            {
                if (knownEnemy?.IsZombie != true)
                {
                    hasShooterContact = true;
                }
            }
            if (!hasShooterContact)
            {
                reason = "shootZombie";
                return true;
            }
        }
        if (ShallFightCloseInsteadOfCover(enemy))
        {
            reason = "closeFightNoCoverRun";
            return true;
        }
        bool searchingForEnemy = enemy.Events.OnSearch.Value;
        float holdGroundInterval = Bot.Info.HoldGroundDelay;
        if (searchingForEnemy)
        {
            holdGroundInterval = Mathf.Max(holdGroundInterval, 0.5f) * UnityEngine.Random.Range(0.66f, 1.33f);
        }
        if (holdGroundInterval <= 0.0f)
        {
            reason = "wontHoldGround";
            return false;
        }

        if (!enemy.EnemyLookingAtMe)
        {
            reason = "enemyNotLooking";
            return true;
        }

        float visibleFor = Time.time - enemy.Vision.VisibleStartTime;
        if (visibleFor > holdGroundInterval)
        {
            reason = "visibleTooLong";
            return false;
        }

        if (visibleFor < holdGroundInterval / 1.5f)
        {
            reason = "holdingFromTime";
            return true;
        }
        else if (Bot.Cover.CheckLimbsForCover(enemy))
        {
            reason = "holdingHaveSomeCover";
            return true;
        }
        reason = "outOfTime";
        return false;
    }

    private CoverSettings CoverSettings
    {
        get { return SAINPlugin.LoadedPreset.GlobalSettings.General.Cover; }
    }

    private float ShiftCoverChangeDecisionTime
    {
        get { return CoverSettings.ShiftCoverChangeDecisionTime; }
    }

    private float ShiftCoverTimeSinceSeen
    {
        get { return CoverSettings.ShiftCoverTimeSinceSeen; }
    }

    private float ShiftCoverTimeSinceEnemyCreated
    {
        get { return CoverSettings.ShiftCoverTimeSinceEnemyCreated; }
    }

    private float ShiftCoverNoEnemyResetTime
    {
        get { return CoverSettings.ShiftCoverNoEnemyResetTime; }
    }

    private float ShiftCoverNewCoverTime
    {
        get { return CoverSettings.ShiftCoverNewCoverTime; }
    }

    private float ShiftCoverResetTime
    {
        get { return CoverSettings.ShiftCoverResetTime; }
    }

    private float _nextShootDistTargetTime;
    private float _endShootDistTargetTime;
    private float TimeForNewShift;
    private float ShiftResetTimer;
}
