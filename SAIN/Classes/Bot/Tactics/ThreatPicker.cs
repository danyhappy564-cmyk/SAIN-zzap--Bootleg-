using System.Collections.Generic;
using EFT;
using SAIN.Components;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.SAINComponent.Classes.EnemyClasses;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork: target by threat (field 2026-09-29 bot-vs-bot sim: "a third party shoots a bot and it keeps aiming at the one
/// it was fighting, however close and dangerous the new one is" / "suppressed from behind and it just walks on"). SAIN's
/// chooser only considers VISIBLE enemies while any is visible (an unseen shooter behind can't win), and with none visible
/// it keeps the current target unless the bot is running to cover. A player turns to whoever is hurting him.
/// Runs first in SelectEnemy: only an enemy ENGAGING the bot (hit it in the last 3s or shot at it in the last 2s) can take
/// the target away; threat = in sight 0.5 + hit me 1.0 + shooting at me 0.6 + looking at me 0.2 + closeness 0.6 (60m -> 0);
/// the current target gets +0.35 (no flip-flopping) and +0.3 when it's nearly dead (finish him). Null = SAIN decides as before.
/// 3rd sim (user: "PMC vs PMC, a scav shows up and they team up"): a scav just shooting past from out of sight took both
/// PMCs off each other. Now: an enemy still in a duel with me (shot at / hit me in the last 10s) +0.25, a PMC +0.3 over a scav
/// (gear, skill, loot motive), and while my target is IN SIGHT an unseen enemy only takes over by actually hitting me -
/// turning away from a visible gunfight for bullets snapping by gets you killed by the one in front.
/// </summary>
public static class ThreatPicker
{
    private static readonly Dictionary<string, float> _nextLog = new();

    // zzap, 2026-10-02 (3 sims: target A -> B -> A within 0.5s 77-379 times per raid, one bot 40 flips in 4s, its decision
    // flipping StandAndShoot <-> Freeze every 0.1s): once this picker took the target, it returned null on the next tick
    // ("keep what you have"), and SAIN's own chooser - visible enemies first - handed the target straight back to the visible
    // one; next tick the picker switched again. Now the pick is remembered and kept explicitly while that enemy keeps
    // engaging the bot, and at least F6 Threat Target Keep Time seconds.
    private sealed class Held
    {
        public string EnemyId;
        public float KeepUntil;
    }

    private static readonly Dictionary<string, Held> _held = new();

    public static Enemy Pick(BotComponent bot, List<Enemy> known, Enemy goal)
    {
        var settings = GlobalSettingsClass.Instance?.General?.CloseCombat;
        if (settings == null || !settings.ThreatTargeting || known == null || known.Count < 2)
        {
            return null;
        }
        float time = Time.time;
        Enemy best = null;
        float bestThreat = float.MinValue;
        string bestWhy = null;
        foreach (Enemy e in known)
        {
            if (e == null || ReferenceEquals(e, goal) || !Usable(e))
            {
                continue;
            }
            float hitAgo = e.Status.TimeLastShotMe > 0f ? time - e.Status.TimeLastShotMe : 999f;
            float shotAtAgo = e.Status.TimeLastShotAtMe > 0f ? time - e.Status.TimeLastShotAtMe : 999f;
            if (hitAgo > 3f && shotAtAgo > 2f)
            {
                continue;
            }
            if (goal != null && goal.IsVisible && !e.IsVisible && hitAgo > 3f)
            {
                continue;
            }
            float t = Threat(e, hitAgo, shotAtAgo);
            if (t > bestThreat)
            {
                bestThreat = t;
                best = e;
                bestWhy = !TacticDiagnostics.LogOn ? null : $"{(hitAgo < 3f ? $"hit me {hitAgo:0.0}s ago" : $"shooting at me {shotAtAgo:0.0}s ago")}, {e.RealDistance:0}m, {(e.IsVisible ? "in sight" : "unseen")}, {e.EnemyPlayer.Side}";
            }
        }
        if (best == null)
        {
            return KeepHeld(bot.ProfileId, goal, time) ? goal : null;
        }
        float goalThreat = float.MinValue;
        if (goal != null && Usable(goal))
        {
            float gHit = goal.Status.TimeLastShotMe > 0f ? time - goal.Status.TimeLastShotMe : 999f;
            float gShot = goal.Status.TimeLastShotAtMe > 0f ? time - goal.Status.TimeLastShotAtMe : 999f;
            goalThreat = Threat(goal, gHit, gShot) + 0.35f
                + (goal.IsVisible && (goal.EnemyPlayer.HealthStatus == ETagStatus.Dying || goal.EnemyPlayer.HealthStatus == ETagStatus.BadlyInjured) ? 0.3f : 0f);
        }
        if (bestThreat <= goalThreat)
        {
            return KeepHeld(bot.ProfileId, goal, time) ? goal : null;
        }
        if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"threat.switch.{(best.IsVisible ? "seen" : "unseen")}");
        string id = bot.ProfileId;
        if (!_held.TryGetValue(id, out Held held))
        {
            held = new Held();
            _held[id] = held;
        }
        held.EnemyId = best.EnemyProfileId;
        held.KeepUntil = time + Mathf.Max(0f, settings.ThreatTargetKeepTime);
        if (!_nextLog.TryGetValue(id, out float next) || time > next)
        {
            _nextLog[id] = time + 3f;
            if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat(
                $"[Threat] [{bot.name}] [{bot.Info.Personality}] target {goal?.EnemyPlayer?.Profile?.Nickname ?? "none"} {goal?.EnemyPlayer?.Side} ({(goalThreat == float.MinValue ? "-" : goalThreat.ToString("0.00"))}) "
                    + $"-> {best.EnemyPlayer?.Profile?.Nickname} ({bestThreat:0.00}): {bestWhy}");
        }
        return best;
    }

    /// <summary>
    /// The current target is the one this picker chose: keep it while it is still shooting at / hitting the bot (plus a
    /// second), and at least until the keep time runs out. Then SAIN's chooser may take over again.
    /// </summary>
    private static bool KeepHeld(string botId, Enemy goal, float time)
    {
        if (goal == null || !_held.TryGetValue(botId, out Held held) || held.EnemyId != goal.EnemyProfileId)
        {
            return false;
        }
        if (!Usable(goal))
        {
            held.EnemyId = null;
            return false;
        }
        float hitAgo = goal.Status.TimeLastShotMe > 0f ? time - goal.Status.TimeLastShotMe : 999f;
        float shotAtAgo = goal.Status.TimeLastShotAtMe > 0f ? time - goal.Status.TimeLastShotAtMe : 999f;
        if (hitAgo < 3f || shotAtAgo < 2f)
        {
            held.KeepUntil = Mathf.Max(held.KeepUntil, time + 1f);
        }
        if (time < held.KeepUntil)
        {
            TacticDiagnostics.Count("threat.keep");
            return true;
        }
        held.EnemyId = null;
        return false;
    }

    private static bool Usable(Enemy e)
    {
        return e.CheckValid() && Enemy.IsEnemyActive(e) && e.EnemyKnown && e.LastKnownPosition != null && !e.IsZombie && e.EnemyPlayer != null;
    }

    private static float Threat(Enemy e, float hitAgo, float shotAtAgo)
    {
        bool duel = hitAgo < 10f || shotAtAgo < 10f;
        bool pmc = e.EnemyPlayer.Side != EPlayerSide.Savage;
        return (e.IsVisible ? 0.5f : 0f) + (hitAgo < 3f ? 1f : 0f) + (shotAtAgo < 2f ? 0.6f : 0f) + (e.IsVisible && e.EnemyLookingAtMe ? 0.2f : 0f)
            + 0.6f * Mathf.Clamp01(1f - e.RealDistance / 60f) + (duel ? 0.25f : 0f) + (pmc ? 0.3f : 0f);
    }

    public static void Clear()
    {
        _nextLog.Clear();
        _held.Clear();
    }
}
