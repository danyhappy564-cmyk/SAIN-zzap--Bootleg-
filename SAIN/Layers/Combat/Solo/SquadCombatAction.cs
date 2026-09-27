using System.Text;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using SAIN.SAINComponent.Classes.EnemyClasses;

namespace SAIN.Layers.Combat.Solo;

/// <summary>
/// Runs SquadCombatClass (crossfire, cover a reloading/healing teammate, trade a downed teammate) as an
/// action of the regular SAIN combat layer.
/// </summary>
internal class SquadCombatAction(BotOwner bot) : BotAction(bot, nameof(SquadCombatAction)), IBotAction
{
    public override void Update(CustomLayer.ActionData data)
    {
        Bot.SquadCombat.Tick();
    }

    public override void OnSteeringTicked()
    {
        Enemy enemy = Bot.GoalEnemy;
        if (TryShootAnyTarget(enemy))
        {
            Bot.Steering.SteerByPriority(enemy, false);
            return;
        }
        var look = Bot.SquadCombat.LookTarget;
        if (look != null)
        {
            Bot.Steering.LookToPoint(look.Value);
            return;
        }
        Bot.Steering.SteerByPriority(enemy);
    }

    public override void BuildDebugText(StringBuilder stringBuilder)
    {
        stringBuilder.AppendLine($"Squad Combat: mode={Bot.SquadCombat.Mode}");
        base.BuildDebugText(stringBuilder);
    }

    public override void Stop()
    {
        base.Stop();
        Bot.SquadCombat.OnActionStopped();
    }
}
