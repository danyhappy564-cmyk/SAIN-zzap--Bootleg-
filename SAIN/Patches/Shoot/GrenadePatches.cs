using System.Reflection;
using EFT.InventoryLogic;
using HarmonyLib;
using SAIN.Components;
using SAIN.Preset.Shared.GlobalSettings;
using SPT.Reflection.Patching;

namespace SAIN.Patches.Shoot.Grenades;

public class SetGrenadePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotGrenadeController), nameof(BotGrenadeController.SetThrowParams));
    }

    [PatchPostfix]
    public static void Patch(ThrowWeap potentialGrenade, BotGrenadeController __instance)
    {
        if (potentialGrenade == null)
        {
            return;
        }
        if (!BotManagerComponent.Instance.GetSAIN(__instance._owner, out var botComponent))
        {
            return;
        }
        //__instance.Mass = potentialGrenade.Weight;
        botComponent.Grenade.MyGrenade = potentialGrenade;
    }
}

public class ResetGrenadePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotGrenadeController), nameof(BotGrenadeController.CheckGrenade));
    }

    [PatchPostfix]
    public static void Patch(BotGrenadeController __instance)
    {
        if (!BotManagerComponent.Instance.GetSAIN(__instance._owner, out var botComponent))
        {
            return;
        }
        botComponent.Grenade.MyGrenade = __instance.grenade;
    }
}

public class DisableGrenadesPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.PropertyGetter(typeof(BotGrenadeController), nameof(BotGrenadeController.HaveGrenade));
    }

    [PatchPostfix]
    public static void Patch(BotGrenadeController __instance, ref bool __result)
    {
        if (!__result)
        {
            return;
        }

        var settings = GlobalSettingsClass.Instance.General;
        if (!settings.BotsUseGrenades)
        {
            __result = false;
            return;
        }

        if (SAINEnableClass.GetSAIN(__instance._owner.ProfileId, out BotComponent bot))
        {
            // zzap: BSG picks the grenade frag -> flash -> ANY throwable, so a bot whose only grenade is a vanilla smoke
            // threw it at the enemy like a frag (APBS scavs carry smokes ~17-20%). Only the two vanilla smokes are refused
            // by template id - modded grenades cloned from the smoke class (Manimal CS gas, thrown by Black Division) keep
            // working (kb 3.17: the class is SmokeGrenade for both, the template id is not).
            if (IsVanillaSmoke(__instance.grenade))
            {
                __result = false;
                if (SmokeSkipLogged.Add(__instance._owner.ProfileId))
                {
                    SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count("nade.skip.vanillaSmoke");
                    if (SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.LogOn)
                    {
                        SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.LogCloseCombat(
                            $"[Nade] [{bot.name}] only grenade in hand is a smoke ({__instance.grenade.TemplateId}) - not thrown as a frag"
                        );
                    }
                }
                return;
            }

            if (!bot.Info.FileSettings.Core.CanGrenade)
            {
                __result = false;
                return;
            }

            var goalEnemy = bot.EnemyController.GoalEnemy;
            if (goalEnemy == null)
            {
                __result = false;
                return;
            }

            if (!settings.BotVsBotGrenade && goalEnemy.IsAI)
            {
                __result = false;
            }
        }
    }

    // RDG-2B, M18 (APBS / SPT database template ids).
    private static readonly System.Collections.Generic.HashSet<string> VanillaSmokes =
    [
        "5a2a57cfc4a2826c6e06d44a",
        "617aa4dd8166f034d57de9c5",
    ];

    private static readonly System.Collections.Generic.HashSet<string> SmokeSkipLogged = [];

    private static bool IsVanillaSmoke(ThrowWeap grenade)
    {
        if (grenade == null)
        {
            return false;
        }
        string tpl = grenade.TemplateId;
        return tpl != null && VanillaSmokes.Contains(tpl);
    }
}
