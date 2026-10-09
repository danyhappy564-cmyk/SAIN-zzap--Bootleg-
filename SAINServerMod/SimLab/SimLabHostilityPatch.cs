using System.Reflection;
using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Services.InRaid;

namespace SAINServerMod.SimLab;

/// <summary>
/// zzap fork: every squad is its own team in sim raids (user 2026-10-09 screenshot: two USEC standing in a doorway, "why don't
/// they fight"; "every squad should be a different team - BEARs fight BEARs anyway"). SPT's pmc.json makes a PMC hostile to its
/// own faction only by chance (usecEnemyChance / bearEnemyChance 85), so about 1 in 7 meetings of two same-faction squads rolled
/// friendly. In a sim raid both chances are 100 for the PMC roles: only a bot's own squad (its BotsGroup) is friendly.
/// SPT applies pmc.json to the raid's location in <c>LocationLifecycleService.AdjustBotHostilitySettings</c>, after the location
/// was generated, so this is a postfix on that method; it only touches the sim's two PMC roles and only in a sim raid.
/// </summary>
public sealed class SimLabHostilityPatch() : AbstractPatch("zzap.SAIN.SimLabHostilityPatch")
{
    protected override MethodBase? GetTargetMethod()
    {
        return AccessTools.Method(typeof(LocationLifecycleService), "AdjustBotHostilitySettings");
    }

    [PatchPostfix]
    public static void Postfix(LocationBase location)
    {
        SimLabService.Instance?.ApplySimHostility(location);
    }

    /// <summary>Every other PMC squad is an enemy (own faction included) for the sim's PMC roles. Returns what changed.</summary>
    public static string? Apply(LocationBase location, string sideA, string sideB)
    {
        var settings = location?.BotLocationModifier?.AdditionalHostilitySettings;
        if (settings == null)
        {
            return null;
        }
        var changed = new List<string>();
        foreach (var entry in settings)
        {
            string? role = entry?.BotRole;
            bool usec = string.Equals(role, "pmcUSEC", StringComparison.OrdinalIgnoreCase);
            bool bear = string.Equals(role, "pmcBEAR", StringComparison.OrdinalIgnoreCase);
            if (entry == null || !(usec || bear) || !(string.Equals(role, sideA, StringComparison.OrdinalIgnoreCase) || string.Equals(role, sideB, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            entry.UsecEnemyChance = 100;
            entry.BearEnemyChance = 100;
            entry.AlwaysFriends?.Remove("pmcUSEC");
            entry.AlwaysFriends?.Remove("pmcBEAR");
            changed.Add(role!);
        }
        return changed.Count == 0 ? null : string.Join("/", changed);
    }
}
