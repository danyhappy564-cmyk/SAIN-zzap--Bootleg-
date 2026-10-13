using System.Reflection;
using EFT;
using HarmonyLib;
using SAIN.SAINComponent.Classes.Tactics;
using SPT.Reflection.Patching;

namespace SAIN.Patches.Generic;

/// <summary>
/// zzap fork, 2026-10-13 (sim customs t=473s, NullReferenceException in Transform.get_position <- Player.get_Position <-
/// BotWeaponManager.OnBulletNear <- BotMemory.SetUnderFire <- BotMemory.GetHit, no SAIN frame): a bot was hit by damage whose
/// source player's GameObject was already destroyed (sim corpse delete, or another mod's damage carrying a dead player), so
/// EFT read the destroyed source's position and the hit handling stopped half way. Skip "under fire from" a source that is
/// gone - the damage itself was already applied, only the reaction to a source that no longer exists is skipped.
/// </summary>
public class DeadSourceUnderFireGuardPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotMemory), nameof(BotMemory.SetUnderFire), [typeof(IPlayer)]);
    }

    [PatchPrefix]
    public static bool Prefix(IPlayer __0)
    {
        if (__0 is Player player && (player == null || player.Transform?.Original == null))
        {
            TacticDiagnostics.Count("fix.underFireDeadSource");
            return false;
        }
        return true;
    }
}
