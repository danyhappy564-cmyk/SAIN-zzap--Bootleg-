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
        Vector3 bent = Quaternion.AngleAxis(st.Sign * settings.RetreatWeaveAngle, Vector3.up) * flat.normalized;
        // Never bend into a wall or a door frame (navmesh edge or anything solid at chest height within 1.2m).
        if (NavMesh.Raycast(bot.Position, bot.Position + bent * 1.5f, out _, -1)
            || Physics.Raycast(bot.Position + Vector3.up * 1.1f, bent, 1.2f, LayersMaskController.HighPolyWithTerrainMask))
        {
            TacticDiagnostics.Count("retreat.weaveBlocked");
            st.AppliedUntil = 0f;
            return;
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
