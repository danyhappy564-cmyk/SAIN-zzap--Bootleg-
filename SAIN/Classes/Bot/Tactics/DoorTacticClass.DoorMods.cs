using EFT;
using EFT.Interactive;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SAIN.SAINComponent.Classes.Mover;
using UnityEngine;
using UnityEngine.AI;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork (2026-10-13): the room-clear sprint ram, added to the normal clear plan as extra steps. Since the same evening it is
/// SAIN's own ram (BotDoorBreacher, ported from doordash + DualSideDoorBreach) - no other mod needed.
///
///   Sprint ram - a grenade-less clear takes a 2.8-5.5m run-up in line with the door and sprints into it with the ram armed for
///     the charge only; no burst (too slow, blocked) -> open it normally.
/// Rams outside door tactics (retreat / rush / search through a shut door): DoorRammer. Counters: doormod.ram.*.
/// Door Overhaul (shoot through / grenade on the door / push partly open doors) was dropped the same day - memory use.
/// </summary>
public partial class DoorTacticClass
{
    private const float RAM_TIME = 3.5f;
    // Longest first: a bot needs a few meters of sprint to get over the ram speed (F6, default 2.8 m/s).
    private static readonly float[] RAM_RUN_UPS = { 4.5f, 3.5f, 5.5f, 2.8f };

    private static float ModRoll(float percent)
    {
        return Settings.TestModeDoorMods ? 1f : Mathf.Clamp01(percent / 100f);
    }

    /// <summary>Right after a session is built (TryStart): which door mod steps this session gets.</summary>
    private void PlanDoorMods(Session s, EPersonality personality, bool holdStance)
    {
        Door door = s.Door.Door;
        if (door == null || s.IsSupport || holdStance)
        {
            return;
        }
        if (s.Plan != EPlan.Clear || door.DoorState != EDoorState.Shut)
        {
            return;
        }

        if (!s.ClearReal && Settings.DoorModRam && BotDoorBreacher.Active)
        {
            if (!BotDoorBreacher.BothSides && BotDoorBreacher.WillSwingToward(door, Bot.Position))
            {
                TacticDiagnostics.Count("doormod.ram.doorSwingsToward");
            }
            else if (Random.value >= ModRoll(Settings.DoorModRamChance))
            {
                TacticDiagnostics.Count("doormod.ram.rollFailed");
            }
            else if (!FindRamRunUp(s, out string why))
            {
                if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"doormod.ram.noRunUp.{why}");
                if (LogOn) Log($"{Who()} door mod: no straight run-up to door {s.Door.Id} ({why}) - normal clear");
            }
            else
            {
                s.WantRam = true;
                s.ClearFakesLeft = 0;
                s.FirstStep = EStep.ModRamRunUp;
                TacticDiagnostics.Count("doormod.ram.planned");
                if (LogOn) Log($"{Who()} door mod: sprint ram into door {s.Door.Id} ({HorizontalDistance(s.RamRunUp, s.Center):0.0}m run-up)");
            }
        }
    }

    /// <summary>Leaving the tactic for any reason: take the ram away, let the door opener work again.</summary>
    private void EndDoorMods(Session s)
    {
        if (s.RamStarted)
        {
            BotDoorBreacher.Stop(Player);
            Bot.DoorOpener.NoInteractUntil = 0f;
        }
    }

    /// <summary>The ram couldn't go ahead: carry on with the room clear it replaced (open the door, dash in).</summary>
    private void BackToNormalClear(Session s)
    {
        s.WantRam = false;
        s.ClearReal = false;
        Door door = s.Door.Door;
        s.NeedsOpenForPeek = door != null && door.DoorState != EDoorState.Open;
        SetStep(EStep.MoveToStack, "doorModFallback");
    }

    // ---------------------------------------------------------------- sprint ram

    private bool FindRamRunUp(Session s, out string why)
    {
        Vector3 axis = Vector3.Cross(Vector3.up, s.BotSide).normalized;
        why = "noSpot";
        foreach (float depth in RAM_RUN_UPS)
        {
            if (!SampleNav(s.Center + s.BotSide * depth, out Vector3 point))
            {
                continue;
            }
            if (Vector3.Dot(point - s.Center, s.BotSide) < 2.2f || Mathf.Abs(Vector3.Dot(point - s.Center, axis)) > s.HalfWidth * 0.6f
                || Mathf.Abs(point.y - s.Center.y) > 0.6f)
            {
                why = "notInLine";
                continue;
            }
            // A clear straight lane up to the leaf (chest height, stopping just short of the door).
            Vector3 front = s.Center + s.BotSide * 0.3f + Vector3.up * 1.2f;
            if (Physics.Linecast(point + Vector3.up * 1.2f, front, LayersMaskController.HighPolyWithTerrainMask))
            {
                why = "laneBlocked";
                continue;
            }
            if (!Bot.Mover.CanGoToPoint(point, out NavMeshPath path, true) || PathLength(path) > 12f)
            {
                why = "noPath";
                continue;
            }
            // The charge runs a normal path from the run-up through the (shut) doorway to the dash point - check it exists
            // now, or the charge would end in "noPath" instead of the normal-open fallback.
            var through = new NavMeshPath();
            if (!NavMesh.CalculatePath(point, s.RushPoint, NavMesh.AllAreas, through) || through.status != NavMeshPathStatus.PathComplete
                || PathLength(through) > depth + 6f)
            {
                why = "noPathThroughDoor";
                continue;
            }
            s.RamRunUp = point;
            why = string.Empty;
            return true;
        }
        return false;
    }

    private void TickRamCharge(Session s, float stepTime)
    {
        Door door = s.Door.Door;
        s.LookTarget = s.RushLook;
        if (!s.RamStarted)
        {
            if (door == null || door.DoorState != EDoorState.Shut || !BotDoorBreacher.Start(Player, RAM_TIME))
            {
                RamFallback(s, door == null || door.DoorState != EDoorState.Shut ? "doorNotShut" : "noBreacher");
                return;
            }
            s.RamStarted = true;
            Bot.DoorOpener.NoInteractUntil = Time.time + RAM_TIME;
            Bot.Mover.SetTargetPose(1f);
            Bot.Mover.SetTargetMoveSpeed(1f);
            TacticDiagnostics.Count("doormod.ram.charge");
        }
        if (!s.RamBurst && door != null && door.DoorState != EDoorState.Shut)
        {
            // The ram breached it (Breaching -> Open), or someone else opened it: either way, through we go.
            s.RamBurst = true;
            TacticDiagnostics.Count("doormod.ram.burst");
            if (LogOn) Log($"{Who()} door mod: rammed door {s.Door.Id} ({door.DoorState}) at {Player.Velocity.magnitude:0.0} m/s");
        }
        if (!s.RamBurst)
        {
            float toDoor = HorizontalDistance(Bot.Position, s.Center);
            if (toDoor < 1.1f)
            {
                if (s.RamNearDoorSince < 0f)
                {
                    s.RamNearDoorSince = Time.time;
                }
                else if (Time.time - s.RamNearDoorSince > 0.5f)
                {
                    RamFallback(s, $"noBurst({Player.Velocity.magnitude:0.0}m/s, needs {BotDoorBreacher.MinSpeed:0.0})");
                    return;
                }
            }
            if (stepTime > RAM_TIME - 0.5f)
            {
                RamFallback(s, "timeout");
                return;
            }
        }
        if (MoveStep(s, s.RushPoint, true, stepTime) || (s.RamBurst && stepTime > RAM_TIME))
        {
            BotDoorBreacher.Stop(Player);
            Bot.DoorOpener.NoInteractUntil = 0f;
            TacticDiagnostics.Count("door.clear.entered");
            End("roomEntered");
        }
    }

    private void RamFallback(Session s, string why)
    {
        BotDoorBreacher.Stop(Player);
        Bot.DoorOpener.NoInteractUntil = 0f;
        s.RamStarted = false;
        if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"doormod.ram.fallback.{why.Split('(')[0]}");
        if (LogOn) Log($"{Who()} door mod: ram on door {s.Door.Id} didn't happen ({why}) - opening it normally");
        BackToNormalClear(s);
    }
}
