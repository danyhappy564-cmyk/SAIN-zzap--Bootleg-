namespace SAIN.Preset.Shared;

public static class SAINVersionInfo
{
    public const string SAINVersion = SAINCompileConstants.SAIN_VERSION;
    public const string SAINFullVersion = SAINCompileConstants.SAIN_FULL_VERSION;
    public const string SptVersion = SAINCompileConstants.SPT_VERSION;
    public const string SAINPresetVersion = "4.5.0";

    /// <summary>
    /// zzap fork: the release this build belongs to ("remake6", "Remix5.1"). SAINVersion stays SAIN's own 4.5.1 for preset
    /// compatibility, so this is what F6, the BepInEx log and the web updates page show (user 2026-10-19: "the top left of
    /// F6 says 4.5.1"). Bump together with the README and the release notes on every release.
    /// </summary>
    public const string ZzapRelease = "Remix6.2";
}
