using System.Text;
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

    /// <summary>
    /// Shown in SAIN's in-game debug overlay (SAIN debug mode) for the selected bot.
    /// </summary>
    public override void BuildDebugText(StringBuilder stringBuilder)
    {
        stringBuilder.AppendLine($"Door Tactic: plan={Bot.DoorTactic.Plan} step={Bot.DoorTactic.Step} look={(Bot.DoorTactic.LookTarget != null ? "door/room" : "default")}");
        base.BuildDebugText(stringBuilder);
    }

    public override void Stop()
    {
        base.Stop();
        Bot.DoorTactic.OnActionStopped();
    }
}
