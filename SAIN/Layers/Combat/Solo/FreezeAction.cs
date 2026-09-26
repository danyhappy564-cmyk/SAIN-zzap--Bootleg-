using DrakiaXYZ.BigBrain.Brains;
using EFT;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.SAINComponent.Classes.EnemyClasses;
using UnityEngine;

namespace SAIN.Layers.Combat.Solo;

internal class FreezeAction(BotOwner bot) : BotAction(bot, nameof(FreezeAction)), IBotAction
{
    public override void Update(CustomLayer.ActionData data)
    {
        Enemy Enemy = Bot.GoalEnemy;
        Bot.Mover.Pose.SetPoseToCover(Enemy);
    }

    public override void Start()
    {
        base.Start();
        Bot.Mover.Stop();
    }

    /// <summary>
    /// zzap: an ambushing bot aims at the corner the enemy has to come around (the last visible point
    /// on the path to them) rather than SAIN's default look, like a camper holding the entry angle.
    /// Toggle: F6 General > Freeze Ambush (zzap) > Watch Approach Corner.
    /// </summary>
    public override void OnSteeringTicked()
    {
        Enemy enemy = Bot.GoalEnemy;
        if (TryShootAnyTarget(enemy))
        {
            Bot.Steering.SteerByPriority(enemy, false);
            return;
        }
        Vector3? corner = enemy?.VisiblePathPoint;
        if (corner != null && GlobalSettingsClass.Instance.General.FreezeAmbush.WatchApproachCorner)
        {
            Bot.Steering.LookToPoint(corner.Value + Vector3.up * 1.3f);
            return;
        }
        Bot.Steering.SteerByPriority(enemy);
    }
}
