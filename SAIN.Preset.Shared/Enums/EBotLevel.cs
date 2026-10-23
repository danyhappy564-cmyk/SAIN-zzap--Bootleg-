namespace SAIN.Preset.Shared.Enums;

/// <summary>
/// zzap (2026-10-23): the difficulty level picked for one bot type on the bot type page - Easy / Normal / Hard follow the
/// bundled "zzap 쉬움" / "zzap 보통" / "zzap" presets, Impossible goes past zzap. None = never picked.
/// </summary>
public enum EBotLevel
{
    None,
    Easy,
    Normal,
    Hard,
    Impossible,
}
