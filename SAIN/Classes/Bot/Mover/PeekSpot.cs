using SAIN.Components;
using SAIN.SAINComponent.Classes.Tactics;
using UnityEngine;
using UnityEngine.AI;

namespace SAIN.SAINComponent.Classes.Mover;

/// <summary>
/// zzap fork: "step to where the angle can be seen" for any action that holds a position and aims at something (10th sim
/// screenshots: bots holding a corner crouched deep behind the wall, muzzle on the wall). Only when the line from the gun
/// to the aim point is blocked does the bot move, and only as far as the first spot where it opens (least exposure):
/// searched in 0.3m steps toward a given point (the corner / door), then sideways both ways. Call Tick every frame while
/// holding; true = the bot is walking to the spot (the caller must not stop the mover this frame).
/// </summary>
public sealed class PeekSpot(BotComponent bot, string owner)
{
    private const float STEP = 0.3f;

    private readonly BotComponent _bot = bot;
    private readonly string _owner = owner;
    private Vector3? _spot;
    private float _moveUntil;
    private float _nextCheck;
    private int _tries;

    public bool Moving
    {
        get { return _spot != null; }
    }

    /// <summary>
    /// zzap: the angle is hidden only by low cover in front of a crouched gun - standing up right here opens it, no step
    /// needed. Callers raise the pose while this is set (kept until Reset, so the bot doesn't bob up and down).
    /// </summary>
    public bool StandToSee { get; private set; }

    public void Reset()
    {
        _spot = null;
        _tries = 0;
        _nextCheck = Time.time + 0.3f;
        StandToSee = false;
    }

    private const float STAND_GUN_HEIGHT = 1.45f;
    // Head above the weapon root (standing ~1.6 vs 1.45, crouched about the same gap). Bots see from the head.
    private const float EYE_ABOVE_GUN = 0.15f;
    // zzap (10/2 14:38 sim + screenshot): the first spot where the gun line clears is the knife edge - body on the wall,
    // barrel past the frame, head still behind it (the bot "holds" an angle it can't see; one was killed holding it by a
    // player it never saw). Go one more step past the edge when the navmesh allows.
    private const float EDGE_MARGIN = 0.25f;

    /// <param name="aim">What the bot aims at while holding.</param>
    /// <param name="toward">Where the angle opens from (corner edge, door) - searched first.</param>
    /// <param name="maxStep">Farthest the bot may move (m).</param>
    /// <param name="keepFromToward">Never closer than this to <paramref name="toward"/> (m).</param>
    public bool Tick(Vector3 aim, Vector3 toward, float maxStep, float keepFromToward = 0.6f)
    {
        if (_spot != null)
        {
            return Walk();
        }
        if (_tries >= 3 || Time.time < _nextCheck)
        {
            return false;
        }
        _nextCheck = Time.time + 1.5f;
        Vector3 gun = _bot.Transform.WeaponRoot;
        int mask = LayersMaskController.HighPolyWithTerrainMask;
        Vector3 from = _bot.Position;
        float gunUp = gun.y - from.y;
        if (Sees(from, gunUp, aim, mask))
        {
            return false;
        }
        _tries++;

        // zzap: crouched behind low cover - standing up here is the smallest change that opens the angle.
        if (!StandToSee && gunUp < STAND_GUN_HEIGHT - 0.2f && Sees(from, STAND_GUN_HEIGHT, aim, mask))
        {
            StandToSee = true;
            if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"peekSpot.stand.{_owner}");
            return false;
        }
        if (StandToSee)
        {
            gunUp = Mathf.Max(gunUp, STAND_GUN_HEIGHT);
        }

        Vector3 toTarget = toward - from;
        toTarget.y = 0f;
        float toDist = toTarget.magnitude;
        Vector3 toAim = aim - from;
        toAim.y = 0f;
        Vector3 side = toAim.sqrMagnitude > 0.01f ? new Vector3(-toAim.z, 0f, toAim.x).normalized : Vector3.zero;

        Vector3? best = null;
        float bestStep = float.MaxValue;
        if (toDist > keepFromToward + STEP)
        {
            Vector3 toward1 = toTarget / toDist;
            Search(from, toward1, Mathf.Min(maxStep, toDist - keepFromToward), gunUp, aim, mask, ref best, ref bestStep, 0f);
            if (side != Vector3.zero)
            {
                // zzap: half toward the corner, half sideways (most "no spot" cases: the opening is diagonal from the bot -
                // 13th/14th sims, no spot 497-622 times against 111-165 steps).
                Search(from, (toward1 + side).normalized, maxStep, gunUp, aim, mask, ref best, ref bestStep, STEP * 0.5f);
                Search(from, (toward1 - side).normalized, maxStep, gunUp, aim, mask, ref best, ref bestStep, STEP * 0.5f);
            }
        }
        if (side != Vector3.zero)
        {
            // Sideways only wins when clearly shorter than the way toward the corner/door (0.3m penalty).
            Search(from, side, maxStep, gunUp, aim, mask, ref best, ref bestStep, STEP);
            Search(from, -side, maxStep, gunUp, aim, mask, ref best, ref bestStep, STEP);
        }
        if (best == null)
        {
            if (TacticDiagnostics.CountOn) TacticDiagnostics.Count("peekSpot.none");
            return false;
        }
        _spot = best;
        _moveUntil = Time.time + 4f;
        if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"peekSpot.step.{_owner}");
        if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat(
            $"[PeekSpot] [{_bot.name}] [{_bot.Info.Personality}] {_owner}: wall between gun and the angle -> stepping {(best.Value - from).magnitude:0.0}m to see it");
        return Walk();
    }

    private static void Search(Vector3 from, Vector3 dir, float max, float gunUp, Vector3 aim, int mask, ref Vector3? best, ref float bestStep, float penalty)
    {
        for (float step = STEP; step <= max + 0.01f && step + penalty < bestStep; step += STEP)
        {
            if (!NavMesh.SamplePosition(from + dir * step, out NavMeshHit hit, 0.4f, -1))
            {
                continue;
            }
            Vector3 spot = hit.position;
            if (NavMesh.Raycast(from, spot, out _, -1))
            {
                return;
            }
            if (!Sees(spot, gunUp, aim, mask))
            {
                continue;
            }
            // Off the knife edge: a little further out if that is walkable and still sees the angle.
            Vector3 past = spot + dir * EDGE_MARGIN;
            if (!NavMesh.Raycast(spot, past, out NavMeshHit pastHit, -1) && Sees(pastHit.position, gunUp, aim, mask))
            {
                spot = pastHit.position;
            }
            best = spot;
            bestStep = step + penalty;
            return;
        }
    }

    /// <summary>Both the gun and the head clear the wall (a gun past the frame with the head behind it sees nothing).</summary>
    private static bool Sees(Vector3 spot, float gunUp, Vector3 aim, int mask)
    {
        return !Physics.Linecast(spot + Vector3.up * gunUp, aim, mask) && !Physics.Linecast(spot + Vector3.up * (gunUp + EYE_ABOVE_GUN), aim, mask);
    }

    private bool Walk()
    {
        Vector3 spot = _spot.Value;
        Vector3 d = spot - _bot.Position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.25f * 0.25f || Time.time > _moveUntil || !_bot.Mover.WalkToPoint(spot, true, 0.2f))
        {
            _spot = null;
            _bot.Mover.Stop();
            _nextCheck = Time.time + 1f;
            return false;
        }
        _bot.Mover.SetTargetMoveSpeed(0.45f);
        return true;
    }
}
