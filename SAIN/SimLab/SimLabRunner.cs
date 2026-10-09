using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Bots;
using EFT.Communications;
using EFT.UI;
using HarmonyLib;
using JsonType;
using SAIN.Preset.Shared.SimLab;
using SAIN.SAINComponent.Classes.Tactics;
using UnityEngine;

namespace SAIN.SimLab;

/// <summary>
/// zzap SimLab: drives the map rotation. Lives on the SAIN plugin object for the whole game.
///   main menu  -> hello (arms the server, gets the next map) -> countdown -> starts that map's raid
///   in raid    -> spectator (god mode, bots ignore the player, moved next to side A), reserve squads called in as bots die,
///                 a full report every 10 s to the local file and every 60 s to the server, raid ended after its minutes
///   raid end   -> final report (GameWorld.Dispose prefix), result screens skipped, back to the menu
/// Costs one bool check per frame when the sim preset is not loaded.
/// </summary>
public sealed class SimLabRunner : MonoBehaviour
{
    private const float TICK = 2f;
    private const float LOCAL_BEAT = 10f;
    private const float SERVER_BEAT = 60f;
    private const float START_TIMEOUT = 300f;

    public static SimLabRunner Instance { get; private set; }

    private bool _lastActive;
    private bool _startHelloDone;

    // menu
    private float _handledMenuTime = -2f;
    private float _autoStartAt = -1f;
    private float _startRequestedAt = -1f;
    private string _autoStartMap;
    private float _nextCountdownNote;

    // raid
    private int _worldId;
    private bool _inRaid;
    private bool _setupDone;
    private float _raidStart = -1f;
    private float _plannedSeconds;
    private string _map;
    private string _runId;
    private string _endReason;
    private bool _endRequested;
    private float _nextTick;
    private float _nextLocalBeat;
    private float _nextServerBeat;
    private Player _player;

    // spawns
    private readonly List<BossLocationSpawn> _reserveA = new();
    private readonly List<BossLocationSpawn> _reserveB = new();
    private int _activated;
    private float _lastSpawn = -999f;
    private bool _nextIsA = true;
    private bool _reserveEmptyNoted;

    // stats
    private readonly Dictionary<string, string> _seenBots = new();
    private readonly Dictionary<string, int> _roles = new();
    private readonly List<string> _notes = new();
    private int _alive;
    private int _stalls;
    private int _fpsFrames;
    private float _fpsSince;
    private float _lastFps;
    private float _fpsMin;

    public void Awake()
    {
        Instance = this;
        Application.quitting += OnQuit;
    }

    public void OnDestroy()
    {
        Application.quitting -= OnQuit;
    }

    public void Update()
    {
        bool active = SimLab.Active;
        if (active != _lastActive || !_startHelloDone)
        {
            _lastActive = active;
            SayHello(_startHelloDone ? "preset" : "start");
        }
        if (!active && !_inRaid)
        {
            return;
        }
        try
        {
            var world = Singleton<GameWorld>.Instance;
            bool raidWorld = world != null && world.MainPlayer != null && !string.Equals(world.LocationId, "hideout", StringComparison.OrdinalIgnoreCase);
            if (raidWorld)
            {
                RaidUpdate(world);
            }
            else if (active)
            {
                MenuUpdate();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[SimLab] update failed: {ex}");
        }
    }

    private void SayHello(string reason)
    {
        _startHelloDone = true;
        var plan = SimLab.Hello(reason);
        if (reason == "start" && SimLab.Active && plan != null)
        {
            SimLabFile.RecoverUnfinished();
        }
    }

    // ================================================================ main menu

    private void MenuUpdate()
    {
        if (_startRequestedAt > 0f && Time.realtimeSinceStartup - _startRequestedAt > START_TIMEOUT)
        {
            _startRequestedAt = -1f;
            Notify($"[시뮬] {_autoStartMap} 자동 시작이 {START_TIMEOUT:0}초 안에 안 됐습니다. 직접 시작하거나 게임을 재시작해 주세요.", true);
            Logger.LogError($"[SimLab] auto start of {_autoStartMap} did not reach a raid within {START_TIMEOUT:0}s");
        }
        if (SimLab.MenuReadyTime < 0f || SimLab.MenuReadyTime == _handledMenuTime)
        {
            TickCountdown();
            return;
        }
        _handledMenuTime = SimLab.MenuReadyTime;
        _startRequestedAt = -1f;
        var plan = SimLab.Hello("menu");
        var settings = SimLab.Settings;
        if (plan == null || !plan.Armed || settings == null || !settings.AutoStart)
        {
            return;
        }
        if (plan.Paused)
        {
            Notify("[시뮬] 순회 일시정지 중 — 자동 시작 안 함 (https://127.0.0.1:6969/sain/sim)", false);
            return;
        }
        if (string.IsNullOrEmpty(plan.NextMap))
        {
            Notify("[시뮬] " + (plan.Message ?? "다음 맵 없음"), true);
            return;
        }
        _autoStartMap = plan.NextMap;
        _autoStartAt = Time.realtimeSinceStartup + Mathf.Max(5f, settings.AutoStartDelay);
        _nextCountdownNote = 0f;
    }

    private void TickCountdown()
    {
        if (_autoStartAt < 0f)
        {
            return;
        }
        float left = _autoStartAt - Time.realtimeSinceStartup;
        if (Time.realtimeSinceStartup >= _nextCountdownNote && left > 1f)
        {
            _nextCountdownNote = Time.realtimeSinceStartup + 5f;
            var plan = SimLab.Plan;
            Notify($"[시뮬] {left:0}초 후 {plan?.NextMapName ?? _autoStartMap} 시작 ({(plan?.Step ?? 0) + 1}/{plan?.StepCount}, {plan?.Minutes:0}분) — 멈추려면 웹 /sain/sim에서 일시정지", false);
        }
        if (left > 0f)
        {
            return;
        }
        _autoStartAt = -1f;
        // Paused from the page during the countdown?
        var fresh = SimLab.Hello("countdown");
        if (fresh == null || !fresh.Armed || fresh.Paused || !SimLab.Active || !string.Equals(fresh.NextMap, _autoStartMap, StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning($"[SimLab] auto start cancelled (paused={fresh?.Paused} next={fresh?.NextMap})");
            if (fresh != null && fresh.Armed && !fresh.Paused && SimLab.Active)
            {
                SimLab.MenuReadyTime = Time.realtimeSinceStartup; // next map changed on the page: count down again for the new one
            }
            return;
        }
        StartRaid(_autoStartMap);
    }

    private void StartRaid(string map)
    {
        if (!TarkovApplication.Exist(out TarkovApplication app) || app.Session?.LocationSettings?.locations == null)
        {
            Logger.LogError("[SimLab] no TarkovApplication/session - cannot start a raid");
            return;
        }
        var location = app.Session.LocationSettings.locations.Values.FirstOrDefault(l => string.Equals(l.Id, map, StringComparison.OrdinalIgnoreCase));
        if (location == null)
        {
            Notify($"[시뮬] 맵 '{map}'을 찾지 못했습니다.", true);
            Logger.LogError($"[SimLab] location '{map}' not in the session's location list");
            return;
        }
        // Same settings the menu's Ready button ends with in PvE (MainMenuShowOperation: Local + IsPveOffline), PMC, current time.
        var raid = app._raidSettings;
        raid.Side = ESideType.Pmc;
        raid.SelectedLocation = location;
        raid.RaidMode = ERaidMode.Local;
        raid.IsPveOffline = true;
        raid.isInTransition = false;
        raid.SelectedDateTime = EDateTime.CURR;
        raid.MetabolismDisabled = true;
        raid.BotSettings = new BotControllerSettings(false, EBotAmount.AsOnline); // same as BSG's own quick start (InternalStartGame)
        try
        {
            if (AccessTools.Field(typeof(TarkovApplication), "_menuOperation")?.GetValue(app) is MainMenuShowOperation menu)
            {
                menu.IsInSession = true;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[SimLab] could not flag the menu as in session: {ex.Message}");
        }
        _startRequestedAt = Time.realtimeSinceStartup;
        Logger.LogWarning($"[SimLab] auto start: {map} ({location.Name}), run {SimLab.Plan?.RunId}");
        Notify($"[시뮬] {SimLab.Plan?.NextMapName ?? map} 시작", false);
        var task = app.OnReadyToStartMatchingAsync();
        task.ContinueWith(
            t => Logger.LogError($"[SimLab] raid start failed: {t.Exception}"),
            System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted
        );
    }

    // ================================================================ raid

    private void RaidUpdate(GameWorld world)
    {
        if (world.GetInstanceID() != _worldId)
        {
            BeginRaid(world);
        }
        if (!_inRaid)
        {
            return;
        }
        float dt = Time.unscaledDeltaTime;
        if (dt > 1f && _setupDone)
        {
            _stalls++;
        }
        _fpsFrames++;
        float now = Time.realtimeSinceStartup;

        var game = Singleton<AbstractGame>.Instance;
        if (!_setupDone)
        {
            if (game == null || game.Status != GameStatus.Started)
            {
                return;
            }
            Setup(world);
            return;
        }

        float raidSeconds = now - _raidStart;
        SimLogListener.RaidSeconds = raidSeconds;
        if (now >= _nextTick)
        {
            _nextTick = now + TICK;
            Tick(world, now);
        }
        if (now >= _nextLocalBeat)
        {
            _nextLocalBeat = now + LOCAL_BEAT;
            UpdateFps(now);
            var beat = BuildBeat("beat");
            SimLabFile.Write(beat);
            if (now >= _nextServerBeat)
            {
                _nextServerBeat = now + SERVER_BEAT;
                SimLab.PostInBackground("/sain/sim/beat", beat);
            }
        }
        if (!_endRequested && raidSeconds >= _plannedSeconds)
        {
            EndRaid("timeUp");
        }
    }

    private void BeginRaid(GameWorld world)
    {
        _worldId = world.GetInstanceID();
        _inRaid = false;
        _setupDone = false;
        _startRequestedAt = -1f;
        _autoStartAt = -1f;
        if (!SimLab.Active)
        {
            return;
        }
        _inRaid = true;
        _map = (world.LocationId ?? "?").ToLowerInvariant();
        _endReason = null;
        _endRequested = false;
        _reserveA.Clear();
        _reserveB.Clear();
        _activated = 0;
        _lastSpawn = -999f;
        _nextIsA = true;
        _reserveEmptyNoted = false;
        _seenBots.Clear();
        _roles.Clear();
        _notes.Clear();
        _stalls = 0;
        _fpsMin = 0f;
        _lastFps = 0f;
        _player = world.MainPlayer;

        var info = SimLab.Post<SimRaidInfo>("/sain/sim/raid", new object());
        if (info != null && info.Applied && string.Equals(info.Map, _map, StringComparison.OrdinalIgnoreCase))
        {
            SimLab.Raid = info;
        }
        else
        {
            SimLab.Raid = null;
            _notes.Add(
                info == null
                    ? "서버에서 시뮬 설정을 받지 못함 — 시뮬 스폰 없이 진행"
                    : $"이 레이드에는 시뮬 스폰이 적용되지 않음 (서버 마지막 적용 맵: {info.Map ?? "-"} {info.Note})"
            );
        }
        _runId = SimLab.Raid?.RunId ?? SimLab.Plan?.RunId;
        float minutes = SimLab.Raid?.Minutes ?? SimLab.Plan?.Minutes ?? 20f;
        if (minutes <= 0f)
        {
            minutes = 20f;
        }
        _plannedSeconds = minutes * 60f;
        Logger.LogWarning($"[SimLab] sim raid on {_map}: {(SimLab.Raid != null ? $"'{SimLab.Raid.ScenarioName}'" : "no sim setup")}, {minutes:0} min, run {_runId}");
    }

    private void Setup(GameWorld world)
    {
        _setupDone = true;
        float now = Time.realtimeSinceStartup;
        _raidStart = now;
        _nextTick = now + 1f;
        _nextLocalBeat = now + LOCAL_BEAT;
        _nextServerBeat = now + 20f; // first server report early: proves the map started
        _fpsSince = now;
        _fpsFrames = 0;
        _player = world.MainPlayer;
        var info = SimLab.Raid;

        SimLogListener.Attach();
        SimLabFile.Open(_runId, _map);

        if (info != null && info.Spectator && _player != null)
        {
            SimLab.SpectatorProfileId = _player.ProfileId;
            SimLab.SpectatorActive = true;
            ApplyGodMode();
            if (info.HasSpectatorPos)
            {
                try
                {
                    _player.Teleport(new Vector3(info.SpectatorX, info.SpectatorY, info.SpectatorZ), true);
                    _notes.Add($"관전 위치로 이동 ({info.SpectatorX:0},{info.SpectatorY:0},{info.SpectatorZ:0}) - A쪽 구역 [{info.ZonesA}]");
                }
                catch (Exception ex)
                {
                    _notes.Add("관전 위치 이동 실패: " + ex.Message);
                }
            }
            ForgetSpectator(world);
        }

        if (info != null)
        {
            var botGame = Singleton<IBotGame>.Instance;
            var spawner = botGame?.BotsController?.BotSpawner;
            if (spawner != null)
            {
                spawner.SetMaxBots(info.MaxAliveBots + Math.Max(1, info.SquadSizeMax));
            }
            if (info.RemoveOtherSpawns)
            {
                StopOtherScenarios();
            }
            CollectReserves(info);
            Spawn(true, now);
            Spawn(false, now);
        }

        var start = BuildBeat("start");
        SimLabFile.Write(start);
        SimLab.PostInBackground("/sain/sim/beat", start);
        Notify($"[시뮬] {SimLab.Raid?.MapName ?? _map}: {SimLab.Raid?.ScenarioName ?? "시뮬 스폰 없음"} — {_plannedSeconds / 60f:0}분", false);
    }

    private void ApplyGodMode()
    {
        try
        {
            var health = _player?.ActiveHealthController;
            if (health == null)
            {
                return;
            }
            health.SetDamageCoeff(0f);
            health.RestoreFullHealth();
        }
        catch (Exception ex)
        {
            _notes.Add("무적 설정 실패: " + ex.Message);
        }
    }

    /// <summary>Bots that already listed the player as an enemy (before the AddEnemy patch could stop it) drop him.</summary>
    private void ForgetSpectator(GameWorld world)
    {
        if (!SimLab.SpectatorActive || _player == null)
        {
            return;
        }
        foreach (var p in world.AllAlivePlayersList)
        {
            if (p == null || !p.IsAI)
            {
                continue;
            }
            var group = p.AIData?.BotOwner?.BotsGroup;
            if (group != null && group.IsEnemy(_player))
            {
                group.RemoveEnemy(_player);
            }
        }
    }

    private void StopOtherScenarios()
    {
        if (Singleton<AbstractGame>.Instance is not LocalGame game)
        {
            return;
        }
        foreach (string field in new[] { "_nonWavesSpawnScenario", "_wavesSpawnScenario" })
        {
            try
            {
                object scenario = AccessTools.Field(typeof(LocalGame), field)?.GetValue(game);
                AccessTools.Method(scenario?.GetType(), "Stop")?.Invoke(scenario, null);
            }
            catch (Exception ex)
            {
                _notes.Add($"{field} 정지 실패: {ex.Message}");
            }
        }
    }

    private void CollectReserves(SimRaidInfo info)
    {
        if (Singleton<AbstractGame>.Instance is not LocalGame game || game.BossSpawnScenario?.BossSpawnWaves == null)
        {
            _notes.Add("예비 분대를 찾지 못함 (LocalGame/BossSpawnScenario 없음)");
            return;
        }
        foreach (var wave in game.BossSpawnScenario.BossSpawnWaves)
        {
            if (wave == null || wave.Time < info.ReserveTimeBase || wave.Activated)
            {
                continue;
            }
            int index = Mathf.RoundToInt(wave.Time - info.ReserveTimeBase);
            (index % 2 == 0 ? _reserveA : _reserveB).Add(wave);
        }
        _reserveA.Sort((a, b) => a.Time.CompareTo(b.Time));
        _reserveB.Sort((a, b) => a.Time.CompareTo(b.Time));
        if (_reserveA.Count + _reserveB.Count == 0)
        {
            _notes.Add($"예비 분대 0개 (서버는 {info.ReserveWaves}개를 넣었다고 함) — 다른 모드가 보스 웨이브를 바꿨을 수 있음");
        }
        Logger.LogWarning($"[SimLab] reserve squads: A {_reserveA.Count}, B {_reserveB.Count}");
    }

    private void Tick(GameWorld world, float now)
    {
        if (SimLab.SpectatorActive)
        {
            ApplyGodMode();
        }
        int alive = 0;
        int aliveA = 0;
        int aliveB = 0;
        var info = SimLab.Raid;
        foreach (var p in world.AllAlivePlayersList)
        {
            if (p == null || !p.IsAI || p.Profile?.Info?.Settings == null)
            {
                continue;
            }
            alive++;
            string role = p.Profile.Info.Settings.Role.ToString();
            if (!_seenBots.ContainsKey(p.ProfileId))
            {
                _seenBots[p.ProfileId] = role;
                _roles.TryGetValue(role, out int count);
                _roles[role] = count + 1;
                var group = p.AIData?.BotOwner?.BotsGroup;
                if (SimLab.SpectatorActive && _player != null && group != null && group.IsEnemy(_player))
                {
                    group.RemoveEnemy(_player);
                }
            }
            if (info != null && info.SideA != info.SideB)
            {
                if (role == info.SideA)
                {
                    aliveA++;
                }
                else if (role == info.SideB)
                {
                    aliveB++;
                }
            }
        }
        _alive = alive;
        if (info == null || _endRequested)
        {
            return;
        }
        var spawner = Singleton<IBotGame>.Instance?.BotsController?.BotSpawner;
        int total = spawner?.AllBotsWithDelayed ?? alive;
        int squad = Math.Max(1, info.SquadSizeMax);
        if (total + squad > info.MaxAliveBots || now - _lastSpawn < Math.Max(5f, info.RespawnSeconds))
        {
            return;
        }
        bool sideA = info.SideA != info.SideB ? (aliveA != aliveB ? aliveA < aliveB : _nextIsA) : _nextIsA;
        Spawn(sideA, now);
    }

    private void Spawn(bool sideA, float now)
    {
        var list = sideA ? _reserveA : _reserveB;
        if (list.Count == 0)
        {
            list = sideA ? _reserveB : _reserveA;
        }
        if (list.Count == 0)
        {
            if (!_reserveEmptyNoted)
            {
                _reserveEmptyNoted = true;
                _notes.Add($"예비 분대를 다 씀 (t={(now - _raidStart) / 60f:0.0}분)");
            }
            return;
        }
        var wave = list[0];
        list.RemoveAt(0);
        _lastSpawn = now;
        _nextIsA = !sideA;
        if (Singleton<AbstractGame>.Instance is not LocalGame game || game.BossSpawnScenario == null)
        {
            return;
        }
        try
        {
            game.BossSpawnScenario.ActivateWave(wave);
            _activated++;
            if (TacticDiagnostics.LogOn)
            {
                RaidJournal.Line($"[SimLab] squad {_activated}: {wave.BossName} x{wave.BossEscortAmount}+1 in [{wave.BossZone}] (alive bots {_alive})");
            }
        }
        catch (Exception ex)
        {
            _notes.Add($"분대 호출 실패: {ex.Message}");
            Logger.LogError($"[SimLab] ActivateWave failed: {ex}");
        }
    }

    private void UpdateFps(float now)
    {
        float span = now - _fpsSince;
        if (span <= 0f)
        {
            return;
        }
        _lastFps = _fpsFrames / span;
        if (_fpsMin <= 0f || _lastFps < _fpsMin)
        {
            _fpsMin = _lastFps;
        }
        _fpsFrames = 0;
        _fpsSince = now;
    }

    private SimBeat BuildBeat(string kind)
    {
        var beat = new SimBeat
        {
            RunId = _runId,
            Map = _map,
            Kind = kind,
            ClientTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Build = SimLab.Build,
            Preset = SimLab.PresetName,
            JournalFile = RaidJournal.File,
            RaidSeconds = _raidStart > 0f ? Time.realtimeSinceStartup - _raidStart : 0f,
            PlannedSeconds = _plannedSeconds,
            EndReason = _endReason,
            AliveBots = _alive,
            SimWavesActivated = _activated,
            SimWavesLeft = _reserveA.Count + _reserveB.Count,
            BotsSeen = _seenBots.Count,
            Fps = _lastFps,
            FpsMin = _fpsMin,
            MonoUsedMB = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / (1024 * 1024),
            Stalls = _stalls,
            SpawnedRoles = new Dictionary<string, int>(_roles),
            Notes = new List<string>(_notes),
        };
        try
        {
            BattleStats.FillSim(beat);
            TacticDiagnostics.CopyCounts(beat.Counters);
            SimLogListener.CopyTo(beat.Errors);
        }
        catch (Exception ex)
        {
            beat.Notes.Add("보고 만들기 일부 실패: " + ex.Message);
        }
        return beat;
    }

    private void EndRaid(string reason)
    {
        _endRequested = true;
        _endReason = reason;
        try
        {
            if (Singleton<AbstractGame>.Instance is not BaseLocalGame<EftGamePlayerOwner> game || _player == null)
            {
                _notes.Add("레이드를 끝내지 못함 (게임 객체 없음)");
                return;
            }
            SimLab.SkipNextResults = true;
            Logger.LogWarning($"[SimLab] {_map}: time is up ({_plannedSeconds / 60f:0} min) - ending the raid");
            game.Stop(_player.ProfileId, ExitStatus.Survived, null, 0f);
        }
        catch (Exception ex)
        {
            SimLab.SkipNextResults = false;
            _notes.Add("레이드 종료 실패: " + ex.Message);
            Logger.LogError($"[SimLab] ending the raid failed: {ex}");
        }
    }

    /// <summary>GameWorld.Dispose prefix (before SAIN's own raid-end saves clear the scoreboard): the final report.</summary>
    public static void OnRaidWorldDisposing()
    {
        Instance?.FinishRaid(null);
    }

    private void FinishRaid(string forcedReason)
    {
        if (!_inRaid)
        {
            return;
        }
        _inRaid = false;
        try
        {
            if (_setupDone)
            {
                UpdateFps(Time.realtimeSinceStartup);
            }
            _endReason = forcedReason ?? _endReason ?? "raidEnded";
            var beat = BuildBeat("end");
            SimLabFile.Close(beat);
            SimLab.Post<object>("/sain/sim/beat", beat);
            Logger.LogWarning($"[SimLab] {_map} finished ({_endReason}) after {beat.RaidSeconds / 60f:0.0} min: bots seen {beat.BotsSeen}, deaths {beat.BotDeaths}, errors {beat.Errors.Sum(e => e.Count)}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[SimLab] final report failed: {ex}");
        }
        finally
        {
            SimLogListener.Detach();
            SimLab.SpectatorActive = false;
            SimLab.Raid = null;
            _reserveA.Clear();
            _reserveB.Clear();
            _player = null;
        }
    }

    private void OnQuit()
    {
        // Alt+F4 / closing the window mid-map: one last report on the way out (the local file already has the last 10 s).
        FinishRaid("gameClosed");
    }

    private static void Notify(string message, bool warning)
    {
        try
        {
            NotificationManager.DisplayMessageNotification(
                message,
                ENotificationDurationType.Default,
                warning ? ENotificationIconType.Alert : ENotificationIconType.Default,
                warning ? Color.yellow : (Color?)null
            );
        }
        catch
        {
        }
        Logger.LogWarning("[SimLab] " + message);
    }
}
