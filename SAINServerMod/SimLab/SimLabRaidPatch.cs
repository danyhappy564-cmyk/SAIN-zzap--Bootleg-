using System.Reflection;
using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Services.InRaid;

namespace SAINServerMod.SimLab;

/// <summary>
/// zzap fork: sets up a sim raid's spawns on the raid's own copy of the location, right after SPT made it
/// (<c>LocationLifecycleService.GenerateLocationAndLoot</c> clones <c>locationTable.GetLocation(name).Base</c>). A postfix on the
/// copy runs after every load-time change and after MapVariants swapped the base (kb 1.28), and never touches the database.
/// Does nothing unless the client armed the sim (simulation preset loaded).
/// </summary>
public sealed class SimLabRaidPatch() : AbstractPatch("zzap.SAIN.SimLabRaidPatch")
{
    protected override MethodBase? GetTargetMethod()
    {
        return AccessTools.Method(typeof(LocationLifecycleService), nameof(LocationLifecycleService.GenerateLocationAndLoot));
    }

    [PatchPostfix]
    public static void Postfix(string name, LocationBase __result)
    {
        var service = SimLabService.Instance;
        if (service == null || !service.Armed || __result == null || string.Equals(name, "hideout", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        service.ApplyToRaid(name, __result);
    }
}
