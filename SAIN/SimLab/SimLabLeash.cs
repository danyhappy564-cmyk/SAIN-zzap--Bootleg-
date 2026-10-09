using System.Collections.Generic;
using EFT;
using SAIN.Preset.Shared.SimLab;
using SAIN.SAINComponent.Classes.Tactics;
using UnityEngine;

namespace SAIN.SimLab;

/// <summary>
/// zzap SimLab arena leash (user 2026-10-09: "on Customs they keep wandering outside - keep them in the test place or teleport
/// them back, no data otherwise"). A sim bot that is not fighting (brain layer not one of SAIN's - ORBIT roaming, vanilla
/// patrol) and has been outside the arena circle (both sides' zones, see SimRaidInfo.Arena*) for 15 s is put back on the point
/// of its own side's zone farthest from everyone. Fights may go anywhere; only the walk-away is undone. Sim raids only.
/// </summary>
public static class SimLabLeash
{
    private const float OUTSIDE_SECONDS = 15f;
    private const float MARGIN = 1.15f;

    private static readonly Dictionary<string, float> _outsideSince = new();
    public static int Teleports { get; private set; }

    public static void Reset()
    {
        _outsideSince.Clear();
        Teleports = 0;
    }

    public static void Check(Player player, string role, SimRaidInfo info, float now)
    {
        var owner = player?.AIData?.BotOwner;
        if (info == null || !info.HasArena || !info.Leash || owner == null || owner.BotState != EBotState.Active || owner.Brain?.BaseBrain == null)
        {
            return;
        }
        string layer = owner.Brain.ActiveLayerName() ?? string.Empty;
        float dx = player.Position.x - info.ArenaX;
        float dz = player.Position.z - info.ArenaZ;
        float radius = info.ArenaRadius * MARGIN;
        if (layer.StartsWith("SAIN") || dx * dx + dz * dz <= radius * radius)
        {
            _outsideSince.Remove(player.ProfileId);
            return;
        }
        if (!_outsideSince.TryGetValue(player.ProfileId, out float since))
        {
            _outsideSince[player.ProfileId] = now;
            return;
        }
        if (now - since < OUTSIDE_SECONDS)
        {
            return;
        }
        _outsideSince.Remove(player.ProfileId);
        string zones = role == info.SideA ? info.ZonesA : role == info.SideB ? info.ZonesB : info.ZonesA + "," + info.ZonesB;
        var point = SimLabAbpsSpawn.FarthestPoint(zones, 10f);
        if (point == null)
        {
            return;
        }
        Vector3 from = player.Position;
        player.Teleport(point.Position + Vector3.up * 0.25f);
        Teleports++;
        TacticDiagnostics.Count("sim.leash");
        if (TacticDiagnostics.LogOn)
        {
            RaidJournal.Line($"[SimLab] leash: [{player.Profile?.Nickname}] {role} left the arena ({Mathf.Sqrt(dx * dx + dz * dz):0}m from its centre, layer {layer}) - back to [{zones}] from ({from.x:0},{from.z:0})");
        }
    }
}
