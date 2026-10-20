using SAIN.SAINComponent.Classes.EnemyClasses;
using UnityEngine;

namespace SAIN.Components.BotComponentSpace.Classes.EnemyClasses;

public class EnemyKnownChecker(EnemyData enemyData)
{
    private readonly Enemy Enemy = enemyData.Enemy;

    public void Init(BotComponent bot)
    {
        bot.BotActivation.BotActiveToggle.OnToggle += BotStateChanged;
    }

    public void TickEnemy(float currentTime, float forgetEnemyTime, bool botSearching)
    {
        bool enemyKnown = ShallKnowEnemy(currentTime, forgetEnemyTime, botSearching);
        SetEnemyKnown(enemyKnown, currentTime);
    }

    public void Dispose(BotComponent bot)
    {
        bot.BotActivation.BotActiveToggle.OnToggle -= BotStateChanged;
    }

    private void BotStateChanged(bool botActive)
    {
        if (!botActive)
        {
            SetEnemyKnown(false, Time.time);
        }
    }

    public void SetEnemyKnown(bool enemyKnown, float currentTime)
    {
        Enemy.Events.OnEnemyKnownChanged.CheckToggle(enemyKnown, currentTime);
    }

    private bool ShallKnowEnemy(float currentTime, float forgetEnemyTime, bool searching)
    {
        if (!Enemy.IsEnemyActive(Enemy))
        {
            return false;
        }

        var places = Enemy.KnownPlaces;
        if (places.LastKnownPlace == null)
        {
            //if (Enemy.EnemyKnown)
            //    Logger.LogDebug("enemy null lastknown");
            return false;
        }

        float timeSinceUpdate = currentTime - places.TimeLastKnownUpdated;

        //if (Enemy.EnemyKnown && Enemy.EnemyPlayer.IsYourPlayer)
        //    Logger.LogDebug($"timesince update [{timeSinceUpdate}]");

        if (timeSinceUpdate > LAST_KNOWN_TIME_UPDATE_UPPER_LIMIT)
        {
            //if (Enemy.EnemyKnown)
            //    Logger.LogDebug("enemy forgotten becuz update too long");

            return false;
        }

        // zzap (user 2026-10-20, 2nd sim 224327 mark #5: "it killed its enemy and just stands there"): Furiously99 searched
        // to the other enemy's last known spot, found nothing, and then had nothing left to do - push "nowhere to go",
        // search "already searched" - so it stood in place 19 s+ until the forget time (about a minute) ran out; the 1st
        // stage counted the same 826 times. A spot already searched and 20 s without news: he is gone, the fight is over
        // and the normal after-fight flow takes the bot. Seeing or hearing him again brings him straight back.
        if (timeSinceUpdate >= SEARCHED_GONE_AFTER && !Enemy.IsVisible
            && (places.LastKnownPlace.HasArrivedPersonal || places.LastKnownPlace.HasArrivedSquad))
        {
            if (Enemy.EnemyKnown) SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count("enemy.forgotSearched");
            return false;
        }

        if (timeSinceUpdate <= forgetEnemyTime)
        {
            return true;
        }

        if (searching && BotIsSearchingForMe())
        {
            return true;
        }

        //if (Enemy.EnemyKnown)
        //    Logger.LogDebug($"enemy forgotten. timesinceupdate: {timeSinceUpdate} forgetenemytime: {Bot.Info.ForgetEnemyTime}");

        return false;
    }

    private const float LAST_KNOWN_TIME_UPDATE_UPPER_LIMIT = 400f;
    private const float SEARCHED_GONE_AFTER = 20f;

    public bool BotIsSearchingForMe()
    {
        if (Enemy.Events.OnSearch.Value)
        {
            return !Enemy.KnownPlaces.SearchedAllKnownLocations;
        }
        return false;
    }
}
