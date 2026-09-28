using System.Collections.Generic;
using SAIN.Components;
using SAIN.Preset.Shared.GlobalSettings;
using UnityEngine;
using UnityEngine.AI;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork: weave while running away under fire (user 2026-09-29: "when running away, zig-zag left/right, sometimes a
/// jump mixed in"). Same trigger as Retreat Head Down (sprinting with the back to an enemy that sees the bot or just shot
/// at it): the run direction swings 30 deg left/right every 0.45-0.8s, and on some switches the bot hops (headroom only).
/// Hooked into PlayerMovementController.SetTargetMoveDirection; never steers into a wall/ledge (NavMesh check).
/// </summary>
public static class RetreatWeave
{
    private sealed class State
    {
        public float Sign = 1f;
        public float NextSwitch;
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
        if (NavMesh.Raycast(bot.Position, bot.Position + bent * 1.5f, out _, -1))
        {
            return;
        }
        direction = bent;
    }

    public static void Clear()
    {
        _states.Clear();
    }
}
