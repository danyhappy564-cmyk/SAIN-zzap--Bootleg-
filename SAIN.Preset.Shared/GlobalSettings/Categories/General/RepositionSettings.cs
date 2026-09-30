using System.Runtime.Serialization;
using SAIN.Preset.Shared.Attributes;

namespace SAIN.Preset.Shared.GlobalSettings.Categories.General;

/// <summary>
/// zzap fork: repositioning around grenades and contact, from Korean veteran-player advice (DC Inside / forum threads):
/// move under the cover of your own grenade's blast, relocate after being spotted, break off a fight you started
/// but couldn't finish with one magazine, fake peeks to draw fire, and pick the grenade fuse for the situation.
/// </summary>
[DataContract]
public class RepositionSettings : SAINSettingsBase<RepositionSettings>, ISAINSettings
{
    [DataMember]
    [Name("Reposition Enabled")]
    [Description("Master switch for everything below.")]
    public bool Enabled = true;

    [DataMember]
    [Name("Fuse Selection")]
    [Description("When a bot throws a frag (SAIN's normal throws): indoors or close -> the shortest fuse it carries (less time to run or kick it back), outdoors at range -> the longest (area denial). Off = whatever SAIN picks.")]
    public bool FuseSelection = true;

    [DataMember]
    [Name("Short Fuse Distance")]
    [Description("Meters. Throws closer than this (or indoors) use the short fuse.")]
    [MinMax(5f, 40f, 1f)]
    public float ShortFuseDistance = 20f;

    [DataMember]
    [Name("Relocate After Being Spotted")]
    [Description("Outdoors, when the enemy got the first contact (shot at / hit the bot before it saw them): once in cover and patched up, throw a frag at them if possible and move to an angle they can't see.")]
    public bool Relocate = true;

    [DataMember]
    [Name("Relocate Chance")]
    [Description("Percent, rolled once per enemy.")]
    [MinMax(0f, 100f, 1f)]
    public float RelocateChance = 70f;

    [DataMember]
    [Name("Bait Peek")]
    [Description("From cover, aggressive personalities (the ones that rush a reloading enemy) quickly step out and back once or twice to draw fire. If the enemy empties their gun at it, SAIN's own rush-on-reload takes over.")]
    public bool BaitPeek = true;

    [DataMember]
    [Name("Bait Peek Chance")]
    [Description("Percent, rolled at most every 20 seconds per bot while in cover.")]
    [MinMax(0f, 100f, 1f)]
    public float BaitPeekChance = 35f;

    [DataMember]
    [Name("Fake Reload (Mag Check)")]
    [Description("From cover with a healthy magazine: play the magazine-check animation (mag out/in sound, no rounds lost) so the enemy thinks the bot is reloading, then hold the angle they would push through.")]
    public bool FakeReload = true;

    [DataMember]
    [Name("Fake Reload Chance")]
    [Description("Percent, rolled with the other cover tricks at most every 20 seconds.")]
    [MinMax(0f, 100f, 1f)]
    public float FakeReloadChance = 30f;

    [DataMember]
    [Name("TEST MODE: Always Reposition")]
    [Description("For testing only. Every chance above counts as 100%, bait peeks are allowed for every personality and the cover tricks are rolled every 8s instead of 20s. Conditions (indoors/outdoors, in cover, distances) still apply. Turn it off for normal play.")]
    public bool TestMode = false;

    [DataMember]
    [Name("PMC Only")]
    [Description("Apply only to PMCs. Scavs keep vanilla behaviour.")]
    public bool PmcOnly = true;

    [DataMember]
    [Name("Diagnostic Logs")]
    [Description("Writes [Reposition] lines to LogOutput.log and repo.* / nade.fuse.* SUMMARY counters.")]
    public bool DiagnosticLogs = false;
}
