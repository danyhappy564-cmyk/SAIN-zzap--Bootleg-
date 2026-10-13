using System.Runtime.Serialization;
using SAIN.Preset.Shared.Attributes;

namespace SAIN.Preset.Shared.GlobalSettings.Categories.General;

[DataContract]
public class DoorSettings : SAINSettingsBase<DoorSettings>, ISAINSettings
{
    //[Name("SAIN Door Handling")]
    //[Description("WIP")]
    //public bool NewDoorOpening = true;

    [DataMember]
    [Name("No Door Animations")]
    [Description(
        "Bots auto open doors instead of getting stuck in an animation, if fika is loaded, this is ignored and it is always disabled. [zzap: no code reads this setting in this build - turning it on or off changes nothing.]"
    )]
    public bool NoDoorAnimations = true;

    [DataMember]
    [Name("Always Push Open Doors")]
    [Description(
        "A bot opening a door by hand that would swing into it pushes it the other way instead - the leaf no longer shoves the bot aside into the gap by the wall. Not when something blocks the space the leaf would swing into. Can look odd (a door open the 'wrong' way). Bots only. [zzap 10/14: this works again - it was read by nothing before.]"
    )]
    public bool InvertDoors = true;

    [DataMember]
    [Name("Disable All Doors")]
    [Description("Doors are hard, just turn them all off. Only targets doors that can be open/closed normally.")]
    public bool DisableAllDoors = false;
}
