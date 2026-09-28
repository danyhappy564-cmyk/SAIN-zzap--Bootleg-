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
/// run follows the view; on some switches the bot hops (headroom only). The move direction is bent here
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

    public static void Filter(BotComponent bot, ref Vector3 direction)
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
        if (time >= st.NextSwitch)
        {
            st.Sign = -st.Sign;
            st.NextSwitch = time + Random.Range(0.45f, 0.8f);
            st.BlockedThisSwing = false;
            TacticDiagnostics.Count("retreat.weave");
            if (Random.value * 100f < settings.RetreatWeaveJumpChance && bot.Player.MovementContext.IsGrounded
                && !Physics.Raycast(bot.Position + Vector3.up * 1.7f, Vector3.up, 0.7f, LayersMaskController.HighPolyWithTerrainMask)
                && bot.Mover.TryJump())
            {
                TacticDiagnostics.Count("retreat.weaveJump");
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
