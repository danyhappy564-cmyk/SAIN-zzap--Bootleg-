using System.Collections.Generic;
using System.Runtime.Serialization;
using SAIN.Preset.Shared.Attributes;

namespace SAIN.Preset.Shared.GlobalSettings.Categories;

[DataContract]
public class TalkSettings : SAINSettingsBase<TalkSettings>, ISAINSettings
{
    [DataMember]
    [Category("Main Switches")]
    [NewIn("Remix7.6")]
    [Name("Bots Can Talk")]
    [Description("Off: no bot says anything, whatever the bot type's own settings say. Breathing, and pain or death sounds are game sounds and stay.")]
    public bool AllBotsTalk = true;

    [DataMember]
    [Category("Main Switches")]
    [NewIn("Remix7.6")]
    [Name("Taunts and Yelling Back")]
    [Description("Off: no bot taunts enemies, yells back or taunts after a kill. On: each personality's own taunt settings decide.")]
    public bool AllBotsTaunt = true;

    [DataMember]
    [Category("Main Switches")]
    [NewIn("Remix7.6")]
    [Name("Apply Talk Settings (zzap fix)")]
    [Description("zzap fix: bots read their taunt and squad talk settings when they spawn. Off = SAIN's original behaviour: those settings are skipped until a preset is changed, so every personality taunts at the same rate. Applies to bots spawned after the change.")]
    public bool ApplyTalkSettings = true;

    [DataMember]
    [Category("Peaceful Talk")]
    [Name("Talkative Scavs")]
    [Description("When at peace, scavs will talk to each other and be noisy. Revealing their location.")]
    public bool TalkativeScavs = true;

    [DataMember]
    [Category("Peaceful Talk")]
    [Name("Talkative PMCs")]
    [Description("When at peace, pmcs will talk to each other and be noisy. Revealing their location.")]
    public bool TalkativePMCs = false;

    [DataMember]
    [Category("Peaceful Talk")]
    [Name("Talkative Raiders and Rogues")]
    [Description("When at peace, raiders and rogues will talk to each other and be noisy. Revealing their location.")]
    public bool TalkativeRaidersRogues = true;

    [DataMember]
    [Category("Peaceful Talk")]
    [Name("Talkative Bosses")]
    [Description("When at peace, Bosses and boss guards will talk to each other and be noisy. Revealing their location.")]
    public bool TalkativeBosses = true;

    [DataMember]
    [Category("Peaceful Talk")]
    [Name("Talkative Goons")]
    [Description("When at peace, The Goons will talk to each other and be noisy. Revealing their location.")]
    public bool TalkativeGoons = false;

    [DataMember]
    [Advanced]
    [Name("Human Response Chance")]
    [Description("Percentage chance to respond to a voiceline from a friendly human player.")]
    [Category("Friendly Response")]
    [Percentage]
    public float FriendlyReponseChance = 85f;

    [DataMember]
    [Advanced]
    [Name("AI Response Chance")]
    [Description("Percentage chance to respond to a voiceline from a friendly AI player.")]
    [Category("Friendly Response")]
    [Percentage]
    public float FriendlyReponseChanceAI = 80f;

    [DataMember]
    [Advanced]
    [Name("Human Response Max Distance")]
    [Category("Friendly Response")]
    [Percentage]
    public float FriendlyReponseDistance = 65f;

    [DataMember]
    [Advanced]
    [Name("AI Response Max Distance")]
    [Category("Friendly Response")]
    [Percentage]
    public float FriendlyReponseDistanceAI = 35f;

    [DataMember]
    [Advanced]
    [Name("Response Frequency")]
    [Description("2 = 1 check every 2 second")]
    [Category("Friendly Response")]
    [MinMax(0.5f, 10f)]
    public float FriendlyResponseFrequencyLimit = 1f;

    [DataMember]
    [Advanced]
    [Name("Response Delay Randomization Min")]
    [Category("Friendly Response")]
    [MinMax(0.25f, 3f)]
    public float FriendlyResponseMinRandomDelay = 0.33f;

    [DataMember]
    [Advanced]
    [Name("Response Delay Randomization Max")]
    [Category("Friendly Response")]
    [MinMax(0.25f, 3f)]
    public float FriendlyResponseMaxRandomDelay = 0.75f;

    [DataMember]
    [Name("Vanilla Bot Talking")]
    [Description(
        "Disable all SAIN based handling of bot talking. The game's own voice lines all come back (reloading, contact, enemy down...) and the talk settings here stop applying, so bots get louder."
    )]
    public bool DisableBotTalkPatching = false;

    [DataMember]
    [Name("Bot Pain Voice On Hit (zzap)")]
    [Description("zzap fork: bots groan / shout \"I'm hit\" (OnBeingHurt / OnAgony) every time they take a hit. Off (default): no pain voice lines from bots. Death sounds are unaffected.")]
    public bool BotPainVoiceOnHit = false;

    [DataMember]
    [NewIn("Remix7.6")]
    [Name("Fake Death Scream")]
    [Description("A badly hurt lone bot (one chance per wound), or a bot near a grenade blast, may fake a death scream (personalities with Can Fake Death). Off: only real deaths make that sound.")]
    public bool FakeDeathVoice = true;

    public override void Init(List<ISAINSettings> list)
    {
        list.Add(this);
    }
}
