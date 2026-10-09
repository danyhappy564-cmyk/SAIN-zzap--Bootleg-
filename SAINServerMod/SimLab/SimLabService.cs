using System.Reflection;
using SAIN.Preset.Shared.SimLab;
using SAINServerMod.Utils;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;

namespace SAINServerMod.SimLab;

/// <summary>
/// zzap fork: simulation lab (user 2026-10-09). Holds the rotation settings, sets up each sim raid's spawns
/// (<see cref="SimLabRaidPatch"/>), and stores what the client reports (every minute + at the end) so the /sain/sim/analysis
/// page can show it - also after Alt+F4, because every report is written to disk as it arrives.
/// Armed only while the client says its loaded preset has Simulation Mode on; never saved, so a server restart or any
/// other preset leaves raids untouched.
/// </summary>
[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PostLoad + 6)]
public sealed class SimLabService(ModHelper modHelper, LocationTable locationTable, ISptLogger<SimLabService> logger) : IOnLoad
{
    /// <summary>Reserve waves start at this Time (seconds) - far beyond any raid, so only the client activates them.</summary>
    public const float RESERVE_TIME_BASE = 90000f;

    private const int RESERVE_WAVES_PER_SIDE = 60;
    private const int MAX_SAMPLES = 400;

    public static SimLabService? Instance { get; private set; }

    private readonly object _lock = new();
    private string _dir = string.Empty;
    private SimLabRaidPatch? _patch;

    public SimLabConfig Config { get; private set; } = new();
    public SimLabState State { get; private set; } = new();

    public bool Armed { get; private set; }
    public string ArmedPreset { get; private set; } = string.Empty;
    public DateTime LastHello { get; private set; }
    public SimRaidInfo? LastRaid { get; private set; }
    public SimBeat? LastBeat { get; private set; }

    private string ConfigPath => Path.Combine(_dir, "config.json");
    private string StatePath => Path.Combine(_dir, "state.json");
    public string RunsDir => Path.Combine(_dir, "runs");

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        Instance = this;
        _dir = Path.Combine(modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly()), "SimLab");
        try
        {
            Directory.CreateDirectory(RunsDir);
            Config = ReadJson<SimLabConfig>(ConfigPath) ?? new SimLabConfig();
            State = ReadJson<SimLabState>(StatePath) ?? new SimLabState();
            FillDefaults(Config);
            SaveConfig();
        }
        catch (Exception ex)
        {
            logger.Error($"[SAIN SimLab] could not load {_dir}: {ex.Message}");
        }
        try
        {
            _patch = new SimLabRaidPatch();
            _patch.Enable();
        }
        catch (Exception ex)
        {
            logger.Error($"[SAIN SimLab] raid patch failed - sim spawns will not apply: {ex}");
        }
        return Task.CompletedTask;
    }

    /// <summary>Adds the built-in scenarios / rotation entries that the saved config does not have yet (new maps, first run).</summary>
    private static void FillDefaults(SimLabConfig config)
    {
        config.Scenarios ??= new(StringComparer.OrdinalIgnoreCase);
        var scenarios = new Dictionary<string, SimScenario>(config.Scenarios, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in SimLabDefaults.Scenarios())
        {
            if (!scenarios.TryAdd(kv.Key, kv.Value) && scenarios[kv.Key] is { } saved && string.IsNullOrEmpty(saved.NameEn))
            {
                // Config saved before the English texts existed: fill them in while the Korean text is still the default.
                if (saved.Name == kv.Value.Name)
                {
                    saved.NameEn = kv.Value.NameEn;
                }
                if (saved.Focus == kv.Value.Focus)
                {
                    saved.FocusEn = kv.Value.FocusEn;
                }
            }
        }
        config.Scenarios = scenarios;
        config.Rotation ??= [];
        if (config.Rotation.Count == 0)
        {
            config.Rotation = SimLabDefaults.Rotation();
        }
        else
        {
            foreach (var entry in SimLabDefaults.Rotation())
            {
                if (!config.Rotation.Any(x => string.Equals(x.Map, entry.Map, StringComparison.OrdinalIgnoreCase)))
                {
                    entry.Enabled = false;
                    config.Rotation.Add(entry);
                }
            }
        }
        if (config.DefaultsVersion < 2)
        {
            // Older config without per-map pace: give each map its default pace (only where nothing was set).
            foreach (var entry in config.Rotation)
            {
                if (entry.MaxAliveBots == 0 && entry.RespawnSeconds == 0 && entry.SquadSizeMin == 0 && entry.SquadSizeMax == 0)
                {
                    SimLabDefaults.ApplyPace(entry);
                }
            }
            config.DefaultsVersion = 2;
        }
        config.ExcludedMapKeywords ??= [];
        if (!config.ExcludedMapKeywords.Contains("icebreaker", StringComparer.OrdinalIgnoreCase))
        {
            config.ExcludedMapKeywords.Add("icebreaker");
        }
        config.MaxAliveBots = Math.Clamp(config.MaxAliveBots, 2, 40);
        config.SquadSizeMin = Math.Clamp(config.SquadSizeMin, 1, 6);
        config.SquadSizeMax = Math.Clamp(config.SquadSizeMax, config.SquadSizeMin, 6);
        config.RespawnSeconds = Math.Clamp(config.RespawnSeconds, 5, 600);
        config.GapSeconds = Math.Clamp(config.GapSeconds, 0, 1800);
    }

    // ---------------------------------------------------------------- config / state

    public void SaveConfig()
    {
        lock (_lock)
        {
            FillDefaults(Config);
            WriteJson(ConfigPath, Config);
        }
    }

    public void ResetDefaults()
    {
        lock (_lock)
        {
            Config = new SimLabConfig();
            FillDefaults(Config);
            WriteJson(ConfigPath, Config);
        }
    }

    private void SaveState()
    {
        WriteJson(StatePath, State);
    }

    public bool IsExcluded(string map)
    {
        return string.IsNullOrEmpty(map) || Config.ExcludedMapKeywords.Any(k => !string.IsNullOrEmpty(k) && map.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    public List<SimMapEntry> EnabledRotation()
    {
        return Config.Rotation.Where(x => x.Enabled && !IsExcluded(x.Map) && locationTable.GetLocation(x.Map) != null).ToList();
    }

    /// <summary>Location ids the server knows (for the page's map list), custom/excluded maps left out.</summary>
    public List<string> KnownMaps()
    {
        var list = new List<string>();
        foreach (string id in SimLabDefaults.MapNames.Keys)
        {
            if (!IsExcluded(id) && locationTable.GetLocation(id) != null)
            {
                list.Add(id);
            }
        }
        return list;
    }

    /// <summary>Bot zones of a map with how many boss/PMC-capable spawn points each has (for the zone picker).</summary>
    public List<(string Zone, int Points)> ZonesOf(string map)
    {
        var result = new List<(string, int)>();
        try
        {
            var points = locationTable.GetLocation(map)?.Base?.SpawnPointParams;
            if (points == null)
            {
                return result;
            }
            foreach (var group in points.Where(p => !string.IsNullOrEmpty(p.BotZoneName)).GroupBy(p => p.BotZoneName!))
            {
                int usable = group.Count(p => p.Categories != null && p.Categories.Any(c => c is "Boss" or "Bot"));
                result.Add((group.Key, usable));
            }
        }
        catch (Exception ex)
        {
            logger.Warning($"[SAIN SimLab] zones of {map}: {ex.Message}");
        }
        return result.OrderBy(x => x.Item1).ToList();
    }

    public SimScenario ScenarioFor(string map)
    {
        if (Config.Scenarios.TryGetValue(map, out SimScenario? scenario) && scenario != null)
        {
            return scenario;
        }
        return new SimScenario { Name = $"{SimLabDefaults.NameOf(map)} 무작위", Focus = "맵 전체 무작위 스폰 (시나리오 없음)" };
    }

    public float MinutesFor(string map)
    {
        var entry = Config.Rotation.FirstOrDefault(x => string.Equals(x.Map, map, StringComparison.OrdinalIgnoreCase));
        return entry?.Minutes > 0f ? entry.Minutes : Math.Max(1f, Config.DefaultMinutes);
    }

    // ---------------------------------------------------------------- rotation

    /// <summary>
    /// Only the web page's start button starts the rotation (user 2026-10-09: "the countdown ran as soon as the game started").
    /// Memory only: a server restart, the game starting, or the stop button turns it off again.
    /// </summary>
    public bool Running { get; private set; }

    public void SetRunning(bool running)
    {
        lock (_lock)
        {
            if (Running != running)
            {
                logger.Info($"[SAIN SimLab] rotation {(running ? "started" : "stopped")} from the web page");
            }
            Running = running;
            if (running && string.IsNullOrEmpty(State.RunId))
            {
                NewRun();
            }
        }
    }

    public void SetStep(int step)
    {
        lock (_lock)
        {
            int count = Math.Max(1, EnabledRotation().Count);
            State.Step = ((step % count) + count) % count;
            SaveState();
        }
    }

    /// <summary>Starts a new run record (the analysis groups maps by run).</summary>
    public string NewRun()
    {
        lock (_lock)
        {
            State.RunId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            State.Step = 0;
            SaveState();
            var run = new SimRunRecord { RunId = State.RunId, Started = DateTime.Now, Updated = DateTime.Now, Preset = ArmedPreset };
            WriteJson(RunPath(run.RunId), run);
            return State.RunId;
        }
    }

    public SimPlan Hello(SimHello hello)
    {
        lock (_lock)
        {
            bool wasArmed = Armed;
            Armed = hello?.Armed == true;
            ArmedPreset = hello?.Preset ?? string.Empty;
            LastHello = DateTime.Now;
            if (Armed != wasArmed)
            {
                logger.Info($"[SAIN SimLab] {(Armed ? "armed" : "disarmed")} by client ({hello?.Reason}, preset '{ArmedPreset}')");
            }
            if (hello?.Reason == "start" && Running)
            {
                Running = false; // game (re)started: wait for the start button again
                logger.Info("[SAIN SimLab] game started - rotation waits for the start button on /sain/sim");
            }
            if (Armed && hello?.Reason == "menu")
            {
                MarkStaleRunning("main menu reached without an end report");
            }
            return BuildPlan();
        }
    }

    private SimPlan BuildPlan()
    {
        if (Armed && string.IsNullOrEmpty(State.RunId))
        {
            NewRun(); // lock is re-entrant
        }
        var rotation = EnabledRotation();
        var plan = new SimPlan
        {
            Armed = Armed,
            Paused = !Running,
            AutoRotate = Config.AutoRotate,
            RunId = State.RunId,
            GapSeconds = Config.GapSeconds,
            CleanMemory = Config.CleanMemoryBetweenMaps,
            ShowTimer = Config.ShowTimer,
            StepCount = rotation.Count,
        };
        if (rotation.Count == 0)
        {
            plan.Message = "순회에 켜진 맵이 없습니다 (/sain/sim에서 맵을 켜 주세요).";
            return plan;
        }
        int step = ((State.Step % rotation.Count) + rotation.Count) % rotation.Count;
        var entry = rotation[step];
        plan.Step = step;
        plan.NextMap = entry.Map;
        plan.NextMapName = SimLabDefaults.NameOf(entry.Map);
        plan.Minutes = entry.Minutes;
        plan.ScenarioName = ScenarioFor(entry.Map).Name;
        return plan;
    }

    public SimPlan Plan()
    {
        lock (_lock)
        {
            return BuildPlan();
        }
    }

    // ---------------------------------------------------------------- raid setup (called from the patch)

    public void ApplyToRaid(string name, LocationBase location)
    {
        if (!Armed || location == null)
        {
            return;
        }
        string map = (name ?? location.Id ?? string.Empty).ToLowerInvariant();
        if (IsExcluded(map))
        {
            logger.Info($"[SAIN SimLab] {map} is excluded - raid left as is");
            return;
        }
        lock (_lock)
        {
            try
            {
                LastRaid = Apply(map, location);
                logger.Success(
                    $"[SAIN SimLab] {map}: '{LastRaid.ScenarioName}' A={LastRaid.SideA}@[{LastRaid.ZonesA}] B={LastRaid.SideB}@[{LastRaid.ZonesB}] "
                        + $"cap {LastRaid.MaxAliveBots}, {LastRaid.Minutes:0} min, removed {LastRaid.RemovedBossWaves} boss/PMC + {LastRaid.RemovedWaves} scav waves"
                );
            }
            catch (Exception ex)
            {
                logger.Error($"[SAIN SimLab] could not set up {map}: {ex}");
                LastRaid = new SimRaidInfo { Applied = false, Map = map, Note = "서버 설정 실패: " + ex.Message };
            }
        }
    }

    private SimRaidInfo Apply(string map, LocationBase location)
    {
        var scenario = ScenarioFor(map);
        float minutes = MinutesFor(map);
        var entry = Config.Rotation.FirstOrDefault(x => string.Equals(x.Map, map, StringComparison.OrdinalIgnoreCase));
        int cap = entry?.MaxAliveBots > 0 ? entry.MaxAliveBots : Config.MaxAliveBots;
        int respawn = entry?.RespawnSeconds > 0 ? entry.RespawnSeconds : Config.RespawnSeconds;
        int squadMin = entry?.SquadSizeMin > 0 ? entry.SquadSizeMin : Config.SquadSizeMin;
        int squadMax = Math.Max(squadMin, entry?.SquadSizeMax > 0 ? entry.SquadSizeMax : Config.SquadSizeMax);
        var info = new SimRaidInfo
        {
            Applied = true,
            RunId = State.RunId,
            Map = map,
            MapName = SimLabDefaults.NameOf(map),
            ScenarioName = scenario.Name,
            Focus = scenario.Focus,
            Minutes = minutes,
            Spectator = Config.Spectator,
            ShowTimer = Config.ShowTimer,
            RemoveOtherSpawns = Config.RemoveOtherSpawns,
            MaxAliveBots = cap,
            SquadSizeMin = squadMin,
            SquadSizeMax = squadMax,
            RespawnSeconds = respawn,
            ZonesA = scenario.ZonesA ?? string.Empty,
            ZonesB = scenario.ZonesB ?? string.Empty,
            SideA = string.IsNullOrEmpty(scenario.SideA) ? "pmcUSEC" : scenario.SideA,
            SideB = string.IsNullOrEmpty(scenario.SideB) ? "pmcBEAR" : scenario.SideB,
            ReserveTimeBase = RESERVE_TIME_BASE,
        };

        var zones = new HashSet<string>(
            (location.SpawnPointParams ?? []).Select(p => p.BotZoneName ?? string.Empty),
            StringComparer.OrdinalIgnoreCase
        );
        info.ZonesA = KeepKnownZones(info.ZonesA, zones, out string droppedA);
        info.ZonesB = KeepKnownZones(info.ZonesB, zones, out string droppedB);
        if (droppedA.Length + droppedB.Length > 0)
        {
            info.Note = $"맵에 없는 구역 무시: {droppedA} {droppedB}".Trim();
        }

        location.BossLocationSpawn ??= [];
        location.Waves ??= [];
        if (Config.RemoveOtherSpawns)
        {
            info.RemovedBossWaves = location.BossLocationSpawn.Count;
            info.RemovedWaves = location.Waves.Count;
            location.BossLocationSpawn.Clear();
            location.Waves.Clear();
            // Vanilla non-wave scav refill (and ABPS's version of it reads the same fields): off.
            location.NewSpawn = false;
            location.OfflineNewSpawn = false;
            location.BotStop = 0;
            location.BotSpawnCountStep = 0;
            location.MinMaxBots = [];
        }

        int escortMin = Math.Max(0, squadMin - 1);
        int escortMax = Math.Max(escortMin, squadMax - 1);
        string escorts = string.Join(",", Enumerable.Range(escortMin, escortMax - escortMin + 1));
        for (int i = 0; i < RESERVE_WAVES_PER_SIDE * 2; i++)
        {
            bool sideA = i % 2 == 0;
            string role = sideA ? info.SideA : info.SideB;
            location.BossLocationSpawn.Add(
                new BossLocationSpawn
                {
                    BossChance = 100,
                    BossDifficulty = "normal",
                    BossEscortAmount = escorts,
                    BossEscortDifficulty = "normal",
                    BossEscortType = role,
                    BossName = role,
                    IsBossPlayer = false,
                    BossZone = sideA ? info.ZonesA : info.ZonesB,
                    IsRandomTimeSpawn = false,
                    ShowOnTarkovMap = false,
                    ShowOnTarkovMapPvE = false,
                    Time = RESERVE_TIME_BASE + i,
                    TriggerId = string.Empty,
                    TriggerName = string.Empty,
                    Delay = 0,
                    DependKarma = false,
                    DependKarmaPVE = false,
                    ForceSpawn = true,
                    IgnoreMaxBots = true,
                    Supports = null!,
                    SpawnMode = ["regular", "pve"],
                }
            );
        }
        info.ReserveWaves = RESERVE_WAVES_PER_SIDE * 2;

        cap = Math.Max(cap, 2);
        location.BotMax = cap + squadMax;
        location.BotMaxPvE = cap + squadMax;
        location.MaxBotPerZone = Math.Max(location.MaxBotPerZone ?? 0, cap);
        // Raid timer: the sim ends the raid itself after `minutes`; leave room so the game's own timer never runs out first.
        location.EscapeTimeLimit = minutes + 15;
        location.EscapeTimeLimitPVE = (int)Math.Ceiling(minutes + 15);

        if (Config.Spectator && Config.TeleportToArena)
        {
            PickSpectatorPoint(location, info);
        }
        return info;
    }

    private static string KeepKnownZones(string zones, HashSet<string> known, out string dropped)
    {
        var keep = new List<string>();
        var drop = new List<string>();
        foreach (string z in (zones ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            (known.Contains(z) ? keep : drop).Add(z);
        }
        dropped = string.Join(",", drop);
        return string.Join(",", keep);
    }

    /// <summary>Spectator stands on a spawn point of side A's zone (a point the game itself uses, so it is on the ground).</summary>
    private static void PickSpectatorPoint(LocationBase location, SimRaidInfo info)
    {
        var zonesA = new HashSet<string>(info.ZonesA.Split(',', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
        var points = (location.SpawnPointParams ?? []).Where(p => p.Position.HasValue).ToList();
        var inZone = zonesA.Count == 0 ? points : points.Where(p => zonesA.Contains(p.BotZoneName ?? string.Empty)).ToList();
        if (inZone.Count == 0)
        {
            return;
        }
        var pick = inZone.FirstOrDefault(p => p.Categories != null && p.Categories.Contains("Player"))
            ?? inZone.FirstOrDefault(p => p.Categories != null && p.Categories.Contains("Bot"))
            ?? inZone[0];
        var pos = pick.Position!.Value;
        info.HasSpectatorPos = true;
        info.SpectatorX = pos.X;
        info.SpectatorY = pos.Y + 0.3f;
        info.SpectatorZ = pos.Z;
    }

    // ---------------------------------------------------------------- reports

    public string RunPath(string runId)
    {
        return Path.Combine(RunsDir, JsonFileStoreUtil.SanitizeFileName(runId) + ".json");
    }

    public SimRunRecord? LoadRun(string runId)
    {
        lock (_lock)
        {
            return ReadJson<SimRunRecord>(RunPath(runId));
        }
    }

    public List<SimRunRecord> ListRuns()
    {
        lock (_lock)
        {
            var list = new List<SimRunRecord>();
            if (!Directory.Exists(RunsDir))
            {
                return list;
            }
            foreach (string file in Directory.GetFiles(RunsDir, "*.json"))
            {
                var run = ReadJson<SimRunRecord>(file);
                if (run != null)
                {
                    list.Add(run);
                }
            }
            return list.OrderByDescending(x => x.Started).ToList();
        }
    }

    public bool DeleteRun(string runId)
    {
        lock (_lock)
        {
            string path = RunPath(runId);
            if (!File.Exists(path))
            {
                return false;
            }
            File.Delete(path);
            return true;
        }
    }

    /// <summary>
    /// The game's SAIN-zzap data folder on this PC (BepInEx/config/SAIN-zzap), found from the server's own folder
    /// (&lt;game&gt;/SPT_Runtime is the server, &lt;game&gt;/BepInEx the client). Null when it isn't there.
    /// </summary>
    public string? ClientDataDir()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            try
            {
                var dir = new DirectoryInfo(start);
                for (int i = 0; i < 4 && dir != null; i++, dir = dir.Parent)
                {
                    string candidate = Path.Combine(dir.FullName, "BepInEx", "config", "SAIN-zzap");
                    if (Directory.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            catch
            {
            }
        }
        return null;
    }

    /// <summary>
    /// Bulk delete (user 2026-10-09: "old data piles up"). Server run records + summaries; optionally the PC's sim files and raid
    /// journals. The run being played right now and files the game still has open are kept. Returns (deleted, kept/failed).
    /// </summary>
    public (int Deleted, int Kept) DeleteAll(bool serverRuns, bool pcSimFiles, bool journals)
    {
        int deleted = 0;
        int kept = 0;
        lock (_lock)
        {
            if (serverRuns && Directory.Exists(RunsDir))
            {
                foreach (string file in Directory.GetFiles(RunsDir))
                {
                    if (Running && !string.IsNullOrEmpty(State.RunId) && Path.GetFileName(file).StartsWith(State.RunId, StringComparison.Ordinal))
                    {
                        kept++;
                        continue;
                    }
                    TryDelete(file, ref deleted, ref kept);
                }
                if (!Running)
                {
                    State.RunId = string.Empty;
                    State.Step = 0;
                    SaveState();
                }
            }
            string? client = ClientDataDir();
            if (client != null)
            {
                if (pcSimFiles && Directory.Exists(Path.Combine(client, "SimLab")))
                {
                    foreach (string file in Directory.GetFiles(Path.Combine(client, "SimLab"), "*.jsonl"))
                    {
                        TryDelete(file, ref deleted, ref kept);
                    }
                }
                if (journals && Directory.Exists(Path.Combine(client, "Journal")))
                {
                    foreach (string file in Directory.GetFiles(Path.Combine(client, "Journal")))
                    {
                        TryDelete(file, ref deleted, ref kept);
                    }
                }
            }
        }
        logger.Info($"[SAIN SimLab] bulk delete: {deleted} files deleted, {kept} kept (in use / current run)");
        return (deleted, kept);
    }

    private static void TryDelete(string file, ref int deleted, ref int kept)
    {
        try
        {
            File.Delete(file);
            deleted++;
        }
        catch
        {
            kept++; // still open by the game (current journal) or read-only
        }
    }

    public void Beat(SimBeat beat)
    {
        if (beat == null)
        {
            return;
        }
        lock (_lock)
        {
            LastBeat = beat;
            string runId = string.IsNullOrEmpty(beat.RunId) ? State.RunId : beat.RunId;
            if (string.IsNullOrEmpty(runId))
            {
                return;
            }
            string path = RunPath(runId);
            var run = ReadJson<SimRunRecord>(path) ?? new SimRunRecord { RunId = runId, Started = DateTime.Now };
            run.Updated = DateTime.Now;
            run.Preset = beat.Preset ?? run.Preset;
            run.Build = beat.Build ?? run.Build;

            var record = run.Maps.LastOrDefault();
            bool sameMap = record != null && string.Equals(record.Map, beat.Map, StringComparison.OrdinalIgnoreCase);
            // "recovered" = the client found its own sim file without an end line: it belongs to the last record of that map.
            bool needNew = beat.Kind == "recovered" ? !sameMap : beat.Kind == "start" || !sameMap || record!.Status != "running";
            if (needNew)
            {
                if (record != null && record.Status == "running")
                {
                    record.Status = "interrupted";
                    record.Ended = record.Updated;
                }
                var scenario = ScenarioFor(beat.Map ?? string.Empty);
                bool fromLastRaid = LastRaid != null && string.Equals(LastRaid.Map, beat.Map, StringComparison.OrdinalIgnoreCase);
                record = new SimMapRecord
                {
                    Index = run.Maps.Count,
                    Map = beat.Map ?? string.Empty,
                    MapName = SimLabDefaults.NameOf(beat.Map ?? string.Empty),
                    Scenario = fromLastRaid ? LastRaid!.ScenarioName : scenario.Name,
                    Focus = scenario.Focus,
                    Watch = scenario.Watch,
                    Started = DateTime.Now,
                    PlannedMinutes = beat.PlannedSeconds / 60f,
                    Raid = fromLastRaid ? LastRaid : null,
                };
                run.Maps.Add(record);
            }

            record!.Updated = DateTime.Now;
            if (beat.Kind == "recovered")
            {
                // A late copy of the last state from the client's own file: keep the richer one.
                if (record.Last == null || beat.RaidSeconds >= record.Last.RaidSeconds)
                {
                    record.Last = beat;
                }
                record.Status = "interrupted";
                record.Ended ??= DateTime.Now;
            }
            else
            {
                record.Last = beat;
            }
            if (beat.Kind is "beat" or "end" or "start")
            {
                record.Samples.Add(new SimSample
                {
                    T = beat.RaidSeconds,
                    Alive = beat.AliveBots,
                    Deaths = beat.BotDeaths,
                    Fps = beat.Fps,
                    Mono = beat.MonoUsedMB,
                    Errors = beat.Errors?.Sum(e => e.Count) ?? 0,
                });
                if (record.Samples.Count > MAX_SAMPLES)
                {
                    record.Samples.RemoveAt(0);
                }
            }
            if (beat.Kind == "end")
            {
                record.Ended = DateTime.Now;
                bool timeUp = beat.EndReason == "timeUp";
                // "gameClosed" = Alt+F4 / window closed mid-map: the client managed one last report on the way out.
                record.Status = timeUp ? "done" : beat.EndReason == "gameClosed" ? "interrupted" : "ended";
                if (timeUp && Config.AutoRotate && runId == State.RunId)
                {
                    var rotation = EnabledRotation();
                    int index = rotation.FindIndex(x => string.Equals(x.Map, beat.Map, StringComparison.OrdinalIgnoreCase));
                    int count = Math.Max(1, rotation.Count);
                    State.Step = index >= 0 ? (index + 1) % count : (State.Step + 1) % count;
                    SaveState();
                }
            }
            WriteJson(path, run);
        }
    }

    /// <summary>The client is in the main menu but the last map never reported its end (Alt+F4, crash, closed by hand).</summary>
    private void MarkStaleRunning(string why)
    {
        if (string.IsNullOrEmpty(State.RunId))
        {
            return;
        }
        string path = RunPath(State.RunId);
        var run = ReadJson<SimRunRecord>(path);
        var record = run?.Maps.LastOrDefault();
        if (run == null || record == null || record.Status != "running")
        {
            return;
        }
        record.Status = "interrupted";
        record.Ended = record.Updated;
        record.Last ??= new SimBeat();
        record.Last.Notes ??= [];
        record.Last.Notes.Add($"서버 판단: {why} (마지막 보고 {record.Updated:HH:mm:ss})");
        WriteJson(path, run);
        logger.Warning($"[SAIN SimLab] {record.Map} marked interrupted: {why}");
    }

    // ---------------------------------------------------------------- json

    private T? ReadJson<T>(string path)
        where T : class
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }
            return SAINJsonUtil.Deserialize<T>(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            logger.Warning($"[SAIN SimLab] could not read {path}: {ex.Message}");
            return null;
        }
    }

    private void WriteJson(string path, object value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, SAINJsonUtil.Serialize(value));
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            logger.Warning($"[SAIN SimLab] could not write {path}: {ex.Message}");
        }
    }
}
