using SAIN.Preset.Shared.SimLab;

namespace SAINServerMod.SimLab;

// zzap fork: simulation lab settings, edited on the /sain/sim page and stored in <mod>/SimLab/config.json.
// Nothing here does anything unless the client says the loaded preset has Simulation Mode on (SimLabService.Armed).

public sealed class SimLabConfig
{
    /// <summary>Rotation paused: the client does not auto-start the next raid (sim spawns still apply to raids you start).</summary>
    public bool Paused = false;

    /// <summary>After a map's time is up, move on to the next map of the rotation.</summary>
    public bool AutoRotate = true;

    /// <summary>Player is invincible and bots ignore him (pure bot-vs-bot observation).</summary>
    public bool Spectator = true;

    /// <summary>Move the player next to side A's zone at raid start (bots far from the player run with reduced AI).</summary>
    public bool TeleportToArena = true;

    // ORBIT during sim raids (user 2026-10-09: "ORBIT settings / zones can spoil the fight we want"). Applied by overriding
    // what ORBIT's server mod hands the game at raid start - ORBIT's own files and code are never changed.
    /// <summary>Ghost Mode off: every arena bot is a real bot (no off-screen simulated fights, no sleeping bodies).</summary>
    public bool OrbitGhostOff = true;
    /// <summary>ORBIT bots don't extract (loot / objectives done / raid clock / emergency) - squads stay in the arena.</summary>
    public bool OrbitNoExtract = true;
    /// <summary>ORBIT PMC objectives (loot runs, kill hunts elsewhere, quests) off for PMCs.</summary>
    public bool OrbitNoObjectives = true;
    /// <summary>This map's ORBIT hotspots replaced by one attractor over the arena (both sides' zones).</summary>
    public bool OrbitArenaZone = true;

    /// <summary>ABPS leaves sim raids alone (user 2026-10-09: "reserve squads called, no bot came"). ABPS's game plugin otherwise takes
    /// over the sim's PMC squads: ignores their zones, needs spawn points far from every PMC incl. the spectator, and skips silently.</summary>
    public bool AbpsOffInSim = true;

    /// <summary>Remaining sim time at the top right of the game screen (user 2026-10-09: on by default, switchable on the page).</summary>
    public bool ShowTimer = true;

    /// <summary>Bumped when new per-map defaults should be filled into an older saved config.</summary>
    public int DefaultsVersion;

    /// <summary>Remove every other spawn of the raid (vanilla/ABPS PMC waves, scav waves, bosses) - only the arena squads.</summary>
    public bool RemoveOtherSpawns = true;

    public int MaxAliveBots = 12;
    public int SquadSizeMin = 3;
    public int SquadSizeMax = 4;

    /// <summary>Minimum seconds between two refill squads.</summary>
    public int RespawnSeconds = 45;

    /// <summary>Seconds to wait in the main menu between two sim maps (RAM recovers in the menu - user 2026-10-09).</summary>
    public int GapSeconds = 120;

    /// <summary>At the start of that wait, the client also unloads unused assets and runs the GC.</summary>
    public bool CleanMemoryBetweenMaps = true;

    /// <summary>Minutes for a map that is not in the rotation list (a raid you started yourself).</summary>
    public float DefaultMinutes = 20f;

    /// <summary>Location ids containing any of these are never touched (custom maps like the Icebreaker port).</summary>
    public List<string> ExcludedMapKeywords = ["icebreaker", "hideout", "develop", "privatearea", "suburbs", "terminal", "town"];

    public List<SimMapEntry> Rotation = [];

    /// <summary>Per location id (lower case): where the two sides spawn and what the map's test looks at.</summary>
    public Dictionary<string, SimScenario> Scenarios = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SimMapEntry
{
    public string Map = string.Empty;
    public bool Enabled = true;
    public float Minutes = 20f;

    // Per-map spawn pace (user 2026-10-09: "factory should refill faster to see more fights"). 0 = use the common value.
    public int MaxAliveBots;
    public int RespawnSeconds;
    public int SquadSizeMin;
    public int SquadSizeMax;
}

public sealed class SimScenario
{
    public string Name = string.Empty;

    /// <summary>What this map's test is for (shown on the page and in the analysis).</summary>
    public string Focus = string.Empty;

    /// <summary>English name / focus for the web page in English (empty = the Korean text is shown).</summary>
    public string NameEn = string.Empty;
    public string FocusEn = string.Empty;

    /// <summary>Bot zones (comma separated) for side A / side B. Empty = anywhere on the map.</summary>
    public string ZonesA = string.Empty;
    public string ZonesB = string.Empty;

    public string SideA = "pmcUSEC";
    public string SideB = "pmcBEAR";

    /// <summary>Counter prefixes the analysis highlights for this map (comma separated, e.g. "door.,diamond.").</summary>
    public string Watch = string.Empty;
}

/// <summary>Rotation position, kept across server restarts (&lt;mod&gt;/SimLab/state.json).</summary>
public sealed class SimLabState
{
    public string RunId = string.Empty;
    public int Step;
}

public sealed class SimSample
{
    public float T;
    public int Alive;
    public int Deaths;
    public float Fps;
    public long Mono;
    public int Errors;
}

public sealed class SimMapRecord
{
    public int Index;
    public string Map = string.Empty;
    public string MapName = string.Empty;
    public string Scenario = string.Empty;
    public string Focus = string.Empty;
    public string Watch = string.Empty;

    /// <summary>running / done / interrupted / ended (raid ended by something else than the sim timer).</summary>
    public string Status = "running";
    public DateTime Started;
    public DateTime Updated;
    public DateTime? Ended;
    public float PlannedMinutes;
    public SimRaidInfo? Raid;
    public SimBeat? Last;
    public List<SimSample> Samples = [];
}

public sealed class SimRunRecord
{
    public string RunId = string.Empty;
    public DateTime Started;
    public DateTime Updated;
    public string Preset = string.Empty;
    public string Build = string.Empty;
    public List<SimMapRecord> Maps = [];
}
