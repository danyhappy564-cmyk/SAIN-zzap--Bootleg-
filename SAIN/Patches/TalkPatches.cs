using System.Reflection;
using EFT;
using HarmonyLib;
using SAIN.Components;
using SPT.Reflection.Patching;

namespace SAIN.Patches.Talk;

public class PlayerHurtPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(Player), nameof(Player.ApplyHitDebuff));
    }

    [PatchPrefix]
    public static void PatchPrefix(Player __instance, float damage)
    {
        if (__instance?.HealthController?.IsAlive != true || !__instance.IsAI)
        {
            return;
        }

        if (SAINPlugin.LoadedPreset.GlobalSettings.Talk.DisableBotTalkPatching)
        {
            return;
        }

        if (!SAINPlugin.LoadedPreset.GlobalSettings.Talk.BotPainVoiceOnHit)
        {
            return;
        }

        if (!SAINEnableClass.GetSAIN(__instance.ProfileId, out BotComponent bot) || !bot.Talk.PlayerInEarshot)
        {
            return;
        }

        if (__instance.MovementContext.PhysicalConditionIs(EPhysicalCondition.OnPainkillers) && damage <= 4f)
        {
            return;
        }

        __instance.Speaker?.Play(EPhraseTrigger.OnBeingHurt, __instance.HealthStatus, true, null);
    }
}

public class PlayerTalkPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(Player), nameof(Player.Say));
    }

    [PatchPrefix]
    public static bool PatchPrefix(Player __instance, EPhraseTrigger phrase, ETagStatus mask, bool aggressive)
    {
        if (__instance.IsAI && IsPainPhrase(phrase) && !SAINPlugin.LoadedPreset.GlobalSettings.Talk.BotPainVoiceOnHit)
        {
            // zzap: no "ugh / I'm hit" from bots on every hit (F6 Talk > Bot Pain Voice On Hit).
            return false;
        }
        switch (phrase)
        {
            case EPhraseTrigger.OnDeath:
            case EPhraseTrigger.OnBeingHurt:
            case EPhraseTrigger.OnAgony:
            case EPhraseTrigger.OnBreath:
                BotManagerComponent.Instance?.BotHearing.PlayerTalked(phrase, mask, __instance);
                return true;

            default:
                break;
        }

        if (__instance.IsAI)
        {
            if (SAINPlugin.LoadedPreset.GlobalSettings.Talk.DisableBotTalkPatching || !SAINEnableClass.GetSAIN(__instance.ProfileId, out _))
            {
                BotManagerComponent.Instance?.BotHearing.PlayerTalked(phrase, mask, __instance);
                return true;
            }
            return false;
        }

        BotManagerComponent.Instance?.BotHearing.PlayerTalked(phrase, mask, __instance);
        return true;
    }

    public static bool IsPainPhrase(EPhraseTrigger phrase)
    {
        return phrase == EPhraseTrigger.OnBeingHurt || phrase == EPhraseTrigger.OnAgony;
    }
}

public class BotTalkPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotTalk), nameof(BotTalk.Say));
    }

    [PatchPrefix]
    public static bool PatchPrefix(BotTalk __instance, EPhraseTrigger type)
    {
        if (SAINPlugin.LoadedPreset.GlobalSettings.Talk.DisableBotTalkPatching)
        {
            return true;
        }
        if (__instance._owner?.HealthController?.IsAlive == false)
        {
            return true;
        }
        if (PlayerTalkPatch.IsPainPhrase(type) && !SAINPlugin.LoadedPreset.GlobalSettings.Talk.BotPainVoiceOnHit)
        {
            return false;
        }
        switch (type)
        {
            case EPhraseTrigger.OnDeath:
            case EPhraseTrigger.OnBeingHurt:
            case EPhraseTrigger.OnAgony:
            case EPhraseTrigger.OnBreath:
                return true;

            default:
                break;
        }
        if (!SAINEnableClass.GetSAIN(__instance._owner.ProfileId, out BotComponent bot))
        {
            return true;
        }
        switch (type)
        {
            case EPhraseTrigger.HandBroken:
            case EPhraseTrigger.LegBroken:
                bot.Talk.GroupSay(type, null, false, 60);
                break;

            default:
                break;
        }
        return false;
    }
}

public class BotTalkManualUpdatePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotTalk), nameof(BotTalk.ManualUpdate));
    }

    [PatchPrefix]
    public static bool PatchPrefix(BotTalk __instance)
    {
        // If handling of bots talking is disabled, let the original method run
        return SAINPlugin.LoadedPreset.GlobalSettings.Talk.DisableBotTalkPatching
            || !SAINEnableClass.GetSAIN(__instance._owner.ProfileId, out _);
    }
}
