using System.Reflection;
using HarmonyLib;
using SPTarkov.Reflection.Patching;

namespace SAINServerMod.SimLab;

/// <summary>
/// zzap fork: no modded weapons / attachments / gear / clothing on sim bots (user 2026-10-10: "during the sim, no mod weapons or
/// gear - APBS needs controlling"). APBS (Acid's Progressive Bot System, server) already keeps a per-raid list of the mod items
/// bots may use this raid (<c>RaidModItemSubsetService</c>): it rolls the list at /client/match/local/start - after SPT generated
/// the raid, so our sim setup for this raid has run - and every equipment / attachment / clothing pick filters through it (a
/// pool that would be left empty keeps its originals, so no bot goes without a required part). For a sim raid this postfix
/// swaps the freshly rolled list for an empty one: no mod item passes the filters. APBS's code and settings are untouched,
/// normal raids roll as usual. Found by name at runtime - nothing happens when APBS isn't installed.
/// </summary>
public static class SimLabApbsItemsBridge
{
    public const string SERVICE = "ProgressiveBotSystem.Services.RaidModItemSubsetService";

    private static bool _looked;
    private static MethodInfo? _roll;
    private static FieldInfo? _current;
    private static Type? _selection;

    private static void Look()
    {
        if (_looked)
        {
            return;
        }
        _looked = true;
        var type = AccessTools.TypeByName(SERVICE);
        if (type == null)
        {
            return;
        }
        var roll = AccessTools.Method(type, "RollForNewRaid", [typeof(string)]);
        var current = AccessTools.Field(type, "_current");
        var selection = AccessTools.Inner(type, "RaidSelection");
        if (roll == null || current == null || selection == null || current.FieldType != selection
            || AccessTools.Property(selection, "ModItems") == null || AccessTools.Property(selection, "ModClothing") == null)
        {
            return;
        }
        _roll = roll;
        _current = current;
        _selection = selection;
    }

    /// <summary>APBS's RollForNewRaid, or null when APBS isn't installed or changed shape.</summary>
    public static MethodBase? Method()
    {
        Look();
        return _roll;
    }

    /// <summary>Replaces APBS's list for this raid with an empty one (no mod item allowed). False when it couldn't.</summary>
    public static bool Empty(object service)
    {
        Look();
        if (_current == null || _selection == null || service == null)
        {
            return false;
        }
        object selection = Activator.CreateInstance(_selection, nonPublic: true)!;
        var items = AccessTools.Property(_selection, "ModItems");
        var clothing = AccessTools.Property(_selection, "ModClothing");
        items.SetValue(selection, Activator.CreateInstance(items.PropertyType));
        clothing.SetValue(selection, Activator.CreateInstance(clothing.PropertyType));
        _current.SetValue(service, selection);
        return true;
    }
}

public sealed class SimLabApbsItemsPatch() : AbstractPatch("zzap.SAIN.SimLabApbsItemsPatch")
{
    protected override MethodBase? GetTargetMethod()
    {
        return SimLabApbsItemsBridge.Method();
    }

    [PatchPostfix]
    public static void Postfix(object __instance, string? location)
    {
        var sim = SimLabService.Instance;
        if (sim == null || !sim.BlockModItemsFor(location))
        {
            return;
        }
        try
        {
            if (SimLabApbsItemsBridge.Empty(__instance))
            {
                sim.Log($"[SAIN SimLab] {location}: no modded weapons / attachments / gear / clothing on bots this sim raid (APBS list emptied)");
            }
        }
        catch (Exception ex)
        {
            sim.Log($"[SAIN SimLab] {location}: could not empty APBS's mod item list ({ex.Message}) - bots may carry mod items");
        }
    }
}
