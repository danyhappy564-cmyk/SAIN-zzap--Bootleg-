using System.Text;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using SAIN.SAINComponent.Classes.EnemyClasses;

namespace SAIN.Layers.Combat.Solo;

/// <summary>
/// Runs RepositionClass (grenade blast flank, relocate, disengage, bait peek) as an action of the SAIN combat layer.
/// </summary>
internal class RepositionAction(BotOwner bot) : BotAction(bot, nameof(RepositionAction)), IBotAction
{
    public override void Update(CustomLayer.ActionData data)
    {
        Bot.Reposition.Tick();
    }

    public override void OnSteeringTicked()
    {
        Enemy enemy = Bot.GoalEnemy;
        if (TryShootAnyTarget(enemy))
        {
            Bot.Steering.SteerByPriority(enemy, false);
            return;
        }
        var look = Bot.Reposition.LookTarget;
        if (look != null)
        {
            Bot.Steering.LookToPoint(look.Value);
            return;
        }
        Bot.Steering.SteerByPriority(enemy);
    }

    public override void BuildDebugText(StringBuilder stringBuilder)
    {
        stringBuilder.AppendLine($"Reposition: mode={Bot.Reposition.Mode}");
        base.BuildDebugText(stringBuilder);
    }

    public override void Stop()
    {
        base.Stop();
        Bot.Reposition.OnActionStopped();
    }
}
