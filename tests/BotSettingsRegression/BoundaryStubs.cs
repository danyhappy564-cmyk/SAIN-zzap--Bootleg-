global using SAIN.Extensions;
using SAIN.Preset.Shared.Preset;
using SAIN.Preset.Shared.Personalities.BasePersonality;
using SAIN.Preset.Shared.GearStealthValues;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using SAIN.Preset.Shared.Models.Preset.Personalities;
namespace EFT
{
    public enum WildSpawnType
    {
        marksman = 0,
        assault = 1,
        bossTest = 2,
        bossBully = 3,
        followerTest = 4,
        followerBully = 5,
        bossKilla = 6,
        bossKojaniy = 7,
        followerKojaniy = 8,
        pmcBot = 9,
        cursedAssault = 10,
        bossGluhar = 11,
        followerGluharAssault = 12,
        followerGluharSecurity = 13,
        followerGluharScout = 14,
        followerGluharSnipe = 15,
        followerSanitar = 16,
        bossSanitar = 17,
        test = 18,
        assaultGroup = 19,
        sectantWarrior = 20,
        sectantPriest = 21,
        bossTagilla = 22,
        followerTagilla = 23,
        exUsec = 24,
        gifter = 25,
        bossKnight = 26,
        followerBigPipe = 27,
        followerBirdEye = 28,
        bossZryachiy = 29,
        followerZryachiy = 30,
        bossBoar = 32,
        followerBoar = 33,
        arenaFighter = 34,
        arenaFighterEvent = 35,
        bossBoarSniper = 36,
        crazyAssaultEvent = 37,
        peacefullZryachiyEvent = 38,
        sectactPriestEvent = 39,
        ravangeZryachiyEvent = 40,
        followerBoarClose1 = 41,
        followerBoarClose2 = 42,
        bossKolontay = 43,
        followerKolontayAssault = 44,
        followerKolontaySecurity = 45,
        shooterBTR = 46,
        bossPartisan = 47,
        spiritWinter = 48,
        spiritSpring = 49,
        peacemaker = 50,
        pmcBEAR = 51,
        pmcUSEC = 52,
        skier = 53,
        sectantPredvestnik = 57,
        sectantPrizrak = 58,
        sectantOni = 59,
        infectedAssault = 60,
        infectedPmc = 61,
        infectedCivil = 62,
        infectedLaborant = 63,
        infectedTagilla = 64,
        bossTagillaAgro = 65,
        bossKillaAgro = 66,
        tagillaHelperAgro = 67,
    }
    public enum BotDifficulty { easy, normal, hard, impossible }
    public class ProfileSettings { public WildSpawnType Role; }
    public class ProfileInfo { public ProfileSettings Settings = new(); public string Nickname = "bot"; public string MainProfileNickname; }
    public class Profile { public ProfileInfo Info = new(); public string Nickname => Info.Nickname; }
    public interface IPlayer { bool IsAI { get; } string ProfileId { get; } Profile Profile { get; } AIData AIData { get; } event Action<IPlayer> OnIPlayerDeadOrUnspawn; }
    public class AIData { public BotOwner BotOwner; }
    public class Player : IPlayer { public bool IsAI => true; public string ProfileId { get; set; } = Guid.NewGuid().ToString(); public Profile Profile { get; set; } = new(); public AIData AIData { get; } = new(); public event Action<IPlayer> OnIPlayerDeadOrUnspawn; }
    public class BotOwner { public Player GetPlayer = new(); public Profile Profile => GetPlayer.Profile; public string ProfileId => GetPlayer.ProfileId; public Brain Brain = new(); }
    public class Brain { public BaseBrain BaseBrain = new(); }
    public class BaseBrain { public Dictionary<int, Layer> _layers = new(); public string ShortName() => "Pmc"; public bool IsLayerActive(Layer layer) => true; }
    public class Layer { public string Name() => "Pmc"; }
    public static class GameWorld { public static event Action OnDispose; }
}
namespace SAIN
{
    public static class Logger { public static void LogInfo(string s) { } public static void LogError(string s) { } public static void LogWarning(string s) { } public static void LogDebug(string s) { } }
    public static class SAINPlugin { public static SAIN.Preset.SAINPresetClass LoadedPreset => SAIN.Plugin.PresetHandler.LoadedPreset; }
}
namespace SAIN.Extensions { public static class Extensions { public static bool IsNullOrEmpty(this string s) => string.IsNullOrEmpty(s); public static bool IsPmcBot(this EFT.WildSpawnType t) => t is EFT.WildSpawnType.pmcUSEC or EFT.WildSpawnType.pmcBEAR; public static bool IsSectant(this EFT.WildSpawnType t) => t is EFT.WildSpawnType.sectantPriest or EFT.WildSpawnType.sectantWarrior; public static ESainWildSpawnType ToESain(this EFT.WildSpawnType t) => (ESainWildSpawnType) (int) t; } }
// SAINEnableClass uses role extension methods through its existing global imports.
namespace SAIN.Components
{
    public class GameWorldComponent { public static GameWorldComponent Instance; }
    public class BotComponent { public bool SAINLayersActive; }
    public class BotManagerComponent { public static BotManagerComponent Instance; public Dictionary<string, BotComponent> Bots = new(); public SAIN.Components.BotController.BotSpawnController BotSpawnController = new(); }
}
namespace SAIN.Components.BotController { public class BotSpawnController { public static bool IsExcluded(EFT.WildSpawnType type) => false; public Dictionary<string, SAIN.Components.BotComponent> BotDictionary = new(); } }
namespace SAIN.Preset
{
    public class BotType { public EFT.WildSpawnType WildSpawnType; public string Name; public string BaseBrain; public List<string> BrainsToApply; public List<string> LayersToRemove; }
    public static class BotTypeDefinitions { public static Dictionary<EFT.WildSpawnType, BotType> BotTypes = new(); public static List<BotType> BotTypesList => BotTypes.Values.ToList(); public static bool IsModded(EFT.WildSpawnType t) => (int) t > 1000; }
    public class SettingsHolder { public Dictionary<EFT.WildSpawnType, SAINSettingsGroupClass> SAINSettings = new(); }
    public class PersonalityHolder { public Dictionary<EPersonality, PersonalitySettingsClass> PersonalityDictionary = new(); }
    public class GearHolder { public Dictionary<EEquipmentType, List<ItemStealthValue>> ItemStealthValues = new(); }
    public class SAINPresetClass
    {
        public SAINPresetDefinition Info; public GlobalSettingsClass GlobalSettings = new(); public SettingsHolder BotSettings = new(); public PersonalityHolder PersonalityManager = new(); public GearHolder GearStealthValuesClass = new();
        public void MakeCurrent() { GlobalSettingsClass.Instance = GlobalSettings; GlobalSettings.General.Debug.MakeCurrent(); }
    }
}
namespace SAIN.Plugin
{
    public static class PresetHandler { public static SAIN.Preset.SAINPresetClass LoadedPreset; public static void ApplyDefinition(SAINPresetDefinition d) { } public static void InitPresetFromDefinition(SAINPresetDefinition d) { } public static SAINPresetDefinition FindDefinitionByName(string s) => null; public static bool IsDefaultAvailable(string s) => true; public static SAINPresetDefinition NearestAvailableDefault(SAINDifficulty d) => null; public static void LoadCustomPresetOptions() { } }
}
namespace SAIN.Preset.Server
{
    public static class ServerPresetClient { public static Dictionary<string, string> Json = new(); public static string DefaultsJson; public static string GetGeneratedDefaults() => DefaultsJson; public static IEnumerable<string> ListCustom() => Json.Keys.ToArray(); public static string GetCustom(string name) => Json.GetValueOrDefault(name); public static void DeleteCustom(string n) => Json.Remove(n); public static bool ThrowOnSave; public static void SaveCustom(string n, string json) { if (ThrowOnSave) throw new InvalidOperationException("offline save failure"); Json[n] = json; } }
    public static class ServerConfigClient { public static bool ForcedActive; public static string ForcedPresetName; public static bool EditingAllowed = true; public static void Fetch() { } }
}
namespace SAIN.Layers { public class SAINLayer { } public class DebugLayer : SAINLayer { public static string Name = "Debug"; } public class ExtractLayer : SAINLayer { public static string Name = "Extract"; } }
namespace SAIN.Layers.Combat.Run { public class SAINAvoidThreatLayer : SAIN.Layers.SAINLayer { public static string Name = "Avoid"; } }
namespace SAIN.Layers.Combat.Solo { public class CombatSoloLayer : SAIN.Layers.SAINLayer { public static string Name = "Solo"; } }
namespace SAIN.Layers.Combat.Squad { public class CombatSquadLayer : SAIN.Layers.SAINLayer { public static string Name = "Squad"; } }
namespace SAIN.Layers.Flashed { public class SAINFlashedLayer : SAIN.Layers.SAINLayer { public static string Name = "Flashed"; } }
namespace DrakiaXYZ.BigBrain.Brains
{
    public static class BrainManager
    {
        public record LayerInfo(Type customLayerType, IReadOnlyList<string> CustomLayerBrains, IReadOnlyList<EFT.WildSpawnType> CustomLayerRoles);
        public record ExcludeLayerInfo(string excludeLayerName, IReadOnlyList<string> ExcludeLayerBrains, IReadOnlyList<EFT.WildSpawnType> ExcludeLayerRoles);
        public static Dictionary<int, LayerInfo> CustomLayersReadOnly = new(); public static List<ExcludeLayerInfo> ExcludeLayersReadOnly = new();
        public static HashSet<(string Layer, string Brain, EFT.WildSpawnType Role)> Disabled = new();
        public static void AddCustomLayer(Type t, List<string> brains, int priority, List<EFT.WildSpawnType> roles = null) { CustomLayersReadOnly[CustomLayersReadOnly.Count] = new(t, brains, roles ?? new()); }
        public static void RemoveLayers(List<string> layers, List<string> brains, List<EFT.WildSpawnType> roles = null) { foreach (var l in layers) foreach (var b in brains) foreach (var r in roles ?? Enum.GetValues<EFT.WildSpawnType>().ToList()) Disabled.Add((l, b, r)); }
        public static void RestoreLayers(List<string> layers, List<string> brains, List<EFT.WildSpawnType> roles = null) { foreach (var l in layers) foreach (var b in brains) foreach (var r in roles ?? Enum.GetValues<EFT.WildSpawnType>().ToList()) Disabled.Remove((l, b, r)); }
    }
}
