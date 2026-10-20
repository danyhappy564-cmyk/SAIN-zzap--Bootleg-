using System.Collections.Generic;
using SAIN.Components;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.SAINComponent.Classes.EnemyClasses;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork: heal timing by expected gain while enemies are around (user 2026-09-29: "apply the gain calculation wherever
/// it makes sense"). SAIN's fixed rule was "no first aid unless every known enemy passes a distance/time check". Now:
///   FirstAid - hurt/bleeding, in cover, the nearest threat old or far; not with an enemy in sight, him coming, or just hit.
///   Stim     - badly hurt or dying: fast, worth it even mid-fight (not the very moment of being hit).
///   Surgery  - a blacked limb, only when really safe (no one in sight, nobody coming, last info 10s+ old).
///   Wait     - the risk isn't worth it right now: an enemy fresh and close, or he's coming.
/// Only options the bot can actually do are scored. [Heal] logs the scores and why.
/// </summary>
public static class HealUtility
{
    public enum EChoice
    {
        FirstAid,
        Stim,
        Surgery,
        Wait,
    }

    private static readonly Dictionary<string, float> _nextLog = new();

    public static bool Applies(BotComponent bot)
    {
        var settings = GlobalSettingsClass.Instance?.General?.CloseCombat;
        // Not EnemyController.AtPeace: despite the name it is TRUE once the bot knows an enemy (the peace toggle is set on
        // the first known enemy and never reset), so gating on !AtPeace kept this off in every fight (field 2026-09-29:
        // 0 [Heal] lines in a 16 min raid with 88 surgery starts).
        return settings != null && settings.UtilityHeal && bot.EnemyController.KnownEnemies.Count > 0;
    }

    public static List<(EChoice choice, float score)> Rank(BotComponent bot, bool canFirstAid, bool canStim, bool canSurgery)
    {
        var hs = bot.Memory.Health.HealthStatus;
        float health = hs switch
        {
            ETagStatus.Healthy => 1f,
            ETagStatus.Injured => 0.7f,
            ETagStatus.BadlyInjured => 0.35f,
            _ => 0.1f,
        };
        // CoverInUse is also the cover the bot is still running to - only BotInThisCover means arrived.
        var cover = bot.Cover.CoverInUse;
        bool inCover = cover != null && cover.BotInThisCover;
        // 10/14 sim F8 marks #11/#12 (user: "takes hits and just walks slowly"): first aid picked on the way to cover - EFT
        // blocks sprinting while meds are in use (MovementContext.CanSprint: UsingMeds), so the bot crossed the open at walking
        // pace. Get there first; with a damaged leg it can't sprint anyway, so the cost is smaller.
        var decision = bot.Decision.CurrentCombatDecision;
        bool runningToCover = !inCover && bot.Player.Velocity.magnitude > 0.5f
            && (decision == ECombatDecision.SeekCover || decision == ECombatDecision.Retreat
                || decision == ECombatDecision.RunAway);
        bool legsHold = bot.Player.MovementContext != null && !bot.Player.MovementContext.CanSprint;
        bool hit = bot.Medical.TimeSinceShot < 2f;
        bool anyVisible = false;
        bool coming = false;
        float nearestAge = 999f;
        float nearestPath = 999f;
        Vector3? nearestKnown = null;
        float freshPath = 999f;
        float freshAge = 999f;
        Vector3? freshKnown = null;
        float time = Time.time;
        foreach (Enemy enemy in bot.EnemyController.KnownEnemies)
        {
            // Every enemy whose position is known counts - not only seen/heard ones (sim 4: the player 45m away, known 0s
            // ago through the squad, was skipped -> "nearest threat 999m" -> first aid in his face).
            if (enemy == null || enemy.KnownPlaces.LastKnownPosition == null || enemy.TimeSinceLastKnownUpdated > 120f)
            {
                continue;
            }
            if (enemy.IsVisible || enemy.InLineOfSight)
            {
                anyVisible = true;
            }
            float path = Mathf.Min(enemy.Path.PathLength, enemy.KnownPlaces.BotDistanceFromLastKnown * 1.3f);
            if (path < nearestPath)
            {
                nearestPath = path;
                nearestAge = enemy.TimeSinceLastKnownUpdated;
                nearestKnown = enemy.KnownPlaces.LastKnownPosition;
            }
            // 15:34 sim: "nearest threat 1m / 55s ago" (a stale entry) hid a fresh enemy 21m away -> first aid in a push.
            // The nearest fresh one (under 30 s) within 40 m wins over stale ones.
            if (enemy.TimeSinceLastKnownUpdated < 30f && path < 40f && path < freshPath)
            {
                freshPath = path;
                freshAge = enemy.TimeSinceLastKnownUpdated;
                freshKnown = enemy.KnownPlaces.LastKnownPosition;
            }
            if (HeardSteps(bot, enemy, 5f, 30f))
            {
                coming = true;
            }
        }
        if (freshKnown != null && nearestAge >= 30f)
        {
            nearestPath = freshPath;
            nearestAge = freshAge;
            nearestKnown = freshKnown;
        }
        float safe = Mathf.Clamp01((nearestAge - 3f) / 15f) * 0.5f + Mathf.Clamp01((nearestPath - 10f) / 30f) * 0.5f;
        // Standing where he could see us (e.g. still at the corner we were peeking from): step back before patching up
        // (user: "they heal while peeking a corner mid-fight").
        bool inHisView = nearestKnown != null && nearestAge < 30f
            && !Physics.Linecast(nearestKnown.Value + Vector3.up * 1.5f, bot.Position + Vector3.up * 1.3f, LayersMaskController.HighPolyWithTerrainMask);
        // zzap (user 2026-10-20: "why do some heal while running toward where the enemy is?" - 2nd sim Bot12: first aid 19 s
        // into a push, then walked 11 m on toward that enemy): the cover still being walked to lies closer to where he was than
        // we are now - no meds on that walk.
        bool coverTowardHim = !inCover && cover != null && nearestKnown != null && nearestAge < 30f
            && (cover.Position - nearestKnown.Value).magnitude < (bot.Position - nearestKnown.Value).magnitude - 1.5f;

        // Plenty of meds -> heal more readily; the last one is worth saving for something serious.
        var load = LoadoutProfile.Of(bot);
        float medBias = load.Meds >= 3 ? 0.1f : load.Meds <= 1 && health > 0.35f ? -0.1f : 0f;
        // Close and recent = he can be on us before the meds are done (sim 4: 6m / 6s ago counted as safe).
        // zzap (user 2026-10-20: "many heal while they hear footsteps close by and know someone is there"): 2nd sim 13:54
        // first aid won with the nearest threat 8 m / 5 s ago (0.77 > 0.57) and with steps heard 17 m off (0.64 > 0.61) - the
        // window was 15 s and the wait bonus 0.3, the heal lost only 0.35 to steps. Now 25 s, it weighs on first aid too, and
        // steps heard in the last 5 s within 30 m cost first aid 0.55.
        float closeRecent = Mathf.Clamp01((30f - nearestPath) / 25f) * Mathf.Clamp01((25f - nearestAge) / 15f);
        float firstAid = canFirstAid
            ? medBias + 0.2f + 0.5f * (1f - health) + (inCover ? 0.2f : 0f) + 0.3f * safe - (anyVisible ? 0.6f : 0f) - (coming ? 0.55f : 0f) - (hit ? 0.3f : 0f)
                - (inHisView ? 0.4f : 0f) - (runningToCover ? (legsHold ? 0.2f : 0.5f) : 0f) - 0.35f * closeRecent - (coverTowardHim ? 0.5f : 0f)
            : -1f;
        float stim = canStim && health <= 0.35f
            // 10/14 (user screenshots, Bot35: "fighting, then healing where he could be seen, and died"): +0.1 for an enemy in
            // sight held even standing in the open - he stimmed 25 m from a visible enemy. In cover it still helps; out of it,
            // with the enemy in sight or able to see us, it costs.
            ? 0.3f + 0.4f * (1f - health) + (anyVisible ? (inCover ? 0.1f : -0.25f) : 0f) - (inHisView && !inCover ? 0.2f : 0f)
                - (bot.Medical.TimeSinceShot < 0.5f ? 0.2f : 0f)
            : -1f;
        float surgery = canSurgery
            ? 0.1f + 0.4f * safe + (inCover ? 0.15f : 0f) - (anyVisible ? 0.8f : 0f) - (coming ? 0.7f : 0f) - (nearestAge < 10f ? 0.3f : 0f) - (inHisView ? 0.5f : 0f)
                - (coverTowardHim ? 0.5f : 0f)
            : -1f;
        // Ties go to waiting.
        float wait = 0.31f + 0.45f * closeRecent + (coming ? 0.25f : 0f) + (anyVisible ? 0.2f : 0f);

        var list = new List<(EChoice choice, float score)>
        {
            (EChoice.FirstAid, firstAid),
            (EChoice.Stim, stim),
            (EChoice.Surgery, surgery),
            (EChoice.Wait, wait),
        };
        list.Sort((x, y) => y.score.CompareTo(x.score));
        bool mistake = UtilityMistake.Apply(bot, list, "heal");

        string id = bot.ProfileId;
        if (!_nextLog.TryGetValue(id, out float next) || time > next)
        {
            _nextLog[id] = time + 4f;
            if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat(
                $"[Heal] [{bot.name}] {list[0].choice}{(mistake ? " (mistake)" : "")} {list[0].score:0.00} > {list[1].choice} {list[1].score:0.00} | "
                    + $"{hs}; {(anyVisible ? "enemy in sight; " : "")}{(inHisView ? "standing where he could see me; " : "")}{(coming ? "someone coming; " : "")}{(hit ? "just hit; " : "")}{(inCover ? "in cover; " : "")}{(runningToCover ? "running to cover; " : "")}{(coverTowardHim ? "cover is toward him; " : "")}"
                    + $"nearest threat {nearestPath:0}m / {nearestAge:0}s ago; meds {load.Meds}");
        }
        if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"heal.pick.{list[0].choice}");
        return list;
    }

    /// <summary>This enemy's footsteps / sprint heard within the last seconds, the sound within meters of the bot.</summary>
    public static bool HeardSteps(BotComponent bot, Enemy enemy, float seconds, float meters)
    {
        var hearing = enemy?.Hearing;
        return hearing != null && Time.time - hearing.LastHeardSoundTime < seconds
            && (hearing.LastHeardSoundType == SAINSoundType.FootStep || hearing.LastHeardSoundType == SAINSoundType.Sprint)
            && (hearing.LastHeardSoundPosition - bot.Position).sqrMagnitude < meters * meters;
    }

    public static void Clear()
    {
        _nextLog.Clear();
    }
}
