using System.Collections.Generic;
using SAIN.Components;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork: human mistakes on top of the utility decisions (user 2026-09-29: bots that always pick the best move look
/// too calculated). Now and then a bot takes its 2nd (or 3rd) best option instead - by personality: Timmy 12%, Wreckless
/// 8%, Coward 6%, Normal 5%, Chad 4%, Rat / SnappingTurtle 3%, GigaChad 2% (x F6 multiplier). Off in the zzap TEST preset
/// so tests show the pure decision. Counted as utility.mistake.*, marked "(mistake)" in the decision logs.
/// </summary>
public static class UtilityMistake
{
    private static readonly Dictionary<EPersonality, float> _chance = new()
    {
        { EPersonality.Timmy, 0.12f },
        { EPersonality.Wreckless, 0.08f },
        { EPersonality.Coward, 0.06f },
        { EPersonality.Normal, 0.05f },
        { EPersonality.Chad, 0.04f },
        { EPersonality.Rat, 0.03f },
        { EPersonality.SnappingTurtle, 0.03f },
        { EPersonality.GigaChad, 0.02f },
    };

    private const float VERDICT_TIME = 5f;

    private sealed class Verdict
    {
        public object Best;
        public object Picked;
        public float Until;
    }

    private static readonly Dictionary<(string, string), Verdict> _verdicts = new();

    public static void Clear()
    {
        _verdicts.Clear();
    }

    /// <summary>Maybe swap the best entry with a worse one (only options that scored above zero). True when it did.</summary>
    public static bool Apply<T>(BotComponent bot, List<(T option, float score)> ranked, string what)
    {
        var settings = GlobalSettingsClass.Instance?.General?.CloseCombat;
        if (settings == null || !settings.UtilityMistakes || ranked == null || ranked.Count < 2 || ranked[1].score <= 0f)
        {
            return false;
        }
        // zzap (10/12 sim: a Rat 18 m from a visible enemy went cover -> shoot "(mistake)" -> cover in 0.5 s): the roll was made on
        // every re-score (several per second), so a mistake lasted one evaluation and the next one undid it. A mistake is a
        // misjudged moment: roll once while the natural best option stays the same, keep that verdict for 5 s.
        var key = (bot.ProfileId, what);
        float time = Time.time;
        var comparer = EqualityComparer<T>.Default;
        int pick;
        bool fresh = false;
        if (_verdicts.TryGetValue(key, out Verdict v) && time < v.Until && v.Best is T best && comparer.Equals(best, ranked[0].option))
        {
            if (v.Picked is not T picked)
            {
                return false;
            }
            pick = ranked.FindIndex(1, r => comparer.Equals(r.option, picked));
            if (pick < 1 || ranked[pick].score <= 0f)
            {
                return false;
            }
        }
        else
        {
            float chance = (_chance.TryGetValue(bot.Info.Personality, out float c) ? c : 0.05f) * settings.UtilityMistakeMultiplier;
            bool mistake = Random.value < chance;
            TacticDiagnostics.CountRoll($"mistake.{what}.{bot.Info.Personality}", 100f * chance, mistake);
            pick = mistake ? (ranked.Count > 2 && ranked[2].score > 0f && Random.value < 0.3f ? 2 : 1) : 0;
            _verdicts[key] = new Verdict { Best = ranked[0].option, Picked = pick > 0 ? ranked[pick].option : null, Until = time + VERDICT_TIME };
            if (_verdicts.Count > 1024)
            {
                _verdicts.Clear();
            }
            if (pick == 0)
            {
                return false;
            }
            fresh = true;
        }
        // A mistake is misjudging a close call, not doing something plainly suicidal (sim 4: first aid at 0.10 over waiting
        // at 0.65 with the enemy 9m away).
        if (ranked[0].score - ranked[pick].score > 0.35f)
        {
            return false;
        }
        (ranked[0], ranked[pick]) = (ranked[pick], ranked[0]);
        if (fresh && TacticDiagnostics.CountOn) TacticDiagnostics.Count($"utility.mistake.{what}.{bot.Info.Personality}");
        return true;
    }
}
