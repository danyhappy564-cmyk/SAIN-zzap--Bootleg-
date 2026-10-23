using System;
using System.Linq;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.GlobalSettings.Categories.General;

namespace SAIN.Preset.Shared.Preset;

public enum BotAiMode { Sain, PerBotVanilla, GlobalVanilla, AlwaysVanilla, ExternalVanilla }

/// <summary>Effective AI and the edit permissions both settings screens display.</summary>
public readonly struct BotAiState
{
    public BotAiMode Mode { get; }
    public string Reason { get; }
    public bool UsesVanilla => Mode != BotAiMode.Sain;
    public bool CanEditSain => Mode == BotAiMode.Sain;
    public bool CanChooseAI => Mode == BotAiMode.Sain || Mode == BotAiMode.PerBotVanilla;
    public BotAiState(BotAiMode mode, string reason = null) { Mode = mode; Reason = reason; }
}

public static class BotAiPolicy
{
    public static readonly ESainWildSpawnType[] AlwaysVanillaRoles =
    [
        ESainWildSpawnType.bossZryachiy, ESainWildSpawnType.followerZryachiy,
        ESainWildSpawnType.peacefullZryachiyEvent, ESainWildSpawnType.ravangeZryachiyEvent,
        ESainWildSpawnType.shooterBTR, ESainWildSpawnType.marksman,
        ESainWildSpawnType.infectedAssault, ESainWildSpawnType.infectedCivil,
        ESainWildSpawnType.infectedLaborant, ESainWildSpawnType.infectedPmc, ESainWildSpawnType.infectedTagilla,
    ];
    private static readonly ESainWildSpawnType[] Scavs =
    [
        ESainWildSpawnType.assault,
        ESainWildSpawnType.assaultGroup,
        ESainWildSpawnType.crazyAssaultEvent,
        ESainWildSpawnType.cursedAssault,
        ESainWildSpawnType.marksman,
    ];
    private static readonly ESainWildSpawnType[] Goons =
    [
        ESainWildSpawnType.bossKnight,
        ESainWildSpawnType.followerBigPipe,
        ESainWildSpawnType.followerBirdEye,
    ];
    private static readonly ESainWildSpawnType[] NormalBosses =
    [
        ESainWildSpawnType.bossBully,
        ESainWildSpawnType.bossGluhar,
        ESainWildSpawnType.bossKojaniy,
        ESainWildSpawnType.bossSanitar,
        ESainWildSpawnType.bossTagilla,
        ESainWildSpawnType.bossTest,
        ESainWildSpawnType.bossKilla,
        ESainWildSpawnType.bossBoar,
        ESainWildSpawnType.bossKolontay,
        ESainWildSpawnType.bossPartisan,
    ];
    private static readonly ESainWildSpawnType[] NormalFollowers =
    [
        ESainWildSpawnType.followerBully,
        ESainWildSpawnType.followerGluharAssault,
        ESainWildSpawnType.followerGluharSecurity,
        ESainWildSpawnType.followerGluharScout,
        ESainWildSpawnType.followerKojaniy,
        ESainWildSpawnType.followerSanitar,
        ESainWildSpawnType.followerTagilla,
        ESainWildSpawnType.followerBoar,
        ESainWildSpawnType.followerBoarClose1,
        ESainWildSpawnType.followerBoarClose2,
        ESainWildSpawnType.followerKolontayAssault,
        ESainWildSpawnType.followerKolontaySecurity,
    ];
    private static readonly ESainWildSpawnType[] LabyrinthBots =
    [
        ESainWildSpawnType.bossTagillaAgro,
        ESainWildSpawnType.bossKillaAgro,
        ESainWildSpawnType.tagillaHelperAgro,
    ];
    private static readonly ESainWildSpawnType[] SpecialBots =
    [
        ESainWildSpawnType.crazyAssaultEvent,
        ESainWildSpawnType.gifter,
    ];

    public static BotAiState Resolve(ESainWildSpawnType role, VanillaBotSettings global, SAINSettingsGroupClass group, bool externallyExcluded = false)
    {
        if (AlwaysVanillaRoles.Contains(role)) return new(BotAiMode.AlwaysVanilla);
        if (externallyExcluded) return new(BotAiMode.ExternalVanilla);
        string setting = GroupSetting(role, global);
        if (setting != null) return new(BotAiMode.GlobalVanilla, setting);
        return new(group?.UseVanillaAI == true ? BotAiMode.PerBotVanilla : BotAiMode.Sain);
    }

    public static string GroupSetting(ESainWildSpawnType role, VanillaBotSettings global)
    {
        if (global == null) return null;
        if (global.VanillaScavs && Scavs.Contains(role)) return "Vanilla Scavs";
        if (global.VanillaBosses && NormalBosses.Contains(role)) return "Vanilla Bosses";
        if (global.VanillaFollowers && NormalFollowers.Contains(role)) return "Vanilla Boss Followers";
        if (global.VanillaGoons && Goons.Contains(role)) return "Vanilla Goons";
        if (global.VanillaCultists && role.ToString().StartsWith("sectant", StringComparison.OrdinalIgnoreCase)) return "Vanilla Cultists";
        if (global.VanillaRogues && role == ESainWildSpawnType.exUsec) return "Vanilla Rogues";
        if (global.VanillaRaiders && role == ESainWildSpawnType.pmcBot) return "Vanilla Raiders";
        if (global.VanillaBloodHounds && (role == ESainWildSpawnType.arenaFighter || role == ESainWildSpawnType.arenaFighterEvent)) return "Vanilla Bloodhounds";
        if (global.VanillaLabyrinthBots && LabyrinthBots.Contains(role)) return "Vanilla Labyrinth Bots";
        if (global.VanillaSpecialBots && SpecialBots.Contains(role)) return "Vanilla Special Bots";
        return null;
    }
}
