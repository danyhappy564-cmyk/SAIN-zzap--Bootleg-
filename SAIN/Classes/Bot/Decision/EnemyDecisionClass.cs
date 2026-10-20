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
        // 15:34 sim (Alberta: Retreat "noBulletsOrReloading" for 80 s with 1 round in a half-loaded double barrel and no spare
        // ammo): EFT's BotReload can leave Reloading set when its reload found nothing to load (ReloadAmmo returns early for
        // an OnlyBarrel gun that isn't fully empty). Reloading for 6 s with no reload animation running -> clear it.
        WatchStuckReload(weaponManager);
        if (weaponManager == null || !weaponManager.HaveBullets || weaponManager.Reload.Reloading)
        {
            // 04:55 sim (Hjal): retreating with an empty gun and 3 magazines, standing still for 10 s without reloading.
            // Start the reload from here once the retreat has run 1.5 s (ReloadNow has its own retry block).
            if (weaponManager != null && !weaponManager.HaveBullets && weaponManager.Reload != null && !weaponManager.Reload.Reloading
                && Bot.Decision.TimeSinceChangeDecision > 1.5f && Bot.Decision.SelfActionDecisions.ReloadNow())
            {
                TacticDiagnostics.Count("reload.fromEmptyRetreat");
            }
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
            LastReason = _keptMidRange ? "midOcclusionKeep" : "closeOcclusionKeep";
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
            // zzap: searching toward a room behind a door -> clear it first instead of walking in.
            if (search && Bot.DoorTactic.ShallClearOnSearch(enemy, out string clearWhy))
            {
                LastReason = "searchClear";
                result = ECombatDecision.DoorTactic;
                return true;
            }
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
    // zzap (sims 2026-10-09/10, "should move but stands" 14-28 per map): a FallBack pick maps to SeekCover, which stands still
    // when there is no cover to go to ("standing in the open - no place to hold from") - a dying Coward on Customs did it for
    // over a minute. FallBack only runs when there is a cover point, and after 5 s of SeekCover without moving 0.5 m (not in
    // cover) it is off for 15 s, so the next stance (hold an angle, search...) runs instead.
    private float _fallBackBanUntil;
    private float _fallBackWatchSince = -1f;
    private Vector3 _fallBackWatchPos;

    private bool FallBackAllowed(float time)
    {
        if (time < _fallBackBanUntil)
        {
            return false;
        }
        var points = Bot.Cover.CoverPoints;
        return points != null && points.Count > 0;
    }

    // 10/12 sim: 6 "should move but stands" scenes in Search / Push, 7-17 s each. One was confirmed: the bot already stood
    // on his last known spot, Push still ran and its mover answered "arrived" every tick. The others had a path but no
    // move was issued (Bot14 kept switching Push / Search / FallBack on the spot, so a per-decision watch never fired).
    // One watch for all three: no 0.5 m of movement for 6 s -> his last known spot counts as reached, no Push/Search for 12 s
    // and no FallBack for 15 s, so Hold / Flank / the old chain decide instead.
    private float _moveBanUntil;

    private void WatchFallBackStall(float time)
    {
        ECombatDecision current = Bot.Decision.CurrentCombatDecision;
        bool fallingBack = current == ECombatDecision.SeekCover && LastReason == "utilityFallBack" && Bot.Cover.CoverInUse == null;
        bool moving = fallingBack || current == ECombatDecision.Search || current == ECombatDecision.RushEnemy;
        if (!moving)
        {
            _fallBackWatchSince = -1f;
            return;
        }
        Vector3 pos = Bot.Position;
        if (_fallBackWatchSince < 0f || (pos - _fallBackWatchPos).sqrMagnitude > 0.25f)
        {
            _fallBackWatchSince = time;
            _fallBackWatchPos = pos;
            return;
        }
        // Search standing at the end of its path ("0m to go" in [Stuck]) - 2 s is enough near his last known spot (15:34 sim)
        var goal = Bot.GoalEnemy;
        bool atLastKnown = current == ECombatDecision.Search && goal != null && goal.KnownPlaces.BotDistanceFromLastKnown < 2f;
        float limit = fallingBack ? 5f : atLastKnown ? 2f : 6f;
        if (time - _fallBackWatchSince < limit)
        {
            return;
        }
        _fallBackWatchSince = -1f;
        _fallBackBanUntil = time + 15f;
        if (fallingBack)
        {
            TacticDiagnostics.Count("utility.fallBackStalled");
            if (TacticDiagnostics.LogOn)
            {
                RaidJournal.Line($"[Utility] [{Bot.name}] [{Bot.Info.Personality}] fall back went nowhere for 5s (no cover reached) - no falling back for 15s");
            }
            return;
        }
        _moveBanUntil = time + 12f;
        var place = Bot.GoalEnemy?.KnownPlaces?.LastKnownPlace;
        if (place != null)
        {
            place.HasArrivedPersonal = true;
        }
        TacticDiagnostics.Count($"utility.moveStalled.{current}");
        if (TacticDiagnostics.LogOn)
        {
            RaidJournal.Line($"[Utility] [{Bot.name}] [{Bot.Info.Personality}] {current} went nowhere for 6s - his last known spot counts as reached, no push/search for 12s");
        }
    }

    private bool TryUtility(Enemy enemy, out ECombatDecision result, out string reason)
    {
        result = ECombatDecision.None;
        reason = string.Empty;
        WatchFallBackStall(Time.time);
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
        // zzap: squad maneuver (suppress + flank) - start a squad plan or take this bot's role in one.
        if (Bot.SquadCombat.ShallManeuver(enemy, out reason))
        {
            result = ECombatDecision.SquadTactic;
            TacticDiagnostics.Count("utility.do.maneuver");
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
                    // Already standing on his last known spot (info old): a push has nowhere to go (10/12 sim, Bot6).
                    // 15:34 sim (Zersus, 30 s standing): "path 0m" to an enemy 26-34 m away is a failed path, not "already
                    // there" - use the straight distance to his last known spot.
                    if (Time.time < _moveBanUntil || (enemy.KnownPlaces.BotDistanceFromLastKnown < 2.5f && enemy.TimeSinceLastKnownUpdated > 3f))
                    {
                        r = "pushNowhere";
                        break;
                    }
                    if (shallRushEnemy(enemy, out r))
                    {
                        result = ECombatDecision.RushEnemy;
                    }
                    else if (Bot.DoorTactic.ShallUse(enemy, out r))
                    {
                        result = ECombatDecision.DoorTactic;
                    }
                    // zzap (user 2026-10-21): a Coward never rushes past a refused rush check - 1st stage: Coward RushEnemy 2-3 % of its
                    // combat samples, nearly all utilityPush through this fallback (path 40 m, mag 35 %) after shallRushEnemy said no.
                    else if (Bot.Info.Personality == EPersonality.Coward)
                    {
                        TacticDiagnostics.Count("utility.push.cowardRefused");
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
                        // 10/12 sim (only death during a freeze): Bot24 froze in the open against a hidden enemy 19m away and
                        // was shot in the head 3.6s later by another enemy it had seen 13s before at 55m - no shot first,
                        // so the under-fire check above never fired. Out of cover, a second enemy that may see us = no hold.
                        // 04:55 sim: the "out of cover" part made it flip at 10 Hz (148 times on Factory) - switching to
                        // Freeze stops the cover move and clears CoverInUse, so the next tick skipped the hold, FallBack put
                        // the bot back on the same point, CoverInUse came back, hold again... That cover was picked against
                        // the goal enemy only anyway (one freeze death was "in cover"). Now: the line check alone decides,
                        // and a skip holds for 4 s.
                        if (Time.time < _holdOtherBanUntil || OtherEnemyMaySeeMe(enemy))
                        {
                            if (Time.time >= _holdOtherBanUntil)
                            {
                                _holdOtherBanUntil = Time.time + 4f;
                                TacticDiagnostics.Count("utility.hold.skippedOtherEnemy");
                            }
                            holdSkipped = true;
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
                    if (Time.time < _moveBanUntil)
                    {
                        r = "searchStalled";
                        break;
                    }
                    if (shallSearch(enemy, out r))
                    {
                        // zzap: searching toward a room behind a door -> clear it first instead of walking in.
                        if (Bot.DoorTactic.ShallClearOnSearch(enemy, out r))
                        {
                            result = ECombatDecision.DoorTactic;
                            break;
                        }
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
                    if ((stance == top || score >= ranked[0].score - 0.12f || holdSkipped) && FallBackAllowed(Time.time))
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
        // zzap (sims 2026-10-10: "decision flip-flop" 16-34 per map - utility picks and the old chain's cover moves taking turns
        // every 1.5-3 s, often while the goal enemy switched between two unseen enemies): the keep also covers a change of goal
        // enemy as long as the new one is out of sight and not close.
        bool sameFight = ReferenceEquals(_utilityPickEnemy, enemy)
            || (_utilityPickEnemy != null && !enemy.IsVisible && enemy.RealDistance > 15f);
        if (sameFight && Time.time - _utilityPickTime < UTILITY_KEEP_TIME && !beingShot
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

    private float _holdOtherBanUntil;
    private float _reloadingSince = -1f;

    private void WatchStuckReload(BotWeaponManager weaponManager)
    {
        var reload = weaponManager?.Reload;
        if (reload == null || !reload.Reloading)
        {
            _reloadingSince = -1f;
            return;
        }
        float time = Time.time;
        if (_reloadingSince < 0f)
        {
            _reloadingSince = time;
            return;
        }
        if (time - _reloadingSince < 6f)
        {
            return;
        }
        if (Player.HandsController is EFT.Player.FirearmController fc && fc.IsInReloadOperation())
        {
            return;
        }
        reload.Reloading = false;
        _reloadingSince = -1f;
        TacticDiagnostics.Count("reload.stuckCleared");
        if (TacticDiagnostics.LogOn) RaidJournal.Line($"[Reload] [{Bot.name}] reloading flag stuck 6 s with no reload running - cleared");
    }

    public bool OtherEnemyMaySeeMe(Enemy goal)
    {
        var known = Bot.EnemyController.KnownEnemies;
        for (int i = 0; i < known.Count; i++)
        {
            Enemy e = known[i];
            if (e == null || ReferenceEquals(e, goal))
            {
                continue;
            }
            if (e.IsVisible || e.InLineOfSight)
            {
                return true;
            }
            // Seen lately and close enough, or heard close by just now (04:55 sim: both freeze killers were enemies the bot
            // had only heard or not noticed): only if his last known spot has a clear line to our head (one ray per enemy).
            Vector3? last = e.LastKnownPosition;
            bool recentSeen = e.Seen && e.TimeSinceSeen < 15f && e.RealDistance < 80f;
            bool heard = e.Heard && e.TimeSinceHeard < 6f && e.RealDistance < 25f;
            if ((recentSeen || heard) && last != null
                && !Physics.Linecast(last.Value + Vector3.up * 1.5f, Bot.Transform.EyePosition, LayersMaskController.HighPolyWithTerrainMask))
            {
                return true;
            }
        }
        return false;
    }

    private const float UTILITY_KEEP_TIME = 2f;
    private Enemy _utilityPickEnemy;
    private float _utilityPickTime = -100f;
    private ECombatDecision _utilityPick;

    private const float CLOSE_OCCLUSION_KEEP_TIME = 0.8f;
    private const float CLOSE_OCCLUSION_DIST = 8f;
    // 10/12 sim: 29 flip-flops, most of them StandAndShoot <-> hidden-enemy decision at 15-45m, the enemy dropping out of
    // sight for 0.2-1.5s (Bot88: six swaps in 6s, each one stopping the diamond step and starting a new corner chase).
    // Mid range: only a shoot decision is kept, a bit longer.
    private const float MID_OCCLUSION_KEEP_TIME = 1.5f;
    private const float MID_OCCLUSION_DIST = 45f;
    private bool _keptMidRange;
    // A jiggle hide tap is 0.18-0.32s; he may also step aside meanwhile. Same value as DiamondStepper's sight tolerance.
    private const float JIGGLE_KEEP_TIME = SAIN.SAINComponent.Classes.Mover.DiamondStepper.JIGGLE_SIGHT_TOLERANCE;
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
        // zzap (user 2026-10-02 19:46 sim: "the in-and-out A/D peek shots at doors/corners got rare"): a corner jiggle
        // ducks the bot behind cover on purpose, so the enemy drops out of sight - and the hidden-enemy decision took over
        // and stopped the action (57 of 70 jiggles ended that way, median 0.5s = one peek). While jiggling, the bot's own
        // duck is not a reason to re-decide: keep the fight up to JIGGLE_KEEP_TIME, at any jiggle distance.
        bool jiggling = SAIN.SAINComponent.Classes.Mover.DiamondStepper.JiggleActive(Bot.ProfileId);
        bool close = enemy.RealDistance <= CLOSE_OCCLUSION_DIST;
        _keptMidRange = !jiggling && !close;
        float keepTime = jiggling ? JIGGLE_KEEP_TIME : close ? CLOSE_OCCLUSION_KEEP_TIME : MID_OCCLUSION_KEEP_TIME;
        if (enemy.IsVisible || !enemy.Seen || enemy.TimeSinceSeen > keepTime || (!jiggling && enemy.RealDistance > MID_OCCLUSION_DIST))
        {
            return false;
        }
        if (!ReferenceEquals(_visibleFightEnemy, enemy) || Time.time - _visibleFightTime > keepTime + 0.4f)
        {
            return false;
        }
        ECombatDecision current = Bot.Decision.CurrentCombatDecision;
        if (current != ECombatDecision.StandAndShoot && current != ECombatDecision.RushEnemy && !(jiggling && current == ECombatDecision.DogFight))
        {
            return false;
        }
        if (_keptMidRange && current != ECombatDecision.StandAndShoot)
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
        TacticDiagnostics.Count(jiggling ? "diamond.jiggleKept" : _keptMidRange ? "decision.midOcclusionKeep" : "decision.closeOcclusionKeep");
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

        // 04:55 Labs sim: a freeze from this old chain (right after a grenade throw, out of cover) had no check for a second
        // enemy at all - same check as the utility Hold before starting one.
        if (Bot.Decision.CurrentCombatDecision != ECombatDecision.Freeze && OtherEnemyMaySeeMe(enemy))
        {
            reason = "exposedFreeze";
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
        bool inRange = checkInRangeForRush(enemy);
        // zzap remake5 (user 2026-10-11): a busy enemy is judged by what the sound means - can the bot get there before the
        // reload / heal / surgery / looting ends (VulnerableRush) - not only by SAIN's 10 m path rule. Grenade out = never.
        EEnemyAction busy = enemy.Status.VulnerableAction;
        if (busy != EEnemyAction.None && Bot.Info.PersonalitySettings.Rush.CanRushEnemyReloadHeal)
        {
            bool reach = SAIN.SAINComponent.Classes.Tactics.VulnerableRush.CanReach(Bot, enemy, busy, inRange, out string why);
            SAIN.SAINComponent.Classes.Tactics.VulnerableRush.OnDecision(Bot, enemy, busy, reach, why);
            if (reach)
            {
                reason = "enemyVulnerable";
                return true;
            }
        }
        if (!inRange)
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
        // remake5: grenade in his hand - no rush (a dying / prone-hurt enemy below still is one; the corner chase above too)
        if (busy == EEnemyAction.HasGrenade && enemy.EnemyPlayer.HealthStatus != ETagStatus.Dying)
        {
            reason = "enemyHasGrenade";
            return false;
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
        if (settings.PmcOnly && Bot.Info.Profile.SkippedByPmcOnly)
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
