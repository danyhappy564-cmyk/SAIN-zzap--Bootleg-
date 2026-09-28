using System.Collections.Generic;
using SAIN.Components;
using SAIN.Models.Enums;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.SAINComponent.Classes.EnemyClasses;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork: reload timing by expected gain (user 2026-09-29). With a magazine under 80% in a fight:
///   Reload - the mag is low and it's a safe moment: he's out of sight or we're in cover, he's busy (out of sight);
///            not while he's looking at us in the open, not point-blank.
///   Hold   - keep shooting: rounds left and he's looking at us (a reload now is a free kill for him), he isn't looking
///            (free shots), he's reloading/healing in sight (his window, not ours to waste); never on an empty mag.
///   Pistol - nearly empty (10% or less) with him in sight within 30m and a loaded pistol: faster than any reload.
/// The quick (drop the mag) vs keep-the-mag reload choice is in QuickReloadPatch. [Reload] logs the scores and why.
/// </summary>
public static class ReloadUtility
{
    public enum EChoice
    {
        Reload,
        Hold,
        Pistol,
    }

    private static readonly Dictionary<string, float> _nextLog = new();

    public static bool Applies(Enemy enemy, float ammoRatio)
    {
        var settings = GlobalSettingsClass.Instance?.General?.CloseCombat;
        return settings != null && settings.UtilityReload && enemy != null && ammoRatio < 0.8f && !enemy.Events.OnSearch.Value;
    }

    public static List<(EChoice choice, float score)> Rank(BotComponent bot, Enemy enemy, float r, bool pistolReady)
    {
        bool visible = enemy.IsVisible;
        bool looking = visible && enemy.EnemyLookingAtMe;
        float dist = enemy.RealDistance;
        bool inCover = bot.Cover.CoverInUse != null;
        bool busy = enemy.Status.VulnerableAction != EEnemyAction.None;
        bool hit = bot.Medical.TimeSinceShot < 1.5f;
        float need = 1f - r;

        float reload = 0.15f + 0.7f * need * need + (inCover || !visible ? 0.25f : 0f) + (busy && !visible ? 0.15f : 0f)
            - (visible && !looking ? 0.1f : 0f) - (looking ? 0.35f : 0f) - (dist < 10f && visible ? 0.2f : 0f) - (hit && visible ? 0.15f : 0f);
        float hold = 0.2f + 0.4f * r + (looking ? 0.3f : 0f) + (busy && visible ? 0.25f : 0f) + (visible && !looking ? 0.2f : 0f)
            + (dist < 10f && visible ? 0.15f : 0f) - (r <= 0.02f ? 0.6f : r < 0.1f ? 0.25f : 0f);
        float pistol = pistolReady && visible && dist < 30f
            ? 0.2f + 0.6f * need + (dist < 15f ? 0.2f : 0f) + (looking ? 0.15f : 0f) - (r > 0.1f ? 0.6f : 0f)
            : 0f;

        var list = new List<(EChoice choice, float score)> { (EChoice.Reload, reload), (EChoice.Hold, hold), (EChoice.Pistol, pistol) };
        list.Sort((x, y) => y.score.CompareTo(x.score));

        string id = bot.ProfileId;
        if (!_nextLog.TryGetValue(id, out float next) || Time.time > next)
        {
            _nextLog[id] = Time.time + 3f;
            string why = $"mag {r:P0}; {(visible ? (looking ? "he's looking at me" : "he isn't looking") : "he's out of sight")}; "
                + $"{dist:0}m; {(inCover ? "in cover; " : "")}{(busy ? $"he's {enemy.Status.VulnerableAction}; " : "")}{(hit ? "just hit; " : "")}pistol {(pistolReady ? "ready" : "no")}";
            TacticDiagnostics.LogCloseCombat(
                $"[Reload] [{bot.name}] {list[0].choice} {list[0].score:0.00} > {list[1].choice} {list[1].score:0.00} > {list[2].choice} {list[2].score:0.00} | {why}");
        }
        TacticDiagnostics.Count($"reload.pick.{list[0].choice}");
        return list;
    }

    public static void Clear()
    {
        _nextLog.Clear();
    }
}
