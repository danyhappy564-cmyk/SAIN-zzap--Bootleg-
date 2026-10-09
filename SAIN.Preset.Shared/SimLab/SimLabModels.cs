using System.Collections.Generic;

namespace SAIN.Preset.Shared.SimLab;

// zzap fork: messages between the SAIN client (BepInEx) and the SAIN server mod for the simulation lab.
// Plain public fields only (no enums): the client writes them with Newtonsoft, the server reads them with
// System.Text.Json (IncludeFields) - both sides see the same PascalCase names.

/// <summary>Client -> server when the client starts, the preset changes or the main menu is ready.</summary>
public class SimHello
{
    /// <summary>True only when the loaded preset has Simulation Mode on. False disarms the server (no sim spawns).</summary>
    public bool Armed;
    public string Preset;
    public string Build;
    /// <summary>"start" (game start), "preset" (preset changed), "menu" (main menu ready).</summary>
    public string Reason;
}

/// <summary>Server -> client: what the rotation wants next.</summary>
public class SimPlan
{
    public bool Armed;
    public bool Paused;
    public bool AutoRotate;
    public string RunId;
    public int Step;
    public int StepCount;
    public string NextMap;
    public string NextMapName;
    public float Minutes;
    public string ScenarioName;
    public string Message;
    /// <summary>Seconds to wait in the main menu after a sim raid before the next one (RAM comes back there).</summary>
    public float GapSeconds;
    /// <summary>At the start of that wait, also unload unused assets + GC.</summary>
    public bool CleanMemory;
    /// <summary>On-screen sim timer (raid time left / countdown to the next map).</summary>
    public bool ShowTimer = true;
}

/// <summary>Server -> client after the raid's location was generated: how this raid was set up.</summary>
public class SimRaidInfo
{
    public bool Applied;
    public string RunId;
    public string Map;
    public string MapName;
    public string ScenarioName;
    public string Focus;
    public float Minutes;
    public bool Spectator;
    /// <summary>Show the remaining sim time at the top right of the screen.</summary>
    public bool ShowTimer = true;
    /// <summary>Server removed the raid's other spawns: the client also stops the vanilla wave / non-wave scenarios.</summary>
    public bool RemoveOtherSpawns;
    public bool HasSpectatorPos;
    public float SpectatorX;
    public float SpectatorY;
    public float SpectatorZ;
    public int MaxAliveBots;
    public int SquadSizeMin;
    public int SquadSizeMax;
    public float RespawnSeconds;
    public string ZonesA;
    public string ZonesB;
    public string SideA;
    public string SideB;
    /// <summary>Reserve waves sit at this Time and above - they never fire by themselves, the client activates them.</summary>
    public float ReserveTimeBase;
    public int ReserveWaves;
    public int RemovedBossWaves;
    public int RemovedWaves;
    public string Note;

    /// <summary>Arena center (between side A's and side B's zones) and radius covering both - ORBIT's sim attractor.</summary>
    public bool HasArena;
    public float ArenaX;
    public float ArenaZ;
    public float ArenaRadius;
    /// <summary>What the server changed in ORBIT's config / zones for this raid (empty = ORBIT not installed or nothing).</summary>
    public string OrbitOverrides;
}

/// <summary>A sim bot that spawned outside the zones its side was given.</summary>
public class SimSpawnSample
{
    public string Role;
    public string Side;
    public string Zone;
    public string Wanted;
    public float X;
    public float Y;
    public float Z;
    /// <summary>Metres to the nearest spawn point of the wanted zones (-1 = unknown).</summary>
    public float Distance;
    public float RaidTime;
}

public class SimRow
{
    public int Kills;
    public int Deaths;
    public int DiedSwitching;
}

public class SimError
{
    public string Key;
    public string Level;
    public string Source;
    public int Count;
    public float FirstRaidTime;
    public string Sample;
}

/// <summary>Client -> server every minute and at the end of each sim raid (also kept line by line in a local file).</summary>
public class SimBeat
{
    public string RunId;
    public string Map;
    /// <summary>"start", "beat", "end", "recovered".</summary>
    public string Kind;
    public string ClientTime;
    public string Build;
    public string Preset;
    public string JournalFile;
    public float RaidSeconds;
    public float PlannedSeconds;
    public string EndReason;
    public int AliveBots;
    public int SimWavesActivated;
    public int SimWavesLeft;
    public int BotsSeen;
    public int BotDeaths;
    public int BotKillsByBots;
    public int OtherKills;
    public int TeamKills;
    public int PlayerHits;
    public float Fps;
    public float FpsMin;
    public long MonoUsedMB;
    public int Stalls;
    public Dictionary<string, int> SpawnedRoles = new();
    public Dictionary<string, int> Counters = new();
    public Dictionary<string, SimRow> ByPersonality = new();
    public Dictionary<string, SimRow> ByReason = new();
    public Dictionary<string, SimRow> ByCombat = new();
    public List<SimError> Errors = new();
    public List<string> Notes = new();

    /// <summary>Seconds bots spent in each brain layer (sampled every 2 s per bot): ORBIT vs SAIN vs vanilla.</summary>
    public Dictionary<string, float> LayerSeconds = new();

    /// <summary>Samples of bots in SAIN's combat layer, and how many of them stood with a wall within 1 m in front of the eyes.</summary>
    public int CombatSamples;
    public int WallStareSamples;
    /// <summary>Of the wall-staring samples: the goal enemy was in that direction (looking at the enemy through the wall).</summary>
    public int WallStareTowardEnemy;
    public Dictionary<string, int> WallStareByDecision = new();

    /// <summary>Where sim bots really spawned: zone counts per side ("A:ZoneDormitory"), in/out of the wanted zones, examples.</summary>
    public Dictionary<string, int> SpawnZones = new();
    public int SpawnInZone;
    public int SpawnOffZone;
    public List<SimSpawnSample> SpawnOff = new();

    /// <summary>ORBIT telemetry (read-only API): bot seconds asleep in Ghost Mode, and bot seconds per ORBIT objective
    /// ("Kills/Active", "Loot/Active", "extract: loot threshold reached"...). Empty without ORBIT.</summary>
    public float OrbitGhostSeconds;
    public Dictionary<string, float> OrbitObjectiveSeconds = new();
}

public static class SimOrbitKeys
{
    /// <summary>OrbitObjectiveSeconds key: bot seconds whose ORBIT objective lay outside the sim arena circle.</summary>
    public const string AwayFromArena = "_awayFromArena";
}
