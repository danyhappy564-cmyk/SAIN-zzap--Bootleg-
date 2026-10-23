using System.Runtime.Serialization;
using SAIN.Preset.Shared.Attributes;
using SAIN.Preset.Shared.GlobalSettings;

namespace SAIN.Preset.Shared.BotSettings.SAINSettings.Categories;

[DataContract]
public class SAINCoreSettings : SAINSettingsBase<SAINCoreSettings>, ISAINSettings
{
    [DataMember]
    [Category("Vision")]
    [Name("Field of View")]
    [Description("How wide this bot's view is, in degrees from one side to the other. Enemies outside it are not seen.")]
    [MinMax(45f, 180f)]
    public float VisibleAngle = 160f;

    [DataMember]
    [Category("Vision")]
    [Name("Base Vision Distance")]
    [Description("How far this bot can see enemies in good light. Darkness, weather and foliage shorten it from here.")]
    [MinMax(50f, 500f)]
    public float VisibleDistance = 150f;

    [DataMember]
    [Category("Vision")]
    [Name("Gain Sight Coeficient")]
    [Description(
        "Default EFT Config. Affects how quickly this bot will notice their enemies. Small changes to this have dramatic affects on bot vision speed."
    )]
    [MinMax(0.001f, 10f, 10000f)]
    [Advanced]
    public float GainSightCoef = 0.2f;

    [DataMember]
    [Category("Aim and Shoot")]
    [Name("Accuracy Speed")]
    [Description("Default EFT Config. Affects how quickly this bot will aim at targets.")]
    [MinMax(0.01f, 10f, 100f)]
    [Advanced]
    [CopyValue]
    public float AccuratySpeed = 0.3f;

    [DataMember]
    [Category("Aim and Shoot")]
    [Description("Default EFT Config. I do not know what this does exactly.")]
    [MinMax(0.001f, 1f, 1000f)]
    [Advanced]
    [CopyValue]
    public float ScatteringPerMeter = 0.08f;

    [DataMember]
    [Category("Aim and Shoot")]
    [Description("Default EFT Config. I do not know what this does exactly.")]
    [MinMax(0.001f, 1f, 1000f)]
    [Advanced]
    [CopyValue]
    public float ScatteringClosePerMeter = 0.12f;

    [DataMember]
    [Category("Hearing")]
    [Name("Hearing Distance Multiplier")]
    [Description("Modifies the distance that this bot can hear sounds")]
    [MinMax(0.1f, 3f, 1000f)]
    public float HearingDistanceMulti = 1f;

    [DataMember]
    [Name("Can Use Grenades")]
    [Description("Off = this bot never throws grenades.")]
    public bool CanGrenade = true;

    [Hidden]
    [IgnoreDataMember]
    public bool CanRun = true;

    [Hidden]
    [IgnoreDataMember]
    public float DamageCoeff = 1f;
}
