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
/// 10/2: a silent enemy in sight closing in on the bot's side (flanker, see Flanker) can take the target too.
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
            bool flanker = Flanker(e, goal, bot, out bool heardOnly);
            if (hitAgo > 3f && shotAtAgo > 2f && !flanker)
            {
                continue;
            }
            if (goal != null && goal.IsVisible && !e.IsVisible && hitAgo > 3f && !heardOnly)
            {
                continue;
            }
            float t = Threat(e, hitAgo, shotAtAgo) + (flanker ? FlankBonus(e, heardOnly) : 0f);
            if (t > bestThreat)
            {
                bestThreat = t;
                best = e;
                bestWhy = !TacticDiagnostics.LogOn ? null
                    : $"{(hitAgo < 3f ? $"hit me {hitAgo:0.0}s ago" : shotAtAgo < 2f ? $"shooting at me {shotAtAgo:0.0}s ago" : (heardOnly ? "footsteps right next to me" : "not shooting yet - closing in on my side"))}, {e.RealDistance:0}m, {(e.IsVisible ? "in sight" : "unseen")}, {e.EnemyPlayer.Side}";
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
            // A flanker we already turned to keeps its flank weight against the one we turned away from - otherwise the
            // old target (still shooting) took it straight back after the keep time: flanker <-> shooter ping-pong.
            if (Flanker(goal, best, bot, out bool goalHeardOnly))
            {
                goalThreat += FlankBonus(goal, goalHeardOnly);
            }
        }
        if (bestThreat <= goalThreat)
        {
            return KeepHeld(bot.ProfileId, goal, time) ? goal : null;
        }
        if (TacticDiagnostics.CountOn)
        {
            float bHit = best.Status.TimeLastShotMe > 0f ? time - best.Status.TimeLastShotMe : 999f;
            float bShot = best.Status.TimeLastShotAtMe > 0f ? time - best.Status.TimeLastShotAtMe : 999f;
            TacticDiagnostics.Count(bHit > 3f && bShot > 2f ? "threat.switch.flanker" : $"threat.switch.{(best.IsVisible ? "seen" : "unseen")}");
        }
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

    // zzap (user 2026-10-02, said before too: "while it's aiming at someone else I don't register, and if I don't shoot it
    // never takes me as its target"): only an enemy already shooting could take the target, and SAIN's own chooser keeps a
    // visible target unless a non-shooter is within a third of its distance (target 20m -> him under 6.6m). A player turns
    // to the one walking up on his side before he opens fire. A flanker = in sight, within FLANK_DIST, and clearly closer
    // than the current target (or point-blank). Its bonus beats a target that is only "there"; against a target that is
    // shooting at the bot right now it only wins when the flanker also looks at the bot or is point-blank.
    private const float FLANK_DIST = 15f;
    private const float FLANK_POINT_BLANK = 6f;

    // User (same day): "footsteps that close are always audible - not reacting is odd". Unseen but his steps were heard
    // within HEARD_FLANK_DIST in the last 1.5s: the bot turns to him (taking him as the target turns its head, and the front
    // of its view gains sight fastest) - only when the current target is clearly farther.
    private const float HEARD_FLANK_DIST = 10f;

    private static bool Flanker(Enemy e, Enemy goal, BotComponent bot, out bool heardOnly)
    {
        heardOnly = false;
        if (goal == null || ReferenceEquals(goal, e))
        {
            return false;
        }
        if (e.IsVisible)
        {
            if (e.RealDistance > FLANK_DIST)
            {
                return false;
            }
            return e.RealDistance < FLANK_POINT_BLANK || e.RealDistance < goal.RealDistance * 0.6f;
        }
        var hearing = e.Hearing;
        if (hearing == null || Time.time - hearing.LastHeardSoundTime > 1.5f
            || (hearing.LastHeardSoundType != SAIN.Preset.Shared.Enums.SAINSoundType.FootStep && hearing.LastHeardSoundType != SAIN.Preset.Shared.Enums.SAINSoundType.Sprint))
        {
            return false;
        }
        float heardDist = (hearing.LastHeardSoundPosition - bot.Position).magnitude;
        if (heardDist > HEARD_FLANK_DIST || heardDist > goal.RealDistance * 0.6f)
        {
            return false;
        }
        heardOnly = true;
        return true;
    }

    private static float FlankBonus(Enemy e, bool heardOnly)
    {
        if (heardOnly)
        {
            return 1.0f + (e.RealDistance < FLANK_POINT_BLANK ? 0.5f : 0f);
        }
        return 1.2f + (e.RealDistance < FLANK_POINT_BLANK ? 0.5f : 0f);
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
