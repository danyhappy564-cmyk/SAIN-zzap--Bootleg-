using System.Reflection;
using HarmonyLib;

namespace SAINServerMod.SimLab;

/// <summary>
/// zzap fork: ABPS (acidphantasm Bot Placement System) during sim raids (user 2026-10-09: "reserve squads called, no bot came").
/// The sim's reserve squads are boss waves of type pmcUSEC / pmcBEAR. ABPS's game plugin takes over every such wave
/// (PmcSpawnHookPatch, prefix on BotBossSpawn.TrySpawn): it ignores the wave's BossZone, picks player spawn points far from every
/// PMC (the spectator included) and from reserved points, and when none qualify it silently skips the spawn while telling the game
/// it worked. So sim bots spawned away from the zones we asked for, or not at all.
/// ABPS already has an off switch per map version (its web "original/variant maps" tab): the client asks
/// /botplacementsystem/raidstate once per raid and leaves the map alone when the answer says inactive for this map. The answer is
/// <c>MapSpawns.LastRaidState</c>, set by ABPS's prefix on GenerateLocationAndLoot; our postfix on the same method runs after it, so
/// for a sim raid we set it to (map, inactive). ABPS's code and settings are untouched; normal raids are not affected.
/// Found by name at runtime - nothing happens when ABPS isn't installed.
/// </summary>
public static class SimLabAbpsBridge
{
    public const string MAP_SPAWNS = "BotPlacementSystemServer.Controllers.MapSpawns";

    private static PropertyInfo? _state;
    private static bool _looked;

    public static bool Found
    {
        get
        {
            Look();
            return _state != null;
        }
    }

    private static void Look()
    {
        if (_looked)
        {
            return;
        }
        _looked = true;
        var type = AccessTools.TypeByName(MAP_SPAWNS);
        var prop = type == null ? null : AccessTools.Property(type, "LastRaidState");
        _state = prop != null && prop.PropertyType == typeof(ValueTuple<string, bool>) && prop.GetSetMethod(true) != null ? prop : null;
    }

    /// <summary>Makes ABPS's game plugin leave this raid's spawns to the game. False when ABPS isn't installed or changed shape.</summary>
    public static bool TurnOffForRaid(string map)
    {
        Look();
        if (_state == null)
        {
            return false;
        }
        _state.SetValue(null, ValueTuple.Create(map, false));
        return true;
    }
}
