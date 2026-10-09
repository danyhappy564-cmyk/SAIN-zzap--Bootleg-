using System.Collections.Generic;
using SAIN.Components;
using SAIN.Preset.Shared.GlobalSettings;
using UnityEngine;
using UnityEngine.AI;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork: weave while running away under fire (user 2026-09-29: "when running away, zig-zag left/right, sometimes a
/// jump mixed in"). Same trigger as Retreat Head Down (running to cover with the back to the enemy): like a player holding
/// W + sprint and swinging the MOUSE left/right (not A/D), the view yaw swings 30 deg left/right every 0.45-0.8s and the
/// run follows the view; on some switches the bot hops (headroom only) and flies straight. The move direction is bent here
/// (PlayerMovementController.SetTargetMoveDirection) and the look by the same angle (SAINSteeringClass via LookYaw).
/// Never swings toward a wall / door frame (navmesh edge or anything solid at chest height).
/// </summary>
public static class RetreatWeave
{
    private sealed class State
    {
        public float Sign = 1f;
        public float NextSwitch;
        public float AppliedAngle;
        public float AppliedUntil;
        public bool BlockedThisSwing;
        public float StraightUntil;
        public bool LandingJumpDone;
        public Vector3 LastDestination;
    }

    private static Vector3 Bend(Vector3 flat, float angle)
    {
        return Quaternion.AngleAxis(angle, Vector3.up) * flat.normalized;
    }

    private static bool Blocked(BotComponent bot, Vector3 dir)
    {
        return NavMesh.Raycast(bot.Position, bot.Position + dir * 1.5f, out _, -1)
            || Physics.Raycast(bot.Position + Vector3.up * 1.1f, dir, 1.2f, LayersMaskController.HighPolyWithTerrainMask);
    }

    /// <summary>The yaw offset (degrees) the look should carry this frame so the view swings with the run.</summary>
    public static float LookYaw(BotComponent bot)
    {
        if (bot != null && _states.TryGetValue(bot.ProfileId, out State st) && Time.time < st.AppliedUntil)
        {
            return st.AppliedAngle;
        }
        return 0f;
    }

    private static readonly Dictionary<string, State> _states = new();

    // zzap (user 2026-10-10: "running to cover, the jump is too short - it should be one fast long jump that lands right there,
    // now it feels like one more hide"): the hop came on a weave switch, i.e. while the run was turning 30 deg - short and
    // sideways. Now the weave holds straight while airborne so a hop keeps the sprint's full length, and one straight hop
    // when the cover is 3-5 m ahead on a clear, level line lands at the cover. The switch hops stay (user 2026-10-02:
    // "fewer bunny retreats" -> chance 12 -> 20), just not within 6 m of the cover, where the landing jump takes over.
    private const float SWITCH_HOP_MIN_COVER = 6f;
    private const float LANDING_JUMP_MIN = 3f;
    private const float LANDING_JUMP_MAX = 5f;

    private static bool TryLandingJump(BotComponent bot, State st, Vector3 destination, float time, float chance)
    {
        if (st.LandingJumpDone)
        {
            return false;
        }
        Vector3 pos = bot.Position;
        Vector3 to = destination - pos;
        float dy = Mathf.Abs(to.y);
        to.y = 0f;
        float dist = to.magnitude;
        if (dist < LANDING_JUMP_MIN || dist > LANDING_JUMP_MAX || dy > 0.4f || !bot.Player.MovementContext.IsGrounded)
        {
            return false;
        }
        Vector3 dir = to / dist;
        int mask = LayersMaskController.HighPolyWithTerrainMask;
        // clear, level run-up to the landing spot: navmesh straight line, nothing at knee/chest height, headroom
        if (NavMesh.Raycast(pos, destination, out _, -1)
            || Physics.Raycast(pos + Vector3.up * 0.5f, dir, dist, mask)
            || Physics.Raycast(pos + Vector3.up * 1.2f, dir, dist, mask)
            || Physics.Raycast(pos + Vector3.up * 1.7f, Vector3.up, 0.7f, mask))
        {
            return false;
        }
        st.LandingJumpDone = true; // one roll per run to a cover, taken the first time the line is clear
        if (Random.value * 100f >= chance || !bot.Mover.TryJump())
        {
            return false;
        }
        st.StraightUntil = time + 0.8f;
        TacticDiagnostics.Count("retreat.landingJump");
        return true;
    }

    public static void Filter(BotComponent bot, ref Vector3 direction)
    {
        Filter(bot, ref direction, null);
    }

    public static void Filter(BotComponent bot, ref Vector3 direction, Vector3? destination)
    {
        var settings = GlobalSettingsClass.Instance?.General?.CloseCombat;
        if (bot == null || settings == null || !settings.RetreatWeave || !bot.Steering.HeadDownActive || !bot.Player.IsSprintEnabled)
        {
            return;
        }
        string id = bot.ProfileId;
        if (!_states.TryGetValue(id, out State st))
        {
            st = new State { Sign = Random.value < 0.5f ? -1f : 1f };
            _states[id] = st;
        }
        float time = Time.time;
        if (destination != null)
        {
            if ((destination.Value - st.LastDestination).sqrMagnitude > 4f)
            {
                st.LastDestination = destination.Value; // a new cover to run to: one landing jump allowed again
                st.LandingJumpDone = false;
            }
            TryLandingJump(bot, st, destination.Value, time, settings.RetreatLandingJumpChance);
        }
        // airborne / just jumped: run straight, no weave - the hop keeps the sprint's full length
        if (time < st.StraightUntil || !bot.Player.MovementContext.IsGrounded)
        {
            st.AppliedUntil = 0f;
            return;
        }
        if (time >= st.NextSwitch)
        {
            st.Sign = -st.Sign;
            st.NextSwitch = time + Random.Range(0.45f, 0.8f);
            st.BlockedThisSwing = false;
            TacticDiagnostics.Count("retreat.weave");
            bool nearCover = destination != null
                && (destination.Value - bot.Position).sqrMagnitude < SWITCH_HOP_MIN_COVER * SWITCH_HOP_MIN_COVER;
            if (!nearCover && Random.value * 100f < settings.RetreatWeaveJumpChance && bot.Player.MovementContext.IsGrounded
                && !Physics.Raycast(bot.Position + Vector3.up * 1.7f, Vector3.up, 0.7f, LayersMaskController.HighPolyWithTerrainMask)
                && bot.Mover.TryJump())
            {
                st.StraightUntil = time + 0.8f; // fly along the run line, not the bent one
                st.AppliedUntil = 0f;
                TacticDiagnostics.Count("retreat.weaveJump");
                return;
            }
        }
        Vector3 flat = direction;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f)
        {
            return;
        }
        Vector3 bent = Bend(flat, st.Sign * settings.RetreatWeaveAngle);
        // Never bend into a wall or a door frame (navmesh edge or anything solid at chest height within 1.2m). In a
        // corridor one side is often a wall: swing to the open side early instead of giving up the swing (field
        // 2026-09-29: 1725 blocked frames vs 157 swings - nearly no visible weave indoors on Factory).
        if (Blocked(bot, bent))
        {
            Vector3 other = Bend(flat, -st.Sign * settings.RetreatWeaveAngle);
            if (Blocked(bot, other))
            {
                if (!st.BlockedThisSwing)
                {
                    st.BlockedThisSwing = true;
                    TacticDiagnostics.Count("retreat.weaveBlocked");
                }
                st.AppliedUntil = 0f;
                return;
            }
            st.Sign = -st.Sign;
            st.NextSwitch = time + Random.Range(0.45f, 0.8f);
            st.BlockedThisSwing = false;
            TacticDiagnostics.Count("retreat.weaveFlipOpenSide");
            bent = other;
        }
        direction = bent;
        st.AppliedAngle = st.Sign * settings.RetreatWeaveAngle;
        st.AppliedUntil = time + 0.2f;
    }

    public static void Clear()
    {
        _states.Clear();
    }
}
