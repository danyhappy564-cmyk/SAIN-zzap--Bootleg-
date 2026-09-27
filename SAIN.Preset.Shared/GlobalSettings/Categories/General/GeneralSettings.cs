using System.Collections.Generic;
using System.Runtime.Serialization;
using SAIN.Preset.Shared.Attributes;

namespace SAIN.Preset.Shared.GlobalSettings.Categories.General;

[DataContract]
public class GeneralSettings : SAINSettingsBase<GeneralSettings>, ISAINSettings
{
    [DataMember]
    [Name("Bots Use Grenades")]
    public bool BotsUseGrenades = true;

    [DataMember]
    [Name("Bots Use Grenades Vs Other Bots")]
    [Description("Bots are not as careful with grenades as players, this will prevent accidental deaths fighting other bots.")]
    public bool BotVsBotGrenade = true;

    [DataMember]
    [Name("Bot Inertia")]
    [Description(
        "Bots are properly affected by the weight of their equipment and loot for inertia. Requires raid restart for existing bots, as it applies on bot creation."
    )]
    public bool BOT_INERTIA_TOGGLE = true;

    [DataMember]
    [Name("Vanilla Bot Behavior Settings")]
    [Description(
        "If a option here is set to ON, they will use vanilla logic, ALL Features will be disabled for these types, including personality, recoil, difficulty, and behavior."
    )]
    public VanillaBotSettings VanillaBots = new();

    [DataMember]
    public PerformanceSettings Performance = new();

    [DataMember]
    public AILimitSettings AILimit = new();

    [DataMember]
    public CoverSettings Cover = new();

    [DataMember]
    public DoorSettings Doors = new();

    [DataMember]
    [Name("Door Tactics (zzap)")]
    [Description("Skilled-player door tactics added by the zzap fork: jump peek, fake grenade/heal, room trap, door grenade, silent ambush.")]
    public DoorTacticSettings DoorTactics = new();

    [DataMember]
    [Name("Freeze Ambush (zzap)")]
    [Description("When a Freeze personality hears an enemy close by before any fight, it holds still and ambushes. These limits were hardcoded before.")]
    public FreezeAmbushSettings FreezeAmbush = new();

    [DataMember]
    [Name("Squad Combat (zzap)")]
    [Description("Squad engagement in a live firefight: crossfire angles, covering a reloading/healing teammate, trading a downed teammate.")]
    public SquadCombatSettings SquadCombat = new();

    [DataMember]
    [Name("Close Combat (zzap)")]
    [Description("Close-range standoff: don't sprint away showing the back to an enemy that was just seen nearby.")]
    public CloseCombatSettings CloseCombat = new();

    [DataMember]
    public ExtractSettings Extract = new();

    [DataMember]
    public FlashlightSettings Flashlight = new();

    [DataMember]
    [Name("Looting Bots Integration")]
    [Description("Modify settings that relate to Looting Bots. Requires Looting Bots to be installed.")]
    public LootingBotsSettings LootingBots = new();

    [DataMember]
    public JokeSettings Jokes = new();

    [DataMember]
    public DebugSettings Debug = new();

    [DataMember]
    [Hidden]
    public LayerSettings Layers = new();

    public override void Init(List<ISAINSettings> list)
    {
        list.Add(this);
        list.Add(VanillaBots);
        list.Add(Performance);
        list.Add(AILimit);
        list.Add(Cover);
        list.Add(Doors);
        list.Add(DoorTactics);
        list.Add(FreezeAmbush);
        list.Add(SquadCombat);
        list.Add(CloseCombat);
        list.Add(Extract);
        list.Add(Flashlight);
        list.Add(LootingBots);
        list.Add(Jokes);
        list.Add(Layers);
        Debug.Init(list);
    }
}
