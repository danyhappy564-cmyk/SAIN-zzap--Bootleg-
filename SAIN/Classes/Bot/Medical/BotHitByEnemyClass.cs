using EFT;
using EFT.Ballistics;
using SAIN.Components;
using SAIN.Models.Structs;
using SAIN.Preset;
using SAIN.Preset.Shared.Enums;
using SAIN.SAINComponent.Classes.EnemyClasses;
using UnityEngine;

namespace SAIN.SAINComponent.Classes;

public class BotHitByEnemyClass : BotBase
{
    public Enemy EnemyWhoLastShotMe { get; private set; }

    public BotHitByEnemyClass(BotComponent bot)
        : base(bot) { }

    public override void Init()
    {
        Bot.EnemyController.Events.OnEnemyRemoved += clearEnemy;
        base.Init();
    }

    public void GetHit(DamageInfo DamageInfo, EBodyPart bodyPart, float floatVal)
    {
        var player = DamageInfo.Player?.iPlayer;
        if (player == null)
        {
            return;
        }
        if (BotTypeDefinitions.IsModded(Bot.Info.Profile.WildSpawnType))
        {
            RevealShooter(player);
        }
        Enemy enemy = Bot.EnemyController.GetEnemy(player.ProfileId, true);
        if (enemy == null)
        {
            return;
        }
        EnemyWhoLastShotMe = enemy;
        enemy.Status.GetHit(DamageInfo);
    }

    /// <summary>
    /// zzap (2026-10-23, moved from Black Division's fork - BDHitRevealsShooter, felt in game 2026-09-24): being hit
    /// never makes an enemy known in SAIN - only sight and hearing update KnownPlaces - so a modded bot hit by a shooter
    /// it neither saw nor heard kept GoalEnemy null and its SAIN combat layers stayed off ("shot and never reacts").
    /// Register the shooter and mark where the shot came from as an injury place, so SAIN picks it as an enemy.
    /// CheckAddEnemy refuses squadmates and players that aren't enemies of this bot.
    /// </summary>
    private void RevealShooter(IPlayer shooter)
    {
        if (shooter.HealthController?.IsAlive != true)
        {
            return;
        }
        Enemy enemy = Bot.EnemyController.CheckAddEnemy(shooter);
        if (enemy == null)
        {
            // CheckAddEnemy only finds players already in the group's enemy list - hostile but not listed yet: add him to
            // the group first and try again (same as CloseSense / the frag blast).
            var group = BotOwner.BotsGroup;
            if (group == null || group.Enemies.ContainsKey(shooter) || group.IsAlly(shooter) || !group.IsPlayerEnemy(shooter))
            {
                return;
            }
            group.AddEnemy(shooter, EBotEnemyCause.checkAddTODO);
            enemy = Bot.EnemyController.CheckAddEnemy(shooter);
            if (enemy == null)
            {
                return;
            }
        }

        bool wasKnown = enemy.EnemyKnown;
        var report = new SAINHearingReport
        {
            position = shooter.Position,
            soundType = SAINSoundType.Shot,
            placeType = EEnemyPlaceType.Injury,
            isDanger = true,
            shallReportToSquad = false,
        };
        enemy.KnownPlaces.UpdatePersonalHeardPosition(report, Time.time);
        Bot.Memory.SetUnderFire(enemy, shooter.Position);

        if (!wasKnown && BigBrainHandler.BrainAssignment.ReportHitRevealOnce(Bot.ProfileId))
        {
            Logger.LogWarning(
                $"[SAIN ModdedBots] {Bot.BotOwner.name} ({Bot.Info.Profile.WildSpawnType}) was hit by an enemy it did not know about - marked it known"
            );
        }
    }

    private void clearEnemy(string profileId, Enemy enemy)
    {
        if (enemy == EnemyWhoLastShotMe)
        {
            EnemyWhoLastShotMe = null;
        }
    }

    public override void Dispose()
    {
        Bot.EnemyController.Events.OnEnemyRemoved -= clearEnemy;
        base.Dispose();
    }
}
