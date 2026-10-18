using System;
using System.Collections.Generic;
using System.Linq;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using SAIN.Preset.Shared.GlobalSettings;

namespace SAIN.Preset.Shared.Preset;

/// <summary>A preset seen by the quick settings: its global settings and its bot types (by WildSpawnType name).</summary>
public sealed class QuickPresetView
{
    public GlobalSettingsClass Global;
    public IEnumerable<KeyValuePair<string, SAINSettingsGroupClass>> Bots = Array.Empty<KeyValuePair<string, SAINSettingsGroupClass>>();

    public QuickPresetView(GlobalSettingsClass global, IEnumerable<KeyValuePair<string, SAINSettingsGroupClass>> bots)
    {
        Global = global;
        Bots = bots ?? Array.Empty<KeyValuePair<string, SAINSettingsGroupClass>>();
    }
}

public enum QuickKind
{
    Number,
    Toggle,
}

/// <summary>One quick setting: a value (toggles are 0/1) that may write to several preset fields at once.</summary>
public sealed class QuickSetting
{
    public string Id;
    public string Group;
    public string NameKo;
    public string NameEn;
    public string HelpKo;
    public string HelpEn;
    public QuickKind Kind;
    public float Min;
    public float Max = 1f;
    public float Step = 0.05f;
    public Func<QuickPresetView, float> Get;
    public Action<QuickPresetView, float> Set;
    /// <summary>The bundled zzap preset's value (reference tick, "x of zzap" text).</summary>
    public float ZzapValue;
    /// <summary>False when a bigger number makes bots weaker (recoil, aim time, scatter): sliders are drawn reversed so that
    /// right always means "stronger bots" (user 2026-10-18: "the numbers are too hard to read").</summary>
    public bool HigherIsStronger = true;
    /// <summary>The value in plain words, e.g. "noticeable kick · 8x zzap".</summary>
    public Func<float, bool, string> Describe;

    public string Name(bool korean) => korean ? NameKo : NameEn;

    /// <summary>Slider position for a value: reversed for "lower = stronger" settings.</summary>
    public float ToSlider(float value) => HigherIsStronger ? value : Min + Max - value;

    public float FromSlider(float position) => HigherIsStronger ? position : Min + Max - position;

    public float Round(float value)
    {
        if (Step <= 0f)
        {
            return value;
        }
        return (float)Math.Round(Math.Round(value / Step) * Step, 3);
    }

    public string Text(float value, bool korean)
    {
        return Describe != null ? Describe(value, korean) : value.ToString("0.##");
    }

    public string Help(bool korean) => korean ? HelpKo : HelpEn;
}

/// <summary>A bundled zzap preset as shown on the web page and in F6: difficulty level (1-3), a tag and a one-line summary.</summary>
public sealed class ZzapPresetCard
{
    public string Name;
    public int Level;
    public string TagKo;
    public string TagEn;
    public string Ko;
    public string En;
    /// <summary>Kept for whoever picked it, but replaced by the quick "Low-spec PC" switch (shown greyed).</summary>
    public bool Legacy;

    public string Tag(bool korean) => korean ? TagKo : TagEn;

    public string Summary(bool korean) => korean ? Ko : En;
}

/// <summary>One part of the low-spec bundle (user 2026-10-18: "replace the low-spec preset with a performance switch, so any
/// difficulty can be turned low-spec quickly"). Same values the old "zzap 저사양 [테스트]" preset wrote.</summary>
public sealed class LowSpecItem
{
    public string NameKo;
    public string NameEn;
    public Func<QuickPresetView, bool> IsLowSpec;
    public Action<QuickPresetView> Apply;
    public Action<QuickPresetView, QuickPresetView> Restore;
    /// <summary>The normal (not low-spec) zzap values, used when the reference is itself low-spec.</summary>
    public Action<QuickPresetView> Off;

    public string Name(bool korean) => korean ? NameKo : NameEn;
}

/// <summary>
/// zzap fork: the handful of values people change most, shared by the web preset page and the F6 editor
/// (user 2026-10-18: "make preset settings easier to read and set"). Each one maps onto the normal preset fields,
/// so the full editors still show the same values.
/// </summary>
public static class QuickSettings
{
    /// <summary>Bot types that count as PMC-like for the per-bot aim / fire-rate rows (same list as tools/presets/make_variants.py).</summary>
    public static readonly HashSet<string> PmcLike = new(StringComparer.OrdinalIgnoreCase)
    {
        "pmcUSEC", "pmcBEAR", "exUsec", "pmcBot", "arenaFighter", "arenaFighterEvent",
    };

    public static readonly string[] Groups = { "aim", "sense", "tactics", "log" };

    /// <summary>The bundled zzap presets in difficulty order (same list as PresetHandler's ordering and tools/presets/make_variants.py).</summary>
    public static readonly ZzapPresetCard[] ZzapPresets =
    {
        new() { Name = "zzap 쉬움", Level = 1, Ko = "처음 하거나 편하게. 조준 2초 · 시야 SAIN의 ½ · 반동 큼", En = "New or relaxed. Aim 2 s · sight ½ of SAIN · heavy recoil" },
        new() { Name = "zzap 보통", Level = 2, Ko = "쉬움과 zzap의 중간. 조준 0.6초 · 시야 1.4배 · 연사 zzap의 75%", En = "Halfway to zzap. Aim 0.6 s · sight 1.4x · 75% of zzap's fire rate" },
        new() { Name = "zzap", Level = 3, TagKo = "추천", TagEn = "pick", Ko = "하드코어. 조준 0.3초 · 시야 2배 · 반동 거의 없음 · 전술 전부", En = "Hardcore. Aim 0.3 s · sight 2x · almost no recoil · every tactic" },
        new() { Name = "zzap 저사양 [테스트]", Level = 2, TagKo = "예전 방식", TagEn = "old way", Legacy = true, Ko = "보통 + 성능 설정. 이제 '빠른 설정 › 저사양 PC' 스위치로 어느 난이도든 같은 효과", En = "Normal + performance settings. Now any difficulty can do this with 'Quick settings › Low-spec PC'" },
        new() { Name = "zzap TEST [시뮬 전용]", Level = 3, TagKo = "시뮬 전용", TagEn = "sim only", Ko = "봇끼리 시뮬레이션용. 실제 플레이엔 쓰지 마세요", En = "For bot-vs-bot simulation. Not for real raids" },
    };

    public static ZzapPresetCard FindZzap(string name) => ZzapPresets.FirstOrDefault(z => z.Name == name);

    public static string GroupName(string group, bool korean)
    {
        return group switch
        {
            "aim" => korean ? "사격 · 조준" : "Shooting · aim",
            "sense" => korean ? "시야 · 청각" : "Sight · hearing",
            "tactics" => korean ? "zzap 전술 빈도" : "zzap tactic frequency",
            "log" => korean ? "기록 · 제보용" : "Logs · for bug reports",
            _ => group,
        };
    }

    private static IEnumerable<SAINSettingsClass> Settings(QuickPresetView v, bool pmcOnly)
    {
        foreach (var kv in v.Bots)
        {
            if (kv.Value?.Settings == null || (pmcOnly && !PmcLike.Contains(kv.Key)))
            {
                continue;
            }
            foreach (var s in kv.Value.Settings.Values)
            {
                if (s != null)
                {
                    yield return s;
                }
            }
        }
    }

    // Per-bot rows show the first PMC's value (zzap presets give every PMC the same one) and write to all of them.
    private static float FirstPmc(QuickPresetView v, Func<SAINSettingsClass, float> get, float fallback)
    {
        var first = v.Bots.Where(kv => string.Equals(kv.Key, "pmcUSEC", StringComparison.OrdinalIgnoreCase)).Concat(v.Bots.Where(kv => PmcLike.Contains(kv.Key)))
            .Select(kv => kv.Value?.Settings?.Values.FirstOrDefault(s => s != null)).FirstOrDefault(s => s != null);
        return first == null ? fallback : get(first);
    }

    private static bool On(float value) => value >= 0.5f;

    private static string Num(float v) => Math.Round(v, 2).ToString("0.##");

    /// <summary>"zzap의 75%" / "zzap과 같음" / "zzap의 8배".</summary>
    private static string VsZzap(float v, float zzap, bool korean)
    {
        if (zzap <= 0f)
        {
            return string.Empty;
        }
        float r = v / zzap;
        if (Math.Abs(r - 1f) < 0.03f)
        {
            return korean ? "zzap과 같음" : "same as zzap";
        }
        if (r >= 1.5f)
        {
            return korean ? $"zzap의 {Num(r)}배" : $"{Num(r)}x zzap";
        }
        return korean ? $"zzap의 {r * 100f:0}%" : $"{r * 100f:0}% of zzap";
    }

    private static string Recoil(float v, bool k)
    {
        string word = v <= 0.15f ? (k ? "거의 안 튐" : "barely kicks")
            : v <= 0.5f ? (k ? "조금 튐" : "slight kick")
            : v <= 1.2f ? (k ? "꽤 튐" : "noticeable kick")
            : v <= 2f ? (k ? "많이 튐" : "strong kick")
            : (k ? "아주 많이 튐" : "very strong kick");
        return word + " · " + VsZzap(v, 0.1f, k);
    }

    private static string Scatter(float v, bool k)
    {
        if (Math.Abs(v - 1f) < 0.03f)
        {
            return k ? "기본 퍼짐 (zzap과 같음)" : "normal spread (same as zzap)";
        }
        return v > 1f
            ? (k ? $"{Num(v)}배 더 퍼짐 (덜 맞음)" : $"{Num(v)}x wider (fewer hits)")
            : (k ? $"덜 퍼짐 ({v * 100f:0}%, 더 잘 맞음)" : $"tighter ({v * 100f:0}%, more hits)");
    }

    public static readonly QuickSetting[] All =
    {
        new()
        {
            Id = "recoil", Group = "aim", Kind = QuickKind.Number, Min = 0.01f, Max = 3f, Step = 0.05f,
            ZzapValue = 0.1f, HigherIsStronger = false, Describe = Recoil,
            NameKo = "반동 크기", NameEn = "Recoil",
            HelpKo = "전역 반동 배율. 클수록 봇 총이 많이 튐 (zzap 0.1, 쉬움 3.0)", HelpEn = "Global recoil multiplier. Higher = bots' guns kick more (zzap 0.1, easy 3.0)",
            Get = v => v.Global.Shoot.BOT_RECOIL_COEF, Set = (v, x) => v.Global.Shoot.BOT_RECOIL_COEF = x,
        },
        new()
        {
            Id = "aimTime", Group = "aim", Kind = QuickKind.Number, Min = 0.1f, Max = 3f, Step = 0.05f,
            ZzapValue = 0.3f, HigherIsStronger = false, Describe = (v, k) => (k ? $"{Num(v)}초 만에 조준 끝 · " : $"aims in {Num(v)} s · ") + (k ? "zzap 0.3초" : "zzap 0.3 s"),
            NameKo = "조준 시간 (PMC, 초)", NameEn = "Aim time (PMC, s)",
            HelpKo = "PMC가 조준을 끝내는 최대 시간. 작을수록 빨리 맞힘 — 모든 PMC 종류·난이도에 같이 적용", HelpEn = "Longest time a PMC takes to settle its aim. Lower = hits sooner - written to every PMC type and difficulty",
            Get = v => FirstPmc(v, s => s.Aiming.MAX_AIM_TIME, 1f),
            Set = (v, x) => { foreach (var s in Settings(v, true)) { s.Aiming.MAX_AIM_TIME = x; } },
        },
        new()
        {
            Id = "fireRate", Group = "aim", Kind = QuickKind.Number, Min = 0.25f, Max = 3f, Step = 0.05f,
            ZzapValue = 3f, Describe = (v, k) => (k ? "연사 속도 " : "fire rate ") + VsZzap(v, 3f, k),
            NameKo = "연사 속도 (PMC)", NameEn = "Fire rate (PMC)",
            HelpKo = "PMC 발사 간격 배율(점사 길이도 같이). 클수록 빨리 쏨 — 모든 PMC 종류·난이도에 같이 적용", HelpEn = "PMC fire rate multiplier (burst length follows). Higher = faster - written to every PMC type and difficulty",
            Get = v => FirstPmc(v, s => s.Shoot.FireratMulti, 1.5f),
            Set = (v, x) => { foreach (var s in Settings(v, true)) { s.Shoot.FireratMulti = x; s.Shoot.BurstMulti = x; } },
        },
        new()
        {
            Id = "scatter", Group = "aim", Kind = QuickKind.Number, Min = 0.1f, Max = 3f, Step = 0.05f,
            ZzapValue = 1f, HigherIsStronger = false, Describe = Scatter,
            NameKo = "탄 퍼짐", NameEn = "Bullet scatter",
            HelpKo = "전역 탄 퍼짐 배율. 클수록 빗나감", HelpEn = "Global scatter multiplier. Higher = more misses",
            Get = v => v.Global.Difficulty.ScatteringCoef, Set = (v, x) => v.Global.Difficulty.ScatteringCoef = x,
        },
        new()
        {
            Id = "sight", Group = "sense", Kind = QuickKind.Number, Min = 0.25f, Max = 3f, Step = 0.05f,
            ZzapValue = 2f, Describe = (v, k) => (k ? $"SAIN 기본의 {Num(v)}배 거리 · " : $"{Num(v)}x SAIN default range · ") + VsZzap(v, 2f, k),
            NameKo = "시야 거리 배율", NameEn = "Sight distance",
            HelpKo = "봇이 볼 수 있는 거리 (SAIN 기본 1, zzap 2)", HelpEn = "How far bots can see (SAIN default 1, zzap 2)",
            Get = v => v.Global.Difficulty.VisibleDistCoef, Set = (v, x) => v.Global.Difficulty.VisibleDistCoef = x,
        },
        new()
        {
            Id = "gainSight", Group = "sense", Kind = QuickKind.Number, Min = 0.2f, Max = 2f, Step = 0.05f,
            ZzapValue = 1f, Describe = (v, k) => (k ? "적을 알아채는 속도 " : "spots enemies at ") + VsZzap(v, 1f, k),
            NameKo = "발견 속도 배율", NameEn = "Spotting speed",
            HelpKo = "보인 적을 알아채는 속도. 작을수록 늦게 발견", HelpEn = "How fast a visible enemy is noticed. Lower = later",
            Get = v => v.Global.Difficulty.GainSightCoef, Set = (v, x) => v.Global.Difficulty.GainSightCoef = x,
        },
        new()
        {
            Id = "hearing", Group = "sense", Kind = QuickKind.Number, Min = 0.2f, Max = 2f, Step = 0.05f,
            ZzapValue = 1f, Describe = (v, k) => (k ? "소리 듣는 거리 " : "hearing range ") + VsZzap(v, 1f, k),
            NameKo = "청각 거리 배율", NameEn = "Hearing distance",
            HelpKo = "발소리·총소리를 듣는 거리", HelpEn = "How far footsteps and shots are heard",
            Get = v => v.Global.Difficulty.HearingDistanceCoef, Set = (v, x) => v.Global.Difficulty.HearingDistanceCoef = x,
        },
        new()
        {
            Id = "fov", Group = "sense", Kind = QuickKind.Number, Min = 90f, Max = 180f, Step = 5f,
            ZzapValue = 180f, Describe = (v, k) => k ? $"좌우 {v:0}°를 봄 (zzap 180°)" : $"sees {v:0}° side to side (zzap 180°)",
            NameKo = "시야각 (도)", NameEn = "Field of view (deg)",
            HelpKo = "봇이 보는 좌우 범위 — 모든 봇 종류·난이도에 같이 적용", HelpEn = "Bots' left-right view - written to every bot type and difficulty",
            Get = v => FirstPmc(v, s => s.Core.VisibleAngle, 160f),
            Set = (v, x) => { foreach (var s in Settings(v, false)) { s.Core.VisibleAngle = x; } },
        },
        new()
        {
            Id = "doorMulti", Group = "tactics", Kind = QuickKind.Number, Min = 0f, Max = 2f, Step = 0.05f,
            ZzapValue = 1f, Describe = (v, k) => (k ? "문 전술 빈도 " : "door tactics ") + (v <= 0.001f ? (k ? "안 씀" : "never") : VsZzap(v, 1f, k)),
            NameKo = "문 전술 배율", NameEn = "Door tactic multiplier",
            HelpKo = "문 피킹·방 진입·가두기 시작 확률에 곱함", HelpEn = "Multiplies the start chance of door peeks, room clears and traps",
            Get = v => v.Global.General.DoorTactics.ChanceMultiplier, Set = (v, x) => v.Global.General.DoorTactics.ChanceMultiplier = x,
        },
        new()
        {
            Id = "ramClear", Group = "tactics", Kind = QuickKind.Number, Min = 0f, Max = 100f, Step = 5f,
            ZzapValue = 50f, Describe = (v, k) => k ? $"방에 들어갈 때 {v:0}% 확률로 문을 박차 엶 (zzap {50}%)" : $"rams the door on {v:0}% of room entries (zzap 50%)",
            NameKo = "문 박치기 (방 진입 %)", NameEn = "Door ram (room clear %)",
            HelpKo = "방에 들어갈 때 닫힌 문을 달려서 박차 열 확률", HelpEn = "Chance to sprint-ram a closed door when clearing a room",
            Get = v => v.Global.General.DoorTactics.DoorModRamChance, Set = (v, x) => v.Global.General.DoorTactics.DoorModRamChance = x,
        },
        new()
        {
            Id = "prefire", Group = "tactics", Kind = QuickKind.Number, Min = 0f, Max = 100f, Step = 5f,
            ZzapValue = 60f, Describe = (v, k) => k ? $"코너 돌기 전 {v:0}% 확률로 미리 쏨 (zzap {60}%)" : $"fires before the corner {v:0}% of the time (zzap 60%)",
            NameKo = "코너 프리파이어 %", NameEn = "Corner prefire %",
            HelpKo = "코너를 돌기 전에 미리 쏠 확률", HelpEn = "Chance to fire before rounding a corner",
            Get = v => v.Global.General.CloseCombat.CornerChasePrefireChance, Set = (v, x) => v.Global.General.CloseCombat.CornerChasePrefireChance = x,
        },
        new()
        {
            Id = "squadStorm", Group = "tactics", Kind = QuickKind.Toggle,
            ZzapValue = 1f,
            NameKo = "분대 일제 돌격", NameEn = "Squad storm",
            HelpKo = "적이 약해 보이면 분대가 한꺼번에 밀고 들어감", HelpEn = "The squad rushes together when the enemy looks weak",
            Get = v => v.Global.General.SquadCombat.SquadStorm ? 1f : 0f, Set = (v, x) => v.Global.General.SquadCombat.SquadStorm = On(x),
        },
        new()
        {
            Id = "journal", Group = "log", Kind = QuickKind.Toggle,
            ZzapValue = 0f,
            NameKo = "레이드 일지", NameEn = "Raid journal",
            HelpKo = "BepInEx/config/SAIN-zzap/Journal 에 레이드마다 기록 — 버그 제보할 때만 켜기", HelpEn = "One file per raid in BepInEx/config/SAIN-zzap/Journal - only needed for bug reports",
            Get = v => v.Global.General.PlayerStyle.RaidJournal ? 1f : 0f, Set = (v, x) => v.Global.General.PlayerStyle.RaidJournal = On(x),
        },
        new()
        {
            Id = "diag", Group = "log", Kind = QuickKind.Toggle,
            ZzapValue = 0f,
            NameKo = "진단 로그", NameEn = "Diagnostic logs",
            HelpKo = "문 전술·분대 교전·재배치·정지 및 매복 기록을 LogOutput.log에 — 제보 끝나면 끄기", HelpEn = "Door / squad / reposition / freeze logs in LogOutput.log - switch off after the report",
            Get = v => v.Global.General.DoorTactics.DiagnosticLogs || v.Global.General.SquadCombat.DiagnosticLogs || v.Global.General.Reposition.DiagnosticLogs || v.Global.General.FreezeAmbush.DiagnosticLogs ? 1f : 0f,
            Set = (v, x) =>
            {
                bool on = On(x);
                v.Global.General.DoorTactics.DiagnosticLogs = on;
                v.Global.General.SquadCombat.DiagnosticLogs = on;
                v.Global.General.Reposition.DiagnosticLogs = on;
                v.Global.General.FreezeAmbush.DiagnosticLogs = on;
            },
        },
    };

    public static QuickSetting Find(string id) => All.FirstOrDefault(q => q.Id == id);

    public static readonly LowSpecItem[] LowSpec =
    {
        new()
        {
            NameKo = "성능 모드 (봇 계산 주기를 늘림)", NameEn = "Performance mode (longer bot update intervals)",
            IsLowSpec = v => v.Global.General.Performance.PerformanceMode,
            Apply = v => v.Global.General.Performance.PerformanceMode = true,
            Restore = (v, r) => v.Global.General.Performance.PerformanceMode = r.Global.General.Performance.PerformanceMode,
            Off = v => v.Global.General.Performance.PerformanceMode = false,
        },
        new()
        {
            NameKo = "멀리 있는 봇끼리 시야·청각·업데이트 줄이기", NameEn = "Limit far-away bot vs bot sight / hearing / updates",
            IsLowSpec = v => v.Global.General.AILimit.LimitAIvsAIGlobal && v.Global.General.AILimit.LimitAIvsAIVision && v.Global.General.AILimit.LimitAIvsAIHearing,
            Apply = v =>
            {
                v.Global.General.AILimit.LimitAIvsAIGlobal = true;
                v.Global.General.AILimit.LimitAIvsAIVision = true;
                v.Global.General.AILimit.LimitAIvsAIHearing = true;
            },
            Restore = (v, r) =>
            {
                v.Global.General.AILimit.LimitAIvsAIGlobal = r.Global.General.AILimit.LimitAIvsAIGlobal;
                v.Global.General.AILimit.LimitAIvsAIVision = r.Global.General.AILimit.LimitAIvsAIVision;
                v.Global.General.AILimit.LimitAIvsAIHearing = r.Global.General.AILimit.LimitAIvsAIHearing;
            },
            Off = v =>
            {
                v.Global.General.AILimit.LimitAIvsAIGlobal = false;
                v.Global.General.AILimit.LimitAIvsAIVision = false;
                v.Global.General.AILimit.LimitAIvsAIHearing = false;
            },
        },
        new()
        {
            NameKo = "봇이 다른 봇 발소리 안 들음 (사람 발소리는 들음)", NameEn = "Bots ignore other bots' footsteps (players' still heard)",
            IsLowSpec = v => !v.Global.Hearing.BotsHearBotFootsteps,
            Apply = v => v.Global.Hearing.BotsHearBotFootsteps = false,
            Restore = (v, r) => v.Global.Hearing.BotsHearBotFootsteps = r.Global.Hearing.BotsHearBotFootsteps,
            Off = v => v.Global.Hearing.BotsHearBotFootsteps = true,
        },
        new()
        {
            NameKo = "봇끼리 장전·치료 소리 미끼 안 씀", NameEn = "Bots don't bait each other with reload / heal sounds",
            IsLowSpec = v => !v.Global.Hearing.BotsTakeBotBait,
            Apply = v => v.Global.Hearing.BotsTakeBotBait = false,
            Restore = (v, r) => v.Global.Hearing.BotsTakeBotBait = r.Global.Hearing.BotsTakeBotBait,
            Off = v => v.Global.Hearing.BotsTakeBotBait = true,
        },
        new()
        {
            NameKo = "조준선 자리 찾기 범위 축소", NameEn = "Smaller search for a spot with a firing line",
            IsLowSpec = v => v.Global.General.CloseCombat.HoldStepToOpenAngleMax <= 1f && v.Global.General.FreezeAmbush.PeekSpotMaxStep <= 1.5f,
            Apply = v =>
            {
                v.Global.General.CloseCombat.HoldStepToOpenAngleMax = Math.Min(v.Global.General.CloseCombat.HoldStepToOpenAngleMax, 1f);
                v.Global.General.FreezeAmbush.PeekSpotMaxStep = Math.Min(v.Global.General.FreezeAmbush.PeekSpotMaxStep, 1.5f);
            },
            Restore = (v, r) =>
            {
                v.Global.General.CloseCombat.HoldStepToOpenAngleMax = r.Global.General.CloseCombat.HoldStepToOpenAngleMax;
                v.Global.General.FreezeAmbush.PeekSpotMaxStep = r.Global.General.FreezeAmbush.PeekSpotMaxStep;
            },
            Off = v =>
            {
                v.Global.General.CloseCombat.HoldStepToOpenAngleMax = 1.5f;
                v.Global.General.FreezeAmbush.PeekSpotMaxStep = 2.5f;
            },
        },
    };

    public static int LowSpecCount(QuickPresetView v) => LowSpec.Count(i => i.IsLowSpec(v));

    public static void ApplyLowSpec(QuickPresetView v)
    {
        foreach (var item in LowSpec)
        {
            item.Apply(v);
        }
    }

    /// <summary>Puts the low-spec fields back to <paramref name="reference"/>'s values. A reference that is itself low-spec
    /// (e.g. a saved low-spec copy) gets the normal zzap values instead, so "off" really means off.</summary>
    public static void RestoreLowSpec(QuickPresetView v, QuickPresetView reference)
    {
        foreach (var item in LowSpec)
        {
            if (reference == null || item.IsLowSpec(reference))
            {
                item.Off(v);
            }
            else
            {
                item.Restore(v, reference);
            }
        }
    }
}
