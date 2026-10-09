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
}
