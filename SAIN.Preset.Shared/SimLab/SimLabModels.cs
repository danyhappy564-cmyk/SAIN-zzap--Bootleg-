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
    /// <summary>Pick the map's original (SPT's own) version through MapVariants before each sim raid.</summary>
    public bool OriginalMaps = true;
    /// <summary>Start each sim raid at the daytime one of the two raid times (user 2026-10-09: "sometimes a night raid gets picked").</summary>
    public bool DayOnly = true;
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
    /// <summary>Bots walking out of the arena (not fighting) are put back into their side's zone.</summary>
    public bool Leash;
    /// <summary>What the server changed in ORBIT's config / zones for this raid (empty = ORBIT not installed or nothing).</summary>
    public string OrbitOverrides;
    /// <summary>The server told ABPS to leave this raid alone (squads spawn in their own zones through the game's boss spawner).</summary>
    public bool AbpsOff;
    /// <summary>The server made every other squad an enemy (own faction included) for this raid.</summary>
    public bool AllSquadsEnemies;
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
    /// <summary>Samples (2 s, per bot) of a hostile bot within 4 m that this bot doesn't know about (user screenshot 2026-10-09:
    /// two enemies shoulder to shoulder, then a scramble), and a few examples.</summary>
    /// <summary>In-game clock at the raid start (hour, 0-24) and whether it is night (before 6 or from 21, or Factory night).</summary>
    /// <summary>Managed heap in the main menu right before this raid was started, after a full GC (MB, -1 = unknown). Rising map
    /// after map = something keeps old raids alive (user 2026-10-10: "looks like a RAM leak").</summary>
    public long MenuMonoMB = -1;
    public int LeashTeleports;
    public float RaidHour = -1f;
    public bool Night;

    public int CloseUnawareSamples;
    public List<SimCloseSample> CloseUnaware = new();
    /// <summary>Automatic oddity detector (user 2026-10-09: "can't you measure it yourself?"): episodes per kind and examples.</summary>
    public Dictionary<string, int> OddityCounts = new();
    public List<SimOddity> Oddities = new();

    public float OrbitGhostSeconds;
    public Dictionary<string, float> OrbitObjectiveSeconds = new();
}

/// <summary>One moment the oddity detector flagged, with what the bot was doing (to match with what the user saw).</summary>
public class SimOddity
{
    public string Kind;
    public string Name;
    public string Role;
    public string Personality;
    public string Decision;
    public string Reason;
    public string Layer;
    public float EnemyDistance;
    public bool EnemyVisible;
    public float EnemyAngle;
    public float Speed;
    public int Bullets;
    public float Seconds;
    public float X;
    public float Y;
    public float Z;
    public float RaidTime;
}

/// <summary>A bot with a hostile bot within 4 m that it doesn't know about.</summary>
public class SimCloseSample
{
    public string Role;
    public string OtherRole;
    public string Layer;
    public float Distance;
    public bool OtherKnows;
    public float X;
    public float Y;
    public float Z;
    public float RaidTime;
}

public static class SimOrbitKeys
{
    /// <summary>OrbitObjectiveSeconds key: bot seconds whose ORBIT objective lay outside the sim arena circle.</summary>
    public const string AwayFromArena = "_awayFromArena";
}
