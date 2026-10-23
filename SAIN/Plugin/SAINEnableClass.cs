using System.Collections.Generic;
using EFT;
using SAIN.Components;
using SAIN.Components.BotController;
using SAIN.Preset.Shared.GlobalSettings.Categories.General;
using static SAIN.Helpers.EnumValues;

namespace SAIN;

public static class SAINEnableClass
{
    static SAINEnableClass()
    {
        GameWorld.OnDispose += Clear;
    }

    private static readonly HashSet<string> _excludedBots = [];
    private static readonly HashSet<string> _enabledBots = [];

    /// <summary>
    /// Checks if this bot has SAIN enabled or if it is a vanilla bot.
    /// </summary>
    public static bool IsSAINDisabledForBot(BotOwner botOwner)
    {
        if (botOwner == null)
        {
            return true;
        }

        Player player = botOwner.GetPlayer;
        if (player == null)
        {
            return true;
        }

        string id = player.ProfileId;
        if (_excludedBots.Contains(id))
        {
            return true;
        }

        if (_enabledBots.Contains(id))
        {
            return false;
        }

        ProfileSettings settings = botOwner.Profile?.Info?.Settings;
        if (settings == null)
        {
            return true;
        }

        player.OnIPlayerDeadOrUnspawn += ClearBot;

        if (IsBotExcluded(botOwner))
        {
            _excludedBots.Add(id);
#if DEBUG
            Logger.LogDebug($"Added Excluded Bot [{player.Profile.Nickname},{id}]");
#endif
            return true;
        }
        _enabledBots.Add(id);
#if DEBUG
        Logger.LogDebug($"Added Enabled Bot [{player.Profile.Nickname},{id}]");
#endif
        return false;
    }

    /// <summary>
    /// Checks if this IPlayer has SAIN enabled or if it is a vanilla bot.
    /// </summary>
    public static bool IsSAINDisabledForBot(IPlayer iPlayer)
    {
        if (iPlayer == null || !iPlayer.IsAI)
        {
            return true;
        }

        BotOwner botOwner = iPlayer.AIData?.BotOwner;
        if (botOwner == null)
        {
            return true;
        }

        string id = iPlayer.ProfileId;
        if (_excludedBots.Contains(id))
        {
            return true;
        }

        if (_enabledBots.Contains(id))
        {
            return false;
        }

        ProfileSettings settings = iPlayer.Profile?.Info?.Settings;
        if (settings == null)
        {
            return true;
        }

        botOwner.GetPlayer.OnIPlayerDeadOrUnspawn += ClearBot;

        if (IsBotExcluded(botOwner))
        {
            _excludedBots.Add(id);
            return true;
        }
        _enabledBots.Add(id);
        return false;
    }

    private static void Clear()
    {
        _excludedBots.Clear();
        _enabledBots.Clear();
    }

    private static void ClearBot(IPlayer player)
    {
        if (player != null)
        {
            player.OnIPlayerDeadOrUnspawn -= ClearBot;
            string id = player.ProfileId;
            _excludedBots.Remove(id);
            _enabledBots.Remove(id);
        }
    }

    /// <summary>
    /// Checks if this bot has SAIN enabled or if it is a vanilla bot.
    /// </summary>
    public static bool IsBotExcluded(string profileId)
    {
        return !_enabledBots.Contains(profileId);
    }

    /// <summary>
    /// Checks if this bot has SAIN enabled or if it is a vanilla bot.
    /// </summary>
    public static bool IsBotExcluded(BotOwner botOwner)
    {
        var settings = botOwner.Profile?.Info?.Settings;
        if (settings == null)
        {
            return true;
        }

        WildSpawnType type = settings.Role;

        if (BotSpawnController.IsExcluded(type))
        {
            return true;
        }

        // zzap (2026-10-24): per-role vanilla must also keep SAIN components/patches off PMCs and modded bots.
        var bots = SAINPlugin.LoadedPreset?.BotSettings?.SAINSettings;
        if (bots != null && bots.TryGetValue(type, out var group) && group?.UseVanillaAI == true)
        {
            return true;
        }

        if (IsAlwaysEnabled(type, botOwner))
        {
            return false;
        }

        return ShallExludeByWildSpawnType(type, botOwner);
    }

    public static bool ShallExludeByWildSpawnType(WildSpawnType wildSpawnType, BotOwner botOwner)
    {
        return ExcludeScav(wildSpawnType, botOwner)
            || ExcludeNormalBoss(wildSpawnType)
            || ExcludeNormalFollower(wildSpawnType)
            || ExcludeGoons(wildSpawnType)
            || ExcludeCultists(wildSpawnType)
            || ExcludeRogues(wildSpawnType)
            || ExcludeRaiders(wildSpawnType)
            || ExcludeBloodHounds(wildSpawnType)
            || ExcludeLabyrinthBots(wildSpawnType)
            || ExcludeSpecialBots(wildSpawnType);
    }

    /// <summary>The same role predicates used to exclude SAIN components, for the F6 global-group lock.</summary>
    public static bool VanillaByGroupSetting(WildSpawnType role, out string settingName)
    {
        settingName = null;
        if (SAINPlugin.LoadedPreset?.GlobalSettings?.General?.VanillaBots == null)
        {
            return false;
        }
        (bool Excluded, string Name)[] groups =
        [
            (SAINEnabled.VanillaScavs && WildSpawn.IsScav(role), "Vanilla Scavs"),
            (ExcludeNormalBoss(role), "Vanilla Bosses"),
            (ExcludeNormalFollower(role), "Vanilla Boss Followers"),
            (ExcludeGoons(role), "Vanilla Goons"),
            (ExcludeCultists(role), "Vanilla Cultists"),
            (ExcludeRogues(role), "Vanilla Rogues"),
            (ExcludeRaiders(role), "Vanilla Raiders"),
            (ExcludeBloodHounds(role), "Vanilla Bloodhounds"),
            (ExcludeLabyrinthBots(role), "Vanilla Labyrinth Bots"),
            (ExcludeSpecialBots(role), "Vanilla Special Bots"),
        ];
        foreach (var group in groups)
        {
            if (group.Excluded)
            {
                settingName = group.Name;
                return true;
            }
        }
        return false;
    }

    private static bool IsAlwaysEnabled(WildSpawnType wildSpawnType, BotOwner botOwner)
    {
        return wildSpawnType.IsPmcBot() || BotManagerComponent.Instance?.Bots?.ContainsKey(botOwner.ProfileId) == true;
    }

    private static bool ExcludeScav(WildSpawnType wildSpawnType, BotOwner botOwner)
    {
        return SAINEnabled.VanillaScavs && WildSpawn.IsScav(wildSpawnType) && !IsPlayerScav(botOwner.Profile);
    }

    private static bool ExcludeNormalBoss(WildSpawnType wildSpawnType)
    {
        return SAINEnabled.VanillaBosses && WildSpawn.IsNormalBoss(wildSpawnType);
    }

    private static bool ExcludeNormalFollower(WildSpawnType wildSpawnType)
    {
        return SAINEnabled.VanillaFollowers && WildSpawn.IsNormalFollower(wildSpawnType);
    }

    private static bool ExcludeGoons(WildSpawnType wildSpawnType)
    {
        return SAINEnabled.VanillaGoons && WildSpawn.IsGoons(wildSpawnType);
    }

    private static bool ExcludeCultists(WildSpawnType wildSpawnType)
    {
        return SAINEnabled.VanillaCultists && wildSpawnType.IsSectant();
    }

    private static bool ExcludeRogues(WildSpawnType wildSpawnType)
    {
        return SAINEnabled.VanillaRogues && wildSpawnType == WildSpawnType.exUsec;
    }

    private static bool ExcludeRaiders(WildSpawnType wildSpawnType)
    {
        return SAINEnabled.VanillaRaiders && wildSpawnType == WildSpawnType.pmcBot;
    }

    private static bool ExcludeBloodHounds(WildSpawnType wildSpawnType)
    {
        return SAINEnabled.VanillaBloodHounds
            && (wildSpawnType == WildSpawnType.arenaFighter || wildSpawnType == WildSpawnType.arenaFighterEvent);
    }

    private static bool ExcludeLabyrinthBots(WildSpawnType wildSpawnType)
    {
        return SAINEnabled.VanillaLabyrinthBots && WildSpawn.IsLabyrinthBot(wildSpawnType);
    }

    private static bool ExcludeSpecialBots(WildSpawnType wildSpawnType)
    {
        return SAINEnabled.VanillaSpecialBots && WildSpawn.IsSpecialBot(wildSpawnType);
    }

    public static bool IsPlayerScav(Profile profile)
    {
        // Handle the old version of creating player Scavs
        if (profile.Info.Nickname.Contains(" ("))
        {
            return true;
        }
        // Check for player Scavs created by SPT
        return profile.Info.Settings.Role == WildSpawnType.assault && !string.IsNullOrEmpty(profile.Info.MainProfileNickname);
    }

    /// <summary>
    /// Is this player a sain bot, and are they also in combat state?
    /// </summary>
    /// <param name="player"></param>
    /// <returns></returns>
    public static bool IsBotInCombat(IPlayer player)
    {
        GetSAIN(player.ProfileId, out var bot);
        return bot != null && bot.SAINLayersActive;
    }

    public static bool GetSAIN(string profileId, out BotComponent sain)
    {
        sain = null;
        if (profileId.IsNullOrEmpty())
        {
            return false;
        }

        if (!_enabledBots.Contains(profileId))
        {
            return false;
        }

        return BotManagerComponent.Instance != null
            && BotManagerComponent.Instance.BotSpawnController.BotDictionary.TryGetValue(profileId, out sain);
    }

    private static VanillaBotSettings SAINEnabled
    {
        get { return SAINPlugin.LoadedPreset.GlobalSettings.General.VanillaBots; }
    }
}
