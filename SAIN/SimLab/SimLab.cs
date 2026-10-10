using System;
using System.Threading.Tasks;
using EFT;
using Newtonsoft.Json;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.GlobalSettings.Categories.General;
using SAIN.Preset.Shared.SimLab;
using SPT.Common.Http;

namespace SAIN.SimLab;

/// <summary>
/// zzap fork: simulation lab, client side (user 2026-10-09). Everything in SAIN/SimLab is gated on <see cref="Active"/> -
/// the loaded preset's General > Simulation Lab > Simulation Mode, which only the bundled "zzap TEST [시뮬 전용]" preset turns on.
/// With any other preset every hook returns on its first line and the server is told "disarmed" (no sim spawns).
/// </summary>
public static class SimLab
{
    public static SimLabSettings Settings
    {
        get { return GlobalSettingsClass.Instance?.General?.SimLab; }
    }

    public static bool Active
    {
        get { return Settings?.Enabled == true; }
    }

    /// <summary>Last plan from the server (null until the first hello).</summary>
    public static SimPlan Plan;

    /// <summary>The sim raid being played (null outside a sim raid).</summary>
    public static SimRaidInfo Raid;

    /// <summary>Bots ignore this player (spectator mode). Checked by the BotsGroup.AddEnemy patch and SAIN's enemy list.</summary>
    public static bool SpectatorActive;
    /// <summary>In-game hour the last sim raid was started at (-1 = unknown).</summary>
    public static float RaidHour = -1f;
    /// <summary>Managed heap after a full GC in the menu right before the last sim raid start (MB).</summary>
    public static long MenuMonoMB = -1;
    public static string SpectatorProfileId;

    /// <summary>Set when the sim ended a raid itself: the next session result screens are skipped.</summary>
    public static bool SkipNextResults;

    private static readonly string[] EVEN = { "GigaChad", "Chad", "Wreckless", "Normal", "SnappingTurtle", "Rat", "Timmy", "Coward" };
    private static string _mixKey;
    private static readonly System.Collections.Generic.List<(SAIN.Preset.Shared.Models.Preset.Personalities.EPersonality P, float W)> _mix = new();
    private static float _mixTotal;

    /// <summary>
    /// zzap (user 2026-10-10: sim presets with a personality mix): in a sim raid whose preset sets a mix, a PMC's personality is
    /// drawn from it (weights "Chad:2,Normal:1", or "even") instead of SAIN's level / gear rules. Called first thing by
    /// PersonalityDictionary.GetPersonality after a preset's Force Personality. False = SAIN's normal assignment.
    /// </summary>
    public static bool TryPickPersonality(bool isPmc, Func<SAIN.Preset.Shared.Models.Preset.Personalities.EPersonality, bool> available,
        out SAIN.Preset.Shared.Models.Preset.Personalities.EPersonality personality)
    {
        personality = SAIN.Preset.Shared.Models.Preset.Personalities.EPersonality.Normal;
        string spec = Raid?.Personalities;
        if (!isPmc || !Active || Raid == null || !Raid.Applied || string.IsNullOrWhiteSpace(spec))
        {
            return false;
        }
        if (_mixKey != spec)
        {
            _mixKey = spec;
            _mix.Clear();
            _mixTotal = 0f;
            var parts = string.Equals(spec.Trim(), "even", StringComparison.OrdinalIgnoreCase)
                ? Array.ConvertAll(EVEN, x => x + ":1")
                : spec.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string[] kv = part.Split(':');
                float w = 1f;
                if (Enum.TryParse(kv[0].Trim(), true, out SAIN.Preset.Shared.Models.Preset.Personalities.EPersonality p)
                    && (kv.Length < 2 || float.TryParse(kv[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out w))
                    && w > 0f && available(p))
                {
                    _mix.Add((p, w));
                    _mixTotal += w;
                }
            }
        }
        if (_mix.Count == 0 || _mixTotal <= 0f)
        {
            return false;
        }
        float roll = UnityEngine.Random.value * _mixTotal;
        foreach (var (p, w) in _mix)
        {
            roll -= w;
            if (roll <= 0f)
            {
                personality = p;
                return true;
            }
        }
        personality = _mix[_mix.Count - 1].P;
        return true;
    }

    /// <summary>Raised by the TarkovApplication.OnApplicationLoaded patch every time the main menu is ready.</summary>
    public static float MenuReadyTime = -1f;

    public static bool IsSpectatorPlayer(IPlayer player)
    {
        return SpectatorActive && player != null && !player.IsAI && player.ProfileId == SpectatorProfileId;
    }

    public static string Build
    {
        get
        {
            try
            {
                return System.IO.File.GetLastWriteTime(typeof(SimLab).Assembly.Location).ToString("yyyy-MM-dd HH:mm");
            }
            catch
            {
                return "?";
            }
        }
    }

    public static string PresetName
    {
        get { return SAINPlugin.LoadedPreset?.Info?.Name ?? "?"; }
    }

    // ------------------------------------------------------------- server calls

    private static string Envelope(object payload)
    {
        return JsonConvert.SerializeObject(new { Json = JsonConvert.SerializeObject(payload) });
    }

    /// <summary>Blocking call (the local SPT server answers in milliseconds). Null on any failure.</summary>
    /// <summary>
    /// Asks MapVariants (LennoxP90) to serve the map's original version for the next raid - the same POST its map screen sends
    /// when the player picks a version. Without a pick the server keeps the variant it last installed (2026-10-09: the classic
    /// Factory backport on every sim start). Returns what the server recorded, "none" without MapVariants, null on failure.
    /// </summary>
    public static string ChooseOriginalVariant(string locationId)
    {
        try
        {
            string body = "{\"locationId\":" + JsonConvert.ToString(locationId) + ",\"variant\":\"original\"}";
            string json = RequestHandler.PostJson("/mapvariants/variant", body);
            if (string.IsNullOrWhiteSpace(json))
            {
                return "none";
            }
            var answer = Newtonsoft.Json.Linq.JObject.Parse(json);
            return answer["variant"]?.ToString() ?? "none";
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[SimLab] MapVariants choice for {locationId} not sent (not installed?): {ex.Message}");
            return null;
        }
    }

    public static T Post<T>(string path, object payload)
        where T : class
    {
        try
        {
            string json = RequestHandler.PostJson(path, Envelope(payload));
            if (string.IsNullOrEmpty(json) || json == "null")
            {
                return null;
            }
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[SimLab] {path} failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>Fire-and-forget report (minute beats) so a slow answer never stalls a frame.</summary>
    public static void PostInBackground(string path, object payload)
    {
        string body;
        try
        {
            body = Envelope(payload);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[SimLab] could not serialize {path}: {ex.Message}");
            return;
        }
        Task.Run(() =>
        {
            try
            {
                RequestHandler.PostJson(path, body);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[SimLab] {path} failed: {ex.Message}");
            }
        });
    }

    /// <summary>Tells the server whether the sim preset is loaded (arms/disarms the sim spawns) and gets the rotation plan.</summary>
    private static string _lastPlanSummary;

    public static SimPlan Hello(string reason)
    {
        var plan = Post<SimPlan>("/sain/sim/hello", new SimHello { Armed = Active, Preset = PresetName, Build = Build, Reason = reason });
        if (plan != null)
        {
            Plan = plan;
            string summary = $"{plan.Armed}|{plan.Paused}|{plan.NextMap}|{plan.Step}|{plan.RunId}|{plan.GapSeconds}";
            if (Active && (reason != "poll" || summary != _lastPlanSummary))
            {
                Logger.LogWarning(
                    $"[SimLab] hello ({reason}): armed={plan.Armed} paused={plan.Paused} next={plan.NextMap} ({plan.Step + 1}/{plan.StepCount}) {plan.Minutes:0} min '{plan.ScenarioName}' run {plan.RunId} gap {plan.GapSeconds:0}s {plan.Message}"
                );
            }
            _lastPlanSummary = summary;
        }
        return plan;
    }
}
