using System.Reflection;
using EFT;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SAIN;
using SAIN.Plugin;
using SAIN.Preset;
using SAIN.Preset.Server;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.Preset;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using DrakiaXYZ.BigBrain.Brains;

class Program
{
    static int failed, passed;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Test(string name, Action body) { try { body(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + (e.InnerException?.Message ?? e.Message)); } }
    static readonly JsonSerializerSettings JsonSettings = new() { Formatting = Formatting.Indented, Converters = { new StringEnumConverter() } };
    static SAINPresetBundle Fixture(string name = "zzap") { var b = new SAINPresetBundle { GlobalSettings = new(), Info = new() { Name = name, IsCustom = true } }; var g = new SAINSettingsGroupClass(Enum.GetValues<ESainBotDifficulty>()); foreach (var s in g.Settings.Values) s.Core.VisibleDistance = 225f; b.BotSettings[ESainWildSpawnType.pmcUSEC] = g; return b; }
    static void LoadFixture() { ServerPresetClient.ThrowOnSave = false; BotTypeDefinitions.BotTypes.Clear(); ServerPresetClient.Json.Clear(); ServerPresetClient.Json["zzap"] = JsonConvert.SerializeObject(Fixture(), JsonSettings); PresetSync.PullCustomPresets(); PresetHandler.LoadedPreset = new() { Info = new() { Name = "zzap", IsCustom = true } }; PresetHandler.LoadedPreset.MakeCurrent(); }
    static SAINPresetBundle SavedCustom() { var method = typeof(PresetSync).GetMethod("TryGetSavedCustomBundle"); Check(method != null, "saved-reference API missing"); object[] args = { "zzap", null }; Check((bool) method.Invoke(null, args), "saved reference missing"); return (SAINPresetBundle) args[1]; }
    static SAINSettingsGroupClass Group(SAINPresetBundle b) => b.BotSettings[ESainWildSpawnType.pmcUSEC];
    static void Role(WildSpawnType r, string brain, bool vanilla) { BotTypeDefinitions.BotTypes[r] = new() { WildSpawnType = r, Name = r.ToString(), BaseBrain = brain }; PresetHandler.LoadedPreset.BotSettings.SAINSettings[r] = new SAINSettingsGroupClass(Enum.GetValues<ESainBotDifficulty>()) { UseVanillaAI = vanilla }; }
    static BotOwner Bot(WildSpawnType role) { var b = new BotOwner(); b.Profile.Info.Settings.Role = role; b.GetPlayer.AIData.BotOwner = b; return b; }
    static int Main()
    {
        Test("saved custom reference is independent of live edits", () => { LoadFixture(); PresetSync.TryGetCustomBundle("zzap", out var live); Group(live).Settings[ESainBotDifficulty.normal].Core.VisibleDistance = 999; var saved = SavedCustom(); Check(Group(saved).Settings[ESainBotDifficulty.normal].Core.VisibleDistance == 225, "edited value used as reference"); Check(!ReferenceEquals(Group(saved), Group(live)), "source aliases edited group"); Check(ReferenceEquals(SAIN.Preset.Shared.GlobalSettings.GlobalSettingsClass.Instance, PresetHandler.LoadedPreset.GlobalSettings), "copy changed active globals"); });
        Test("F6 levels can be picked repeatedly on loaded zzap", () =>
        {
            LoadFixture();
            PresetSync.TryGetCustomBundle("zzap", out var live);
            var saved = SavedCustom();
            var original = Group(saved);
            var easy = Fixture("zzap easy");
            easy.GlobalSettings.Difficulty.VisibleDistCoef = 0.5f;
            var src = new LevelSources
            {
                Reference = original, Current = LevelGlobals.From(live),
                Easy = LevelGlobals.From(easy), Normal = LevelGlobals.From(saved), Hard = LevelGlobals.From(saved),
                EasyBot = Group(easy), NormalBot = original, HardBot = original,
            };
            var g = Group(live);
            Check(BotLevels.Compute(EBotLevel.Easy, g, src) != null, "valid level refused");
            BotLevels.Apply(EBotLevel.Easy, g, src);
            float first = g.Settings[ESainBotDifficulty.normal].Core.VisibleDistance;
            Check(Math.Abs(first - 112.5f) < 0.001f, "easy did not halve the independent 225 m reference");
            BotLevels.Apply(EBotLevel.Hard, g, src);
            Check(Math.Abs(g.Settings[ESainBotDifficulty.normal].Core.VisibleDistance - 225f) < 0.001f, "hard did not restore 225 m");
            BotLevels.Apply(EBotLevel.Easy, g, src);
            Check(Math.Abs(g.Settings[ESainBotDifficulty.normal].Core.VisibleDistance - first) < 0.001f, "level compounds");
        });
        Test("revert uses saved values while live preset is dirty", () => { LoadFixture(); PresetSync.TryGetCustomBundle("zzap", out var live); Group(live).Settings[ESainBotDifficulty.normal].Core.VisibleDistance = 999; var copy = BotLevels.Snapshot(Group(SavedCustom())); BotLevels.RestoreSaved(Group(live), copy); Check(Group(live).Settings[ESainBotDifficulty.normal].Core.VisibleDistance == 225, "unsaved value captured by revert"); });
        Test("saved reference refreshes after remote update", () => { LoadFixture(); _ = SavedCustom(); var updated = Fixture(); Group(updated).Settings[ESainBotDifficulty.normal].Core.VisibleDistance = 333; ServerPresetClient.Json["zzap"] = JsonConvert.SerializeObject(updated, JsonSettings); PresetSync.ApplyRemoteChange(new() { PresetName = "zzap" }); Check(Group(SavedCustom()).Settings[ESainBotDifficulty.normal].Core.VisibleDistance == 333, "old reference reused after update"); });
        Test("saved default reference survives edits to generated preset", () => { LoadFixture(); var fixture = Fixture("default"); fixture.Info.IsCustom = false; ServerPresetClient.DefaultsJson = JsonConvert.SerializeObject(new[] { fixture }, JsonSettings); PresetSync.RefreshServerDefaults(); Group(PresetSync.ServerDefaults()[0]).Settings[ESainBotDifficulty.normal].Core.VisibleDistance = 999; var method = typeof(PresetSync).GetMethod("SavedServerDefaults"); Check(method != null, "saved-default API missing"); var saved = (IEnumerable<SAINPresetBundle>) method.Invoke(null, null); Check(Group(saved.First()).Settings[ESainBotDifficulty.normal].Core.VisibleDistance == 225, "default aliases live values"); });
        Test("per-bot vanilla excludes PMC SAIN components", () => { LoadFixture(); Role(WildSpawnType.pmcUSEC, "PmcUsec", true); var b = Bot(WildSpawnType.pmcUSEC); Check(SAINEnableClass.IsBotExcluded(b), "PMC bypassed per-bot vanilla"); Check(SAINEnableClass.IsSAINDisabledForBot(b), "BotOwner path still enabled"); Check(SAINEnableClass.IsSAINDisabledForBot((IPlayer) b.GetPlayer), "IPlayer path still enabled"); });
        Test("per-bot vanilla excludes modded SAIN components", () => { LoadFixture(); var role = (WildSpawnType) 848421; Role(role, "PmcUsec", true); Check(SAINEnableClass.IsBotExcluded(Bot(role)), "modded bot still enabled"); });
        Test("global scav lock follows roles not a shared PMC brain", () => { LoadFixture(); Role(WildSpawnType.marksman, "PmcUsec", false); Role(WildSpawnType.pmcUSEC, "PmcUsec", false); var settings = PresetHandler.LoadedPreset.GlobalSettings.General.VanillaBots; settings.VanillaScavs = true; Check(BigBrainHandler.BrainAssignment.VanillaByGroupSetting(WildSpawnType.marksman, out _), "marksman missing group lock"); Check(!BigBrainHandler.BrainAssignment.VanillaByGroupSetting(WildSpawnType.pmcUSEC, out _), "PMC incorrectly locked as scav"); });
        Test("per-bot vanilla restores extraction after SAIN removal", () => { LoadFixture(); BotTypeDefinitions.BotTypes.Clear(); Role(WildSpawnType.pmcUSEC, "PmcUsec", true); var extract = PresetHandler.LoadedPreset.GlobalSettings.General.Extract; extract.SAIN_EXTRACT_TOGGLE = true; BrainManager.Disabled.Clear(); BigBrainHandler.BrainAssignment.ToggleVanillaLayersForPMCs(false); Check(BrainManager.Disabled.Contains(("Exfiltration", "PmcUsec", WildSpawnType.pmcUSEC)), "fixture did not remove extraction"); BigBrainHandler.BrainAssignment.ApplyPerBotVanilla(); Check(!BrainManager.Disabled.Contains(("Exfiltration", "PmcUsec", WildSpawnType.pmcUSEC)), "vanilla extraction remains removed"); Check(BrainManager.Disabled.Contains(("Solo", "PmcUsec", WildSpawnType.pmcUSEC)), "SAIN combat still attached"); Check(!BrainManager.Disabled.Contains(("Solo", "PmcUsec", WildSpawnType.pmcBEAR)), "unselected role affected"); });

        Test("remote save matching unsaved edits still advances saved baseline", () => { LoadFixture(); _ = SavedCustom(); PresetSync.TryGetCustomBundle("zzap", out var live); Group(live).Settings[ESainBotDifficulty.normal].Core.VisibleDistance = 333; ServerPresetClient.Json["zzap"] = JsonConvert.SerializeObject(live, JsonSettings); PresetSync.ApplyRemoteChange(new() { PresetName = "zzap" }); Check(Group(SavedCustom()).Settings[ESainBotDifficulty.normal].Core.VisibleDistance == 333, "equal live values hid newly saved state"); });
        Test("failed save keeps previous saved baseline", () => { LoadFixture(); _ = SavedCustom(); var preset = PresetHandler.LoadedPreset; Role(WildSpawnType.pmcUSEC, "PmcUsec", false); preset.BotSettings.SAINSettings[WildSpawnType.pmcUSEC].Settings[ESainBotDifficulty.normal].Core.VisibleDistance = 999; ServerPresetClient.ThrowOnSave = true; PresetSync.PublishCustom(preset, preset.Info); Check(Group(SavedCustom()).Settings[ESainBotDifficulty.normal].Core.VisibleDistance == 225, "failed save replaced saved reference"); });
        Test("remote deletion removes saved reference", () => { LoadFixture(); _ = SavedCustom(); PresetSync.ApplyRemoteChange(new() { PresetName = "zzap", Change = SAIN.Preset.Shared.Models.WS.EPresetSyncChange.Deleted }); object[] args = { "zzap", null }; Check(!(bool) typeof(PresetSync).GetMethod("TryGetSavedCustomBundle").Invoke(null, args), "deleted preset stayed available as reference"); });
        Test("vanilla global locks agree with exclusion across all role groups", () => { LoadFixture(); var settings = PresetHandler.LoadedPreset.GlobalSettings.General.VanillaBots; (string Flag, WildSpawnType Role)[] cases = { ("VanillaScavs", WildSpawnType.marksman), ("VanillaBosses", WildSpawnType.bossKilla), ("VanillaFollowers", WildSpawnType.followerBully), ("VanillaGoons", WildSpawnType.followerBirdEye), ("VanillaCultists", WildSpawnType.sectantPriest), ("VanillaRogues", WildSpawnType.exUsec), ("VanillaRaiders", WildSpawnType.pmcBot), ("VanillaBloodHounds", WildSpawnType.arenaFighterEvent), ("VanillaLabyrinthBots", WildSpawnType.bossTagillaAgro), ("VanillaSpecialBots", WildSpawnType.gifter) }; foreach (var c in cases) { foreach (var f in settings.GetType().GetFields().Where(f => f.FieldType == typeof(bool))) f.SetValue(settings, false); settings.GetType().GetField(c.Flag).SetValue(settings, true); Check(SAINEnableClass.ShallExludeByWildSpawnType(c.Role, Bot(c.Role)), c.Flag + " exclusion fixture"); Check(BigBrainHandler.BrainAssignment.VanillaByGroupSetting(c.Role, out _), c.Flag + " not locked"); Check(!BigBrainHandler.BrainAssignment.VanillaByGroupSetting(WildSpawnType.pmcUSEC, out _), c.Flag + " locked PMC"); Check(!BigBrainHandler.BrainAssignment.VanillaByGroupSetting((WildSpawnType) 848421, out _), c.Flag + " locked modded role sharing brain"); } });
        Test("SAIN enabled PMC remains enabled without per-bot override", () => { LoadFixture(); Role(WildSpawnType.pmcUSEC, "PmcUsec", false); Check(!SAINEnableClass.IsBotExcluded(Bot(WildSpawnType.pmcUSEC)), "unselected PMC disabled"); });
        Console.WriteLine($"regression: {passed} passed, {failed} failed"); return failed == 0 ? 0 : 1;
    }
}
