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
    private bool _wasReloading;
    private Vector3 _campAnchor;
    private float _campStart = -1f;

    private readonly StyleData _d = new();

    public PlayerStyleRecorder(BotManagerComponent manager)
    {
        _manager = manager;
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
        if (_player == null)
        {
            Player main = _manager.GameWorld?.MainPlayer;
            if (main == null)
            {
                return;
            }
            _player = main;
            _d.ProfileId = main.ProfileId;
            _d.Nickname = main.Profile?.Nickname;
            _d.Map = _manager.GameWorld?.LocationId;
            _player.OnPlayerDead += OnPlayerDead;
            _campAnchor = main.Position;
            _campStart = Time.time;
            _d.RaidStartTime = Time.time;
            if (_manager.GrenadeController != null)
            {
                _manager.GrenadeController.OnGrenadeThrown += OnGrenadeThrown;
            }
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
        bool reloading = fc != null && fc.IsInReloadOperation();

        _d.Seconds += dt;
        if (speed > 0.6f) _d.MovingSec += dt;
        else _d.StillSec += dt;
        if (sprinting) _d.SprintSec += dt;
        if (crouched) _d.CrouchSec += dt;
        if (prone) _d.ProneSec += dt;
        if (leaning) _d.LeanSec += dt;
        if (aiming) _d.AdsSec += dt;
        if (_player.Environment == EnvironmentType.Indoor) _d.IndoorSec += dt;

        if (_wasGrounded && !grounded && speed > 0.3f) _d.Jumps++;
        if (!_wasLeaning && leaning) _d.LeanPeeks++;
        if (_wasCrouched != crouched) _d.CrouchToggles++;
        if (!_wasSprinting && sprinting) _d.SprintBursts++;
        if (!_wasReloading && reloading)
        {
            _d.Reloads++;
            int left = fc.Item?.GetCurrentMagazineCount() ?? 0;
            if (left <= 0) _d.ReloadsEmpty++;
            _d.ReloadRoundsLeftSum += left;
        }
        _wasGrounded = grounded;
        _wasLeaning = leaning;
        _wasCrouched = crouched;
        _wasSprinting = sprinting;
        _wasReloading = reloading;

        Vector3 pos = _player.Position;
        Vector3 flat = pos - _campAnchor;
        flat.y = 0f;
        if (flat.magnitude > CAMP_RADIUS)
        {
            CloseCampSegment(time);
            _campAnchor = pos;
            _campStart = time;
        }

        float every = GlobalSettingsClass.Instance.General.PlayerStyle.LogEveryMinutes;
        if (every > 0f && time > _nextLog)
        {
            if (_nextLog > 0f)
            {
                Logger.LogWarning($"[PlayerStyle] RUNNING: {Summary()}");
            }
            _nextLog = time + every * 60f;
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
        if (_player.Velocity.magnitude > 0.6f) _d.ShotsMoving++;
        if (_shotSource != null && _shotSource.IsAiming) _d.ShotsAds++;
        if (Mathf.Abs(_player.MovementContext.Tilt) > 0.1f) _d.ShotsLeaning++;
        if (!_player.MovementContext.IsGrounded) _d.ShotsInAir++;
        if (_player.PoseLevel < 0.7f || _player.IsInPronePose) _d.ShotsLow++;
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
        try
        {
            if (_player != null)
            {
                _player.OnPlayerDead -= OnPlayerDead;
            }
            if (_shotSource != null)
            {
                _shotSource.OnShot -= OnShot;
            }
            if (_manager.GrenadeController != null)
            {
                _manager.GrenadeController.OnGrenadeThrown -= OnGrenadeThrown;
            }
            Write("raidEnd");
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
        if (_written || !Enabled || _player == null || _d.Seconds < 30f)
        {
            return;
        }
        _written = true;
        CloseCampSegment(Time.time);
        _d.EndReason = why;
        Logger.LogWarning($"[PlayerStyle] RAID SUMMARY ({why}): {Summary()}");
        try
        {
            string dir = Path.Combine(BepInEx.Paths.ConfigPath, "SAIN-zzap", "PlayerStyle");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, $"{_d.ProfileId}.jsonl");
            File.AppendAllText(file, JsonConvert.SerializeObject(_d, Formatting.None) + Environment.NewLine);
            Logger.LogWarning($"[PlayerStyle] saved to {file}");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[PlayerStyle] could not save: {ex.Message}");
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
    }
}
