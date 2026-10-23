using System.Text.Json;
using System.Text.Json.Nodes;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.SimLab;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Json;

namespace SAINServerMod.SimLab;

/// <summary>
/// zzap remake5 cross-check (user 2026-10-11): sim raids used to have only the two PMC sides, so anything that only goes wrong for
/// rogues, raiders, bosses or faction mods (the "PMC only" switch left them on vanilla close combat) never showed in a sim.
///   - faction squads (rogues, raiders) as extra reserve waves - the game's boss spawner makes them (ABPS's hook only takes PMC waves)
///   - one random boss per raid with its followers, from the server's own location data (what SPT/ABPS put there) or a built-in
///     template; zone = the sim zone, so it shows up in the arena (Factory: BotZone)
///   - waves of modded factions (Black Division...) are kept when the map's spawns are cleared
///   - hostility: every sim role is an enemy of every other sim role (a boss and its own followers stay friends)
/// Client side: SimLabRunner picks these waves up by their Time (FactionTimeBase / BossTime).
/// </summary>
public static class SimLabFactions
{
    public const float FACTION_TIME_OFFSET = 1000f;
    public const float BOSS_TIME_OFFSET = 2000f;
    private const int FACTION_WAVES_PER_SIDE = 15;
    private const float MODDED_CHECK_SHARE = 0.5f;

    /// <summary>Leader bosses a sim raid may roll (followers come with them). Zryachiy is never on SAIN (kept vanilla), so not here.</summary>
    public static readonly string[] BossPool =
    [
        "bossBully", "bossKilla", "bossKojaniy", "bossGluhar", "bossSanitar", "bossTagilla", "bossKnight", "bossBoar", "bossKolontay", "bossPartisan",
    ];

    private static readonly string[] Maps =
    [
        "bigmap", "factory4_day", "factory4_night", "interchange", "laboratory", "lighthouse", "rezervbase", "shoreline", "tarkovstreets",
        "woods", "sandbox", "sandbox_high", "labyrinth",
    ];

    private static readonly HashSet<string> VanillaRoles = new(Enum.GetNames(typeof(ESainWildSpawnType)), StringComparer.OrdinalIgnoreCase);

    private static string _lastBoss = string.Empty;

    /// <summary>zzap (2026-10-23) modded bot check: role -> the mod's section; roles of one section are one family (friends).</summary>
    public static Dictionary<string, string> ModdedFamilies = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Random _rng = new();

    /// <summary>Waves of roles EFT doesn't know (custom factions from MoreBotsAPI-style mods) - kept when the map is cleared.</summary>
    public static bool IsModdedWave(BossLocationSpawn wave)
    {
        string name = wave?.BossName ?? string.Empty;
        return name.Length > 0 && !VanillaRoles.Contains(name);
    }

    public static void AddFactionWaves(LocationBase location, SimRaidInfo info, SimLabConfig config, string escorts, List<string>? moddedRoles = null)
    {
        var roles = (config.FactionRoles ?? []).Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Distinct().ToList();
        float share = config.FactionShare;
        // zzap (2026-10-23) modded bot check: the installed modded types instead, on half of the refills.
        if (config.ModdedBotCheckInSim && moddedRoles is { Count: > 0 })
        {
            roles = moddedRoles;
            share = MODDED_CHECK_SHARE;
        }
        if (!config.FactionMix || roles.Count == 0 || share <= 0f)
        {
            return;
        }
        info.FactionTimeBase = SimLabService.RESERVE_TIME_BASE + FACTION_TIME_OFFSET;
        info.FactionShare = Math.Clamp(share, 0f, 1f);
        info.FactionRoles = string.Join(",", roles);
        int count = FACTION_WAVES_PER_SIDE * 2;
        for (int i = 0; i < count; i++)
        {
            bool sideA = i % 2 == 0;
            string role = roles[(i / 2) % roles.Count];
            location.BossLocationSpawn!.Add(Wave(role, role, escorts, sideA ? info.ZonesA : info.ZonesB, info.FactionTimeBase + i));
        }
        info.FactionWaves = count;
    }

    public static void AddBoss(LocationTable locations, LocationBase location, SimRaidInfo info, SimLabConfig config)
    {
        if (!config.RandomBoss)
        {
            return;
        }
        var pool = BossPool.Where(b => b != _lastBoss).ToList();
        string boss = pool[_rng.Next(pool.Count)];
        _lastBoss = boss;
        BossLocationSpawn? template = null;
        string source = "builtin";
        foreach (string map in Maps)
        {
            var waves = locations.GetLocation(map)?.Base?.BossLocationSpawn;
            if (waves == null)
            {
                continue;
            }
            // Prefer a wave without a trigger (door / switch) - those never fire in a sim.
            var found = waves.Where(w => string.Equals(w?.BossName, boss, StringComparison.OrdinalIgnoreCase))
                .OrderBy(w => string.IsNullOrEmpty(w.TriggerName) ? 0 : 1)
                .FirstOrDefault();
            if (found != null && (template == null || (!string.IsNullOrEmpty(template.TriggerName) && string.IsNullOrEmpty(found.TriggerName))))
            {
                template = found;
                source = "database:" + map;
            }
        }
        string zone = _rng.Next(2) == 0 ? info.ZonesA : info.ZonesB;
        BossLocationSpawn wave;
        if (template != null)
        {
            wave = template with
            {
                BossChance = 100,
                BossZone = zone,
                Time = SimLabService.RESERVE_TIME_BASE + BOSS_TIME_OFFSET,
                TriggerId = string.Empty,
                TriggerName = string.Empty,
                Delay = 0,
                IsRandomTimeSpawn = false,
                ForceSpawn = true,
                IgnoreMaxBots = true,
                DependKarma = false,
                DependKarmaPVE = false,
                SpawnMode = ["regular", "pve"],
                ShowOnTarkovMap = false,
                ShowOnTarkovMapPvE = false,
            };
        }
        else
        {
            wave = Builtin(boss, zone);
        }
        location.BossLocationSpawn!.Add(wave);
        info.Boss = boss;
        info.BossDelay = Math.Clamp(config.BossDelaySeconds, 0, 900);
        info.BossTime = (float)(wave.Time ?? 0d);
        info.BossSource = source;
        var escorts = new List<string>();
        if (!string.IsNullOrEmpty(wave.BossEscortType) && wave.BossEscortAmount != "0")
        {
            escorts.Add(wave.BossEscortType);
        }
        foreach (var s in wave.Supports ?? [])
        {
            if (!string.IsNullOrEmpty(s?.BossEscortType) && s.BossEscortAmount != "0")
            {
                escorts.Add(s.BossEscortType!);
            }
        }
        info.BossEscorts = string.Join(",", escorts.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Fallback when no location has the boss (followers as plain escorts).</summary>
    private static BossLocationSpawn Builtin(string boss, string zone)
    {
        (string escort, string amount) = boss switch
        {
            "bossBully" => ("followerBully", "4"),
            "bossKojaniy" => ("followerKojaniy", "2"),
            "bossGluhar" => ("followerGluharAssault", "3"),
            "bossSanitar" => ("followerSanitar", "2"),
            "bossKnight" => ("followerBigPipe", "1"),
            "bossBoar" => ("followerBoar", "3"),
            "bossKolontay" => ("followerKolontayAssault", "2"),
            _ => ("followerBully", "0"),
        };
        var wave = Wave(boss, escort, amount, zone, SimLabService.RESERVE_TIME_BASE + BOSS_TIME_OFFSET);
        if (boss == "bossKnight")
        {
            wave = wave with { Supports = [new BossSupport { BossEscortType = "followerBigPipe", BossEscortAmount = "1", BossEscortDifficulty = new(null, "normal") }, new BossSupport { BossEscortType = "followerBirdEye", BossEscortAmount = "1", BossEscortDifficulty = new(null, "normal") }] };
        }
        return wave;
    }

    private static BossLocationSpawn Wave(string role, string escortType, string escorts, string zone, double time)
    {
        return new BossLocationSpawn
        {
            BossChance = 100,
            BossDifficulty = "normal",
            BossEscortAmount = escorts,
            BossEscortDifficulty = "normal",
            BossEscortType = escortType,
            BossName = role,
            IsBossPlayer = false,
            BossZone = zone,
            IsRandomTimeSpawn = false,
            ShowOnTarkovMap = false,
            ShowOnTarkovMapPvE = false,
            Time = time,
            TriggerId = string.Empty,
            TriggerName = string.Empty,
            Delay = 0,
            DependKarma = false,
            DependKarmaPVE = false,
            ForceSpawn = true,
            IgnoreMaxBots = true,
            Supports = null!,
            SpawnMode = ["regular", "pve"],
        };
    }

    /// <summary>Sim roles grouped into "families" that stay friends (a boss and its followers); everyone else is an enemy.</summary>
    public static List<HashSet<string>> Families(SimRaidInfo raid)
    {
        var families = new List<HashSet<string>>();
        void Add(params string[] roles)
        {
            var set = new HashSet<string>(roles.Where(r => !string.IsNullOrWhiteSpace(r)), StringComparer.OrdinalIgnoreCase);
            if (set.Count > 0)
            {
                families.Add(set);
            }
        }
        Add(raid.SideA);
        if (!string.Equals(raid.SideA, raid.SideB, StringComparison.OrdinalIgnoreCase))
        {
            Add(raid.SideB);
        }
        var moddedFamilies = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (string r in Split(raid.FactionRoles))
        {
            if (ModdedFamilies.TryGetValue(r, out string? section) && section.Length > 0)
            {
                if (!moddedFamilies.TryGetValue(section, out var members))
                {
                    moddedFamilies[section] = members = [];
                }
                members.Add(r);
                continue;
            }
            Add(r);
        }
        foreach (var members in moddedFamilies.Values)
        {
            Add([.. members]);
        }
        if (!string.IsNullOrEmpty(raid.Boss))
        {
            Add([raid.Boss, .. Split(raid.BossEscorts)]);
        }
        return families;
    }

    public static IEnumerable<string> Split(string? list)
    {
        return (list ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Every sim role hostile to every other sim role (own family excluded) through the location's AdditionalHostilitySettings,
    /// which EFT reads per bot role. PMC roles keep the old rule too (own faction enemy chance 100). Returns a short summary.
    /// </summary>
    public static string ApplyHostility(LocationBase location, SimRaidInfo raid)
    {
        var modifier = location?.BotLocationModifier;
        if (modifier == null)
        {
            return "no BotLocationModifier";
        }
        var families = Families(raid);
        var all = new HashSet<string>(families.SelectMany(f => f), StringComparer.OrdinalIgnoreCase);
        var list = (modifier.AdditionalHostilitySettings ?? []).ToList();
        var touched = new List<string>();
        foreach (var family in families)
        {
            foreach (string role in family)
            {
                var others = all.Where(r => !family.Contains(r)).ToList();
                var entry = list.FirstOrDefault(e => string.Equals(e?.BotRole, role, StringComparison.OrdinalIgnoreCase));
                bool created = false;
                if (entry == null)
                {
                    entry = new AdditionalHostilitySettings
                    {
                        BotRole = role,
                        AlwaysEnemies = [],
                        AlwaysFriends = [],
                        Neutral = [],
                        Warn = [],
                        ChancedEnemies = [],
                        BearEnemyChance = 100,
                        UsecEnemyChance = 100,
                        SavageEnemyChance = 100,
                        BearPlayerBehaviour = "AlwaysEnemies",
                        UsecPlayerBehaviour = "AlwaysEnemies",
                        SavagePlayerBehaviour = "AlwaysEnemies",
                    };
                    list.Add(entry);
                    created = true;
                }
                entry.AlwaysEnemies ??= [];
                foreach (string o in others)
                {
                    entry.AlwaysEnemies.Add(o);
                    entry.AlwaysFriends?.Remove(o);
                    entry.Neutral?.Remove(o);
                }
                if (entry.Warn != null)
                {
                    entry.Warn = entry.Warn.Where(w => !others.Contains(w, StringComparer.OrdinalIgnoreCase)).ToList();
                }
                entry.ChancedEnemies?.RemoveAll(c => c?.Role != null && others.Contains(c.Role, StringComparer.OrdinalIgnoreCase));
                bool pmc = role.Equals("pmcUSEC", StringComparison.OrdinalIgnoreCase) || role.Equals("pmcBEAR", StringComparison.OrdinalIgnoreCase);
                if (pmc)
                {
                    entry.UsecEnemyChance = 100;
                    entry.BearEnemyChance = 100;
                    entry.AlwaysFriends?.Remove("pmcUSEC");
                    entry.AlwaysFriends?.Remove("pmcBEAR");
                }
                touched.Add(created ? role + "(new)" : role);
            }
        }
        modifier.AdditionalHostilitySettings = list;
        return string.Join("/", touched);
    }

    /// <summary>
    /// Keys where the sim preset differs from zzap that the client-side cross-check does NOT reset (it resets the six behaviour
    /// sections wholesale, logging switches excluded). Anything listed here means a sim result may not hold for zzap users.
    /// </summary>
    public static string UncoveredPresetDiff(string presetsDir)
    {
        try
        {
            if (!Directory.Exists(presetsDir))
            {
                return string.Empty;
            }
            var files = Directory.GetFiles(presetsDir, "*.json");
            // The bundled file first: saving it from the web/F6 now makes "... [Modified].json" copies that also contain "TEST".
            string? test = files.FirstOrDefault(f => string.Equals(Path.GetFileName(f), "zzap TEST [시뮬 전용].json", StringComparison.Ordinal))
                ?? files.FirstOrDefault(f => Path.GetFileName(f).Contains("TEST", StringComparison.Ordinal));
            string? zzap = files.FirstOrDefault(f => string.Equals(Path.GetFileName(f), "zzap.json", StringComparison.OrdinalIgnoreCase));
            if (test == null || zzap == null)
            {
                return string.Empty;
            }
            var a = JsonNode.Parse(File.ReadAllText(test));
            var b = JsonNode.Parse(File.ReadAllText(zzap));
            var diffs = new List<string>();
            Walk(a, b, string.Empty, diffs);
            return string.Join(", ", diffs.Take(30));
        }
        catch (Exception ex)
        {
            return "check failed: " + ex.Message;
        }
    }

    private static readonly string[] ResetSections =
    [
        "/GlobalSettings/General/CloseCombat", "/GlobalSettings/General/DoorTactics", "/GlobalSettings/General/Reposition",
        "/GlobalSettings/General/PlayerStyle", "/GlobalSettings/General/FreezeAmbush", "/GlobalSettings/General/SquadCombat",
    ];

    private static readonly string[] IgnoredWords = ["Diagnostic", "Verbose", "Journal", "RecordKeys", "SimLab", "SimCorpse", "Log", "/Info/"];

    private static void Walk(JsonNode? a, JsonNode? b, string path, List<string> diffs)
    {
        if (IgnoredWords.Any(w => path.Contains(w, StringComparison.Ordinal)) || ResetSections.Any(s => path.StartsWith(s, StringComparison.Ordinal)))
        {
            return;
        }
        // A whole bot type the zzap file doesn't list (10/12 run: Black Division 848420-848426): the TEST preset was exported
        // after BD support was added, so it carries those types written out at their code defaults - which is what zzap users
        // get too. Not a behaviour difference; a type zzap.json DOES list is still compared field by field.
        if (b == null && path.StartsWith("/BotSettings/", StringComparison.Ordinal) && path.IndexOf('/', "/BotSettings/".Length) < 0)
        {
            return;
        }
        if (a is JsonObject oa && b is JsonObject ob)
        {
            foreach (var key in oa.Select(kv => kv.Key).Union(ob.Select(kv => kv.Key)))
            {
                Walk(oa[key], ob[key], path + "/" + key, diffs);
            }
            return;
        }
        string sa = a?.ToJsonString() ?? "null";
        string sb = b?.ToJsonString() ?? "null";
        // 2.0 vs 2: the same number written differently (2026-10-11 run listed 30 such "differences")
        if (a is JsonValue va && b is JsonValue vb && double.TryParse(sa, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double da)
            && double.TryParse(sb, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double db) && Math.Abs(da - db) < 1e-6)
        {
            return;
        }
        if (sa != sb)
        {
            diffs.Add(b == null ? $"{path}={Short(sa)} (zzap: 코드 기본값)" : $"{path}={Short(sa)} (zzap {Short(sb)})");
        }
    }

    private static string Short(string s)
    {
        return s.Length > 40 ? s[..40] + "…" : s;
    }
}
