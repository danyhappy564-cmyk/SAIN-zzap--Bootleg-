using System;
using System.Reflection;
using EFT;
using EFT.Interactive;
using HarmonyLib;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.SAINComponent.Classes.Tactics;
using SPT.Reflection.Patching;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Mover;

/// <summary>
/// zzap (2026-10-14, user: "make the doors bots open swing away from them - kicked or by hand"): SAIN's old "Always Push Open
/// Doors" (F6 Doors > InvertDoors, on by default) was read by nothing in this build. A bot opening a shut door by hand that
/// would swing into it gets the open angle negated - the door is pushed away instead of pulled into the bot, which used to
/// shove the bot sideways into the gap between the leaf and the wall (10/14 screenshots). Same mechanism as
/// DualSideDoorBreach uses for kicks (InitSmoothOpen resultAngle), so the leaf ends where it was swung to.
/// Not when the space the leaf would swing into is blocked (wall / object right there) - then the normal way.
/// Bots only; the human player's doors are untouched. Counters door.pushOpen / door.pushOpen.blocked.
/// </summary>
public static class BotDoorPushOpen
{
    public static bool Active { get; private set; }

    private static bool Enabled => GlobalSettingsClass.Instance?.General?.Doors?.InvertDoors == true;

    public static void Begin(WorldInteractiveObject obj, EDoorState state)
    {
        Active = false;
        if (state != EDoorState.Open || !Enabled || obj is not Door door || BotDoorBreacher.IsSliding(door) || door.InteractingPlayer is not Player player)
        {
            return;
        }
        if (!player.IsAI || player.IsYourPlayer)
        {
            return;
        }
        // opening from shut (by angle - the state may already read Interacting here)
        if (Mathf.Abs(Mathf.DeltaAngle(door.CurrentAngle, door.GetAngle(EDoorState.Shut))) > 5f)
        {
            return;
        }
        if (!BotDoorBreacher.WillSwingToward(door, player.Position))
        {
            return;
        }
        if (!SwingSpaceClear(door))
        {
            TacticDiagnostics.Count("door.pushOpen.blocked");
            return;
        }
        Active = true;
        TacticDiagnostics.Count("door.pushOpen");
    }

    public static void End()
    {
        Active = false;
    }

    /// <summary>The leaf turned to the reversed open angle: its middle and its far edge must not be inside static geometry.</summary>
    internal static bool SwingSpaceClear(Door door)
    {
        try
        {
            Transform t = door.transform;
            Collider leaf = door.GetComponentInChildren<Collider>();
            if (t.parent == null || leaf == null)
            {
                return false;
            }
            Vector3 hinge = t.position;
            Vector3 offset = Quaternion.Inverse(t.rotation) * (leaf.bounds.center - hinge);
            offset.y = 0f;
            Quaternion reversed = door.GetDoorRotation(-door.GetAngle(EDoorState.Open)) * t.parent.rotation;
            float y = leaf.bounds.center.y - hinge.y;
            for (int i = 1; i <= 2; i++)
            {
                Vector3 point = hinge + reversed * (offset * (i == 1 ? 1f : 1.8f)) + Vector3.up * y;
                if (Physics.CheckSphere(point, 0.2f, LayersMaskController.HighPolyWithTerrainMask, QueryTriggerInteraction.Ignore))
                {
                    return false;
                }
            }
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

/// <summary>Marks a bot's hand-open of a door for BotDoorPushOpen (BSG: SetDoorState -> InitializeSmoothOpenInteraction ->
/// InteractionState.InitSmoothOpen, synchronous).</summary>
public class BotDoorPushOpenPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(WorldInteractiveObject), nameof(WorldInteractiveObject.InitializeSmoothOpenInteraction));
    }

    [PatchPrefix]
    public static void Prefix(WorldInteractiveObject __instance, EDoorState state)
    {
        BotDoorPushOpen.Begin(__instance, state);
    }

    [PatchPostfix]
    public static void Postfix()
    {
        BotDoorPushOpen.End();
    }

    [PatchFinalizer]
    public static Exception Finalizer(Exception __exception)
    {
        BotDoorPushOpen.End();
        return __exception;
    }
}
