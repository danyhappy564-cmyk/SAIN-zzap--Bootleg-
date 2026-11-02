using System.Security.Cryptography;

namespace SAINServerMod.Web.Services;

/// <summary>
/// The stylesheet link with a version taken from the file itself, so an updated sain.css is fetched instead of the browser's
/// cached copy. Hashing the file rather than the build also catches a CSS-only update.
/// </summary>
public static class SainAssets
{
    public static readonly string CssUrl = "/SAIN/css/sain.css?v=" + CssVersion();

    private static string CssVersion()
    {
        try
        {
            string folder = Path.GetDirectoryName(typeof(SainAssets).Assembly.Location) ?? string.Empty;
            byte[] css = File.ReadAllBytes(Path.Combine(folder, "wwwroot", "css", "sain.css"));
            return Convert.ToHexString(SHA1.HashData(css))[..8].ToLowerInvariant();
        }
        catch
        {
            return typeof(SainAssets).Assembly.ManifestModule.ModuleVersionId.ToString("N")[..8];
        }
    }
}
