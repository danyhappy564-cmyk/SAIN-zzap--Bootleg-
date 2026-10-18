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

    public string Name(bool korean) => korean ? NameKo : NameEn;

    public string Help(bool korean) => korean ? HelpKo : HelpEn;
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

    public static readonly QuickSetting[] All =
    {
        new()
        {
            Id = "recoil", Group = "aim", Kind = QuickKind.Number, Min = 0.01f, Max = 3f, Step = 0.05f,
            NameKo = "반동 크기", NameEn = "Recoil",
            HelpKo = "전역 반동 배율. 클수록 봇 총이 많이 튐 (zzap 0.1, 쉬움 3.0)", HelpEn = "Global recoil multiplier. Higher = bots' guns kick more (zzap 0.1, easy 3.0)",
            Get = v => v.Global.Shoot.BOT_RECOIL_COEF, Set = (v, x) => v.Global.Shoot.BOT_RECOIL_COEF = x,
        },
        new()
        {
            Id = "aimTime", Group = "aim", Kind = QuickKind.Number, Min = 0.1f, Max = 3f, Step = 0.05f,
            NameKo = "조준 시간 (PMC, 초)", NameEn = "Aim time (PMC, s)",
            HelpKo = "PMC가 조준을 끝내는 최대 시간. 작을수록 빨리 맞힘 — 모든 PMC 종류·난이도에 같이 적용", HelpEn = "Longest time a PMC takes to settle its aim. Lower = hits sooner - written to every PMC type and difficulty",
            Get = v => FirstPmc(v, s => s.Aiming.MAX_AIM_TIME, 1f),
            Set = (v, x) => { foreach (var s in Settings(v, true)) { s.Aiming.MAX_AIM_TIME = x; } },
        },
        new()
        {
            Id = "fireRate", Group = "aim", Kind = QuickKind.Number, Min = 0.25f, Max = 3f, Step = 0.05f,
            NameKo = "연사 속도 (PMC)", NameEn = "Fire rate (PMC)",
            HelpKo = "PMC 발사 간격 배율(점사 길이도 같이). 클수록 빨리 쏨 — 모든 PMC 종류·난이도에 같이 적용", HelpEn = "PMC fire rate multiplier (burst length follows). Higher = faster - written to every PMC type and difficulty",
            Get = v => FirstPmc(v, s => s.Shoot.FireratMulti, 1.5f),
            Set = (v, x) => { foreach (var s in Settings(v, true)) { s.Shoot.FireratMulti = x; s.Shoot.BurstMulti = x; } },
        },
        new()
        {
            Id = "scatter", Group = "aim", Kind = QuickKind.Number, Min = 0.1f, Max = 3f, Step = 0.05f,
            NameKo = "탄 퍼짐", NameEn = "Bullet scatter",
            HelpKo = "전역 탄 퍼짐 배율. 클수록 빗나감", HelpEn = "Global scatter multiplier. Higher = more misses",
            Get = v => v.Global.Difficulty.ScatteringCoef, Set = (v, x) => v.Global.Difficulty.ScatteringCoef = x,
        },
        new()
        {
            Id = "sight", Group = "sense", Kind = QuickKind.Number, Min = 0.25f, Max = 3f, Step = 0.05f,
            NameKo = "시야 거리 배율", NameEn = "Sight distance",
            HelpKo = "봇이 볼 수 있는 거리 (SAIN 기본 1, zzap 2)", HelpEn = "How far bots can see (SAIN default 1, zzap 2)",
            Get = v => v.Global.Difficulty.VisibleDistCoef, Set = (v, x) => v.Global.Difficulty.VisibleDistCoef = x,
        },
        new()
        {
            Id = "gainSight", Group = "sense", Kind = QuickKind.Number, Min = 0.2f, Max = 2f, Step = 0.05f,
            NameKo = "발견 속도 배율", NameEn = "Spotting speed",
            HelpKo = "보인 적을 알아채는 속도. 작을수록 늦게 발견", HelpEn = "How fast a visible enemy is noticed. Lower = later",
            Get = v => v.Global.Difficulty.GainSightCoef, Set = (v, x) => v.Global.Difficulty.GainSightCoef = x,
        },
        new()
        {
            Id = "hearing", Group = "sense", Kind = QuickKind.Number, Min = 0.2f, Max = 2f, Step = 0.05f,
            NameKo = "청각 거리 배율", NameEn = "Hearing distance",
            HelpKo = "발소리·총소리를 듣는 거리", HelpEn = "How far footsteps and shots are heard",
            Get = v => v.Global.Difficulty.HearingDistanceCoef, Set = (v, x) => v.Global.Difficulty.HearingDistanceCoef = x,
        },
        new()
        {
            Id = "fov", Group = "sense", Kind = QuickKind.Number, Min = 90f, Max = 180f, Step = 5f,
            NameKo = "시야각 (도)", NameEn = "Field of view (deg)",
            HelpKo = "봇이 보는 좌우 범위 — 모든 봇 종류·난이도에 같이 적용", HelpEn = "Bots' left-right view - written to every bot type and difficulty",
            Get = v => FirstPmc(v, s => s.Core.VisibleAngle, 160f),
            Set = (v, x) => { foreach (var s in Settings(v, false)) { s.Core.VisibleAngle = x; } },
        },
        new()
        {
            Id = "doorMulti", Group = "tactics", Kind = QuickKind.Number, Min = 0f, Max = 2f, Step = 0.05f,
            NameKo = "문 전술 배율", NameEn = "Door tactic multiplier",
            HelpKo = "문 피킹·방 진입·가두기 시작 확률에 곱함", HelpEn = "Multiplies the start chance of door peeks, room clears and traps",
            Get = v => v.Global.General.DoorTactics.ChanceMultiplier, Set = (v, x) => v.Global.General.DoorTactics.ChanceMultiplier = x,
        },
        new()
        {
            Id = "ramClear", Group = "tactics", Kind = QuickKind.Number, Min = 0f, Max = 100f, Step = 5f,
            NameKo = "문 박치기 (방 진입 %)", NameEn = "Door ram (room clear %)",
            HelpKo = "방에 들어갈 때 닫힌 문을 달려서 박차 열 확률", HelpEn = "Chance to sprint-ram a closed door when clearing a room",
            Get = v => v.Global.General.DoorTactics.DoorModRamChance, Set = (v, x) => v.Global.General.DoorTactics.DoorModRamChance = x,
        },
        new()
        {
            Id = "prefire", Group = "tactics", Kind = QuickKind.Number, Min = 0f, Max = 100f, Step = 5f,
            NameKo = "코너 프리파이어 %", NameEn = "Corner prefire %",
            HelpKo = "코너를 돌기 전에 미리 쏠 확률", HelpEn = "Chance to fire before rounding a corner",
            Get = v => v.Global.General.CloseCombat.CornerChasePrefireChance, Set = (v, x) => v.Global.General.CloseCombat.CornerChasePrefireChance = x,
        },
        new()
        {
            Id = "squadStorm", Group = "tactics", Kind = QuickKind.Toggle,
            NameKo = "분대 일제 돌격", NameEn = "Squad storm",
            HelpKo = "적이 약해 보이면 분대가 한꺼번에 밀고 들어감", HelpEn = "The squad rushes together when the enemy looks weak",
            Get = v => v.Global.General.SquadCombat.SquadStorm ? 1f : 0f, Set = (v, x) => v.Global.General.SquadCombat.SquadStorm = On(x),
        },
        new()
        {
            Id = "journal", Group = "log", Kind = QuickKind.Toggle,
            NameKo = "레이드 일지", NameEn = "Raid journal",
            HelpKo = "BepInEx/config/SAIN-zzap/Journal 에 레이드마다 기록 — 버그 제보할 때만 켜기", HelpEn = "One file per raid in BepInEx/config/SAIN-zzap/Journal - only needed for bug reports",
            Get = v => v.Global.General.PlayerStyle.RaidJournal ? 1f : 0f, Set = (v, x) => v.Global.General.PlayerStyle.RaidJournal = On(x),
        },
        new()
        {
            Id = "diag", Group = "log", Kind = QuickKind.Toggle,
            NameKo = "진단 로그", NameEn = "Diagnostic logs",
            HelpKo = "문 전술·분대 교전·재배치·얼음 매복 기록을 LogOutput.log에 — 제보 끝나면 끄기", HelpEn = "Door / squad / reposition / freeze logs in LogOutput.log - switch off after the report",
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
