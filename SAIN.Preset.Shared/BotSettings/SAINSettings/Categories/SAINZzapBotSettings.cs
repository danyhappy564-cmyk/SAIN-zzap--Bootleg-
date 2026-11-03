using System.Runtime.Serialization;
using SAIN.Preset.Shared.Attributes;
using SAIN.Preset.Shared.GlobalSettings;

namespace SAIN.Preset.Shared.BotSettings.SAINSettings.Categories;

/// <summary>Per-bot-type switch for a zzap tactic: follow the global (ZZAP tab) setting, or force it on / off for this bot type.</summary>
public enum EZzapToggle
{
    Global = 0,
    On = 1,
    Off = 2,
}

/// <summary>
/// zzap (user 2026-11-04: "the bot settings' exact numbers can't set anything ZZAP - give every bot a 'ZZAP (tactics etc.)' part:
/// can it QEQE, does it diamond step..."). Everything starts at Global, so presets from before this change play exactly as before.
/// On wins over the global switch and over "Skip Scavs"; personality rules (who diamond-steps) stay.
/// </summary>
[DataContract]
public class SAINZzapBotSettings : SAINSettingsBase<SAINZzapBotSettings>, ISAINSettings
{
    [DataMember]
    [Name("Door Tactics")]
    [Description("Door ambush, jump peek, fake grenade, room clearing, door ramming. Global = the ZZAP tab's switch.")]
    public EZzapToggle DoorTactics = EZzapToggle.Global;

    [DataMember]
    [Name("Squad Combat")]
    [Description("Crossfire, covering a mate, trading, squad storm. Global = the ZZAP tab's switch.")]
    public EZzapToggle SquadCombat = EZzapToggle.Global;

    [DataMember]
    [Name("Reposition")]
    [Description("Leave a spotted spot, bait peek, fake reload. Global = the ZZAP tab's switch and its Skip Scavs.")]
    public EZzapToggle Reposition = EZzapToggle.Global;

    [DataMember]
    [Name("Close Combat Moves")]
    [Description("All of the close-combat zzap moves (fight close, no back turning, pistol swap, quick reload, suppression discipline, cover sprint...). Global = by Skip Scavs. On = even for scavs. Off = this bot type keeps SAIN's default close-combat behaviour.")]
    public EZzapToggle CloseCombat = EZzapToggle.Global;

    [DataMember]
    [Name("Diamond Step")]
    [Description("Short W/A/S/D taps while shooting (personalities GigaChad, Chad, Wreckless, Normal). Global = the ZZAP tab's switch.")]
    public EZzapToggle DiamondStep = EZzapToggle.Global;

    [DataMember]
    [Name("QEQE Lean Shooting")]
    [Description("Q/E leaning while shooting - only bots that diamond step. Global = the ZZAP tab's switch.")]
    public EZzapToggle LeanSpam = EZzapToggle.Global;

    [DataMember]
    [Name("Corner Jiggle Peek")]
    [Description("Peek in and out from a corner or door frame while shooting. Global = the ZZAP tab's switch.")]
    public EZzapToggle CornerJiggle = EZzapToggle.Global;

    /// <summary>The effective switch: this bot type's choice, else the global one.</summary>
    public static bool Allows(EZzapToggle toggle, bool global) => toggle == EZzapToggle.Global ? global : toggle == EZzapToggle.On;
}
