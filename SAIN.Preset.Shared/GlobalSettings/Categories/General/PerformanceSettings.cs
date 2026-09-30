using System.Runtime.Serialization;
using SAIN.Preset.Shared.Attributes;

namespace SAIN.Preset.Shared.GlobalSettings.Categories.General;

[DataContract]
public class PerformanceSettings : SAINSettingsBase<PerformanceSettings>, ISAINSettings
{
    [DataMember]
    [Name("Performance Mode")]
    [Description(
        "Limits the cover finder to maximize performance. Reduces frequency on some raycasts. "
            + "If your PC is CPU limited, this might let you regain some frames lost while using SAIN. Can cause bots to take too long to find cover to go to."
    )]
    public bool PerformanceMode = false;

    [DataMember]
    [Name("Sim: Hide Old Bot Corpses Above (TEST preset only)")]
    [Description(
        "zzap fork, for long bot-vs-bot simulations: when more bot corpses than this lie on the map, the oldest ones (20s+) are hidden. "
            + "Only works in a preset whose name contains TEST. 0 = off. The raid journal's [Perf] line shows how many were hidden."
    )]
    [MinMax(0f, 200f, 1f)]
    public float SimCorpseLimit = 0f;

    [DataMember]
    [Name("Sim: Delete Instead Of Hide (TEST preset only)")]
    [Description(
        "zzap fork, experiment: corpses over the limit above are removed through EFT's own loot removal instead of just hidden (hiding keeps the body in memory). "
            + "Their loot is gone with them. Only works in a preset whose name contains TEST. Used to measure how much RAM the corpses themselves hold."
    )]
    public bool SimCorpseDelete = false;

    [DataMember]
    [Name("Free Corpse Blood Decal Textures")]
    [Description(
        "zzap fork: every character mesh that gets hit gets its own blood/bullet-hole texture (about 4 MB each) that EFT keeps until the raid ends, "
            + "corpses included - several GB in a raid with many deaths. On = once a character has been dead for the delay below, its blood marks "
            + "are removed and the texture goes back to EFT's pool for the next hit. Corpses themselves are not touched."
    )]
    public bool FreeCorpseDecals = true;

    [DataMember]
    [Name("Free Corpse Decals After (seconds)")]
    [Description("How long a character has to be dead before its blood/bullet-hole textures are freed.")]
    [MinMax(0f, 300f, 1f)]
    public float FreeCorpseDecalsDelay = 30f;
}
