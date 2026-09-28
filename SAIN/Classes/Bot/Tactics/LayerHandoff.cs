using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork: measures the brain handoff between SAIN combat and ORBIT (user 2026-09-29: "the layer order and timing -
/// combat going on -> handing over to ORBIT - needs thought"). ORBIT (priority 19, below SAIN Combat 20) only takes over
/// once the "SAIN : Combat Layer" has been inactive for 15s, the bot isn't under fire and isn't healing. This logs, per bot:
/// how the combat layer ended (last decision), which layers ran in the gap and for how long (BSG vanilla layers run then),
/// how long until ORBIT took over, and ORBIT -> combat flips (ORBIT walking the bot back into the fight within seconds).
/// Logged as [Handoff] (Door Tactics diagnostic logs), counted as handoff.*.
/// </summary>
public static class LayerHandoff
{
    public const string SainCombat = "SAIN : Combat Layer";
    public const string Orbit = "OrbitBrainLayer";

    private sealed class State
    {
        public string Layer;
        public float Since;
        public float CombatEndedAt = -1f;
        public string EndDecision;
        public bool Waiting;
        public readonly List<(string layer, float seconds)> Between = new();
    }

    private static readonly Dictionary<string, State> _states = new();

    public static void OnLayerChanged(string profileId, string botName, string newLayer, string lastDecision)
    {
        if (string.IsNullOrEmpty(profileId) || string.IsNullOrEmpty(newLayer))
        {
            return;
        }
        float now = Time.time;
        if (!_states.TryGetValue(profileId, out State st))
        {
            st = new State { Layer = newLayer, Since = now };
            _states[profileId] = st;
            return;
        }
        string prev = st.Layer;
        if (prev == newLayer)
        {
            return;
        }
        float prevTime = now - st.Since;

        if (prev == SainCombat)
        {
            st.CombatEndedAt = now;
            st.EndDecision = lastDecision;
            st.Waiting = true;
            st.Between.Clear();
        }
        else if (st.Waiting)
        {
            // Time spent in the layer we're leaving belongs to the gap.
            st.Between.Add((prev, prevTime));
        }

        if (st.Waiting && newLayer == Orbit)
        {
            float gap = now - st.CombatEndedAt;
            TacticDiagnostics.Count("handoff.toOrbit");
            TacticDiagnostics.Count(gap < 16f ? "handoff.toOrbit.gap15s" : gap < 30f ? "handoff.toOrbit.gap16to30s" : "handoff.toOrbit.gap30s+");
            TacticDiagnostics.LogCloseCombat($"[Handoff] [{botName}] combat ended ({st.EndDecision}) -> ORBIT after {gap:0.0}s{Gap(st)}");
            st.Waiting = false;
        }
        else if (st.Waiting && newLayer == SainCombat)
        {
            float gap = now - st.CombatEndedAt;
            TacticDiagnostics.Count(gap < 15f ? "handoff.combatResumedBeforeOrbit" : "handoff.combatResumedLate");
            if (st.Between.Count > 0)
            {
                TacticDiagnostics.LogCloseCombat($"[Handoff] [{botName}] combat ended ({st.EndDecision}) and resumed {gap:0.0}s later, ORBIT never got it{Gap(st)}");
            }
            st.Waiting = false;
        }

        if (prev == Orbit && newLayer == SainCombat)
        {
            TacticDiagnostics.Count(prevTime < 5f ? "handoff.orbitFlipFlop" : "handoff.orbitToCombat");
            if (prevTime < 5f)
            {
                TacticDiagnostics.LogCloseCombat($"[Handoff] [{botName}] ORBIT had it only {prevTime:0.0}s -> back to SAIN combat");
            }
        }
        if (st.Waiting && newLayer != SainCombat && newLayer != Orbit && !newLayer.StartsWith("SAIN"))
        {
            TacticDiagnostics.Count($"handoff.gapLayer.{newLayer.Replace(' ', '_')}");
        }
        st.Layer = newLayer;
        st.Since = now;
    }

    private static string Gap(State st)
    {
        if (st.Between.Count == 0)
        {
            return string.Empty;
        }
        var sb = new StringBuilder(" via ");
        for (int i = 0; i < st.Between.Count && i < 6; i++)
        {
            if (i > 0)
            {
                sb.Append(" > ");
            }
            sb.Append($"{st.Between[i].layer} {st.Between[i].seconds:0.0}s");
        }
        return sb.ToString();
    }

    public static void Clear()
    {
        _states.Clear();
    }
}
