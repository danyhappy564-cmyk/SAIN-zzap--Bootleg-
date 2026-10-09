using System.Runtime.Serialization;
using SAIN.Preset.Shared.Attributes;

namespace SAIN.Preset.Shared.GlobalSettings.Categories.General;

/// <summary>
/// zzap fork: simulation lab (user 2026-10-09: "map rotation sim, edit order/time in the web page, analysis tab, survive
/// Alt+F4"). Only the bundled "zzap TEST [시뮬 전용]" preset turns it on - every other preset leaves it off, so normal
/// players pay nothing (each hook checks this one bool first). Everything else (map order, minutes, spawn scenario) is
/// edited in the browser at https://127.0.0.1:6969/sain/sim and stored by the server mod.
/// </summary>
[DataContract]
public class SimLabSettings : SAINSettingsBase<SimLabSettings>, ISAINSettings
{
    [DataMember]
    [Name("Simulation Mode")]
    [Description("Simulation preset only. Spectator god mode, arena spawns from the server's sim page, map rotation (start the next map by itself, end each map after its minutes) and a crash-safe sim log. Settings: https://127.0.0.1:6969/sain/sim")]
    public bool Enabled = false;

    [DataMember]
    [Name("Auto Start Next Raid")]
    [Description("In the main menu, start the next map of the rotation by itself after a short countdown. Off = start each raid yourself (the sim spawns and time limit still apply).")]
    public bool AutoStart = true;

    [DataMember]
    [Name("Auto Start Delay")]
    [Description("Seconds in the main menu before the next raid starts by itself. Pause the rotation on the sim web page to stop it.")]
    [MinMax(5f, 120f, 1f)]
    public float AutoStartDelay = 15f;
}
