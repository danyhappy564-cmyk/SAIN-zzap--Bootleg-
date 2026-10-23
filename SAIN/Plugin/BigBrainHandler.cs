using System;
using System.Collections.Generic;
using System.Reflection;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using SAIN.Layers;
using SAIN.Layers.Combat.Run;
using SAIN.Layers.Combat.Solo;
using SAIN.Layers.Combat.Squad;
using SAIN.Layers.Flashed;
using SAIN.Preset;
using SAIN.Preset.GlobalSettings.Categories;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.GlobalSettings.Categories.General;

namespace SAIN;

public static class BigBrainHandler
{
    public const bool INCLUDE_RAIDER_BRAIN_FOR_PMCS = true;

    private static readonly string[] _commonVanillaLayersToRemove =
    [
        //"FightReqNull",
        //"PeacecReqNull",
        //"Follow Player",
        //"HideHW", // Used by Scavs, Raiders, and Bloodhounds when zombies spawn
        //"Khorovod",
        //"GeneratorDef", // Used by Scavs and Killa
        "GroupForce", // Used by Cursed Scavs, Rogues, and Oni (event) Cultist
        "AdvAssaultTarget",
        "Simple Target",
        "Pmc", // Used by Raiders, Bloodhounds, and Scav groups
        "AssaultHaveEnemy",
        "PushAndSup", // Used by Scavs and Shadow of Tagilla's followers
        "Pursuit",
        "Kill logic",
    ];

    private static readonly string[] _commonVanillaBossAndFollowerLayersToRemove =
    [
        //"ExURequest", // Used by Rogues and Kaban's followers to warn neutral players/bots
        "BirdEyeFight", // Used by Birdeye and Ghost (event) Cultist
        "BossSanitarFight", // Used by Sanitar and Harbinger (event) Cultist
        "SanitarGoal", // Used by Sanitar and his followers
        "TagillaAmbush", // Used by Tagilla and his followers
        "TagillaMain", // Used by Tagilla and his followers
        "BoarGrenadeDanger", // Used by Kaban and his followers
        "HoldOrCoverF", // Used by Cultists and Gluhar's followers
        "SecurityGluhar", // Used by Gluhar and his followers
        "HoldNearBoss", // Used by Gluhar's and Kollontay's followers
        "Kln_NIMH", // Used by Kollontay and his followers
        "KlnTrg", // Used by Kollontay and his followers
        "Kojaniy Target", // Used by Shturman and his followers
    ];

    // zzap (2026-10-23): the rest of the list MoreBotsAPI asks for on its bots (Black Division's fork, verified in a raid,
    // excluded the same 16). Modded bot types only, so the raid-start check also restores these.
    private static readonly string[] _moddedExtraVanillaLayersToRemove = ["Help", "Hit", "Assault Building", "Enemy Building"];

    private static readonly List<Type> _SAINLayers = [];
    private static readonly List<string> _SAINLayerNames = [];

    public static List<string> SAINLayerNames
    {
        get { return FindAllSAINLayers(); }
    }

    public static List<Type> SAINLayers
    {
        get
        {
            if (_SAINLayers.Count == 0)
            {
                Type[] allTypes = typeof(SAINPlugin).Assembly.GetTypes();
                for (int i = 0; i < allTypes.Length; i++)
                {
                    Type type = allTypes[i];
                    if (type.IsSubclassOf(typeof(SAINLayer)))
                    {
                        _SAINLayers.Add(type);
                    }
                }
            }

            return _SAINLayers;
        }
    }

    public static void Init()
    {
        BrainAssignment.Init();
    }

    private static List<string> FindAllSAINLayers()
    {
        if (_SAINLayerNames.Count != 0)
        {
            return _SAINLayerNames;
        }

        foreach (Type layerType in SAINLayers)
        {
            FieldInfo nameFieldInfo = layerType.GetField("Name", BindingFlags.Public | BindingFlags.Static);
            if (nameFieldInfo == null)
            {
                Logger.LogError(
                    $"{layerType.Name} does not have a public static Name field. This is required for enabling vanilla layers!"
                );
                continue;
            }

            _SAINLayerNames.Add((string)nameFieldInfo.GetValue(null));
        }

        return _SAINLayerNames;
    }

    public static bool BigBrainInitialized;

    public static class BrainAssignment
    {
        public static void Init()
        {
            AddCustomLayersToPMCs();
            AddCustomLayersToScavs();
            AddCustomLayersToRaiders([WildSpawnType.pmcBot]);
            AddCustomLayersToRogues();
            AddCustomLayersToBloodHounds();
            AddCustomLayersToNormalBosses();
            AddCustomLayersToNormalFollowers();
            AddCustomLayersToGoons();
            AddCustomLayersToLabyrinthBots();
            AddCustomLayersToCultists();
            AddCustomLayersToSpecialBots();

            ToggleVanillaLayersForPMCs(false);
            ToggleVanillaLayersForAllBots();

            ApplyModdedBotLayers();
        }

        /// <summary>
        /// Attaches SAIN's layers to bot types registered by other mods through the server interop
        /// </summary>
        public static void ApplyModdedBotLayers()
        {
            ApplyModdedBotLayers(false);
        }

        /// <summary>
        /// zzap (2026-10-23): runs again when a raid's BotsController starts, before any bot spawns. Black Division's
        /// fork re-applied this at raid start because the vanilla exclusions it needed had gone missing by then
        /// (SainBrainLayerPatch, verified in a 4.0.10 raid) - this does the same for every registered modded bot, so the
        /// original mods work without a fork. Both halves are idempotent: a layer that is already registered for the
        /// brain and role is not added again, and BigBrain ignores an exclusion that already exists.
        /// </summary>
        public static void ReassertModdedBotLayers()
        {
            _hitRevealReported.Clear();
            _spawnExclusionReported.Clear();
            ApplyModdedBotLayers(true);
        }

        // Modded roles whose "vanilla layers excluded at spawn" line was already logged this raid.
        private static readonly HashSet<WildSpawnType> _spawnExclusionReported = [];

        /// <summary>
        /// zzap (2026-10-23 sim 16:55): Black Division spawned (12 bots) and never once ran a SAIN layer - its bots fought on the
        /// raider brain's vanilla "Pmc" / "AdvAssaultTarget" layers. SAIN's combat layers sit at 20/22 and only win once those
        /// vanilla layers are excluded. Why the exclusion didn't hold is not proven: MoreBotsAPI swaps the bot's brain inside
        /// StandartBotBrain.Activate (Priority.First) and BigBrain excludes on the current brain after it, so the swap alone
        /// doesn't explain it (Codex review). So this does two things, once a modded bot's brain is final (last-priority
        /// Activate postfix): re-excludes this role's vanilla layers on the brain the bot really has (BigBrain applies it to
        /// activated bots, this one included - same call as the Black Division fork's SainBrainLayerPatch, verified in a 4.0.10
        /// raid), and logs, once per role per raid, what that brain held before and after (SAIN layers present / active, vanilla
        /// layers still active) so the next raid shows the real cause. Per spawn of a modded role only - not per frame.
        /// </summary>
        public static void ExcludeVanillaLayersForModdedBot(BotOwner owner)
        {
            WildSpawnType role = owner.Profile.Info.Settings.Role;
            if (!BotTypeDefinitions.IsModded(role) || !BotTypeDefinitions.BotTypes.TryGetValue(role, out BotType botType))
            {
                return;
            }
            if (SAIN.Components.BotController.BotSpawnController.IsExcluded(role))
            {
                return;
            }
            string brain = owner.Brain?.BaseBrain?.ShortName();
            if (string.IsNullOrEmpty(brain))
            {
                return;
            }
            List<string> brains = botType.BrainsToApply is { Count: > 0 } ? botType.BrainsToApply : [botType.BaseBrain];
            bool first = _spawnExclusionReported.Add(role);
            if (!brains.Contains(brain))
            {
                // SAIN's layers were registered for other brains - nothing to uncover; say so once so the mismatch shows.
                if (first)
                {
                    Logger.LogWarning($"[SAIN ModdedBots] spawn: {botType.Name} ({role}) has brain '{brain}', not in its SAIN brains [{string.Join(", ", brains)}] - SAIN layers can't run on it");
                }
                return;
            }

            List<string> layers = [.. _commonVanillaLayersToRemove, .. _moddedExtraVanillaLayersToRemove];
            if (botType.LayersToRemove != null)
            {
                layers.AddRange(botType.LayersToRemove);
            }
            string before = first ? DescribeBrainLayers(owner, layers) : null;
            BrainManager.RemoveLayers(layers, [brain], [role]);
            if (first)
            {
                Logger.LogWarning($"[SAIN ModdedBots] spawn: {botType.Name} ({role}) on brain '{brain}' - before: {before} | after re-exclude: {DescribeBrainLayers(owner, layers)}");
            }
        }

        /// <summary>"SAIN layers 5 (active 5), vanilla to exclude still active 0 []" for one bot's current brain (diagnostic, once per role).</summary>
        private static string DescribeBrainLayers(BotOwner owner, List<string> vanillaToExclude)
        {
            try
            {
                var baseBrain = owner.Brain.BaseBrain;
                List<string> sainNames = SAINLayerNames;
                int sain = 0;
                int sainActive = 0;
                List<string> vanillaActive = null;
                foreach (var layer in baseBrain._layers.Values)
                {
                    string name = layer.Name();
                    bool active = baseBrain.IsLayerActive(layer);
                    if (sainNames.Contains(name))
                    {
                        sain++;
                        if (active)
                        {
                            sainActive++;
                        }
                    }
                    else if (active && vanillaToExclude.Contains(name))
                    {
                        (vanillaActive ??= []).Add(name);
                    }
                }
                return $"SAIN layers {sain} (active {sainActive}), vanilla to exclude still active {vanillaActive?.Count ?? 0} [{(vanillaActive == null ? "" : string.Join(", ", vanillaActive))}]";
            }
            catch (Exception ex)
            {
                return $"layer read failed: {ex.GetType().Name}";
            }
        }

        // Bots whose "shot by an enemy SAIN didn't know" reveal was already logged this raid (BotHitByEnemyClass).
        private static readonly HashSet<string> _hitRevealReported = [];

        public static bool ReportHitRevealOnce(string profileId) => _hitRevealReported.Add(profileId);

        private static void ApplyModdedBotLayers(bool raidStart)
        {
            int types = 0;
            int layersMissing = 0;
            int exclusionsMissing = 0;
            List<string> names = null;

            foreach (BotType botType in BotTypeDefinitions.BotTypesList)
            {
                List<string> brains = botType.BrainsToApply;
                if (brains == null || brains.Count == 0)
                {
                    if (string.IsNullOrEmpty(botType.BaseBrain))
                    {
                        continue;
                    }

                    brains = [botType.BaseBrain];
                }

                // A mod that runs its own AI can keep SAIN off its bots through the exclusion list.
                if (SAIN.Components.BotController.BotSpawnController.IsExcluded(botType.WildSpawnType))
                {
                    continue;
                }

                List<string> layers = [.. _commonVanillaLayersToRemove, .. _moddedExtraVanillaLayersToRemove];

                if (botType.LayersToRemove != null)
                {
                    layers.AddRange(botType.LayersToRemove);
                }

                List<WildSpawnType> roles = [botType.WildSpawnType];

                if (raidStart)
                {
                    types++;
                    (names ??= []).Add(botType.Name);
                    for (int i = 0; i < brains.Count; i++)
                    {
                        if (!HasCustomLayer(typeof(CombatSoloLayer), brains[i], botType.WildSpawnType))
                        {
                            layersMissing++;
                        }
                        for (int j = 0; j < layers.Count; j++)
                        {
                            if (!IsLayerExcluded(layers[j], brains[i], botType.WildSpawnType))
                            {
                                exclusionsMissing++;
                            }
                        }
                    }
                }

                AddCustomLayersToBrainsAndRoles(brains, roles, false);
                ToggleVanillaLayersForBrainsAndRoles(brains, roles, layers, false);

                Logger.LogInfo($"[SAIN] Applied SAIN layers to modded bot '{botType.Name}' ({botType.WildSpawnType}).");
            }

            if (raidStart && types > 0)
            {
                Logger.LogWarning(
                    $"[SAIN ModdedBots] raid start: SAIN layers checked for {types} modded bot type(s) ({string.Join(", ", names)}) - "
                        + $"combat layer re-added {layersMissing}, vanilla layer exclusions re-applied {exclusionsMissing}"
                );
            }
        }

        private static bool HasCustomLayer(Type layerType, string brain, WildSpawnType role)
        {
            foreach (BrainManager.LayerInfo info in BrainManager.CustomLayersReadOnly.Values)
            {
                if (info.customLayerType == layerType && ListHas(info.CustomLayerBrains, brain) && ListHas(info.CustomLayerRoles, role))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsLayerExcluded(string layerName, string brain, WildSpawnType role)
        {
            foreach (BrainManager.ExcludeLayerInfo info in BrainManager.ExcludeLayersReadOnly)
            {
                if (info.excludeLayerName == layerName && ListHas(info.ExcludeLayerBrains, brain) && ListHas(info.ExcludeLayerRoles, role))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ListHas<T>(IReadOnlyList<T> list, T value)
        {
            var comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < list.Count; i++)
            {
                if (comparer.Equals(list[i], value))
                {
                    return true;
                }
            }
            return false;
        }

        public static void ToggleVanillaLayersForAllBots()
        {
            ToggleVanillaLayersForScavs(VanillaBotSettings.VanillaScavs);
            ToggleVanillaLayersForRogues(VanillaBotSettings.VanillaRogues);
            ToggleVanillaLayersForRaiders([WildSpawnType.pmcBot], VanillaBotSettings.VanillaRaiders);
            ToggleVanillaLayersForBloodHounds(VanillaBotSettings.VanillaBloodHounds);
            ToggleVanillaLayersForNormalBosses(VanillaBotSettings.VanillaBosses);
            ToggleVanillaLayersForNormalFollowers(VanillaBotSettings.VanillaFollowers);
            ToggleVanillaLayersForGoons(VanillaBotSettings.VanillaGoons);
            ToggleVanillaLayersForLaybrinthBots(VanillaBotSettings.VanillaLabyrinthBots);
            ToggleVanillaLayersForCultists(VanillaBotSettings.VanillaCultists);
            ToggleVanillaLayersForSpecialBots(VanillaBotSettings.VanillaSpecialBots);
        }

        public static void ToggleVanillaLayersForScavs(bool useVanillaLayers)
        {
            List<string> brainList = GetBrainList(AIBrains.Scavs);

            List<string> LayersToToggle = ["Help", "AssaultEnemyFar", .. _commonVanillaLayersToRemove];

            ToggleVanillaLayers(brainList, LayersToToggle, useVanillaLayers);

            ToggleVanillaLayersForRaiders([WildSpawnType.assaultGroup], useVanillaLayers);
        }

        public static void ToggleVanillaLayersForRaiders(List<WildSpawnType> roles, bool useVanillaLayers)
        {
            List<string> brainList = [nameof(EBrain.PMC)];

            List<string> LayersToToggle =
            [
                "AssaultEnemyFar", // Used by Raiders and Scav groups
                .. _commonVanillaLayersToRemove,
            ];

            ToggleVanillaLayers(brainList, LayersToToggle, roles, useVanillaLayers);
        }

        public static void ToggleVanillaLayersForSpecialBots(bool useVanillaLayers)
        {
            List<string> brainList = GetBrainList(AIBrains.SpecialBots);

            List<string> LayersToToggle =
            [
                "ObdolbosFight", // Used by crazy event Scavs
                .. _commonVanillaLayersToRemove,
            ];

            ToggleVanillaLayers(brainList, LayersToToggle, useVanillaLayers);
        }

        public static void ToggleVanillaLayersForRogues(bool useVanillaLayers)
        {
            List<string> brainList = [nameof(EBrain.ExUsec)];

            List<string> LayersToToggle = [.. _commonVanillaLayersToRemove];

            ToggleVanillaLayers(brainList, LayersToToggle, useVanillaLayers);
        }

        public static void ToggleVanillaLayersForBloodHounds(bool useVanillaLayers)
        {
            List<string> brainList = [nameof(EBrain.ArenaFighter)];

            List<string> LayersToToggle = [.. _commonVanillaLayersToRemove];

            ToggleVanillaLayers(brainList, LayersToToggle, useVanillaLayers);

            ToggleVanillaLayersForRaiders([WildSpawnType.arenaFighterEvent], useVanillaLayers);
        }

        public static void ToggleVanillaLayersForLaybrinthBots(bool useVanillaLayers)
        {
            List<string> brainList = GetBrainList(AIBrains.LabyrinthBots);

            List<string> LayersToToggle =
            [
                "KillaAgro", // Used by Vengeful Killa
                "TagillaAgro", // Used by Shadow of Tagilla
                .. _commonVanillaLayersToRemove,
            ];

            ToggleVanillaLayers(brainList, LayersToToggle, useVanillaLayers);
        }

        public static void ToggleVanillaLayersForCultists(bool useVanillaLayers)
        {
            List<string> brainList = GetBrainList(AIBrains.Cultists);

            List<string> LayersToToggle =
            [
                //"GrenSuicide", // Used by Cultist priests
                "Run&Strike", // Used by Cultist followers
                "MeleeS_IN", // Used by Cultist followers
                "R&H_OUT", // Used by Cultist priests
                "SupShootSect_IN", // Used by Cultist followers
                .. _commonVanillaLayersToRemove,
                .. _commonVanillaBossAndFollowerLayersToRemove,
            ];

            ToggleVanillaLayers(brainList, LayersToToggle, useVanillaLayers);
        }

        public static void ToggleVanillaLayersForNormalBosses(bool useVanillaLayers)
        {
            List<string> brainList = GetBrainList(AIBrains.NormalBosses);

            List<string> LayersToToggle =
            [
                //"PartisanMine", // Used by Partisan
                //"PartMineAll", // Used by Partisan
                "PrtFMN", // Used by Partisan
                "PrtPst", // Used by Partisan
                "PrtZrSvg", // Used by Partisan
                "PrtFight", // Used by Partisan
                "PrtBadTrg", // Used by Partisan
                "PrtMany", // Used by Partisan
                "PrtStalk", // Used by Partisan
                "HoldOrCoverT", // Used by Partisan
                "BossBoarFight", // Used by Kaban
                "BossGlFight", // Used by Gluhar
                "KojaniyB_Enemy", // Used by Shturman
                "Bully Layer", // Used by Reshala
                "KlnSolo", // Used by Kollontay
                "KolontayFight", // Used by Kollontay
                .. _commonVanillaLayersToRemove,
                .. _commonVanillaBossAndFollowerLayersToRemove,
            ];
            ToggleVanillaLayers(brainList, LayersToToggle, useVanillaLayers);
        }

        public static void ToggleVanillaLayersForNormalFollowers(bool useVanillaLayers)
        {
            List<string> brainList = GetBrainList(AIBrains.NormalFollowers);

            List<string> LayersToToggle =
            [
                "BoarStationary", // Used by Kaban's followers
                "BoarPatrol", // Used by Kaban's followers
                "BoarClPatrol", // Used by Kaban's followers
                "FBoarFght", // Used by Kaban's followers
                "SecurityKln", // Used by Kollontay's followers
                "KlnForceAtk", // Used by Kollontay's followers
                "KolontayAP", // Used by Kollontay's followers
                "FolKojEnemy", // Used by Shturman's followers
                "GluharKilla", // Used by Gluhar's followers
                "GluhAssKilla", // Used by Gluhar's followers
                "FlGlScout", // Used by Gluhar's followers
                "GlGoal", // Used by Gluhar's followers
                "FlSanFight", // Used by Sanitar's followers
                "TagillaFollower", // Used by Tagilla's followers
                "Follower bully", // Used by Reshala's followers
                .. _commonVanillaLayersToRemove,
                .. _commonVanillaBossAndFollowerLayersToRemove,
            ];

            ToggleVanillaLayers(brainList, LayersToToggle, useVanillaLayers);
        }

        public static void ToggleVanillaLayersForGoons(bool useVanillaLayers)
        {
            List<string> brainList = GetBrainList(AIBrains.Goons);

            List<string> LayersToToggle =
            [
                "KnightFight", // Used by Knight
                "Assault Building", // Used by Knight, Big Pipe, and Birdeye
                "Enemy Building", // Used by Knight, Big Pipe, and Birdeye
                "BirdHold", // Used by Birdeye
                .. _commonVanillaLayersToRemove,
                .. _commonVanillaBossAndFollowerLayersToRemove,
            ];

            ToggleVanillaLayers(brainList, LayersToToggle, useVanillaLayers);
        }

        public static void ToggleVanillaLayersForPMCs(bool useVanillaLayers)
        {
            List<string> brainList = GetBrainList(AIBrains.PMCs);

            List<string> LayersToToggle = ["PmcBear", "PmcUsec", .. _commonVanillaLayersToRemove];

            ToggleVanillaLayers(brainList, LayersToToggle, useVanillaLayers);

            if (INCLUDE_RAIDER_BRAIN_FOR_PMCS)
            {
                ToggleVanillaLayersForRaiders([WildSpawnType.pmcBEAR, WildSpawnType.pmcUSEC], useVanillaLayers);
            }
        }

        public static void ToggleVanillaLayersForBrains(List<string> brainList, List<string> layersToToggle, bool useVanillaLayers)
        {
            ToggleVanillaLayers(brainList, layersToToggle, useVanillaLayers);
        }

        public static void ToggleVanillaLayersForBrainsAndRoles(
            List<string> brainList,
            List<WildSpawnType> roles,
            List<string> layersToToggle,
            bool useVanillaLayers
        )
        {
            ToggleVanillaLayers(brainList, layersToToggle, roles, useVanillaLayers);
        }

        public static void AddCustomLayersToBrains(List<string> brainList, bool withExtract)
        {
            var settings = SAINPlugin.LoadedPreset.GlobalSettings.General.Layers;

            BrainManager.AddCustomLayer(typeof(DebugLayer), brainList, 99);
            BrainManager.AddCustomLayer(typeof(SAINAvoidThreatLayer), brainList, 80);
            BrainManager.AddCustomLayer(typeof(SAINFlashedLayer), brainList, 85);
            BrainManager.AddCustomLayer(typeof(CombatSquadLayer), brainList, settings.SAINCombatSquadLayerPriority);
            BrainManager.AddCustomLayer(typeof(CombatSoloLayer), brainList, settings.SAINCombatSoloLayerPriority);

            if (withExtract)
            {
                BrainManager.AddCustomLayer(typeof(ExtractLayer), brainList, settings.SAINExtractLayerPriority);
            }
        }

        public static void AddCustomLayersToBrainsAndRoles(List<string> brainList, List<WildSpawnType> roles, bool withExtract)
        {
            var settings = SAINPlugin.LoadedPreset.GlobalSettings.General.Layers;

            // zzap (2026-10-23): only where missing. MoreBotsAPI calls this again from TarkovApplication.Init and the
            // raid-start reassert calls it every raid; BigBrain stacks a second copy of a layer on every repeat call.
            AddCustomLayerWhereMissing(typeof(DebugLayer), brainList, 99, roles);
            AddCustomLayerWhereMissing(typeof(SAINAvoidThreatLayer), brainList, 80, roles);
            AddCustomLayerWhereMissing(typeof(SAINFlashedLayer), brainList, 85, roles);
            AddCustomLayerWhereMissing(typeof(CombatSquadLayer), brainList, settings.SAINCombatSquadLayerPriority, roles);
            AddCustomLayerWhereMissing(typeof(CombatSoloLayer), brainList, settings.SAINCombatSoloLayerPriority, roles);

            if (withExtract)
            {
                AddCustomLayerWhereMissing(typeof(ExtractLayer), brainList, settings.SAINExtractLayerPriority, roles);
            }
        }

        private static void AddCustomLayerWhereMissing(Type layerType, List<string> brainList, int priority, List<WildSpawnType> roles)
        {
            for (int i = 0; i < brainList.Count; i++)
            {
                List<WildSpawnType> missing = null;
                for (int j = 0; j < roles.Count; j++)
                {
                    if (!HasCustomLayer(layerType, brainList[i], roles[j]))
                    {
                        (missing ??= []).Add(roles[j]);
                    }
                }

                if (missing != null)
                {
                    BrainManager.AddCustomLayer(layerType, [brainList[i]], priority, missing);
                }
            }
        }

        private static void ToggleVanillaLayers(List<string> brainNames, List<string> layerNames, bool useVanillaLayers)
        {
            if (useVanillaLayers)
            {
                BrainManager.RemoveLayers(SAINLayerNames, brainNames);
                BrainManager.RestoreLayers(layerNames, brainNames);
            }
            else
            {
                CheckExtractEnabled(layerNames);

                BrainManager.RestoreLayers(SAINLayerNames, brainNames);
                BrainManager.RemoveLayers(layerNames, brainNames);
            }
        }

        private static void ToggleVanillaLayers(
            List<string> brainNames,
            List<string> layerNames,
            List<WildSpawnType> roles,
            bool useVanillaLayers
        )
        {
            if (useVanillaLayers)
            {
                BrainManager.RemoveLayers(SAINLayerNames, brainNames, roles);
                BrainManager.RestoreLayers(layerNames, brainNames, roles);
            }
            else
            {
                CheckExtractEnabled(layerNames);

                BrainManager.RestoreLayers(SAINLayerNames, brainNames, roles);
                BrainManager.RemoveLayers(layerNames, brainNames, roles);
            }
        }

        private static void AddCustomLayersToPMCs()
        {
            List<string> pmcBrain = GetBrainList(AIBrains.PMCs);
            var settings = SAINPlugin.LoadedPreset.GlobalSettings.General.Layers;

            BrainManager.AddCustomLayer(typeof(DebugLayer), pmcBrain, 99);
            BrainManager.AddCustomLayer(typeof(SAINAvoidThreatLayer), pmcBrain, 80);
            BrainManager.AddCustomLayer(typeof(SAINFlashedLayer), pmcBrain, 85);
            BrainManager.AddCustomLayer(typeof(ExtractLayer), pmcBrain, settings.SAINExtractLayerPriority);
            BrainManager.AddCustomLayer(typeof(CombatSquadLayer), pmcBrain, settings.SAINCombatSquadLayerPriority);
            BrainManager.AddCustomLayer(typeof(CombatSoloLayer), pmcBrain, settings.SAINCombatSoloLayerPriority);

            if (INCLUDE_RAIDER_BRAIN_FOR_PMCS)
            {
                AddCustomLayersToRaiders([WildSpawnType.pmcBEAR, WildSpawnType.pmcUSEC]);
            }
        }

        private static void AddCustomLayersToScavs()
        {
            List<string> brainList = GetBrainList(AIBrains.Scavs);
            var settings = SAINPlugin.LoadedPreset.GlobalSettings.General.Layers;

            //BrainManager.AddCustomLayer(typeof(BotUnstuckLayer), stringList, 98);
            BrainManager.AddCustomLayer(typeof(DebugLayer), brainList, 99);
            BrainManager.AddCustomLayer(typeof(SAINAvoidThreatLayer), brainList, 80);
            BrainManager.AddCustomLayer(typeof(SAINFlashedLayer), brainList, 85);
            BrainManager.AddCustomLayer(typeof(ExtractLayer), brainList, settings.SAINExtractLayerPriority);
            BrainManager.AddCustomLayer(typeof(CombatSquadLayer), brainList, settings.SAINCombatSquadLayerPriority);
            BrainManager.AddCustomLayer(typeof(CombatSoloLayer), brainList, settings.SAINCombatSoloLayerPriority);

            AddCustomLayersToRaiders([WildSpawnType.assaultGroup]);
        }

        private static void AddCustomLayersToRaiders(List<WildSpawnType> roles)
        {
            var settings = SAINPlugin.LoadedPreset.GlobalSettings.General.Layers;
            List<string> raiderBrain = [nameof(EBrain.PMC)];

            BrainManager.AddCustomLayer(typeof(DebugLayer), raiderBrain, 99, roles);
            BrainManager.AddCustomLayer(typeof(SAINAvoidThreatLayer), raiderBrain, 80, roles);
            BrainManager.AddCustomLayer(typeof(SAINFlashedLayer), raiderBrain, 85, roles);
            BrainManager.AddCustomLayer(typeof(ExtractLayer), raiderBrain, settings.SAINExtractLayerPriority, roles);
            BrainManager.AddCustomLayer(typeof(CombatSquadLayer), raiderBrain, settings.SAINCombatSquadLayerPriority, roles);
            BrainManager.AddCustomLayer(typeof(CombatSoloLayer), raiderBrain, settings.SAINCombatSoloLayerPriority, roles);
        }

        private static void AddCustomLayersToSpecialBots()
        {
            List<string> brainList = GetBrainList(AIBrains.SpecialBots);

            var settings = SAINPlugin.LoadedPreset.GlobalSettings.General.Layers;
            //BrainManager.AddCustomLayer(typeof(BotUnstuckLayer), stringList, 98);
            BrainManager.AddCustomLayer(typeof(DebugLayer), brainList, 99);
            BrainManager.AddCustomLayer(typeof(SAINAvoidThreatLayer), brainList, 80);
            BrainManager.AddCustomLayer(typeof(SAINFlashedLayer), brainList, 85);
            BrainManager.AddCustomLayer(typeof(ExtractLayer), brainList, settings.SAINExtractLayerPriority);
            BrainManager.AddCustomLayer(typeof(CombatSquadLayer), brainList, settings.SAINCombatSquadLayerPriority);
            BrainManager.AddCustomLayer(typeof(CombatSoloLayer), brainList, settings.SAINCombatSoloLayerPriority);
        }

        private static void AddCustomLayersToLabyrinthBots()
        {
            List<string> brainList = GetBrainList(AIBrains.LabyrinthBots);
            var settings = SAINPlugin.LoadedPreset.GlobalSettings.General.Layers;

            //BrainManager.AddCustomLayer(typeof(BotUnstuckLayer), stringList, 98);
            BrainManager.AddCustomLayer(typeof(DebugLayer), brainList, 99);
            BrainManager.AddCustomLayer(typeof(SAINAvoidThreatLayer), brainList, 80);
            BrainManager.AddCustomLayer(typeof(SAINFlashedLayer), brainList, 85);
            BrainManager.AddCustomLayer(typeof(ExtractLayer), brainList, settings.SAINExtractLayerPriority);
            BrainManager.AddCustomLayer(typeof(CombatSquadLayer), brainList, settings.SAINCombatSquadLayerPriority);
            BrainManager.AddCustomLayer(typeof(CombatSoloLayer), brainList, settings.SAINCombatSoloLayerPriority);
        }

        private static void AddCustomLayersToCultists()
        {
            List<string> brainList = GetBrainList(AIBrains.Cultists);

            //BrainManager.AddCustomLayer(typeof(BotUnstuckLayer), stringList, 146);
            BrainManager.AddCustomLayer(typeof(DebugLayer), brainList, 150);
            BrainManager.AddCustomLayer(typeof(SAINAvoidThreatLayer), brainList, 118);
            BrainManager.AddCustomLayer(typeof(SAINFlashedLayer), brainList, 123);
            BrainManager.AddCustomLayer(typeof(ExtractLayer), brainList, 108);
            BrainManager.AddCustomLayer(typeof(CombatSquadLayer), brainList, 104);
            BrainManager.AddCustomLayer(typeof(CombatSoloLayer), brainList, 102);
        }

        private static void AddCustomLayersToRogues()
        {
            List<string> brainList = [nameof(EBrain.ExUsec)];

            var settings = SAINPlugin.LoadedPreset.GlobalSettings.General.Layers;
            //BrainManager.AddCustomLayer(typeof(BotUnstuckLayer), stringList, 98);
            BrainManager.AddCustomLayer(typeof(DebugLayer), brainList, 99);
            BrainManager.AddCustomLayer(typeof(SAINAvoidThreatLayer), brainList, 80);
            BrainManager.AddCustomLayer(typeof(SAINFlashedLayer), brainList, 85);
            BrainManager.AddCustomLayer(typeof(ExtractLayer), brainList, settings.SAINExtractLayerPriority);
            BrainManager.AddCustomLayer(typeof(CombatSquadLayer), brainList, settings.SAINCombatSquadLayerPriority);
            BrainManager.AddCustomLayer(typeof(CombatSoloLayer), brainList, settings.SAINCombatSoloLayerPriority);
        }

        private static void AddCustomLayersToBloodHounds()
        {
            List<string> brainList = [nameof(EBrain.ArenaFighter)];

            var settings = SAINPlugin.LoadedPreset.GlobalSettings.General.Layers;
            //BrainManager.AddCustomLayer(typeof(BotUnstuckLayer), stringList, 98);
            BrainManager.AddCustomLayer(typeof(DebugLayer), brainList, 99);
            BrainManager.AddCustomLayer(typeof(SAINAvoidThreatLayer), brainList, 80);
            BrainManager.AddCustomLayer(typeof(SAINFlashedLayer), brainList, 85);
            BrainManager.AddCustomLayer(typeof(ExtractLayer), brainList, settings.SAINExtractLayerPriority);
            BrainManager.AddCustomLayer(typeof(CombatSquadLayer), brainList, settings.SAINCombatSquadLayerPriority);
            BrainManager.AddCustomLayer(typeof(CombatSoloLayer), brainList, settings.SAINCombatSoloLayerPriority);
        }

        private static void AddCustomLayersToNormalBosses()
        {
            List<string> brainList = GetBrainList(AIBrains.NormalBosses);

            //var settings = SAINPlugin.LoadedPreset.GlobalSettings.General;
            //BrainManager.AddCustomLayer(typeof(BotUnstuckLayer), stringList, 98);
            BrainManager.AddCustomLayer(typeof(DebugLayer), brainList, 99);
            BrainManager.AddCustomLayer(typeof(SAINAvoidThreatLayer), brainList, 80);
            BrainManager.AddCustomLayer(typeof(SAINFlashedLayer), brainList, 85);
            BrainManager.AddCustomLayer(typeof(CombatSquadLayer), brainList, 70);
            BrainManager.AddCustomLayer(typeof(CombatSoloLayer), brainList, 69);
        }

        private static void AddCustomLayersToNormalFollowers()
        {
            List<string> brainList = GetBrainList(AIBrains.NormalFollowers);

            //var settings = SAINPlugin.LoadedPreset.GlobalSettings.General;
            //BrainManager.AddCustomLayer(typeof(BotUnstuckLayer), stringList, 98);
            BrainManager.AddCustomLayer(typeof(DebugLayer), brainList, 99);
            BrainManager.AddCustomLayer(typeof(SAINAvoidThreatLayer), brainList, 80);
            BrainManager.AddCustomLayer(typeof(SAINFlashedLayer), brainList, 85);
            BrainManager.AddCustomLayer(typeof(CombatSquadLayer), brainList, 70);
            BrainManager.AddCustomLayer(typeof(CombatSoloLayer), brainList, 69);
        }

        private static void AddCustomLayersToGoons()
        {
            List<string> brainList = GetBrainList(AIBrains.Goons);

            BrainManager.AddCustomLayer(typeof(DebugLayer), brainList, 99);
            BrainManager.AddCustomLayer(typeof(SAINAvoidThreatLayer), brainList, 80);
            BrainManager.AddCustomLayer(typeof(SAINFlashedLayer), brainList, 85);
            BrainManager.AddCustomLayer(typeof(CombatSquadLayer), brainList, 64);
            BrainManager.AddCustomLayer(typeof(CombatSoloLayer), brainList, 62);
        }

        private static void CheckExtractEnabled(List<string> layersToRemove)
        {
            var extract = GlobalSettingsClass.Instance.General.Extract;
            if ((extract.SAIN_EXTRACT_TOGGLE || extract.DisableVanillaExtract) && !layersToRemove.Contains("Exfiltration"))
            {
                layersToRemove.Add("Exfiltration");
            }
        }

        private static List<string> GetBrainList(List<EBrain> brains)
        {
            List<string> brainList = [];
            for (int i = 0; i < brains.Count; i++)
            {
                brainList.Add(brains[i].ToString());
            }
            return brainList;
        }

        private static VanillaBotSettings VanillaBotSettings
        {
            get { return SAINPlugin.LoadedPreset.GlobalSettings.General.VanillaBots; }
        }
    }
}
