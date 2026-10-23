using System;
using System.Linq;

namespace SAIN.Preset.Shared.Preset;

/// <summary>Protects shipped preset names and chooses an unoccupied user-copy name.</summary>
public static class PresetSavePolicy
{
    public static bool IsProtected(string name) => !string.IsNullOrEmpty(name)
        && QuickSettings.ZzapPresets.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    public static string NameForSave(string original, string requested, Func<string, bool> exists)
    {
        if (string.IsNullOrWhiteSpace(requested)) throw new ArgumentException("Preset name is required.", nameof(requested));
        if (IsProtected(requested))
        {
            if (!string.Equals(original, requested, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("That name belongs to a bundled preset. Choose a user-copy name.");
            return QuickSettings.ModifiedCopyName(original, exists);
        }
        return requested;
    }
}
