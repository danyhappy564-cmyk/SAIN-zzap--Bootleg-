using DrakiaXYZ.BigBrain.Brains;
using EFT;
using SAIN.Extensions;
using SAIN.Models.Enums;

namespace SAIN.Layers.Flashed;

internal class SAINFlashedLayer(BotOwner bot, int priority) : SAINLayer(bot, priority, Name, ESAINLayer.Flashed)
{
    public static readonly string Name = BuildLayerName("Flashed");

    public override Action GetNextAction()
    {
        return new Action(typeof(FlashedAction), "Flashed");
    }

    public override bool IsActive()
    {
        if (!BotOwner.IsBotActive())
        {
            CheckActiveChanged(false);
            return false;
        }

        bool active = GetBotComponent() && Bot.Flashed.IsFlashed;
        CheckActiveChanged(active);
        return active;
    }

    /// <summary>
    /// Diagnostic (2026-09-24 field report: gassed bots seemed to snap out of Flashed when shot at
    /// inside the smoke). IsActive only ever checks IsFlashed, so if the brain leaves this layer while
    /// the bot is still flashed, a higher-priority layer (possibly another mod's) took over - log which.
    /// </summary>
    protected override void OnSwitchedAway(string newLayerName)
    {
        if (Bot != null && Bot.Flashed.IsFlashed)
        {
            Logger.LogWarning(
                $"[FlashedLayer] [{Bot.name}] left Flashed layer for [{newLayerName}] while still flashed "
                    + $"([{Bot.Flashed.TimeRemaining:F1}s] left) - a higher-priority layer took over."
            );
        }
    }

    public override bool IsCurrentActionEnding()
    {
        return false;
    }
}
