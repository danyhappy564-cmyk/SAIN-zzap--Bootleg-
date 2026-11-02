namespace SAINServerMod.Web.Services;

/// <summary>
/// The stylesheet link with a per-build version, so an updated sain.css is fetched instead of the browser's cached copy
/// (2026-11-03: after updating, Chrome kept the old file and the new bot settings layout showed without its styles).
/// </summary>
public static class SainAssets
{
    public static readonly string CssUrl = "/SAIN/css/sain.css?v=" + typeof(SainAssets).Assembly.ManifestModule.ModuleVersionId.ToString("N")[..8];
}
