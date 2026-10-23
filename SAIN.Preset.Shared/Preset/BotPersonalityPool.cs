using System;
using System.Collections.Generic;
using System.Linq;
using SAIN.Preset.Shared.BotSettings.SAINSettings.Categories;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.Models.Preset.Personalities;

namespace SAIN.Preset.Shared.Preset;

/// <summary>Resolves explicit, legacy and role-default personality choices without re-rolling a live bot.</summary>
public static class BotPersonalityPool
{
    public static List<EPersonality> DefaultFor(ESainWildSpawnType role)
    {
        if ((int)role == 848424) return [EPersonality.GigaChad];
        return (int)role is 848420 or 848421 or 848422 or 848423 or 848426
            ? [EPersonality.Chad, EPersonality.GigaChad] : [];
    }

    public static IReadOnlyList<EPersonality> Candidates(ESainWildSpawnType role, SAINMindSettings mind)
    {
        if (mind == null) return [];
        IEnumerable<EPersonality> choices = mind.PersonalityPool;
        if (choices == null)
            choices = mind.PersonalityOverride != EPersonality.None ? [mind.PersonalityOverride] : DefaultFor(role);
        return choices.Where(p => p != EPersonality.None && Enum.IsDefined(typeof(EPersonality), p)).Distinct().ToList();
    }

    public static bool TryPick(ESainWildSpawnType role, SAINMindSettings mind, Func<EPersonality, bool> enabled, int choice, out EPersonality personality)
    {
        var choices = Candidates(role, mind).Where(p => enabled == null || enabled(p)).ToList();
        personality = EPersonality.None;
        if (choices.Count == 0) return false;
        personality = choices[(int)((uint)choice % (uint)choices.Count)];
        return true;
    }

    public static void Set(SAINSettingsGroupClass group, IEnumerable<EPersonality> choices)
    {
        if (group?.Settings == null) return;
        var pool = (choices ?? []).Where(p => p != EPersonality.None && Enum.IsDefined(typeof(EPersonality), p)).Distinct().ToList();
        foreach (var slot in group.Settings.Values)
        {
            if (slot == null) continue;
            slot.Mind.PersonalityOverride = EPersonality.None;
            slot.Mind.PersonalityPool = new List<EPersonality>(pool);
        }
    }
}
