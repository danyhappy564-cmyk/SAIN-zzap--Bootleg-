using SAIN.Preset.Shared.Enums;
using SAIN.SAINComponent;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.Classes.Search;
using UnityEngine;
using static SAIN.SAINComponent.Classes.Search.SearchReasonsStruct;

namespace SAIN.Classes.Bot.Search;

public class SearchDecider : BotSubClass<SearchClass>
{
    private const float CHECK_LOOT_INTERVAL = 1f;
    private const float SEARCH_LOOT_CHANCE_PERCENT = 40f;

    private float _nextRecalcSearchTime;
    private float _nextCheckLootTime;

    public SearchDecider(SearchClass searchClass)
        : base(searchClass) { }

    public bool ShallStartSearch(Enemy enemy, out SearchReasonsStruct reasons)
    {
        CalculateSearchTime();
        reasons = new SearchReasonsStruct();

        if (!WantToSearch(enemy, out reasons.WantSearchReasons))
        {
            reasons.NotSearchReason = ENotSearchReason.DontWantTo;
            return false;
        }

        if (enemy.Events.OnSearch.Value)
        {
            if (BaseClass.PathFinder.TargetPlace == null)
            {
                reasons.NotSearchReason = ENotSearchReason.NullTargetPlace;
                return false;
            }
            return true;
        }

        if (!BaseClass.PathFinder.HasPathToSearchTarget(enemy, out string pathCalcFailReason))
        {
            reasons.NotSearchReason = ENotSearchReason.PathCalcFailed;
            reasons.PathCalcFailReason = pathCalcFailReason;
            return false;
        }
        return true;
    }

    private void CalculateSearchTime()
    {
        if (Bot.Decision.CurrentCombatDecision != ECombatDecision.Search && _nextRecalcSearchTime < Time.time)
        {
            _nextRecalcSearchTime = Time.time + 120f;
            Bot.Info.CalcTimeBeforeSearch();
        }
    }

    public bool WantToSearch(Enemy enemy, out WantSearchReasonsStruct reasons)
    {
        reasons = new WantSearchReasonsStruct();
        if (enemy == null)
        {
            reasons.NotWantToSearchReason = ENotWantToSearchReason.NullEnemy;
            return false;
        }

        var lastKnown = enemy.KnownPlaces.LastKnownPlace;

        if (lastKnown == null)
        {
            reasons.NotWantToSearchReason = ENotWantToSearchReason.NullLastKnown;
            return false;
        }

        if (lastKnown.HasArrivedPersonal || lastKnown.HasArrivedSquad)
        {
            reasons.NotWantToSearchReason = ENotWantToSearchReason.AlreadySearchedLastKnown;
            return false;
        }

        if (!enemy.Seen && !Bot.Info.PersonalitySettings.Search.WillSearchFromAudio && !QuietHeardSearch(enemy))
        {
            reasons.NotWantToSearchReason = ENotWantToSearchReason.WontSearchFromAudio;
            return false;
        }

        if (!CanStartSearch(enemy, out reasons.CantStartReason))
        {
            reasons.NotWantToSearchReason = ENotWantToSearchReason.CantStart;
            return false;
        }

        if (!ShallSearch(enemy, out reasons.WantToSearchReason))
        {
            reasons.NotWantToSearchReason = ENotWantToSearchReason.ShallNotSearch;
            return false;
        }

        reasons.NotWantToSearchReason = ENotWantToSearchReason.None;
        return true;
    }

    private bool ShallSearch(Enemy enemy, out EWantToSearchReason reason)
    {
        if (
            enemy.Hearing.EnemyHeardFromPeace
            && Bot.Info.PersonalitySettings.Search.HeardFromPeaceBehavior == EHeardFromPeaceBehavior.SearchNow
        )
        {
            reason = EWantToSearchReason.HeardFromPeaceSearchNow;
            return true;
        }

        if (
            ShallBeStealthyDuringSearch(enemy)
            && Bot.Decision.EnemyDecisions.TimeToUnfreeze > Time.time
            && enemy.TimeSinceLastKnownUpdated > 10f
        )
        {
            reason = EWantToSearchReason.BeingStealthy;
            return true;
        }

        float timeBeforeSearch = Bot.Info.TimeBeforeSearch;

        if (enemy.Events.OnSearch.Value)
        {
            return ShallContinueSearch(enemy, timeBeforeSearch, out reason);
        }

        return shallBeginSearch(enemy, timeBeforeSearch, out reason);
    }

    public bool ShallBeStealthyDuringSearch(Enemy enemy)
    {
        if (!SAINPlugin.LoadedPreset.GlobalSettings.Mind.SneakyBots)
        {
            return false;
        }

        if (SAINPlugin.LoadedPreset.GlobalSettings.Mind.OnlySneakyPersonalitiesSneaky && !Bot.Info.PersonalitySettings.Search.Sneaky)
        {
            return false;
        }

        if (!enemy.Hearing.EnemyHeardFromPeace)
        {
            return false;
        }

        if (Bot.Info.PersonalitySettings.Search.HeardFromPeaceBehavior == EHeardFromPeaceBehavior.SearchNow)
        {
            return false;
        }

        float maxDist = SAINPlugin.LoadedPreset.GlobalSettings.Mind.MaximumDistanceToBeSneaky;
        return enemy.RealDistance < maxDist;
    }

    private bool ShallBeginSearchCauseLooting(Enemy enemy)
    {
        if (!enemy.Status.EnemyIsLooting)
        {
            return false;
        }

        if (_nextCheckLootTime < Time.time)
        {
            _nextCheckLootTime = Time.time + CHECK_LOOT_INTERVAL;
            return EFTMath.RandomBool(SEARCH_LOOT_CHANCE_PERCENT);
        }
        return false;
    }

    private bool shallBeginSearch(Enemy enemy, float timeBeforeSearch, out EWantToSearchReason reason)
    {
        if (ShallBeginSearchCauseLooting(enemy))
        {
            enemy.Status.SearchingBecauseLooting = true;
            reason = EWantToSearchReason.NewSearch_Looting;
            return true;
        }

        float myPower = Bot.Info.Profile.PowerLevel;

        if (enemy.EnemyPlayer.AIData.PowerOfEquipment < myPower * 0.5f)
        {
            reason = EWantToSearchReason.NewSearch_PowerLevel;
            return true;
        }

        if (enemy.Seen && enemy.TimeSinceSeen >= timeBeforeSearch)
        {
            reason = EWantToSearchReason.NewSearch_EnemyNotSeen;
            return true;
        }

        var squadSeenPlace = enemy.KnownPlaces.LastSquadSeenPlace;

        if (squadSeenPlace != null && squadSeenPlace.TimeSincePositionUpdated >= timeBeforeSearch)
        {
            reason = EWantToSearchReason.NewSearch_EnemyNotSeen_Squad;
            return true;
        }

        if (Bot.Info.PersonalitySettings.Search.WillSearchFromAudio)
        {
            if (enemy.Heard && enemy.TimeSinceHeard >= timeBeforeSearch)
            {
                reason = EWantToSearchReason.NewSearch_EnemyNotHeard;
                return true;
            }
            var squadHeardPlace = enemy.KnownPlaces.LastSquadHeardPlace;
            if (squadHeardPlace != null && squadHeardPlace.TimeSincePositionUpdated >= timeBeforeSearch)
            {
                reason = EWantToSearchReason.NewSearch_EnemyNotHeard_Squad;
                return true;
            }
        }

        if (QuietHeardSearch(enemy))
        {
            SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count("search.quietHeard.start");
            reason = EWantToSearchReason.NewSearch_EnemyNotHeard;
            return true;
        }

        reason = EWantToSearchReason.None;
        return false;
    }

    private const float QUIET_HEARD_SEARCH_AFTER = 15f;

    /// <summary>
    /// zzap (user 2026-10-20: "after a fight some just stand there" - F8 mark #6, then "search heard-only enemies carefully once
    /// it's been quiet 15 s"): an enemy only ever heard, nothing new from him for 15 s+, and the bot's own rules would not go
    /// yet - its personality doesn't search from audio (Timmy) or its wait before searching is longer (Normal 40-80 s, Rat,
    /// SnappingTurtle...; the enemy is forgotten about 60 s after the last news, so it never went). 1st stage: 52% of the bots
    /// standing after a quiet fight were refused for audio, 39% for the wait. Bots that never search (Coward) stay out.
    /// SearchClass walks these searches (no sprint).
    /// </summary>
    public bool QuietHeardSearch(Enemy enemy)
    {
        if (enemy == null || enemy.Seen || !enemy.Heard || enemy.TimeSinceLastKnownUpdated < QUIET_HEARD_SEARCH_AFTER)
        {
            return false;
        }
        var search = Bot.Info.PersonalitySettings.Search;
        return search.WillSearchForEnemy && (!search.WillSearchFromAudio || Bot.Info.TimeBeforeSearch > QUIET_HEARD_SEARCH_AFTER);
    }

    private bool CanStartSearch(Enemy enemy, out ECantStartReason reason)
    {
        var searchSettings = Bot.Info.PersonalitySettings.Search;

        if (!searchSettings.WillSearchForEnemy)
        {
            reason = ECantStartReason.WontSearchForEnemy;
            return false;
        }

        if (Bot.Suppression.IsHeavySuppressed)
        {
            reason = ECantStartReason.Suppressed;
            return false;
        }

        reason = ECantStartReason.None;
        return true;
    }

    private bool ShallContinueSearch(Enemy enemy, float timeBeforeSearch, out EWantToSearchReason reason)
    {
        if (enemy.IsVisible)
        {
            reason = EWantToSearchReason.ContinueSearch_EnemyVisible;
            return true;
        }

        if (enemy.Seen && enemy.TimeSinceSeen < 2f)
        {
            reason = EWantToSearchReason.ContinueSearch_EnemyVisible;
            return true;
        }

        if (enemy.Status.SearchingBecauseLooting)
        {
            reason = EWantToSearchReason.ContinueSearch_Looting;
            return true;
        }

        float myPower = Bot.Info.Profile.PowerLevel;
        if (enemy.EnemyPlayer.AIData.PowerOfEquipment < myPower * 0.5f)
        {
            reason = EWantToSearchReason.ContinueSearch_PowerLevel;
            return true;
        }

        timeBeforeSearch = Mathf.Clamp(timeBeforeSearch / 3f, 0f, 120f);
        if (enemy.Seen && enemy.TimeSinceSeen >= timeBeforeSearch)
        {
            reason = EWantToSearchReason.ContinueSearch_EnemyNotSeen_Personal;
            return true;
        }
        var squadSeenPlace = enemy.KnownPlaces.LastSquadSeenPlace;
        if (squadSeenPlace != null && squadSeenPlace.TimeSincePositionUpdated >= timeBeforeSearch)
        {
            reason = EWantToSearchReason.ContinueSearch_EnemyNotSeen_Squad;
            return true;
        }

        if (Bot.Info.PersonalitySettings.Search.WillSearchFromAudio)
        {
            if (enemy.Heard && enemy.TimeSinceHeard >= timeBeforeSearch)
            {
                reason = EWantToSearchReason.ContinueSearch_EnemyNotHeard;
                return true;
            }

            var squadHeardPlace = enemy.KnownPlaces.LastSquadHeardPlace;
            if (squadHeardPlace != null && squadHeardPlace.TimeSincePositionUpdated >= timeBeforeSearch)
            {
                reason = EWantToSearchReason.ContinueSearch_EnemyNotHeard_Squad;
                return true;
            }
        }

        if (QuietHeardSearch(enemy))
        {
            reason = EWantToSearchReason.ContinueSearch_EnemyNotHeard;
            return true;
        }

        reason = EWantToSearchReason.None;
        return false;
    }
}
