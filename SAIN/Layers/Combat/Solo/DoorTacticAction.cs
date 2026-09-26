using DrakiaXYZ.BigBrain.Brains;
using EFT;
using SAIN.SAINComponent.Classes.EnemyClasses;

namespace SAIN.Layers.Combat.Solo;

/// <summary>
/// Runs DoorTacticClass (jump peek, fake grenade, room trap, door grenade, silent ambush) as an
/// action of the regular SAIN combat layer.
/// </summary>
internal class DoorTacticAction(BotOwner bot) : BotAction(bot, nameof(DoorTacticAction)), IBotAction
{
    public override void Update(CustomLayer.ActionData data)
    {
        Bot.DoorTactic.Tick();
    }

    public override void OnSteeringTicked()
    {
        Enemy enemy = Bot.GoalEnemy;
        if (TryShootAnyTarget(enemy))
        {
            Bot.Steering.SteerByPriority(enemy, false);
            return;
        }
        var lookTarget = Bot.DoorTactic.LookTarget;
        if (lookTarget != null)
        {
            Bot.Steering.LookToPoint(lookTarget.Value);
            return;
        }
        Bot.Steering.SteerByPriority(enemy);
    }

    public override void Stop()
    {
        base.Stop();
        Bot.DoorTactic.OnActionStopped();
    }
}
