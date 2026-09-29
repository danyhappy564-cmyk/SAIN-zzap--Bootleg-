using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using EFT;
using EFT.Ballistics;
using Newtonsoft.Json;
using SAIN.Components;
using SAIN.Preset.Shared.GlobalSettings;
using UnityEngine;

namespace SAIN.Components.BotControllerSpace.Classes;

/// <summary>
/// zzap fork, stage 1 of "bots learn the player's style": records how the human player plays during a raid and writes
/// one JSON line per raid to BepInEx/config/SAIN-zzap/PlayerStyle/&lt;profileId&gt;.jsonl, plus a [PlayerStyle] summary
/// in the log. Nothing here changes bot behaviour. Keys are not hooked - the same information comes from the player's
/// state (sprint, pose, lean, aiming, grounded, reload) sampled every 0.1s and from shot / grenade / death events.
/// F6: General > Player Style Recorder (zzap).
/// </summary>
public sealed class PlayerStyleRecorder
{
    private const float SAMPLE_INTERVAL = 0.1f;
    private const float CAMP_RADIUS = 3f;
    private const float CAMP_MIN_TIME = 20f;

    public static PlayerStyleRecorder Instance { get; private set; }

    private readonly BotManagerComponent _manager;
    private Player _player;

    // Set once the main player was found. At GameWorld.Dispose the Player object is already destroyed (Unity-null), so
    // the end-of-raid save must not test _player (it did: "not saving ... player=False").
    private bool _playerFound;
    private Player.FirearmController _shotSource;
    private float _nextSample;
    private float _lastSample;
    private float _nextLog;
    private bool _written;

    // Previous-sample state for edge counts.
    private bool _wasGrounded = true;
    private bool _wasLeaning;
    private bool _wasCrouched;
    private bool _wasSprinting;
    private EFT.InventoryLogic.Weapon _lastWeapon;
    private EFT.InventoryLogic.Magazine _lastMag;
    private int _lastRounds;
    private Vector3 _campAnchor;
    private float _campStart = -1f;

    private readonly StyleData _d = new();
    private PlayerKeyRecorder _keys;

    /// <summary>Every frame, from BotManagerComponent.Update (Unity), so key presses are timed to the frame.</summary>
    public void PollKeys()
    {
        if (_keys == null || _written || _player == null || _player.HealthController?.IsAlive != true)
        {
            return;
        }
        try
        {
            _keys.Poll();
        }
        catch (Exception ex)
        {
            if (!_keyErrorLogged)
            {
                _keyErrorLogged = true;
                Logger.LogWarning($"[PlayerStyle] key recorder error (logged once): {ex}");
            }
        }
    }

    private bool _keyErrorLogged;

    public PlayerStyleRecorder(BotManagerComponent manager)
    {
        _manager = manager;
        // A previous raid's recorder that never got its end-of-raid save: write it now.
        if (Instance != null && !Instance._written)
        {
            Instance.Dispose("nextRaidStart");
        }
        Instance = this;
        _d.StartedUtc = DateTime.UtcNow.ToString("o");
    }

    private static bool Enabled
    {
        get { return GlobalSettingsClass.Instance?.General?.PlayerStyle?.Enabled == true; }
    }

    public void Update()
    {
        if (!Enabled || _written)
        {
            return;
        }
        try
        {
            Tick();
        }
        catch (Exception ex)
        {
            if (!_errorLogged)
            {
                _errorLogged = true;
                Logger.LogWarning($"[PlayerStyle] recorder error (logged once): {ex}");
            }
        }
    }

    private bool _errorLogged;

    private void Tick()
    {
        if (!_playerFound)
        {
            Player main = _manager.GameWorld?.MainPlayer;
            if (main == null)
            {
                return;
            }
            _player = main;
            _playerFound = true;
            _d.ProfileId = main.ProfileId;
            _d.Nickname = main.Profile?.Nickname;
            _d.Map = _manager.GameWorld?.LocationId;
            _player.OnPlayerDead += OnPlayerDead;
            _player.BeingHitAction += OnBeingHit;
            _campAnchor = main.Position;
            _campStart = Time.time;
            _d.RaidStartTime = Time.time;
            if (_manager.GrenadeController != null)
            {
                _manager.GrenadeController.OnGrenadeThrown += OnGrenadeThrown;
            }
            StartFiles();
        }
        if (_player == null)
        {
            return;
        }
        if (_player.HealthController?.IsAlive != true)
        {
            return;
        }
        HookShots();

        float time = Time.time;
        if (time < _nextSample)
        {
            return;
        }
        if (!_d.GodMode && _player.ActiveHealthController != null && _player.ActiveHealthController.DamageCoeff <= 0f)
        {
            MarkTest(ref _d.GodMode, "god mode (damage multiplier 0)");
        }
        float dt = _lastSample > 0f ? Mathf.Min(time - _lastSample, 1f) : SAMPLE_INTERVAL;
        _lastSample = time;
        _nextSample = time + SAMPLE_INTERVAL;

        var mc = _player.MovementContext;
        float speed = _player.Velocity.magnitude;
        bool sprinting = _player.IsSprintEnabled;
        bool prone = _player.IsInPronePose;
        bool crouched = !prone && _player.PoseLevel < 0.7f;
        bool leaning = Mathf.Abs(mc.Tilt) > 0.1f;
        bool grounded = mc.IsGrounded;
        var fc = _player.HandsController as Player.FirearmController;
        bool aiming = fc != null && fc.IsAiming;

        _d.Seconds += dt;
        if (speed > 0.6f) _d.MovingSec += dt;
        else _d.StillSec += dt;
        if (sprinting) _d.SprintSec += dt;
        if (crouched) _d.CrouchSec += dt;
        if (prone) _d.ProneSec += dt;
        if (leaning) _d.LeanSec += dt;
        if (aiming) _d.AdsSec += dt;
        if (_player.Environment == EnvironmentType.Indoor) _d.IndoorSec += dt;
        if (time >= _nextJournalPos)
        {
            _nextJournalPos = time + 2f;
            SAIN.SAINComponent.Classes.Tactics.RaidJournal.Line(
                $"[Player] at {SAIN.SAINComponent.Classes.Tactics.RaidJournal.Pos(_player.Position)} {(sprinting ? "sprint" : speed > 0.6f ? "move" : "still")}"
                    + $"{(prone ? " prone" : crouched ? " crouch" : "")}{(leaning ? " lean" : "")}{(aiming ? " ads" : "")} {(_player.Environment == EnvironmentType.Indoor ? "indoor" : "outdoor")} hp={_player.HealthStatus}");
        }

        if (_wasGrounded && !grounded && speed > 0.3f) _d.Jumps++;
        if (!_wasLeaning && leaning) _d.LeanPeeks++;
        if (_wasCrouched != crouched) _d.CrouchToggles++;
        if (!_wasSprinting && sprinting) _d.SprintBursts++;
        if (_keys == null)
        {
            TrackReload(fc);
        }
        _wasGrounded = grounded;
        _wasLeaning = leaning;
        _wasCrouched = crouched;
        _wasSprinting = sprinting;

        Vector3 pos = _player.Position;
        Vector3 flat = pos - _campAnchor;
        flat.y = 0f;
        if (flat.magnitude > CAMP_RADIUS)
        {
            CloseCampSegment(time);
            _campAnchor = pos;
            _campStart = time;
        }

        SAIN.SAINComponent.Classes.Tactics.BattleStats.Tick();
        float every = GlobalSettingsClass.Instance.General.PlayerStyle.LogEveryMinutes;
        if (every > 0f && time > _nextLog)
        {
            if (_nextLog > 0f)
            {
                Logger.LogWarning($"[PlayerStyle] RUNNING: {Summary()}");
                SaveCheckpoint();
            }
            _nextLog = time + every * 60f;
        }
    }

    /// <summary>
    /// First field test counted 0 reloads in 1342 shots with IsInReloadOperation() - the player's controller doesn't
    /// report it that way. Watch the gun instead: a different magazine object in the same gun (mag swap, incl. quick
    /// reload) or the loaded count jumping up by 2+ (internal/tube reload) = one reload, with the rounds left before it.
    /// </summary>
    private void TrackReload(Player.FirearmController fc)
    {
        var weapon = fc?.Item;
        if (weapon == null)
        {
            _lastWeapon = null;
            return;
        }
        var mag = weapon.GetCurrentMagazine();
        int rounds = (mag?.Count ?? 0) + weapon.ChamberAmmoCount;
        if (weapon == _lastWeapon)
        {
            bool swapped = mag != null && _lastMag != null && mag != _lastMag;
            // (Count jumps in the same magazine are ignored: an infinite-ammo mod refills it - 197 "reloads" for 2 R presses.)
            if (swapped)
            {
                _d.Reloads++;
                if (_lastRounds <= 0)
                {
                    _d.ReloadsEmpty++;
                }
                _d.ReloadRoundsLeftSum += Mathf.Max(0, _lastRounds);
            }
        }
        // While the old mag is out, keep the last count from before it came out.
        if (mag != null || weapon != _lastWeapon)
        {
            _lastMag = mag;
            _lastRounds = rounds;
        }
        _lastWeapon = weapon;
    }

    private void OnKeyPressed(string action)
    {
        if (_player == null)
        {
            return;
        }
        var stats = _keys.Stats;
        if (action == "Reload")
        {
            var weapon = (_player.HandsController as Player.FirearmController)?.Item;
            if (weapon != null)
            {
                int left = (weapon.GetCurrentMagazine()?.Count ?? 0) + weapon.ChamberAmmoCount;
                stats.ReloadsByKey++;
                stats.ReloadsByKeyRoundsLeftSum += left;
                if (left <= 0)
                {
                    stats.ReloadsByKeyEmpty++;
                }
                _d.Reloads = stats.ReloadsByKey;
                _d.ReloadsEmpty = stats.ReloadsByKeyEmpty;
                _d.ReloadRoundsLeftSum = stats.ReloadsByKeyRoundsLeftSum;
            }
        }
        else if (action == "Jump" && _player.IsSprintEnabled)
        {
            // Sprint is toggled with a Shift tap by this player, so the key isn't held - use the sprint state.
            stats.SprintJumps++;
        }
    }

    private void CloseCampSegment(float time)
    {
        if (_campStart < 0f)
        {
            return;
        }
        float held = time - _campStart;
        if (held >= CAMP_MIN_TIME)
        {
            _d.CampSpots++;
            _d.CampSec += held;
            _d.LongestCampSec = Mathf.Max(_d.LongestCampSec, held);
        }
    }

    private void HookShots()
    {
        var fc = _player.HandsController as Player.FirearmController;
        if (fc == _shotSource)
        {
            return;
        }
        if (_shotSource != null)
        {
            _shotSource.OnShot -= OnShot;
        }
        _shotSource = fc;
        if (fc != null)
        {
            fc.OnShot += OnShot;
        }
    }

    private void OnShot()
    {
        if (_player == null)
        {
            return;
        }
        _d.Shots++;
        TrackAmmoUse();
        if (_player.Velocity.magnitude > 0.6f) _d.ShotsMoving++;
        if (_shotSource != null && _shotSource.IsAiming) _d.ShotsAds++;
        if (Mathf.Abs(_player.MovementContext.Tilt) > 0.1f) _d.ShotsLeaning++;
        if (!_player.MovementContext.IsGrounded) _d.ShotsInAir++;
        if (_player.PoseLevel < 0.7f || _player.IsInPronePose) _d.ShotsLow++;
    }

    // ---- test-session detection (DevTools-style god mode / infinite ammo). The user tests invincible with endless ammo but
    // plays his normal style, so these raids stay in the style data (F6 Use Test Raids For Adaptation) - only tagged, so
    // kills/deaths can be read with that in mind.
    private EFT.InventoryLogic.Magazine _shotMag;
    private int _shotRounds = -1;

    private void TrackAmmoUse()
    {
        var weapon = _shotSource?.Item;
        var mag = weapon?.GetCurrentMagazine();
        if (mag == null)
        {
            _shotMag = null;
            _shotRounds = -1;
            return;
        }
        int rounds = mag.Count;
        if (mag == _shotMag && _shotRounds >= 0 && rounds >= _shotRounds)
        {
            _d.ShotsNoAmmoUse++;
            if (!_d.InfiniteAmmo && _d.ShotsNoAmmoUse >= 15 && _d.ShotsNoAmmoUse * 2 >= _d.Shots)
            {
                MarkTest(ref _d.InfiniteAmmo, $"infinite ammo ({_d.ShotsNoAmmoUse} of {_d.Shots} shots used no round)");
            }
        }
        _shotMag = mag;
        _shotRounds = rounds;
    }

    private float _nextJournalPos;

    private void OnBeingHit(DamageInfo damage, EBodyPart part, float absorbed)
    {
        var attacker = damage.Player?.iPlayer;
        SAIN.SAINComponent.Classes.Tactics.RaidJournal.Line(
            $"[PlayerHit] by {attacker?.Profile?.Nickname ?? "?"} {part} {damage.Damage:0} dmg{(attacker != null && _player != null ? $" from {(attacker.Position - _player.Position).magnitude:0}m" : "")}");
        PlayerOutcomeLearner.OnPlayerHit(damage.Player?.iPlayer?.ProfileId);
        _d.HitsTaken++;
        _d.DamageTaken += Mathf.Max(0f, damage.Damage);
        // A PMC has 440 HP in total - soaking well over that and still standing = invincible.
        if (!_d.GodMode && _d.DamageTaken > 1000f && _d.HitsTaken >= 15 && _player != null && _player.HealthController?.IsAlive == true)
        {
            MarkTest(ref _d.GodMode, $"god mode (took {_d.DamageTaken:0} damage in {_d.HitsTaken} hits, still alive)");
        }
    }

    private void MarkTest(ref bool flag, string why)
    {
        flag = true;
        _d.TestSession = true;
        _d.TestReasons = string.IsNullOrEmpty(_d.TestReasons) ? why : $"{_d.TestReasons}; {why}";
        Logger.LogWarning($"[PlayerStyle] test session detected: {why} - style still recorded (movement/fighting style is the same), kills/deaths are not real");
    }

    private static string FindDevToolsPlugins()
    {
        var names = new List<string>();
        try
        {
            foreach (var kv in BepInEx.Bootstrap.Chainloader.PluginInfos)
            {
                string text = $"{kv.Key} {kv.Value?.Metadata?.Name}".ToLowerInvariant();
                if (text.Contains("devtool") || text.Contains("godmode") || text.Contains("god mode") || text.Contains("cheat") || text.Contains("trainer"))
                {
                    names.Add(kv.Value?.Metadata?.Name ?? kv.Key);
                }
            }
        }
        catch
        {
        }
        return names.Count > 0 ? string.Join(", ", names) : null;
    }

    private void OnGrenadeThrown(Grenade grenade, Vector3 dangerPoint, string profileId)
    {
        if (_player != null && profileId == _player.ProfileId)
        {
            _d.Grenades++;
        }
    }

    /// <summary>Called from the bot's death handler (DoorTacticClass.OnOwnDeath).</summary>
    public static void OnBotKilled(BotComponent bot, IPlayer aggressor, EBodyPart part, string victimDecision, bool victimSawPlayer)
    {
        var rec = Instance;
        if (rec == null || rec._player == null || !Enabled || aggressor == null || aggressor.ProfileId != rec._player.ProfileId)
        {
            return;
        }
        float dist = (bot.Position - rec._player.Position).magnitude;
        var d = rec._d;
        d.Kills++;
        d.KillDistanceSum += dist;
        SAIN.SAINComponent.Classes.Tactics.RaidJournal.Line($"[Kill] player killed [{bot.name}] [{bot.Info.Personality}] {dist:0}m {part} while it was {victimDecision}{(victimSawPlayer ? "" : " (it never saw him)")}");
        SAIN.SAINComponent.Classes.Tactics.FearModel.OnPlayerKill();
        if (dist < 10f) d.KillsUnder10m++;
        else if (dist < 30f) d.Kills10to30m++;
        else if (dist < 80f) d.Kills30to80m++;
        else d.KillsOver80m++;
        if (part == EBodyPart.Head) d.KillsHead++;
        if (!victimSawPlayer) d.KillsUnseen++;
        d.KillsByVictimDecision.TryGetValue(victimDecision, out int n);
        d.KillsByVictimDecision[victimDecision] = n + 1;
    }

    private void OnPlayerDead(Player player, IPlayer lastAggressor, DamageInfo damage, EBodyPart part)
    {
        _d.Died = true;
        _d.DeathPart = part.ToString();
        if (lastAggressor is Player killer)
        {
            _d.DeathDistance = (killer.Position - player.Position).magnitude;
            _d.KilledBy = killer.Profile?.Nickname;
            if (_manager.GetSAIN(killer.AIData?.BotOwner, out BotComponent bot))
            {
                _d.KilledByPersonality = bot.Info.Personality.ToString();
                _d.KilledByDecision = bot.Decision.CurrentCombatDecision.ToString();
            }
        }
        Write("playerDied");
    }

    public void Dispose()
    {
        Dispose("raidEnd");
    }

    private bool _disposed;

    public void Dispose(string why)
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        try
        {
            if (!ReferenceEquals(_player, null))
            {
                _player.OnPlayerDead -= OnPlayerDead;
                _player.BeingHitAction -= OnBeingHit;
            }
            if (_shotSource != null)
            {
                _shotSource.OnShot -= OnShot;
            }
            if (_manager.GrenadeController != null)
            {
                _manager.GrenadeController.OnGrenadeThrown -= OnGrenadeThrown;
            }
            Write(why);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[PlayerStyle] dispose error: {ex.Message}");
        }
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Write(string why)
    {
        if (_written)
        {
            return;
        }
        if (!Enabled || !_playerFound || _d.Seconds < 30f)
        {
            Logger.LogWarning($"[PlayerStyle] not saving ({why}): enabled={Enabled} player={_playerFound} recorded={_d.Seconds:0}s (min 30s)");
            return;
        }
        Logger.LogWarning($"[PlayerStyle] saving ({why})...");
        _written = true;
        CloseCampSegment(Time.time);
        try
        {
            _keys?.End();
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[PlayerStyle] key recorder end failed: {ex.Message}");
        }
        _d.EndReason = why;
        PlayerOutcomeLearner.Save(why);
        Logger.LogWarning($"[PlayerStyle] RAID SUMMARY ({why}): {Summary()}");
        SAIN.SAINComponent.Classes.Tactics.RaidJournal.Line($"[PlayerStyle] RAID SUMMARY ({why}): {Summary()}");
        SAIN.SAINComponent.Classes.Tactics.BattleStats.Report(why);
        SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.JournalSummary();
        SAIN.SAINComponent.Classes.Tactics.RaidJournal.End(why);
        try
        {
            Directory.CreateDirectory(Dir);
            File.AppendAllText(RecordFile, JsonConvert.SerializeObject(_d, Formatting.None) + Environment.NewLine);
            if (File.Exists(CheckpointFile))
            {
                File.Delete(CheckpointFile);
            }
            Logger.LogWarning($"[PlayerStyle] saved to {RecordFile}");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[PlayerStyle] could not save: {ex.Message}");
        }
    }

    private static string Dir
    {
        get { return Path.Combine(BepInEx.Paths.ConfigPath, "SAIN-zzap", "PlayerStyle"); }
    }

    private string RecordFile
    {
        get { return Path.Combine(Dir, $"{_d.ProfileId}.jsonl"); }
    }

    private string CheckpointFile
    {
        get { return Path.Combine(Dir, $"{_d.ProfileId}.current.json"); }
    }

    /// <summary>
    /// Raid start: create the folder right away (so it can be checked mid-raid), say where the file goes, and recover a
    /// checkpoint a previous raid left behind if its end-of-raid save never happened.
    /// </summary>
    private void StartFiles()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            if (File.Exists(CheckpointFile))
            {
                string leftover = File.ReadAllText(CheckpointFile).Trim();
                if (leftover.Length > 0)
                {
                    File.AppendAllText(RecordFile, leftover + Environment.NewLine);
                    Logger.LogWarning($"[PlayerStyle] previous raid was not saved at its end - recovered its last checkpoint into {RecordFile}");
                }
                File.Delete(CheckpointFile);
            }
            ClassicMovementInterop.Refresh();
            _d.ClassicMovement = ClassicMovementInterop.Installed;
            _d.PlayerNoInertia = ClassicMovementInterop.PlayerNoInertia;
            _d.BotsNoInertia = ClassicMovementInterop.BotsNoInertia;
            _d.BotsQuickTilt = ClassicMovementInterop.BotsQuickTilt;
            Logger.LogWarning($"[ClassicMovement] {ClassicMovementInterop.Describe()}");
            // Which SAIN.dll is actually running (field test ran an old build without noticing - the log alone must tell).
            string build = "?";
            try
            {
                build = System.IO.File.GetLastWriteTime(typeof(PlayerStyleRecorder).Assembly.Location).ToString("yyyy-MM-dd HH:mm");
            }
            catch
            {
            }
            Logger.LogWarning($"[zzap] SAIN.dll built {build} (compare with the changelog's newest entry)");
            var g = GlobalSettingsClass.Instance.General;
            SAIN.SAINComponent.Classes.Tactics.RaidJournal.Start(_d.Map,
                $"# SAIN.dll built {build} | player {_d.Nickname} ({_d.ProfileId}) | preset {SAINPlugin.LoadedPreset?.Info?.Name} | utility hidden={g.CloseCombat.UtilityHiddenEnemy} visible={g.CloseCombat.UtilityVisibleEnemy} "
                    + $"reload={g.CloseCombat.UtilityReload} heal={g.CloseCombat.UtilityHeal} mistakes={g.CloseCombat.UtilityMistakes} | adapt={g.PlayerStyle.AdaptEnabled} learn={g.PlayerStyle.LearnFromOutcomes} "
                    + $"ignorePlayerGear={g.PlayerStyle.IgnorePlayerGear} | settings from loaded preset: {(ReferenceEquals(GlobalSettingsClass.Instance, SAINPlugin.LoadedPreset?.GlobalSettings) ? "yes" : "NO (another preset's globals!)")} "
                    + $"| classic movement: {ClassicMovementInterop.Describe()}");
            _d.DevToolsPlugins = FindDevToolsPlugins();
            if (_d.DevToolsPlugins != null)
            {
                Logger.LogWarning($"[PlayerStyle] dev/cheat mod installed: {_d.DevToolsPlugins} - god mode / infinite ammo are detected while playing and the raid gets tagged");
            }
            PlayerAdaptation.Load(Dir, _d.ProfileId);
            PlayerOutcomeLearner.Load(Dir, _d.ProfileId);
            Logger.LogWarning($"[PlayerStyle] recording {_d.Nickname} on {_d.Map} -> {RecordFile} (checkpoint every {GlobalSettingsClass.Instance.General.PlayerStyle.LogEveryMinutes:0} min: {CheckpointFile})");
            var settings = GlobalSettingsClass.Instance.General.PlayerStyle;
            if (settings.RecordKeys)
            {
                _keys = new PlayerKeyRecorder(Dir, _d.ProfileId, _d.RaidStartTime, settings.KeyTimeline);
                _d.Keys = _keys.Stats;
                _keys.Pressed += OnKeyPressed;
                Logger.LogWarning($"[PlayerStyle] key recording on, binds from {_keys.BindSource}, timeline {(_keys.Stats.TimelineFile ?? "off")}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[PlayerStyle] could not prepare {Dir}: {ex.Message}");
        }
    }

    /// <summary>Mid-raid safety net: the current record so far, overwritten each time.</summary>
    private void SaveCheckpoint()
    {
        PlayerOutcomeLearner.Save("checkpoint");
        try
        {
            _d.EndReason = "checkpoint";
            _keys?.Flush();
            File.WriteAllText(CheckpointFile, JsonConvert.SerializeObject(_d, Formatting.None));
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[PlayerStyle] checkpoint failed: {ex.Message}");
        }
    }

    private string Summary()
    {
        var d = _d;
        float t = Mathf.Max(d.Seconds, 1f);
        string Pct(float v) => $"{v / t * 100f:0}%";
        float perMin(int v) => v / (t / 60f);
        var sb = new StringBuilder();
        sb.Append($"map={d.Map} time={t / 60f:0.0}min | move={Pct(d.MovingSec)} sprint={Pct(d.SprintSec)} crouch={Pct(d.CrouchSec)} prone={Pct(d.ProneSec)} ");
        sb.Append($"lean={Pct(d.LeanSec)} ads={Pct(d.AdsSec)} indoor={Pct(d.IndoorSec)} | jumps/min={perMin(d.Jumps):0.0} leanPeeks/min={perMin(d.LeanPeeks):0.0} ");
        sb.Append($"crouchToggles/min={perMin(d.CrouchToggles):0.0} | camp spots={d.CampSpots} ({d.CampSec:0}s, longest {d.LongestCampSec:0}s) | ");
        sb.Append($"shots={d.Shots} (moving {Share(d.ShotsMoving, d.Shots)}, ads {Share(d.ShotsAds, d.Shots)}, leaning {Share(d.ShotsLeaning, d.Shots)}, in air {Share(d.ShotsInAir, d.Shots)}, low {Share(d.ShotsLow, d.Shots)}) ");
        sb.Append($"reloads={d.Reloads} (empty {d.ReloadsEmpty}, avg left {(d.Reloads > 0 ? d.ReloadRoundsLeftSum / (float)d.Reloads : 0f):0.0}) grenades={d.Grenades} | ");
        sb.Append($"kills={d.Kills} avgDist={(d.Kills > 0 ? d.KillDistanceSum / d.Kills : 0f):0}m (<10:{d.KillsUnder10m} 10-30:{d.Kills10to30m} 30-80:{d.Kills30to80m} 80+:{d.KillsOver80m}) head={d.KillsHead} unseenByVictim={d.KillsUnseen} ");
        sb.Append($"victimDoing=[{string.Join(", ", FormatDict(d.KillsByVictimDecision))}]");
        if (_keys != null)
        {
            sb.Append(" | ").Append(_keys.Summary(d.Seconds));
        }
        if (d.TestSession)
        {
            sb.Append($" | TEST SESSION: {d.TestReasons}");
        }
        sb.Append($" | hits taken={d.HitsTaken} ({d.DamageTaken:0} dmg)");
        if (d.Died)
        {
            sb.Append($" | DIED {d.DeathDistance:0}m part={d.DeathPart} by={d.KilledBy} ({d.KilledByPersonality}, {d.KilledByDecision})");
        }
        return sb.ToString();
    }

    private static string Share(int part, int total)
    {
        return total > 0 ? $"{part * 100f / total:0}%" : "-";
    }

    private static IEnumerable<string> FormatDict(Dictionary<string, int> dict)
    {
        foreach (var kv in dict)
        {
            yield return $"{kv.Key} {kv.Value}";
        }
    }

    /// <summary>One raid's record. Field names are the JSON keys stage 2 will read.</summary>
    private sealed class StyleData
    {
        public int Version = 1;
        public string ProfileId;
        public string Nickname;
        public string Map;
        public string StartedUtc;
        public string EndReason;
        public float RaidStartTime;
        public float Seconds;
        public float MovingSec;
        public float StillSec;
        public float SprintSec;
        public float CrouchSec;
        public float ProneSec;
        public float LeanSec;
        public float AdsSec;
        public float IndoorSec;
        public int Jumps;
        public int LeanPeeks;
        public int CrouchToggles;
        public int SprintBursts;
        public int CampSpots;
        public float CampSec;
        public float LongestCampSec;
        public int Shots;
        public int ShotsMoving;
        public int ShotsAds;
        public int ShotsLeaning;
        public int ShotsInAir;
        public int ShotsLow;
        public int Reloads;
        public int ReloadsEmpty;
        public int ReloadRoundsLeftSum;
        public int Grenades;
        public int Kills;
        public float KillDistanceSum;
        public int KillsUnder10m;
        public int Kills10to30m;
        public int Kills30to80m;
        public int KillsOver80m;
        public int KillsHead;
        public int KillsUnseen;
        public Dictionary<string, int> KillsByVictimDecision = new();
        public bool Died;
        public float DeathDistance;
        public string DeathPart;
        public string KilledBy;
        public string KilledByPersonality;
        public string KilledByDecision;
        public KeyStats Keys;
        public bool ClassicMovement;
        public bool PlayerNoInertia;
        public bool BotsNoInertia;
        public bool BotsQuickTilt;
        // Test-session tags (DevTools god mode / infinite ammo).
        public bool TestSession;
        public string TestReasons;
        public bool GodMode;
        public bool InfiniteAmmo;
        public string DevToolsPlugins;
        public int HitsTaken;
        public float DamageTaken;
        public int ShotsNoAmmoUse;
    }
}
