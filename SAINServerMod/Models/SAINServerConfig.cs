namespace SAINServerMod.Models;

public sealed class SAINServerConfig
{
    public List<string> DisabledPresets { get; set; } = [];
    /// <summary>
    /// zzap: the release forces the bundled "zzap" preset, so players who still have an older SAIN / fix7-era preset
    /// selected end up on the one this fork is tuned for. Change or clear it on the server settings page
    /// (https://127.0.0.1:6969/sain/server-settings), e.g. force "zzap TEST [시뮬 전용]" for bot-vs-bot sims.
    /// A ServerConfig.json saved with no forced preset keeps it off.
    /// </summary>
    public string? ForcedPresetName { get; set; } = "zzap";
    public bool AllowClientEditing { get; set; } = true;
    public List<string> EditAllowlist { get; set; } = [];
}
