using System.Collections.Generic;
using System.Text;
using EFT;
using SAIN.Components;
using SAIN.Models.Enums;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.Classes.WeaponFunction;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork: utility decision while the enemy is IN SIGHT (user 2026-09-29, second half of "which move pays off more
/// here"). Three stances scored by expected gain:
///   Shoot  - keep fighting from here (diamond step / lean): he isn't looking at us, he's weak or busy, our gun suits the
///            range, the duel just started; worse the longer we stand exposed while he looks at us, when hit, on an
///            empty mag with cover close by.
///   Cover  - break line of sight (SAIN seek cover): hurt, just got hit, mag nearly empty, exposed too long, cover close,
///            cautious personality; never a long run across the open close to him.
///   Push   - close in (SAIN rush -> dog fight): he's reloading/healing, weak, we outnumber him, he's close, aggressive
///            personality; not hurt, not low on ammo, not far.
/// Replaces SAIN's fixed "hold ground N seconds then cover" (that time is still used as the exposure clock).
/// Kept 2-3s (re-decided at once when hit). [UtilityV] logs the scores and reasons, counted as utilityV.*.
/// </summary>
public static class VisibleEnemyUtility
{
    public enum EStance
    {
        Shoot,
        Cover,
        Push,
    }

    private sealed class Memory
    {
        public List<(EStance stance, float score)> Ranked;
        public float Until;
        public string EnemyId;
        public EStance LastTop;
        public float NextLog;
        public float ScoredAt;
    }

    private static readonly Dictionary<string, Memory> _memory = new();

    private static readonly Dictionary<EPersonality, float> _aggression = new()
    {
        { EPersonality.GigaChad, 1f },
        { EPersonality.Wreckless, 1f },
        { EPersonality.Chad, 0.8f },
        { EPersonality.Normal, 0.5f },
        { EPersonality.Timmy, 0.4f },
        { EPersonality.SnappingTurtle, 0.3f },
        { EPersonality.Rat, 0.2f },
        { EPersonality.Coward, 0f },
    };

    public static List<(EStance stance, float score)> Rank(BotComponent bot, Enemy enemy, float holdGroundInterval)
    {
        var settings = GlobalSettingsClass.Instance?.General?.CloseCombat;
        if (settings == null || !settings.UtilityVisibleEnemy || enemy == null || !enemy.IsVisible || !enemy.CanShoot || enemy.IsZombie)
        {
            return null;
        }
        if (bot.BotOwner.WeaponManager?.HaveBullets == false || enemy.RealDistance > bot.Info.WeaponInfo.EffectiveWeaponDistance * 1.25f)
        {
            return null;
        }
        string id = bot.ProfileId;
        float time = Time.time;
        bool hit = bot.Medical.TimeSinceShot < 1.5f;
        if (_memory.TryGetValue(id, out Memory m) && m.EnemyId == enemy.EnemyProfileId && time < m.Until && !hit)
        {
            return m.Ranked;
        }
        var ranked = Score(bot, enemy, holdGroundInterval, hit, out string why);
        if (m == null)
        {
            m = new Memory();
            _memory[id] = m;
        }
        m.Ranked = ranked;
        m.EnemyId = enemy.EnemyProfileId;
        m.ScoredAt = time;
        // 2-3s (was 1-1.8s - user: longer so bots don't look like they flip back and forth; roll back if it feels sluggish).
        m.Until = time + Random.Range(2f, 3f);
        if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"utilityV.top.{ranked[0].stance}");
        if (ranked[0].stance != m.LastTop || time > m.NextLog)
        {
            m.LastTop = ranked[0].stance;
            m.NextLog = time + 4f;
            if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat(
                $"[UtilityV] [{bot.name}] [{bot.Info.Personality}] {enemy.EnemyPlayer?.Profile?.Nickname} in sight {enemy.RealDistance:0}m -> "
                    + $"{ranked[0].stance} {ranked[0].score:0.00} > {ranked[1].stance} {ranked[1].score:0.00} > {ranked[2].stance} {ranked[2].score:0.00} | {why}"
            );
        }
        return ranked;
    }

    private static List<(EStance stance, float score)> Score(BotComponent bot, Enemy enemy, float holdGroundInterval, bool hit, out string why)
    {
        var sb = new StringBuilder();
        var hs = bot.Memory.Health.HealthStatus;
        float health = hs switch
        {
            ETagStatus.Healthy => 1f,
            ETagStatus.Injured => 0.7f,
            ETagStatus.BadlyInjured => 0.35f,
            _ => 0.1f,
        };
        float ammo = SAINBotSuppressClass.CalcAmmoRatio(bot.BotOwner, out _);
        float weak = SquadStorm.Weakness(bot, enemy, out string weakWhy);
        bool busy = enemy.Status.VulnerableAction != EEnemyAction.None;
        bool looking = enemy.EnemyLookingAtMe;
        float lk = looking ? 1f : 0.3f;
        float visibleFor = Time.time - enemy.Vision.VisibleStartTime;
        float exposure = Mathf.Clamp01(visibleFor / Mathf.Max(holdGroundInterval, 0.5f));
        float dist = enemy.RealDistance;
        float cover = NearestCover(bot, enemy);
        float aggr = _aggression.TryGetValue(bot.Info.Personality, out float a) ? a : 0.5f;
        float numbers = Mathf.Clamp(MatesOnHim(bot, enemy), 0, 2) / 2f;
        // Loadout: how far this bot wants to fight (bolt/scoped sniper far, SMG/shotgun close) and how well it does up close.
        var load = LoadoutProfile.Of(bot);
        float ratio = dist / Mathf.Max(load.IdealRange, 1f);
        bool goodRange = ratio > 0.5f && ratio < 1.6f;
        bool tooClose = ratio < 0.35f && load.Long > 0.7f;
        bool tooFar = ratio > 2f && load.Cqb > 0.7f;

        // Running to cover in his sight = seconds of back turned and no shooting back (field 2026-09-29 bot-vs-bot sim: 17 of
        // 54 deaths were bots that switched Shoot -> Cover the moment they were hit, 3-8m from cover, and were shot in the
        // back on the way). A player only breaks off when cover is a step or two away; otherwise he trades, and he never
        // turns his back on someone who is nearly dead.
        float closeness = Mathf.Clamp01((4f - cover) / 3f); // 1 at <= 1m, 0 at >= 4m
        // 6th sim: 25 of 31 VCover deaths ran 4-11m with the back turned to an enemy 7-20m away who was looking at them
        // (K/D 0.19). The closer he is, the less of that run survives: cost per second of running doubles at <= 10m.
        float near = Mathf.Clamp01((30f - dist) / 20f); // 1 at <= 10m, 0 at >= 30m
        float runCost = cover > 12f ? 0f : Mathf.Clamp(cover / 3.5f, 0f, 3f) * (0.15f + 0.2f * near) * lk; // > 12m handled below
        bool finishHim = weak >= 0.45f && dist < 30f && ammo >= 0.15f;
        float shoot = 0.45f + (looking ? 0f : 0.25f) + 0.15f * weak + (busy ? 0.15f : 0f) + 0.1f * numbers + (goodRange ? 0.15f : 0f)
            - (tooClose ? 0.15f : 0f) - (tooFar ? 0.2f : 0f) - 0.3f * exposure * lk - (hit ? 0.2f * closeness : 0f) - (ammo < 0.15f ? (cover <= 8f ? 0.4f : 0.15f) : 0f) - 0.2f * (1f - health)
            + (finishHim ? 0.2f : 0f);
        // Hurt wants out of sight - but only as much as there is cover to get to (2nd sim: dying bots with no cover in 99m still
        // "ran for cover" = ran across the open and died).
        float reachable = Mathf.Clamp01((6f - cover) / 5f);
        float coverScore = 0.1f + 0.35f * (1f - health) * (0.4f + 0.6f * reachable) + (ammo < 0.25f ? 0.35f : 0f) + (hit ? 0.1f * reachable + 0.15f * closeness : 0f) + 0.3f * exposure * lk
            + 0.15f * (1f - aggr) + 0.2f * reachable - runCost - (cover > 12f ? (dist < 25f ? 0.7f : 0.35f) : 0f) - (looking ? 0f : 0.2f)
            + (tooClose ? 0.3f : 0f) + (tooFar ? 0.2f : 0f) - (finishHim ? 0.15f : 0f)
            // He's reloading/healing = he can't shoot back right now; that is the window to trade, not to turn around.
            - (busy && cover > 2f ? 0.25f : 0f);
        float push = 0.1f + 0.3f * weak + (busy ? 0.35f : 0f) + 0.2f * aggr + 0.15f * numbers + (dist < 12f ? 0.15f : 0f)
            - 0.3f * (1f - health) - (ammo < 0.4f ? 0.3f : 0f) - (dist > 30f ? 0.25f : 0f) - (looking && !busy ? 0.1f : 0f)
            + 0.15f * load.Cqb - 0.25f * load.Long - (tooClose ? 0.3f : 0f);

        float fear = FearModel.Fear(bot, enemy, health, false, out string fearWhy);
        push -= 0.4f * fear;
        shoot -= 0.1f * fear;
        coverScore += 0.4f * fear + (fear > 0.75f ? 0.3f : 0f);
        if (fearWhy.Length > 0) sb.Append(fearWhy).Append("; ");
        if (!looking) sb.Append("he isn't looking at me; ");
        if (busy) sb.Append($"he's {enemy.Status.VulnerableAction}; ");
        if (weak >= 0.3f) sb.Append($"he's weak ({weakWhy}); ");
        if (exposure > 0.6f && looking) sb.Append($"exposed {visibleFor:0.0}s while he looks at me; ");
        if (hit) sb.Append("just got hit; ");
        if (health < 1f) sb.Append($"I'm {hs}; ");
        if (ammo < 0.25f) sb.Append($"mag {ammo:P0}; ");
        if (cover > 12f && dist < 25f) sb.Append($"cover {cover:0}m away - too far to run; ");
        else if (runCost > 0.1f) sb.Append($"cover {cover:0.0}m - {cover / 3.5f:0.0}s back turned to get there; ");
        else if (cover <= 6f) sb.Append($"cover {cover:0.0}m; ");
        if (finishHim) sb.Append("he's nearly done - finish him; ");
        if (tooClose) sb.Append($"too close for my long gun (ideal {load.IdealRange:0}m) - break off; ");
        if (tooFar) sb.Append($"too far for my close-range gun (ideal {load.IdealRange:0}m); ");
        if (goodRange) sb.Append("my gun's range; ");
        sb.Append($"[{load.Summary}] ");
        if (numbers > 0f) sb.Append("mates on him; ");
        sb.Append($"{dist:0}m, {bot.Info.Personality}");
        why = sb.ToString();

        var list = new List<(EStance stance, float score)>
        {
            (EStance.Shoot, shoot),
            (EStance.Cover, coverScore),
            (EStance.Push, push),
        };
        for (int i = 0; i < list.Count; i++)
        {
            float bonus = SAIN.Components.BotControllerSpace.Classes.PlayerOutcomeLearner.Bonus(bot, enemy,
                SAIN.Components.BotControllerSpace.Classes.PlayerOutcomeLearner.Key("V", list[i].stance.ToString(), dist, bot));
            if (Mathf.Abs(bonus) > 0.02f)
            {
                list[i] = (list[i].stance, list[i].score + bonus);
                why += $" | learned {list[i].stance} {bonus:+0.00;-0.00}";
            }
        }
        list.Sort((x, y) => y.score.CompareTo(x.score));
        if (UtilityMistake.Apply(bot, list, "visible"))
        {
            why += " | (mistake)";
        }
        return list;
    }

    /// <summary>
    /// Nearest cover point that actually blocks THIS enemy (line from his head to the point at hip height hits something).
    /// SAIN's list is built against the goal enemy's last known spot; a point 0.2m away "counted" as cover while this enemy
    /// was shooting the bot standing on it (5th sim: 12 deaths "in cover", standing still, enemy in sight).
    /// </summary>
    private static float NearestCover(BotComponent bot, Enemy enemy)
    {
        float nearest = 99f;
        var points = bot.Cover.CoverPoints;
        if (points == null)
        {
            return nearest;
        }
        Vector3 pos = bot.Position;
        Vector3 eye = enemy.EnemyPosition + Vector3.up * 1.5f;
        foreach (var p in points)
        {
            if (p == null)
            {
                continue;
            }
            float d = (p.Position - pos).magnitude;
            if (d >= nearest)
            {
                continue;
            }
            if (!Physics.Linecast(eye, p.Position + Vector3.up * 1.0f, LayersMaskController.HighPolyWithTerrainMask))
            {
                continue; // he can see that spot - not cover from him
            }
            nearest = d;
        }
        return nearest;
    }

    private static int MatesOnHim(BotComponent bot, Enemy enemy)
    {
        int n = 0;
        var members = bot.Squad?.Members;
        if (members == null)
        {
            return 0;
        }
        foreach (var m in members.Values)
        {
            if (m == null || ReferenceEquals(m, bot) || m.IsDead)
            {
                continue;
            }
            var goal = m.GoalEnemy;
            if (goal != null && goal.EnemyProfileId == enemy.EnemyProfileId && goal.IsVisible)
            {
                n++;
            }
        }
        return n;
    }

    /// <summary>
    /// zzap (10/2 14:38 sim): the last in-sight pick for this enemy was Cover, within <paramref name="within"/> seconds.
    /// The hidden-enemy utility uses it so "just lost sight" momentum doesn't send the bot straight back at him (one bot
    /// flipped Push (hidden) / Cover (in sight) every half second, walking from 24m into 9m of the player).
    /// </summary>
    public static bool PickedCoverRecently(BotComponent bot, Enemy enemy, float within)
    {
        return _memory.TryGetValue(bot.ProfileId, out Memory m) && m.EnemyId == enemy.EnemyProfileId && m.Ranked != null
            && m.Ranked[0].stance == EStance.Cover && Time.time - m.ScoredAt < within;
    }

    public static void Clear()
    {
        _memory.Clear();
    }
}
