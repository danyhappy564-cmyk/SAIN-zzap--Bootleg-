using System.Reflection;
using System.Text.Json.Nodes;
using HarmonyLib;
using SPTarkov.Reflection.Patching;

namespace SAINServerMod.SimLab;

/// <summary>
/// zzap fork: ORBIT during sim raids (user 2026-10-09: "ORBIT's settings and zone editor can spoil the fights we want - fix it
/// on the SAIN side"). ORBIT's game plugin fetches /orbit/config and /orbit/zones from ORBIT's server mod at every raid start
/// (OrbitInitPatch -> ServerConfig.Fetch). While the sim is armed, these postfixes change what ORBIT's server mod hands out:
///   Ghost Mode off            - arena bots stay real bots (a bot far from the spectator would otherwise sleep and its fights
///                               be resolved off-screen by dice: no SAIN behavior at all)
///   no extraction             - squads don't leave the arena (loot value, objectives done, raid clock, emergency)
///   no PMC objectives         - no loot runs / kill hunts / quest visits across the map
///   arena zone                - this map's hotspots replaced by one attractor over both sides' spawn zones
/// ORBIT's own files, presets and code are untouched; outside a sim raid the responses pass through unchanged.
/// Found by name at runtime - nothing happens when ORBIT isn't installed.
/// </summary>
public static class SimLabOrbitBridge
{
    public const string PRESET_SERVICE = "Orbit.Server.Presets.PresetService";

    public static MethodInfo? Method(string name)
    {
        var type = AccessTools.TypeByName(PRESET_SERVICE);
        return type == null ? null : AccessTools.Method(type, name);
    }

    public static string? EditConfig(string? json, SimLabConfig cfg, List<string> changes)
    {
        if (string.IsNullOrEmpty(json) || JsonNode.Parse(json) is not JsonObject root)
        {
            return json;
        }
        void Set(string section, string key, JsonNode value)
        {
            if (root[section] is not JsonObject obj)
            {
                obj = new JsonObject();
                root[section] = obj;
            }
            obj[key] = value;
        }
        if (cfg.OrbitGhostOff)
        {
            Set("ai_limiter", "enabled", false);
            changes.Add("ghost off");
        }
        if (cfg.OrbitNoExtract)
        {
            Set("loot", "extract_pmc", false);
            Set("loot", "extract_player_scav", false);
            Set("main_objectives", "extract_on_all_completed", false);
            Set("main_objectives", "time_extract_window_min", 600f);
            Set("main_objectives", "time_extract_window_max", 600f);
            Set("player_scav", "time_extract_window_min", 600f);
            Set("player_scav", "time_extract_window_max", 600f);
            Set("general", "emergency_extract_enabled", false);
            changes.Add("no extract");
        }
        if (cfg.OrbitNoObjectives)
        {
            Set("main_objectives", "enabled_for_pmc", false);
            changes.Add("no PMC objectives");
        }
        return root.ToJsonString();
    }

    public static string? EditZones(string? json, string map, SAIN.Preset.Shared.SimLab.SimRaidInfo raid, List<string> changes)
    {
        if (string.IsNullOrEmpty(json) || !raid.HasArena || JsonNode.Parse(json) is not JsonObject root)
        {
            return json;
        }
        // Keep the key spelling ORBIT uses for this map (lower-case ids). A map variant (SPT-MapVariants) is looked up
        // first as "<map>@<variant>" (Orbit.Helpers.MapVariants.ZoneKey), so those keys get the arena too.
        var keys = root.Select(kv => kv.Key)
            .Where(k => string.Equals(k, map, StringComparison.OrdinalIgnoreCase) || k.StartsWith(map + "@", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (!keys.Any(k => string.Equals(k, map, StringComparison.OrdinalIgnoreCase)))
        {
            keys.Add(map);
        }
        foreach (string key in keys)
        {
            var arena = new JsonObject
            {
                ["Name"] = "SAIN sim arena",
                ["Position"] = new JsonObject { ["x"] = raid.ArenaX, ["y"] = raid.ArenaZ },
                ["Radius"] = new JsonObject { ["Min"] = raid.ArenaRadius, ["Max"] = raid.ArenaRadius },
                ["Force"] = new JsonObject { ["Min"] = 1.5f, ["Max"] = 2f },
                ["Decay"] = 1f,
                ["KillMains"] = true,
            };
            root[key] = new JsonObject
            {
                ["SchemaVersion"] = (root[key] as JsonObject)?["SchemaVersion"]?.DeepClone() ?? 0,
                ["BuiltinZones"] = new JsonObject(),
                ["CustomZones"] = new JsonArray(arena),
                ["Convergence"] = new JsonObject
                {
                    ["Radius"] = new JsonObject { ["Min"] = 0f, ["Max"] = 0f },
                    ["Force"] = new JsonObject { ["Min"] = 0f, ["Max"] = 0f },
                    ["Enabled"] = false,
                },
            };
        }
        changes.Add($"arena zone ({raid.ArenaX:0},{raid.ArenaZ:0}) r{raid.ArenaRadius:0}m, other hotspots off on {string.Join("/", keys)}");
        return root.ToJsonString();
    }
}

public sealed class SimLabOrbitConfigPatch() : AbstractPatch("zzap.SAIN.SimLabOrbitConfigPatch")
{
    protected override MethodBase? GetTargetMethod()
    {
        return SimLabOrbitBridge.Method("ConfigForGame");
    }

    [PatchPostfix]
    public static void Postfix(ref string __result)
    {
        SimLabService.Instance?.OrbitConfig(ref __result);
    }
}

public sealed class SimLabOrbitZonesPatch() : AbstractPatch("zzap.SAIN.SimLabOrbitZonesPatch")
{
    protected override MethodBase? GetTargetMethod()
    {
        return SimLabOrbitBridge.Method("ZonesForGame");
    }

    [PatchPostfix]
    public static void Postfix(ref string __result)
    {
        SimLabService.Instance?.OrbitZones(ref __result);
    }
}
