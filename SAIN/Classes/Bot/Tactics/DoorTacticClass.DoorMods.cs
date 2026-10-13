using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using SAIN.Components;
using SAIN.Models.Enums;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.Classes.Mover;
using UnityEngine;
using UnityEngine.AI;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork (2026-10-13): door tactics that need another door mod, added to the normal plans as extra steps. Every one is
/// planned only while its mod is installed (DoorModInterop finds it at game start) - without the mod the plans are exactly
/// what they were before.
///
///   Shoot through (Door Overhaul) - at the stack of a shut door, with the room enemy fresh, a burst or two through the leaf at
///     where he was, when the ammo in the gun punches through that door's material. Then the planned next step.
///   Grenade on the door (Door Overhaul) - a room clear with a real grenade throws it at the shut door from 6-9m back; the blast
///     tears the leaf apart, the bot sprints back to the frame and dashes in. A door that survives -> the old open-and-dash.
///   Sprint ram (doordash) - a grenade-less clear takes a 2.6-4m run-up in line with the door and sprints into it with
///     doordash's RaycastBreacher on the bot for the charge only; no burst (too slow, wrong side) -> open it normally.
/// Counters: doormod.* (TacticDiagnostics), journal / [DoorTactic] lines with "door mod".
/// </summary>
public partial class DoorTacticClass
{
    private const float SHOOT_MIN_ROOM_DEPTH = 0.3f;
    private const float BREACH_TARGET_OFFSET = 0.6f;
    private const float BREACH_MIN_DIST = OwnGrenadeTracker.DANGER_RADIUS + 0.5f;
    private const float BREACH_MAX_PATH = 15f;
    private const float RAM_TIME = 3.5f;

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
        bool shut = door.DoorState == EDoorState.Shut;

        // Shoot through: any pushing plan (not the silent Rat ambush, not hold-stance waits).
        if (Settings.DoorModShootThrough && shut && (s.Plan == EPlan.Clear || s.Plan == EPlan.Peek || s.Plan == EPlan.Trap)
            && DoorModInterop.IsDestructible(door, out _))
        {
            s.WantShootThrough = Random.value < ModRoll(Settings.DoorModShootThroughChance);
            if (TacticDiagnostics.CountOn) TacticDiagnostics.Count(s.WantShootThrough ? "doormod.shoot.planned" : "doormod.shoot.rollFailed");
        }

        if (s.Plan != EPlan.Clear || !shut)
        {
            return;
        }

        if (s.ClearReal && Settings.DoorModGrenadeBreach && DoorModInterop.IsDestructible(door, out _))
        {
            if (Random.value >= ModRoll(Settings.DoorModGrenadeBreachChance))
            {
                TacticDiagnostics.Count("doormod.breach.rollFailed");
            }
            else if (!FindBreachThrowPoint(s, out string why))
            {
                if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"doormod.breach.noThrowPoint.{why}");
                if (LogOn) Log($"{Who()} door mod: no spot to throw at door {s.Door.Id} from ({why}) - normal clear");
            }
            else
            {
                s.WantBreachNade = true;
                s.ClearFakesLeft = 0;
                s.FirstStep = EStep.ModBreachMove;
                TacticDiagnostics.Count("doormod.breach.planned");
                if (LogOn) Log($"{Who()} door mod: grenade on door {s.Door.Id} from {HorizontalDistance(s.BreachThrowPoint, s.BreachTarget):0.0}m back, then dash");
            }
            return;
        }

        if (!s.ClearReal && Settings.DoorModRam && DoorModInterop.RamActive)
        {
            if (DoorOpener.IsDoorPullOpen(s.Door, Bot.Position))
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

    /// <summary>Leaving the tactic for any reason: stop the shooting, take the ram away, let the door opener work again.</summary>
    private void EndDoorMods(Session s)
    {
        if (s.Step == EStep.ModShootThrough && s.ShootFired)
        {
            Bot.ManualShoot.Reset();
        }
        if (s.RamStarted)
        {
            DoorModInterop.StopRam(Player);
            Bot.DoorOpener.NoInteractUntil = 0f;
        }
    }

    // ---------------------------------------------------------------- shoot through

    /// <summary>At the stack: shoot through first (when planned and still sensible right now), else straight to <paramref name="next"/>.</summary>
    private EStep ShootThroughFirst(Session s, EStep next)
    {
        if (!s.WantShootThrough)
        {
            return next;
        }
        s.WantShootThrough = false;
        if (!CanShootThrough(s, out string why))
        {
            if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"doormod.shoot.skipped.{why.Split('(')[0]}");
            if (LogOn) Log($"{Who()} door mod: shoot through door {s.Door.Id} skipped: {why}");
            return next;
        }
        s.AfterShoot = next;
        s.ShootFor = Random.Range(0.9f, 2.0f);
        s.ShootFired = false;
        TacticDiagnostics.Count("doormod.shoot.start");
        if (LogOn) Log($"{Who()} door mod: shooting through door {s.Door.Id} for {s.ShootFor:0.0}s, then {next}");
        return EStep.ModShootThrough;
    }

    private bool CanShootThrough(Session s, out string why)
    {
        Door door = s.Door.Door;
        if (door == null || door.DoorState != EDoorState.Shut)
        {
            why = "doorNotShut";
            return false;
        }
        if (!DoorModInterop.IsDestructible(door, out float needPen))
        {
            why = "notDestructible";
            return false;
        }
        float pen = AmmoPenetration();
        if (pen < needPen)
        {
            why = $"ammoTooWeak(pen {pen:0} < {needPen:0})";
            return false;
        }
        if (!Bot.ManualShoot.CanShoot(false))
        {
            why = "weaponNotReady";
            return false;
        }
        Enemy enemy = s.Enemy;
        Vector3? known = enemy?.KnownPlaces.LastKnownPosition;
        if (enemy == null || known == null || enemy.TimeSinceLastKnownUpdated > Settings.DoorModShootMaxInfoAge)
        {
            why = "enemyInfoOld";
            return false;
        }
        if (Vector3.Dot(known.Value - s.Center, s.BotSide) > -SHOOT_MIN_ROOM_DEPTH)
        {
            why = "enemyNotInRoom";
            return false;
        }
        if (TeammateNear(known.Value, 15f, s.Center, s.BotSide, roomSideOnly: true))
        {
            why = "teammateInRoom";
            return false;
        }

        // Where the line from the muzzle to his chest crosses the door plane, kept on the leaf (not the frame).
        Vector3 fire = Bot.Transform.WeaponData.FirePort;
        Vector3 chest = known.Value + Vector3.up * 1.1f;
        Vector3 axis = Vector3.Cross(Vector3.up, s.BotSide).normalized;
        Vector3 dir = chest - fire;
        float denom = Vector3.Dot(dir, s.BotSide);
        Vector3 cross = Mathf.Abs(denom) > 1e-4f ? fire + dir * (Vector3.Dot(s.Center - fire, s.BotSide) / denom) : s.Center;
        float lateral = Mathf.Clamp(Vector3.Dot(cross - s.Center, axis), -(s.HalfWidth - 0.15f), s.HalfWidth - 0.15f);
        // Kept near the gun's own height: DoorTacticAction levels look points to +-12 deg (ClampPitch), and TryShoot only fires
        // within 10 deg of where the bot looks - a steep aim at the leaf from 1m would never be shot.
        float gunHeight = Bot.Transform.WeaponRoot.y - s.Center.y;
        float height = Mathf.Clamp(Mathf.Clamp(cross.y - s.Center.y, gunHeight - 0.25f, gunHeight + 0.25f), 0.6f, 1.7f);
        Vector3 aim = s.Center + axis * lateral + Vector3.up * height;
        Vector3 toAim = aim - fire;
        float aimDist = toAim.magnitude;
        if (aimDist < 0.5f || aimDist > 6f)
        {
            why = $"badRange({aimDist:0.0}m)";
            return false;
        }
        // Nothing but the leaf between the muzzle and the aim point (stop short of the door itself).
        if (Physics.Linecast(fire, aim - toAim / aimDist * 0.2f, LayersMaskController.HighPolyWithTerrainMask))
        {
            why = "wallBeforeDoor";
            return false;
        }
        s.ShootAim = aim;
        // The "target" handed to SAIN's shot (friendly fire check, steering) sits behind the door on the same line.
        s.ShootTarget = fire + toAim / aimDist * Mathf.Max(aimDist + 1.5f, (chest - fire).magnitude);
        why = string.Empty;
        return true;
    }

    private float AmmoPenetration()
    {
        try
        {
            Weapon weapon = BotOwner.WeaponManager?.CurrentWeapon;
            return weapon?.CurrentAmmoTemplate != null ? weapon.CurrentAmmoTemplate.PenetrationPower : 0f;
        }
        catch (System.Exception)
        {
            return 0f;
        }
    }

    private void TickShootThrough(Session s, float stepTime)
    {
        s.LookTarget = s.ShootAim;
        Bot.Mover.Stop();
        Door door = s.Door.Door;
        if (door == null || door.DoorState != EDoorState.Shut)
        {
            FinishShootThrough(s, "doorMoved");
            return;
        }
        if (stepTime < 0.3f)
        {
            return; // gun onto the leaf first
        }
        if (stepTime > 0.3f + s.ShootFor)
        {
            FinishShootThrough(s, "done");
            return;
        }
        if (!Bot.ManualShoot.CanShoot(false))
        {
            FinishShootThrough(s, "weaponNotReady");
            return;
        }
        if (Bot.ManualShoot.TryShoot(s.Enemy, s.ShootTarget, true, EShootReason.Suppress))
        {
            if (!s.ShootFired)
            {
                s.ShootFired = true;
                TacticDiagnostics.Count("doormod.shoot.fired");
            }
        }
        else if (stepTime > 1.5f && !s.ShootFired)
        {
            FinishShootThrough(s, "couldNotFire");
        }
    }

    private void FinishShootThrough(Session s, string why)
    {
        if (s.ShootFired)
        {
            Bot.ManualShoot.Reset();
        }
        bool hole = DoorModInterop.HasHole(s.Door.Door);
        if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"doormod.shoot.end.{why}{(hole ? ".hole" : "")}");
        if (LogOn) Log($"{Who()} door mod: shoot through door {s.Door.Id} ended ({why}, fired={s.ShootFired}, hole={hole})");
        EStep next = s.AfterShoot;
        if (next == EStep.Hold || next == EStep.FakeNadeDraw || next == EStep.FakeHealStart)
        {
            next = FakeOrHold(s); // re-checked now: the shots may have changed what's sensible
        }
        SetStep(next, "shotThrough");
    }

    // ---------------------------------------------------------------- grenade on the door

    private bool FindBreachThrowPoint(Session s, out string why)
    {
        Vector3 target = s.Center + s.BotSide * BREACH_TARGET_OFFSET;
        s.BreachTarget = target;
        if (TeammateNear(target, OwnGrenadeTracker.DANGER_RADIUS, s.Center, s.BotSide, roomSideOnly: false))
        {
            why = "teammateNearDoor";
            return false;
        }
        Vector3 axis = Vector3.Cross(Vector3.up, s.BotSide).normalized;
        why = "noSpot";
        foreach (float depth in new[] { 7f, 8f, 6.5f, 9f })
        {
            foreach (float lateral in new[] { 0f, 1f, -1f })
            {
                Vector3 raw = s.Center + s.BotSide * depth + axis * lateral;
                if (!SampleNav(raw, out Vector3 point))
                {
                    continue;
                }
                if (Vector3.Dot(point - s.Center, s.BotSide) < BREACH_MIN_DIST - 0.5f || HorizontalDistance(point, target) < BREACH_MIN_DIST
                    || Mathf.Abs(point.y - s.Center.y) > 1.5f)
                {
                    continue;
                }
                if (Physics.Linecast(point + Vector3.up * 1.5f, target + Vector3.up * 0.3f, LayersMaskController.HighPolyWithTerrainMask))
                {
                    why = "noThrowLine";
                    continue;
                }
                if (!Bot.Mover.CanGoToPoint(point, out NavMeshPath path, true) || PathLength(path) > BREACH_MAX_PATH)
                {
                    why = "noPath";
                    continue;
                }
                s.BreachThrowPoint = point;
                why = string.Empty;
                return true;
            }
        }
        return false;
    }

    private void TickBreachThrow(Session s, float stepTime)
    {
        s.LookTarget = s.BreachTarget + Vector3.up * 0.6f;
        Bot.Mover.Stop();
        if (stepTime < 0.25f)
        {
            return;
        }
        var grenades = BotOwner.WeaponManager?.Grenades;
        if (stepTime < 3f && grenades != null && (!grenades.ReadyToThrow || Player.HandsController is IGrenadeController))
        {
            return;
        }
        Door door = s.Door.Door;
        string skip = null;
        if (door == null || door.DoorState != EDoorState.Shut)
        {
            skip = "doorNotShut";
        }
        else if (HorizontalDistance(Bot.Position, s.BreachTarget) < BREACH_MIN_DIST
            && !Physics.Linecast(s.BreachTarget + Vector3.up * 0.3f, Bot.Position + Vector3.up * 1.2f, LayersMaskController.HighPolyWithTerrainMask))
        {
            skip = "tooClose";
        }
        else if (TeammateNear(s.BreachTarget, OwnGrenadeTracker.DANGER_RADIUS, s.Center, s.BotSide, roomSideOnly: false))
        {
            skip = "teammateNearDoor";
        }
        float fuse = skip == null ? ThrowIntoRoom(s, s.BreachTarget, "doorBlast") : 0f;
        if (fuse <= 0f)
        {
            skip ??= "noThrow";
            if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"doormod.breach.skipped.{skip}");
            if (LogOn) Log($"{Who()} door mod: grenade on door {s.Door.Id} not thrown ({skip}) - back to a normal clear");
            BackToNormalClear(s, keepGrenade: skip != "noThrow");
            return;
        }
        s.BreachThrown = true;
        s.ClearReal = false;
        s.BlastTime = Time.time + fuse + 0.3f;
        s.NadeSeenLive = false;
        TacticDiagnostics.Count("doormod.breach.thrown");
        SetStep(EStep.ModBreachWait, "nadeOnDoor");
    }

    private void TickBreachWait(Session s, float stepTime)
    {
        s.LookTarget = s.BreachTarget + Vector3.up * 0.6f;
        bool live = OwnGrenadeTracker.Live(Bot.ProfileId, out Vector3 nadePos);
        if (live && OwnGrenadeTracker.Threatens(Bot.ProfileId, Bot.Position))
        {
            // Bounced back toward us: get clear first (same as the room clear wait).
            Vector3 away = Bot.Position - nadePos;
            away.y = 0f;
            Vector3 fleeRaw = Bot.Position + (away.sqrMagnitude > 0.01f ? away.normalized : s.BotSide) * 5f;
            if (SampleNav(fleeRaw, out Vector3 flee))
            {
                Bot.Mover.RunToPoint(flee, true, 0.5f, ESprintUrgency.High);
            }
            if (!s.FledOwnNade)
            {
                s.FledOwnNade = true;
                TacticDiagnostics.Count("doormod.breach.fledOwnNade");
            }
            return;
        }
        Bot.Mover.Stop();
        if (live)
        {
            s.NadeSeenLive = true;
        }
        bool blown = s.NadeSeenLive && !live;
        if (!(blown || (!live && Time.time >= s.BlastTime) || stepTime > 8f))
        {
            return;
        }
        if (OwnGrenadeTracker.Threatens(Bot.ProfileId, s.Stack))
        {
            return;
        }
        Door door = s.Door.Door;
        bool destroyed = DoorModInterop.IsDestroyed(door);
        bool hole = !destroyed && DoorModInterop.HasHole(door);
        string result = destroyed ? "doorDestroyed" : hole ? "holeOnly" : "noDamage";
        TacticDiagnostics.Count($"doormod.breach.{result}");
        if (LogOn) Log($"{Who()} door mod: grenade on door {s.Door.Id} -> {result}; {(destroyed ? "sprinting in" : "opening it from the side")}");
        // Sprint back to the frame (MoveToStack runs while BreachThrown), then: destroyed -> dash; else open and dash.
        s.ClearFakesLeft = 0;
        s.ClearReal = false;
        s.NeedsOpenForPeek = door != null && door.DoorState != EDoorState.Open;
        s.RushPathChecked = false;
        SetStep(EStep.MoveToStack, blown ? "blastDetected" : "fuseTime");
    }

    /// <summary>The door mod step couldn't go ahead: carry on with the room clear it replaced.</summary>
    private void BackToNormalClear(Session s, bool keepGrenade)
    {
        s.WantBreachNade = false;
        s.WantRam = false;
        s.ClearReal = keepGrenade && BotOwner.WeaponManager?.Grenades?.HaveGrenade == true;
        Door door = s.Door.Door;
        s.NeedsOpenForPeek = door != null && door.DoorState != EDoorState.Open;
        SetStep(EStep.MoveToStack, "doorModFallback");
    }

    // ---------------------------------------------------------------- sprint ram (doordash)

    private bool FindRamRunUp(Session s, out string why)
    {
        Vector3 axis = Vector3.Cross(Vector3.up, s.BotSide).normalized;
        why = "noSpot";
        foreach (float depth in new[] { 3.2f, 4f, 2.6f })
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
            if (door == null || door.DoorState != EDoorState.Shut || !DoorModInterop.StartRam(Player, RAM_TIME))
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
            // doordash breached it (Interacting/Breaching -> Open), or someone else opened it: either way, through we go.
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
                    RamFallback(s, $"noBurst({Player.Velocity.magnitude:0.0}m/s, needs {Mathf.Sqrt(DoorModInterop.RamSpeedSqr):0.0})");
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
            DoorModInterop.StopRam(Player);
            Bot.DoorOpener.NoInteractUntil = 0f;
            TacticDiagnostics.Count("door.clear.entered");
            End("roomEntered");
        }
    }

    private void RamFallback(Session s, string why)
    {
        DoorModInterop.StopRam(Player);
        Bot.DoorOpener.NoInteractUntil = 0f;
        s.RamStarted = false;
        if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"doormod.ram.fallback.{why.Split('(')[0]}");
        if (LogOn) Log($"{Who()} door mod: ram on door {s.Door.Id} didn't happen ({why}) - opening it normally");
        BackToNormalClear(s, keepGrenade: false);
    }

    // ---------------------------------------------------------------- shared

    /// <summary>A living squadmate within <paramref name="radius"/> of <paramref name="point"/> (optionally only on the room side).</summary>
    private bool TeammateNear(Vector3 point, float radius, Vector3 doorCenter, Vector3 botSide, bool roomSideOnly)
    {
        var members = Bot.Squad?.Members;
        if (members == null)
        {
            return false;
        }
        foreach (var member in members.Values)
        {
            if (member == null || ReferenceEquals(member, Bot) || member.IsDead)
            {
                continue;
            }
            if (roomSideOnly && Vector3.Dot(member.Position - doorCenter, botSide) >= 0f)
            {
                continue;
            }
            if ((member.Position - point).magnitude < radius)
            {
                return true;
            }
        }
        return false;
    }
}
