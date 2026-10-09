using SAIN.Preset.Shared.SimLab;
using SAINServerMod.Utils;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace SAINServerMod.SimLab;

/// <summary>Body of the sim client calls: the message itself as a JSON string (shared model, read with SAINJsonUtil).</summary>
public sealed record SimEnvelope : IRequestData
{
    public string Json { get; set; } = string.Empty;
}

/// <summary>zzap fork: client calls of the simulation lab.</summary>
[Injectable(TypePriority = OnLoadOrder.Routers + 6)]
public sealed class SimLabRouter(JsonUtil jsonUtil, SimLabService sim)
    : StaticRouter(
        jsonUtil,
        [
            new RouteAction<SimEnvelope>(
                "/sain/sim/hello",
                (url, info, sessionID, output, cancellationToken) =>
                    new ValueTask<string>(SAINJsonUtil.Serialize(sim.Hello(Read<SimHello>(info) ?? new SimHello())))
            ),
            new RouteAction<SimEnvelope>(
                "/sain/sim/raid",
                (url, info, sessionID, output, cancellationToken) =>
                    new ValueTask<string>(SAINJsonUtil.Serialize(sim.LastRaid ?? new SimRaidInfo { Applied = false }))
            ),
            new RouteAction<SimEnvelope>(
                "/sain/sim/beat",
                (url, info, sessionID, output, cancellationToken) =>
                {
                    var beat = Read<SimBeat>(info);
                    if (beat != null)
                    {
                        sim.Beat(beat);
                    }
                    return new ValueTask<string>("{\"Ok\":" + (beat != null ? "true" : "false") + "}");
                }
            ),
        ]
    )
{
    private static T? Read<T>(SimEnvelope? envelope)
        where T : class
    {
        if (string.IsNullOrEmpty(envelope?.Json))
        {
            return null;
        }
        try
        {
            return SAINJsonUtil.Deserialize<T>(envelope.Json);
        }
        catch
        {
            return null;
        }
    }
}
