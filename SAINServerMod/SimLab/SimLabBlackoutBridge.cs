using System.Reflection;
using HarmonyLib;

namespace SAINServerMod.SimLab;

/// <summary>
/// zzap fork: Blackout (Vultify, Labs power-cut event) during sim raids (user 2026-10-10: "Factory 30 + Labs 30 is best for
/// indoor data - mind the mods I run: ManimalLabs, MapVariants, Blackout"). Blackout rolls each Labs raid dark at its config's
/// chance (25%) - real darkness, emergency floods, keypad doors - so sim runs on Labs would mix lit and dark fights. Its server
/// rolls the next raid ahead (on load and at /client/match/local/end) into <c>BlackoutSpawnController.CurrentRaidBlackout</c>,
/// and the game reads that through /blackout/state when the raid loads. Our sim raid setup (GenerateLocationAndLoot, after the
/// roll, before the client asks) sets it to false for a sim raid. The controller is found through the static
/// <c>BlackoutStateRouter._controller</c>. Blackout's code and config are untouched; normal raids roll as usual; nothing
/// happens without Blackout. (The arsenal key a dark roll put on the desk just stays there.)
/// </summary>
public static class SimLabBlackoutBridge
{
    private static bool _looked;
    private static FieldInfo? _controllerField;
    private static PropertyInfo? _dark;

    private static void Look()
    {
        if (_looked)
        {
            return;
        }
        _looked = true;
        var router = AccessTools.TypeByName("BlackoutServer.BlackoutStateRouter");
        var controller = AccessTools.TypeByName("BlackoutServer.BlackoutSpawnController");
        var field = router == null ? null : AccessTools.Field(router, "_controller");
        var dark = controller == null ? null : AccessTools.Property(controller, "CurrentRaidBlackout");
        if (field != null && field.IsStatic && field.FieldType == controller && dark != null && dark.PropertyType == typeof(bool)
            && dark.GetSetMethod(true) != null)
        {
            _controllerField = field;
            _dark = dark;
        }
    }

    public static bool Found
    {
        get
        {
            Look();
            return _dark != null;
        }
    }

    /// <summary>Turns this raid's blackout off. Returns null when Blackout isn't installed / changed shape, else whether it was dark.</summary>
    public static bool? TurnOff()
    {
        Look();
        object? controller = _controllerField?.GetValue(null);
        if (controller == null || _dark == null)
        {
            return null;
        }
        bool wasDark = (bool)_dark.GetValue(controller)!;
        if (wasDark)
        {
            _dark.SetValue(controller, false);
        }
        return wasDark;
    }
}
