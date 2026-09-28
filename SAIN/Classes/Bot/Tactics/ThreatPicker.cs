using System.Collections.Generic;
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
/// </summary>
public static class ThreatPicker
{
    private static readonly Dictionary<string, float> _nextLog = new();

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
            float t = Threat(e, hitAgo, shotAtAgo);
            if (t > bestThreat)
            {
                bestThreat = t;
                best = e;
                bestWhy = $"{(hitAgo < 3f ? $"hit me {hitAgo:0.0}s ago" : $"shooting at me {shotAtAgo:0.0}s ago")}, {e.RealDistance:0}m, {(e.IsVisible ? "in sight" : "unseen")}";
            }
        }
        if (best == null)
        {
            return null;
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
            return null;
        }
        TacticDiagnostics.Count($"threat.switch.{(best.IsVisible ? "seen" : "unseen")}");
        string id = bot.ProfileId;
        if (!_nextLog.TryGetValue(id, out float next) || time > next)
        {
            _nextLog[id] = time + 3f;
            TacticDiagnostics.LogCloseCombat(
                $"[Threat] [{bot.name}] [{bot.Info.Personality}] target {goal?.EnemyPlayer?.Profile?.Nickname ?? "none"} ({(goalThreat == float.MinValue ? "-" : goalThreat.ToString("0.00"))}) "
                    + $"-> {best.EnemyPlayer?.Profile?.Nickname} ({bestThreat:0.00}): {bestWhy}");
        }
        return best;
    }

    private static bool Usable(Enemy e)
    {
        return e.CheckValid() && Enemy.IsEnemyActive(e) && e.EnemyKnown && e.LastKnownPosition != null && !e.IsZombie && e.EnemyPlayer != null;
    }

    private static float Threat(Enemy e, float hitAgo, float shotAtAgo)
    {
        return (e.IsVisible ? 0.5f : 0f) + (hitAgo < 3f ? 1f : 0f) + (shotAtAgo < 2f ? 0.6f : 0f) + (e.IsVisible && e.EnemyLookingAtMe ? 0.2f : 0f)
            + 0.6f * Mathf.Clamp01(1f - e.RealDistance / 60f);
    }

    public static void Clear()
    {
        _nextLog.Clear();
    }
}
