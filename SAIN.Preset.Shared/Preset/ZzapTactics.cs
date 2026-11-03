using System;
using SAIN.Preset.Shared.GlobalSettings;

namespace SAIN.Preset.Shared.Preset;

/// <summary>One zzap tactic as an on/off line in plain words (the ZZAP tab's "at a glance" list).</summary>
public sealed class ZzapTactic
{
    public string Id;
    public string NameKo;
    public string NameEn;
    public string HelpKo;
    public string HelpEn;
    public Func<GlobalSettingsClass, bool> Get;
    public Action<GlobalSettingsClass, bool> Set;

    public string Name(bool korean) => korean ? NameKo : NameEn;

    public string Help(bool korean) => korean ? HelpKo : HelpEn;
}

/// <summary>
/// zzap (user 2026-11-04: "ZZAP settings in the global settings are too hard - make it simple: can it QEQE, does it diamond step...").
/// The global settings sections that belong to the ZZAP and Simulation tabs (F6 and web), and the main switches of the zzap
/// tactics shown first on the ZZAP tab. The switches only read and write the existing settings - nothing new is saved.
/// </summary>
public static class ZzapTactics
{
    /// <summary>GeneralSettings fields shown on the ZZAP tab instead of "Global settings (all)".</summary>
    public static readonly string[] ZzapSections = { "DoorTactics", "FreezeAmbush", "SquadCombat", "CloseCombat", "Reposition", "PlayerStyle" };

    /// <summary>GeneralSettings fields shown on the Simulation tab.</summary>
    public static readonly string[] SimSections = { "SimLab" };

    public static bool IsZzapSection(string field) => Array.IndexOf(ZzapSections, field) >= 0;

    public static bool IsSimSection(string field) => Array.IndexOf(SimSections, field) >= 0;

    public const string TabAll = "all";
    public const string TabZzap = "zzap";
    public const string TabSim = "sim";

    /// <summary>Which tab a setting belongs to, by its path from the global settings ("General.DoorTactics.Enabled" -> zzap).</summary>
    public static string TabOf(string globalPath)
    {
        if (globalPath == null || !globalPath.StartsWith("General.", StringComparison.Ordinal))
        {
            return TabAll;
        }
        string rest = globalPath.Substring("General.".Length);
        int dot = rest.IndexOf('.');
        string section = dot < 0 ? rest : rest.Substring(0, dot);
        return IsZzapSection(section) ? TabZzap : IsSimSection(section) ? TabSim : TabAll;
    }

    public static readonly ZzapTactic[] All =
    {
        new()
        {
            Id = "door", NameKo = "문 전술", NameEn = "Door tactics",
            HelpKo = "문 앞 매복·점프 피킹·가짜 수류탄·방 정리·문 박차기", HelpEn = "Door ambush, jump peek, fake grenade, room clearing, door ramming",
            Get = g => g.General.DoorTactics.Enabled, Set = (g, v) => g.General.DoorTactics.Enabled = v,
        },
        new()
        {
            Id = "squad", NameKo = "분대 교전", NameEn = "Squad combat",
            HelpKo = "교차 사격·동료 엄호·트레이드·분대 돌격", HelpEn = "Crossfire, covering a mate, trading, squad storm",
            Get = g => g.General.SquadCombat.Enabled, Set = (g, v) => g.General.SquadCombat.Enabled = v,
        },
        new()
        {
            Id = "reposition", NameKo = "위치 바꾸기", NameEn = "Reposition",
            HelpKo = "들킨 자리에서 옮기기·미끼 피킹·가짜 장전", HelpEn = "Leave a spotted spot, bait peek, fake reload",
            Get = g => g.General.Reposition.Enabled, Set = (g, v) => g.General.Reposition.Enabled = v,
        },
        new()
        {
            Id = "diamond", NameKo = "다이아몬드 스텝", NameEn = "Diamond step",
            HelpKo = "쏘면서 W/A/S/D로 짧게 춤추듯 움직이기", HelpEn = "Short W/A/S/D taps while shooting",
            Get = g => g.General.CloseCombat.DiamondStep, Set = (g, v) => g.General.CloseCombat.DiamondStep = v,
        },
        new()
        {
            Id = "qeqe", NameKo = "QEQE 기울이기 사격", NameEn = "QEQE lean shooting",
            HelpKo = "쏘면서 Q/E로 좌우 기울이기 (다이아몬드 스텝을 하는 봇만)", HelpEn = "Q/E leaning while shooting (bots that diamond step)",
            Get = g => g.General.CloseCombat.LeanSpam, Set = (g, v) => g.General.CloseCombat.LeanSpam = v,
        },
        new()
        {
            Id = "jiggle", NameKo = "모서리 흔들기 피킹", NameEn = "Corner jiggle peek",
            HelpKo = "모서리·문틀에서 나왔다 숨었다 하며 쏘기", HelpEn = "Peek in and out from a corner or door frame",
            Get = g => g.General.CloseCombat.CornerJiggle, Set = (g, v) => g.General.CloseCombat.CornerJiggle = v,
        },
        new()
        {
            Id = "closefight", NameKo = "붙으면 엄폐 대신 싸우기", NameEn = "Fight close instead of cover",
            HelpKo = "가까운 적이 보이면 엄폐물로 걸어가지 않고 계속 쏨", HelpEn = "A close visible enemy is fought, not walked away from",
            Get = g => g.General.CloseCombat.CloseFight, Set = (g, v) => g.General.CloseCombat.CloseFight = v,
        },
        new()
        {
            Id = "chase", NameKo = "모서리 추격", NameEn = "Corner chase",
            HelpKo = "가까운 모서리로 숨은 적을 바로 쫓아감", HelpEn = "Chase an enemy who just broke sight around a close corner",
            Get = g => g.General.CloseCombat.CornerChase, Set = (g, v) => g.General.CloseCombat.CornerChase = v,
        },
        new()
        {
            Id = "pistol", NameKo = "탄 떨어지면 권총", NameEn = "Pistol when empty",
            HelpKo = "가까운 교전 중 탄창이 비면 장전 대신 권총", HelpEn = "Draw the pistol instead of reloading in a close fight",
            Get = g => g.General.CloseCombat.PistolSwap, Set = (g, v) => g.General.CloseCombat.PistolSwap = v,
        },
        new()
        {
            Id = "quickreload", NameKo = "빠른 장전", NameEn = "Quick reload",
            HelpKo = "교전 중엔 탄창을 버리고 빠르게 장전 (R 두 번)", HelpEn = "Drop the magazine and reload fast in a fight (double R)",
            Get = g => g.General.CloseCombat.QuickReload, Set = (g, v) => g.General.CloseCombat.QuickReload = v,
        },
        new()
        {
            Id = "mistakes", NameKo = "사람 같은 실수", NameEn = "Human mistakes",
            HelpKo = "가끔 최선이 아닌 두 번째 선택 (성격별 확률)", HelpEn = "Now and then the second best choice (by personality)",
            Get = g => g.General.CloseCombat.UtilityMistakes, Set = (g, v) => g.General.CloseCombat.UtilityMistakes = v,
        },
        new()
        {
            Id = "fear", NameKo = "두려움", NameEn = "Fear",
            HelpKo = "다치거나 밀리면 겁먹고 조심함 (성격별)", HelpEn = "Hurt or outgunned bots get careful (by personality)",
            Get = g => g.General.CloseCombat.Fear, Set = (g, v) => g.General.CloseCombat.Fear = v,
        },
        new()
        {
            Id = "adapt", NameKo = "내 플레이 스타일에 적응", NameEn = "Adapt to my play style",
            HelpKo = "지난 판의 내 스타일(돌격·존버 등)에 맞춰 봇이 대응", HelpEn = "Bots answer your style from past raids (rushing, camping...)",
            Get = g => g.General.PlayerStyle.AdaptEnabled, Set = (g, v) => g.General.PlayerStyle.AdaptEnabled = v,
        },
        new()
        {
            Id = "skipscavs", NameKo = "스캐브는 근접전 zzap 제외", NameEn = "Scavs skip close-combat zzap",
            HelpKo = "켜면 스캐브는 위 근접전 행동 대신 기본 행동 (봇 종류별로 따로 바꿀 수 있음)", HelpEn = "On: scavs keep the default behaviour instead of the close-combat moves (can be changed per bot type)",
            Get = g => g.General.CloseCombat.PmcOnly, Set = (g, v) => g.General.CloseCombat.PmcOnly = v,
        },
    };
}
