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
            LastReason = "noBulletsOrReloading";
            return true;
        }

        LastReason = string.Empty;
#if DEBUG
        if (SAINPlugin.DebugMode)
        {
            DecisionReasons.AppendLine($"1. I've Got Bullets.");
        }
#endif

        if (Bot.DoorTactic.ShallEmergencyRetreat(enemy, out LastReason) || Bot.Reposition.ShallPullBack(out LastReason))
        {
            result = ECombatDecision.Retreat;
            return true;
        }

        if (Bot.Reposition.ShallUse(enemy, knownEnemies, out LastReason))
        {
            result = ECombatDecision.Reposition;
            return true;
        }

        bool canTakeAggressiveAction = CanBeAggressive(ref LastReason);
#if DEBUG
        if (SAINPlugin.DebugMode)
        {
            DecisionReasons.AppendLine($"2. CanTakeAggroActions?: [{canTakeAggressiveAction}, {LastReason}]");
        }
#endif

        // zzap: enemy in sight -> shoot / cover / push by expected gain (falls through to SAIN's own logic when it doesn't apply).
        if (TryVisibleUtility(enemy, out result, out LastReason))
        {
            return true;
        }

        bool shallShoot = shallStandAndShoot(enemy, out LastReason, knownEnemies);
#if DEBUG
        if (SAINPlugin.DebugMode)
        {
            DecisionReasons.AppendLine($"2. Shall Shoot: [{shallShoot}, {LastReason}]");
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
        bool shallShootDistant = shallShootDistantEnemy(enemy, out LastReason);
#if DEBUG
        if (SAINPlugin.DebugMode)
        {
            DecisionReasons.AppendLine($"3. Shall Shoot Distant: [{shallShootDistant}, {LastReason}]");
        }
#endif
        if (shallShootDistant)
        {
            result = ECombatDecision.ShootDistantEnemy;
            return true;
        }

        // zzap stage 2: against a recorded aggressive / bunny-hopping player, hold the corner he's heard coming around.
        if (SAIN.Components.BotControllerSpace.Classes.PlayerAdaptation.ShallHoldCorner(Bot, enemy, out LastReason))
        {
            result = ECombatDecision.Freeze;
            return true;
        }

        // zzap: utility decision for a hidden enemy - score every stance by expected gain, try them best-first.
        if (canTakeAggressiveAction && TryUtility(enemy, out result, out LastReason))
        {
            return true;
        }

        if (canTakeAggressiveAction)
        {
            bool shallRush = shallRushEnemy(enemy, out LastReason);
#if DEBUG
            if (SAINPlugin.DebugMode)
            {
                DecisionReasons.AppendLine($"4. Shall Rush: [{shallRush}, {LastReason}]");
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

            bool doorTactic = Bot.DoorTactic.ShallUse(enemy, out LastReason);
#if DEBUG
            if (SAINPlugin.DebugMode)
            {
                DecisionReasons.AppendLine($"4b. Door Tactic: [{doorTactic}, {LastReason}]");
            }
#endif
            if (doorTactic)
            {
                result = ECombatDecision.DoorTactic;
                return true;
            }

            bool squadTactic = Bot.SquadCombat.ShallUse(enemy, out LastReason);
#if DEBUG
            if (SAINPlugin.DebugMode)
            {
                DecisionReasons.AppendLine($"4c. Squad Combat: [{squadTactic}, {LastReason}]");
            }
#endif
            if (squadTactic)
            {
                result = ECombatDecision.SquadTactic;
                return true;
            }

            bool shallThrowNade = shallThrowGrenade(enemy, out LastReason);
#if DEBUG
            if (SAINPlugin.DebugMode)
            {
                DecisionReasons.AppendLine($"5. Shall Throw Nade: [{shallThrowNade}, {LastReason}]");
            }
#endif
            if (shallThrowNade)
            {
                result = ECombatDecision.ThrowGrenade;
                return true;
            }

            bool moveToEngage = ShallMoveToEngage(enemy, out LastReason);
#if DEBUG
            if (SAINPlugin.DebugMode)
            {
                DecisionReasons.AppendLine($"6. Shall Move To Engage: [{moveToEngage}, {LastReason}]");
            }
#endif
            if (moveToEngage)
            {
                result = ECombatDecision.MoveToEngage;
                return true;
            }

            bool search = shallSearch(enemy, out LastReason);
#if DEBUG
            if (SAINPlugin.DebugMode)
            {
                DecisionReasons.AppendLine($"6. Shall Search: [{search}, {LastReason}]");
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

        bool freeze = shallFreezeAndWait(enemy, knownEnemies, out LastReason);
#if DEBUG
        if (SAINPlugin.DebugMode)
        {
            DecisionReasons.AppendLine($"7. Shall Freeze: [{freeze}, {LastReason}]");
        }
#endif
        if (freeze)
        {
            result = ECombatDecision.Freeze;
            return true;
        }

        bool shift = shallShiftCover(enemy, out LastReason);
#if DEBUG
        if (SAINPlugin.DebugMode)
        {
            DecisionReasons.AppendLine($"8. Shall Shift Cover: [{shift}, {LastReason}]");
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
        LastReason = $"default:{Bot.Cover.CoverSeekingState}";
        result = ECombatDecision.SeekCover;
        return true;
    }

    /// <summary>zzap: why the last combat decision was made (raid journal).</summary>
    public string LastReason = string.Empty;

    /// <summary>
    /// zzap: HiddenEnemyUtility ranks the stances; each is mapped onto SAIN's existing actions (with their own safety
    /// checks). The first one that can run wins; if none can, the old chain below decides as before. Ongoing door /
    /// squad sessions keep going untouched.
    /// </summary>
    private bool TryUtility(Enemy enemy, out ECombatDecision result, out string reason)
    {
        result = ECombatDecision.None;
        reason = string.Empty;
        if (Bot.DoorTactic.Active || Bot.SquadCombat.Active)
        {
            return false;
        }
        var ranked = HiddenEnemyUtility.Rank(Bot, enemy);
        if (ranked == null)
        {
            return false;
        }
        HiddenEnemyUtility.EStance top = ranked[0].stance;
        foreach (var (stance, score) in ranked)
        {
            if (score <= 0.05f)
            {
                break;
            }
            string r;
            switch (stance)
            {
                case HiddenEnemyUtility.EStance.Push:
                    if (shallRushEnemy(enemy, out r))
                    {
                        result = ECombatDecision.RushEnemy;
                    }
                    else if (Bot.DoorTactic.ShallUse(enemy, out r))
                    {
                        result = ECombatDecision.DoorTactic;
                    }
                    else if (enemy.Path.PathToEnemyStatus == NavMeshPathStatus.PathComplete && enemy.Path.PathLength <= 40f
                        && SAINBotSuppressClass.CalcAmmoRatio(BotOwner, out _) >= 0.35f)
                    {
                        result = ECombatDecision.RushEnemy;
                    }
                    break;

                case HiddenEnemyUtility.EStance.Grenade:
                    if (shallThrowGrenade(enemy, out r))
                    {
                        result = ECombatDecision.ThrowGrenade;
                    }
                    break;

                case HiddenEnemyUtility.EStance.Hold:
                    // Hold only when it is really wanted (the pick, or about as good) - it can always run, so as a
                    // fallback for a stance that couldn't start it swallowed everything (2nd sim: 10k fall-throughs to
                    // Hold = bots camping for no reason). Otherwise the old chain decides (search / engage / cover).
                    if (stance == top || score >= ranked[0].score - 0.12f)
                    {
                        result = ECombatDecision.Freeze;
                    }
                    break;

                case HiddenEnemyUtility.EStance.Flank:
                    if (Bot.SquadCombat.ShallUse(enemy, out r))
                    {
                        result = ECombatDecision.SquadTactic;
                    }
                    else if (shallShiftCover(enemy, out r))
                    {
                        result = ECombatDecision.ShiftCover;
                    }
                    break;

                case HiddenEnemyUtility.EStance.Search:
                    if (shallSearch(enemy, out r))
                    {
                        if (Bot.Decision.CurrentCombatDecision != ECombatDecision.Search)
                        {
                            enemy.Status.NumberOfSearchesStarted++;
                        }
                        result = ECombatDecision.Search;
                    }
                    break;

                case HiddenEnemyUtility.EStance.FallBack:
                    result = ECombatDecision.SeekCover;
                    break;
            }
            if (result != ECombatDecision.None)
            {
                if (result == ECombatDecision.RushEnemy && Bot.DoorTactic.Active)
                {
                    Bot.DoorTactic.End("rushInstead");
                }
                if (stance != top)
                {
                    TacticDiagnostics.Count($"utility.fellTo.{stance}");
                }
                TacticDiagnostics.Count($"utility.do.{result}");
                SAIN.Components.BotControllerSpace.Classes.PlayerOutcomeLearner.Begin(Bot, enemy,
                    SAIN.Components.BotControllerSpace.Classes.PlayerOutcomeLearner.Key("H", stance.ToString(), enemy.Path.PathLength, Bot));
                reason = $"utility{stance}";
                return true;
            }
        }
        TacticDiagnostics.Count("utility.noneRunnable");
        return false;
    }

    private bool TryVisibleUtility(Enemy enemy, out ECombatDecision result, out string reason)
    {
        result = ECombatDecision.None;
        reason = string.Empty;
        var ranked = VisibleEnemyUtility.Rank(Bot, enemy, Bot.Info.HoldGroundDelay);
        if (ranked == null)
        {
            return false;
        }
        foreach (var (stance, score) in ranked)
        {
            switch (stance)
            {
                case VisibleEnemyUtility.EStance.Shoot:
                    if (Bot.Decision.CurrentCombatDecision != ECombatDecision.StandAndShoot)
                    {
                        Bot.Info.CalcHoldGroundDelay();
                    }
                    result = ECombatDecision.StandAndShoot;
                    break;

                case VisibleEnemyUtility.EStance.Cover:
                    result = ECombatDecision.SeekCover;
                    break;

                case VisibleEnemyUtility.EStance.Push:
                    if (enemy.Path.PathToEnemyStatus == NavMeshPathStatus.PathComplete && enemy.RealDistance < 25f)
                    {
                        result = ECombatDecision.RushEnemy;
                    }
                    break;
            }
            if (result != ECombatDecision.None)
            {
                TacticDiagnostics.Count($"utilityV.do.{result}");
                SAIN.Components.BotControllerSpace.Classes.PlayerOutcomeLearner.Begin(Bot, enemy,
                    SAIN.Components.BotControllerSpace.Classes.PlayerOutcomeLearner.Key("V", stance.ToString(), enemy.RealDistance, Bot));
                reason = $"utilityV{stance}";
                return true;
            }
        }
        return false;
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
        // zzap: the squad decided this enemy is a pushover -> everyone who joins runs at him (own range limit).
        if (SquadStorm.ShallStorm(Bot, enemy, out string stormReason))
        {
            reason = stormReason;
            return true;
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
        // zzap: the enemy just broke sight around a corner in a close fight -> follow him (CornerChase).
        if (SAIN.SAINComponent.Classes.Tactics.CornerChase.ShallChase(Bot, enemy, out string chaseReason))
        {
            reason = chaseReason;
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
    /// <summary>
    /// zzap audit (4 raids of [Death] logs, 2026-09-28): the biggest single cause was SeekCover with the enemy visible at
    /// 10-20m (26 deaths), bots sprinting 4-5 m/s across open ground to a cover point far away - shot in the back. When
    /// being shot at by a visible enemy within the commit distance and no cover is close, pushy bots fight it out
    /// (diamond step) instead; Normal only when the nearest cover is really far.
    /// </summary>
    private bool ExposedCommit(CloseCombatSettings settings, Enemy enemy)
    {
        if (!settings.ExposedCommit || !enemy.IsVisible || !BotOwner.Memory.IsUnderFire || enemy.RealDistance > settings.ExposedCommitDistance)
        {
            return false;
        }
        EPersonality personality = Bot.Info.Personality;
        float needCover;
        switch (personality)
        {
            case EPersonality.GigaChad:
            case EPersonality.Chad:
            case EPersonality.Wreckless:
                needCover = settings.ExposedCommitCoverDistance;
                break;
            case EPersonality.Normal:
                needCover = settings.ExposedCommitCoverDistance * 2f;
                break;
            default:
                return false;
        }
        if (Bot.Memory.Health.HealthStatus == ETagStatus.Dying)
        {
            return false;
        }
        float nearest = float.MaxValue;
        var points = Bot.Cover.CoverPoints;
        if (points != null)
        {
            Vector3 pos = Bot.Position;
            foreach (var point in points)
            {
                if (point == null)
                {
                    continue;
                }
                float d = (point.Position - pos).magnitude;
                if (d < nearest)
                {
                    nearest = d;
                }
            }
        }
        if (nearest <= needCover)
        {
            return false;
        }
        TacticDiagnostics.Count($"close.exposedCommit.{personality}");
        return true;
    }

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
        float closeFightDistance = settings.CloseFightDistance + SAIN.Components.BotControllerSpace.Classes.PlayerAdaptation.CloseFightBonus(Bot, enemy);
        if (Bot.Info.Personality == EPersonality.Coward)
        {
            return false;
        }
        if (enemy.RealDistance > closeFightDistance && !ExposedCommit(settings, enemy))
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
