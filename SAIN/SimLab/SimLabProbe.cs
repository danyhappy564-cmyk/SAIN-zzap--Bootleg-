using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.Game.Spawning;
using SAIN.Components;
using SAIN.Preset.Shared.SimLab;
using SAIN.SAINComponent;
using UnityEngine;

namespace SAIN.SimLab;

/// <summary>
/// zzap SimLab measurements taken while a sim raid runs (user 2026-10-09), sampled with the runner's 2 s tick:
///   spawn check  - the zone EFT really put each new sim bot in (BotsGroup.BotZone) vs the zones its side was given, plus the
///                  distance to the nearest spawn point of those zones ("Customs bots seemed to spawn in odd places")
///   layer time   - seconds bots spent in each brain layer (ORBIT / SAIN / vanilla), to tune ORBIT vs SAIN per map
///   wall stare   - bots in SAIN's combat layer with a wall within 1 m in front of the eyes (screenshot: a bot "peeking" with
///                  its face on a flat wall) and whether the goal enemy is in that direction (looking at the enemy through it)
/// Only runs inside a sim raid; nothing here exists for normal players.
/// </summary>
public static class SimLabProbe
{
    private const float WALL_DISTANCE = 1.0f;
    private const int MAX_OFF_SAMPLES = 25;

    private static readonly Dictionary<string, BotZone> _zones = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, float> _layerSeconds = new();
    private static readonly Dictionary<string, int> _stareByDecision = new();
    private static readonly Dictionary<string, int> _spawnZones = new();
    private static readonly List<SimSpawnSample> _spawnOff = new();
    private static int _combatSamples;
    private static int _stare;
    private static int _stareTowardEnemy;
    private static int _spawnIn;
    private static int _spawnOffCount;
    private static float _orbitGhost;
    private static readonly Dictionary<string, float> _orbitObjective = new();
    private static HashSet<string> _wantedA = new(StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> _wantedB = new(StringComparer.OrdinalIgnoreCase);
    private static SimRaidInfo _info;

    public static void Reset(SimRaidInfo info)
    {
        _info = info;
        _zones.Clear();
        _layerSeconds.Clear();
        _stareByDecision.Clear();
        _spawnZones.Clear();
        _spawnOff.Clear();
        _combatSamples = 0;
        _stare = 0;
        _stareTowardEnemy = 0;
        _spawnIn = 0;
        _spawnOffCount = 0;
        _orbitGhost = 0f;
        _orbitObjective.Clear();
        _wantedA = Split(info?.ZonesA);
        _wantedB = Split(info?.ZonesB);
        try
        {
            foreach (var zone in UnityEngine.Object.FindObjectsOfType<BotZone>())
            {
                if (zone != null)
                {
                    _zones[zone.NameZone] = zone;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[SimLab] bot zones not readable: {ex.Message}");
        }
    }

    private static HashSet<string> Split(string zones)
    {
        return new HashSet<string>(
            (zones ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(z => z.Trim()),
            StringComparer.OrdinalIgnoreCase
        );
    }

    /// <summary>First time the runner sees a sim bot (within 2 s of its spawn).</summary>
    public static void OnNewBot(Player player, string role, float raidTime)
    {
        if (_info == null || player == null)
        {
            return;
        }
        string side = role == _info.SideA && role != _info.SideB ? "A" : role == _info.SideB && role != _info.SideA ? "B" : "?";
        var wanted = side == "A" ? _wantedA : side == "B" ? _wantedB : new HashSet<string>(_wantedA.Concat(_wantedB), StringComparer.OrdinalIgnoreCase);
        string zone = player.AIData?.BotOwner?.BotsGroup?.BotZone?.NameZone ?? "?";
        _spawnZones.TryGetValue($"{side}:{zone}", out int n);
        _spawnZones[$"{side}:{zone}"] = n + 1;
        if (wanted.Count == 0 || wanted.Contains(zone))
        {
            _spawnIn++; // empty = whole map wanted
            return;
        }
        _spawnOffCount++;
        if (_spawnOff.Count >= MAX_OFF_SAMPLES)
        {
            return;
        }
        Vector3 pos = player.Position;
        _spawnOff.Add(new SimSpawnSample
        {
            Role = role,
            Side = side,
            Zone = zone,
            Wanted = string.Join(",", wanted),
            X = pos.x,
            Y = pos.y,
            Z = pos.z,
            Distance = DistanceToZones(pos, wanted),
            RaidTime = raidTime,
        });
    }

    private static float DistanceToZones(Vector3 pos, HashSet<string> wanted)
    {
        float best = float.MaxValue;
        foreach (string name in wanted)
        {
            if (!_zones.TryGetValue(name, out BotZone zone) || zone.SpawnPoints == null)
            {
                continue;
            }
            foreach (ISpawnPoint point in zone.SpawnPoints)
            {
                float d = Vector3.Distance(pos, point.Position);
                if (d < best)
                {
                    best = d;
                }
            }
        }
        return best == float.MaxValue ? -1f : best;
    }

    /// <summary>Every runner tick (2 s) for each living sim bot.</summary>
    public static void Sample(Player player, float dt)
    {
        var owner = player?.AIData?.BotOwner;
        // Still spawning/activating: the brain isn't built yet (2026-10-09: NRE in ActiveLayerName on a fresh bot).
        if (owner == null || owner.BotState != EBotState.Active || owner.Brain?.BaseBrain == null)
        {
            return;
        }
        string layer = owner.Brain?.ActiveLayerName() ?? "none";
        _layerSeconds.TryGetValue(layer, out float seconds);
        _layerSeconds[layer] = seconds + dt;
        SampleOrbit(player, dt);

        if (layer != SAIN.SAINComponent.Classes.Tactics.LayerHandoff.SainCombat || !SAINEnableClass.GetSAIN(player.ProfileId, out BotComponent bot) || bot == null)
        {
            return;
        }
        _combatSamples++;
        Vector3 eye = bot.Transform.EyePosition;
        Vector3 look = bot.Transform.LookDirection;
        if (look.sqrMagnitude < 0.01f || !Physics.Raycast(eye, look.normalized, WALL_DISTANCE, LayersMaskController.HighPolyWithTerrainMask))
        {
            return;
        }
        _stare++;
        string key = $"{bot.Decision.CurrentCombatDecision}/{ReasonKey(bot.Decision.EnemyDecisions?.LastReason)}";
        _stareByDecision.TryGetValue(key, out int count);
        _stareByDecision[key] = count + 1;
        var enemy = bot.GoalEnemy;
        if (enemy != null)
        {
            Vector3 toEnemy = enemy.EnemyPosition - eye;
            toEnemy.y = 0f;
            Vector3 flatLook = new Vector3(look.x, 0f, look.z);
            if (toEnemy.sqrMagnitude > 0.01f && flatLook.sqrMagnitude > 0.01f && Vector3.Angle(flatLook, toEnemy) < 30f)
            {
                _stareTowardEnemy++;
            }
        }
    }

    // ORBIT's public read-only telemetry (Orbit.Api.OrbitTelemetry in ORBIT.dll), found by reflection so SAIN has no
    // dependency on it: Ghost Mode sleep and the objective each bot walks to. ORBIT itself is never changed.
    private static bool _orbitLooked;
    private static System.Reflection.MethodInfo _isDormant;
    private static System.Reflection.MethodInfo _getObjective;
    private static System.Reflection.FieldInfo _objStatus, _objCategory, _objExtract, _objX, _objZ;

    private static void SampleOrbit(Player player, float dt)
    {
        if (!_orbitLooked)
        {
            _orbitLooked = true;
            try
            {
                var type = HarmonyLib.AccessTools.TypeByName("Orbit.Api.OrbitTelemetry");
                _isDormant = type?.GetMethod("IsBotDormant", new[] { typeof(string) });
                _getObjective = type?.GetMethod("GetBotObjective", new[] { typeof(string) });
                var obj = _getObjective?.ReturnType;
                _objStatus = obj?.GetField("Status");
                _objCategory = obj?.GetField("Category");
                _objExtract = obj?.GetField("ExtractReason");
                _objX = obj?.GetField("ObjectiveX");
                _objZ = obj?.GetField("ObjectiveZ");
                Logger.LogInfo($"[SimLab] ORBIT telemetry {(type != null ? "found" : "not installed")}");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[SimLab] ORBIT telemetry not readable: {ex.Message}");
                _isDormant = null;
                _getObjective = null;
            }
        }
        try
        {
            if (_isDormant != null && _isDormant.Invoke(null, new object[] { player.ProfileId }) is bool dormant && dormant)
            {
                _orbitGhost += dt;
            }
            object obj = _getObjective?.Invoke(null, new object[] { player.ProfileId });
            if (obj == null)
            {
                return;
            }
            string extract = _objExtract?.GetValue(obj) as string;
            string key = !string.IsNullOrEmpty(extract) ? "extract: " + extract : $"{_objCategory?.GetValue(obj)}/{_objStatus?.GetValue(obj)}";
            AddOrbit(key, dt);
            // objective outside the sim arena = ORBIT is walking this bot away from the fight we set up
            if (_info != null && _info.HasArena && _objX != null && _objZ != null)
            {
                float dx = (float)_objX.GetValue(obj) - _info.ArenaX;
                float dz = (float)_objZ.GetValue(obj) - _info.ArenaZ;
                if (dx * dx + dz * dz > _info.ArenaRadius * _info.ArenaRadius)
                {
                    AddOrbit(SimOrbitKeys.AwayFromArena, dt);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[SimLab] ORBIT telemetry stopped: {ex.Message}");
            _isDormant = null;
            _getObjective = null;
        }
    }

    private static void AddOrbit(string key, float dt)
    {
        _orbitObjective.TryGetValue(key, out float seconds);
        _orbitObjective[key] = seconds + dt;
    }

    private static string ReasonKey(string reason)
    {
        if (string.IsNullOrEmpty(reason))
        {
            return "-";
        }
        int cut = reason.IndexOfAny(new[] { ' ', '(' });
        return cut > 0 ? reason.Substring(0, cut) : reason;
    }

    public static void Fill(SimBeat beat)
    {
        beat.LayerSeconds = new Dictionary<string, float>(_layerSeconds);
        beat.CombatSamples = _combatSamples;
        beat.WallStareSamples = _stare;
        beat.WallStareTowardEnemy = _stareTowardEnemy;
        beat.WallStareByDecision = new Dictionary<string, int>(_stareByDecision);
        beat.SpawnZones = new Dictionary<string, int>(_spawnZones);
        beat.SpawnInZone = _spawnIn;
        beat.SpawnOffZone = _spawnOffCount;
        beat.SpawnOff = new List<SimSpawnSample>(_spawnOff);
        beat.OrbitGhostSeconds = _orbitGhost;
        beat.OrbitObjectiveSeconds = new Dictionary<string, float>(_orbitObjective);
    }

    public static void Clear()
    {
        _info = null;
        _zones.Clear();
    }
}
