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
        bool inCover = bot.Cover.CoverInUse != null;
        bool hit = bot.Medical.TimeSinceShot < 2f;
        bool anyVisible = false;
        bool coming = false;
        float nearestAge = 999f;
        float nearestPath = 999f;
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
            }
            var hearing = enemy.Hearing;
            if (hearing != null && time - hearing.LastHeardSoundTime < 3f
                && (hearing.LastHeardSoundType == SAINSoundType.FootStep || hearing.LastHeardSoundType == SAINSoundType.Sprint)
                && (hearing.LastHeardSoundPosition - bot.Position).magnitude < 25f)
            {
                coming = true;
            }
        }
        float safe = Mathf.Clamp01((nearestAge - 3f) / 15f) * 0.5f + Mathf.Clamp01((nearestPath - 10f) / 30f) * 0.5f;

        // Plenty of meds -> heal more readily; the last one is worth saving for something serious.
        var load = LoadoutProfile.Of(bot);
        float medBias = load.Meds >= 3 ? 0.1f : load.Meds <= 1 && health > 0.35f ? -0.1f : 0f;
        float firstAid = canFirstAid
            ? medBias + 0.2f + 0.5f * (1f - health) + (inCover ? 0.2f : 0f) + 0.3f * safe - (anyVisible ? 0.6f : 0f) - (coming ? 0.35f : 0f) - (hit ? 0.3f : 0f)
            : -1f;
        float stim = canStim && health <= 0.35f
            ? 0.3f + 0.4f * (1f - health) + (anyVisible ? 0.1f : 0f) - (bot.Medical.TimeSinceShot < 0.5f ? 0.2f : 0f)
            : -1f;
        float surgery = canSurgery
            ? 0.1f + 0.4f * safe + (inCover ? 0.15f : 0f) - (anyVisible ? 0.8f : 0f) - (coming ? 0.5f : 0f) - (nearestAge < 10f ? 0.3f : 0f)
            : -1f;
        // Close and recent = he can be on us before the meds are done (sim 4: 6m / 6s ago counted as safe). Ties go to waiting.
        float closeRecent = Mathf.Clamp01((30f - nearestPath) / 25f) * Mathf.Clamp01((15f - nearestAge) / 10f);
        float wait = 0.31f + 0.3f * closeRecent + (coming ? 0.15f : 0f) + (anyVisible ? 0.2f : 0f);

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
            TacticDiagnostics.LogCloseCombat(
                $"[Heal] [{bot.name}] {list[0].choice}{(mistake ? " (mistake)" : "")} {list[0].score:0.00} > {list[1].choice} {list[1].score:0.00} | "
                    + $"{hs}; {(anyVisible ? "enemy in sight; " : "")}{(coming ? "someone coming; " : "")}{(hit ? "just hit; " : "")}{(inCover ? "in cover; " : "")}"
                    + $"nearest threat {nearestPath:0}m / {nearestAge:0}s ago; meds {load.Meds}");
        }
        TacticDiagnostics.Count($"heal.pick.{list[0].choice}");
        return list;
    }

    public static void Clear()
    {
        _nextLog.Clear();
    }
}
