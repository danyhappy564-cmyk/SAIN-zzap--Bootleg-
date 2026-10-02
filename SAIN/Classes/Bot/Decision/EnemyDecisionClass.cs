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
            MarkVisibleFight(enemy);
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
            MarkVisibleFight(enemy);
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

        // zzap: close-range occlusion. A few meters away an enemy flickers behind a door frame / pillar for a split second;
        // switching to the hidden-enemy logic each time restarted the action (14th sim, 2m: StandAndShoot -> Freeze ->
        // DogFight in 0.4s, sprint run-by bot shot). Keep the fight going on the spot he just was.
        if (KeepCloseFight(enemy, out result))
        {
            LastReason = "closeOcclusionKeep";
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
            // zzap: a door session already running isn't dropped for a corner chase - the chase is for an enemy who just
            // ducked around a corner, the door tactic is the plan for exactly this enemy behind this door (10/2 sim: door
            // holds / peeks ended "rushInstead" 0.1-0.7s after starting). Storm / he's reloading / he's dying still win.
            if (shallRush && Bot.DoorTactic.Active && LastReason == "cornerChase")
            {
                shallRush = false;
                TacticDiagnostics.Count("door.keptOverChase");
            }
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
        // zzap: a mate working a door handed this bot a squad role (overwatch / rear guard) -> take it.
        if (Bot.DoorTactic.ShallStartSupportRole(enemy, out reason))
        {
            result = ECombatDecision.DoorTactic;
            TacticDiagnostics.Count("utility.do.doorRole");
            return true;
        }
        // zzap: a teammate just went down to this enemy -> trade first (push the killer / take his angle).
        if (Bot.SquadCombat.ShallTrade(enemy, out reason))
        {
            result = ECombatDecision.SquadTactic;
            TacticDiagnostics.Count("utility.do.trade");
            return true;
        }
        var ranked = HiddenEnemyUtility.Rank(Bot, enemy);
        if (ranked == null)
        {
            return false;
        }
        HiddenEnemyUtility.EStance top = ranked[0].stance;
        // zzap: being shot right now (under fire / hit in the last 1.5s) -> never stand still holding an angle. The Hold
        // stance mapped straight to Freeze here, skipping the old chain's "freeze breaks when shot" check (3 sims: 6 bots
        // killed while frozen and under fire, e.g. hit from 2m behind, froze, dead 0.1s later). Falling back to cover may
        // then run even when it isn't near the top.
        bool beingShot = BotOwner.Memory.IsUnderFire || Bot.Medical.TimeSinceShot < 1.5f || AnyEnemyShootingAtMe();
        bool holdSkipped = false;
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
                        // zzap: he's in a room behind a door near us and no door tactic right now (cooldown, door taken, roll):
                        // running straight through the doorway was the fallback for most pushes (13th sim: 467 push picks,
                        // 29 door tactics, the rest rushed in). Only reckless bots or against a weak enemy; otherwise hold
                        // that door from the side, or let the next stance decide.
                        if (Bot.DoorTactic.EnemyBehindDoor(enemy) && !RushThroughDoorOk(enemy))
                        {
                            if (Bot.DoorTactic.ShallHoldDoor(enemy, out r, true))
                            {
                                result = ECombatDecision.DoorTactic;
                            }
                            TacticDiagnostics.Count(result == ECombatDecision.DoorTactic ? "utility.push.holdDoorInstead" : "utility.push.notThroughDoor");
                        }
                        else
                        {
                            result = ECombatDecision.RushEnemy;
                        }
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
                        if (beingShot)
                        {
                            holdSkipped = true;
                            TacticDiagnostics.Count("utility.hold.skippedUnderFire");
                            break;
                        }
                        // zzap: he's in a room behind a door close by -> hold THAT door from beside the frame instead of
                        // freezing wherever the bot happens to stand (13th sim: Hold top 489 times, never at a door).
                        result = Bot.DoorTactic.ShallHoldDoor(enemy, out r) ? ECombatDecision.DoorTactic : ECombatDecision.Freeze;
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
                    // Same as Hold: always runnable, so only when it is really wanted (3rd sim: 5.4k fall-throughs) - or when
                    // the bot is being shot and the hold it wanted was skipped (get out of the line of fire instead).
                    if (stance == top || score >= ranked[0].score - 0.12f || holdSkipped)
                    {
                        result = ECombatDecision.SeekCover;
                    }
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
                    if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"utility.fellTo.{stance}");
                }
                if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"utility.do.{result}");
                SAIN.Components.BotControllerSpace.Classes.PlayerOutcomeLearner.Begin(Bot, enemy,
                    SAIN.Components.BotControllerSpace.Classes.PlayerOutcomeLearner.Key("H", stance.ToString(), enemy.Path.PathLength, Bot));
                reason = $"utility{stance}";
                _utilityPickEnemy = enemy;
                _utilityPickTime = Time.time;
                _utilityPick = result;
                return true;
            }
        }
        // zzap: nothing runnable THIS tick is often a blip (the path to him is being recomputed: "no path yet"), and the old
        // chain then answered "seek cover" for a moment before the utility's pick ran again (3 sims: 39-45% of those cover
        // decisions lasted under 1s - every switch restarts the movement). Keep the utility's last pick for up to 1.2s
        // after it was made, unless the bot is being shot.
        if (ReferenceEquals(_utilityPickEnemy, enemy) && Time.time - _utilityPickTime < UTILITY_KEEP_TIME && !beingShot
            && Bot.Decision.CurrentCombatDecision == _utilityPick && _utilityPick is ECombatDecision.Freeze or ECombatDecision.RushEnemy
                or ECombatDecision.Search or ECombatDecision.SeekCover or ECombatDecision.ShiftCover)
        {
            result = _utilityPick;
            reason = "utilityKeep";
            TacticDiagnostics.Count("utility.keepOnBlip");
            return true;
        }
        TacticDiagnostics.Count("utility.noneRunnable");
        return false;
    }

    /// <summary>
    /// SAIN's own "he shot at me / hit me recently" - it is set the moment the shots are registered, before BSG's
    /// Memory.IsUnderFire turns on (10/2 sim: a bot picked Hold 0.1s after "shooting at me 0.0s ago" and died frozen).
    /// </summary>
    private bool AnyEnemyShootingAtMe()
    {
        var known = Bot.EnemyController.KnownEnemies;
        for (int i = 0; i < known.Count; i++)
        {
            var status = known[i]?.Status;
            if (status != null && (status.ShotAtMeRecently || status.ShotMeRecently))
            {
                return true;
            }
        }
        return false;
    }

    private const float UTILITY_KEEP_TIME = 1.2f;
    private Enemy _utilityPickEnemy;
    private float _utilityPickTime = -100f;
    private ECombatDecision _utilityPick;

    private const float CLOSE_OCCLUSION_KEEP_TIME = 0.8f;
    private const float CLOSE_OCCLUSION_DIST = 8f;
    private Enemy _visibleFightEnemy;
    private float _visibleFightTime = -100f;

    private void MarkVisibleFight(Enemy enemy)
    {
        _visibleFightEnemy = enemy;
        _visibleFightTime = Time.time;
    }

    /// <summary>
    /// zzap: the same enemy was being fought in sight a moment ago, close, and is now hidden for under 0.8s -> keep the
    /// current shoot / push decision (aimed at where he just was) instead of re-deciding as a hidden enemy. Not when
    /// someone else is shooting at the bot (then the fresh decision matters).
    /// </summary>
    private bool KeepCloseFight(Enemy enemy, out ECombatDecision result)
    {
        result = ECombatDecision.None;
        if (enemy.IsVisible || !enemy.Seen || enemy.TimeSinceSeen > CLOSE_OCCLUSION_KEEP_TIME || enemy.RealDistance > CLOSE_OCCLUSION_DIST)
        {
            return false;
        }
        if (!ReferenceEquals(_visibleFightEnemy, enemy) || Time.time - _visibleFightTime > CLOSE_OCCLUSION_KEEP_TIME + 0.4f)
        {
            return false;
        }
        ECombatDecision current = Bot.Decision.CurrentCombatDecision;
        if (current != ECombatDecision.StandAndShoot && current != ECombatDecision.RushEnemy)
        {
            return false;
        }
        var known = Bot.EnemyController.KnownEnemies;
        for (int i = 0; i < known.Count; i++)
        {
            Enemy other = known[i];
            if (other != null && !ReferenceEquals(other, enemy) && (other.IsVisible || other.Status.ShotAtMeRecently))
            {
                return false;
            }
        }
        result = current;
        TacticDiagnostics.Count("decision.closeOcclusionKeep");
        return true;
    }

    /// <summary>
    /// zzap: running through a doorway into his room without a plan is only OK for a reckless bot or against a weak enemy.
    /// </summary>
    private bool RushThroughDoorOk(Enemy enemy)
    {
        if (Bot.Info.Personality == EPersonality.Wreckless)
        {
            return true;
        }
        return SquadStorm.Weakness(Bot, enemy, out _) >= 0.5f;
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
                if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"utilityV.do.{result}");
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
        if (!freeze.AllowOutdoors && !Bot.Memory.Location.UnderRoof)
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
                if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"freeze.broken.{broken}");
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
            if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"freeze.start.{Bot.Info.Personality}.{(Bot.Memory.Location.UnderRoof ? "indoors" : "outdoors")}");
            if (freeze.DiagnosticLogs)
            {
                Logger.LogWarning(
                    $"[Freeze] [{Bot.name}] [{Bot.Info.Personality}] START ambush {timeToFreeze:0}s "
                        + $"enemyDist={enemy.KnownPlaces.BotDistanceFromLastKnown:0}m indoors={Bot.Memory.Location.UnderRoof} "
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
        if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"close.exposedCommit.{personality}");
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
