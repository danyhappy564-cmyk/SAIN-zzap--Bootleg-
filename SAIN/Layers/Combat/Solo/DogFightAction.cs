using System.Text;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.Classes.Mover;

namespace SAIN.Layers.Combat.Solo;

internal class DogFightAction(BotOwner bot) : BotAction(bot, "Dog Fight"), IBotAction
{
    public override void Update(CustomLayer.ActionData data)
    {
        Enemy Enemy = Bot.GoalEnemy;
        Bot.Mover.SetTargetPose(1f);
        _diamond ??= new DiamondStepper(Bot, "DogFight");
        if (_diamond.Tick(Enemy, 1.5f))
        {
            return;
        }
        Bot.Mover.DogFight.DogFightMove(true, Enemy);
    }

    public override void OnSteeringTicked()
    {
        Enemy enemy = Bot.GoalEnemy;
        TryShootAnyTarget(enemy);
        if (!Bot.Steering.SteerByPriority(enemy, false))
        {
            Bot.Steering.LookToLastKnownEnemyPosition(enemy);
        }
    }

    private DiamondStepper _diamond;

    public override void Stop()
    {
        base.Stop();
        _diamond?.Stop();
        Bot.Mover.DogFight.ResetDogFightStatus();
    }

    public override void BuildDebugText(StringBuilder stringBuilder)
    {
        DebugOverlay.AddBaseInfo(Bot, BotOwner, stringBuilder);
    }
}
