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
/// Kept 1-1.8s (re-decided at once when hit). [UtilityV] logs the scores and reasons, counted as utilityV.*.
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
        m.Until = time + Random.Range(1f, 1.8f);
        TacticDiagnostics.Count($"utilityV.top.{ranked[0].stance}");
        if (ranked[0].stance != m.LastTop || time > m.NextLog)
        {
            m.LastTop = ranked[0].stance;
            m.NextLog = time + 4f;
            TacticDiagnostics.LogCloseCombat(
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
        float cover = NearestCover(bot);
        float aggr = _aggression.TryGetValue(bot.Info.Personality, out float a) ? a : 0.5f;
        float numbers = Mathf.Clamp(MatesOnHim(bot, enemy), 0, 2) / 2f;
        float fit = WeaponFit(bot, dist);

        float shoot = 0.45f + (looking ? 0f : 0.25f) + 0.15f * weak + (busy ? 0.15f : 0f) + 0.1f * numbers + 0.1f * fit
            - 0.3f * exposure * lk - (hit ? 0.2f : 0f) - (ammo < 0.15f ? (cover <= 8f ? 0.4f : 0.15f) : 0f) - 0.2f * (1f - health);
        float coverScore = 0.1f + 0.35f * (1f - health) + (ammo < 0.25f ? 0.35f : 0f) + (hit ? 0.25f : 0f) + 0.3f * exposure * lk
            + 0.15f * (1f - aggr) + (cover <= 6f ? 0.15f : 0f) - (cover > 12f && dist < 25f ? 0.35f : 0f) - (looking ? 0f : 0.2f);
        float push = 0.1f + 0.3f * weak + (busy ? 0.35f : 0f) + 0.2f * aggr + 0.15f * numbers + (dist < 12f ? 0.15f : 0f)
            - 0.3f * (1f - health) - (ammo < 0.4f ? 0.3f : 0f) - (dist > 30f ? 0.25f : 0f) - (looking && !busy ? 0.1f : 0f);

        if (!looking) sb.Append("he isn't looking at me; ");
        if (busy) sb.Append($"he's {enemy.Status.VulnerableAction}; ");
        if (weak >= 0.3f) sb.Append($"he's weak ({weakWhy}); ");
        if (exposure > 0.6f && looking) sb.Append($"exposed {visibleFor:0.0}s while he looks at me; ");
        if (hit) sb.Append("just got hit; ");
        if (health < 1f) sb.Append($"I'm {hs}; ");
        if (ammo < 0.25f) sb.Append($"mag {ammo:P0}; ");
        if (cover > 12f && dist < 25f) sb.Append($"cover {cover:0}m away - too far to run; ");
        else if (cover <= 6f) sb.Append($"cover {cover:0.0}m; ");
        if (fit < 0.5f) sb.Append("my gun doesn't suit this range; ");
        if (numbers > 0f) sb.Append("mates on him; ");
        sb.Append($"{dist:0}m, {bot.Info.Personality}");
        why = sb.ToString();

        var list = new List<(EStance stance, float score)>
        {
            (EStance.Shoot, shoot),
            (EStance.Cover, coverScore),
            (EStance.Push, push),
        };
        list.Sort((x, y) => y.score.CompareTo(x.score));
        return list;
    }

    private static float NearestCover(BotComponent bot)
    {
        float nearest = 99f;
        var points = bot.Cover.CoverPoints;
        if (points == null)
        {
            return nearest;
        }
        Vector3 pos = bot.Position;
        foreach (var p in points)
        {
            if (p == null)
            {
                continue;
            }
            float d = (p.Position - pos).magnitude;
            if (d < nearest)
            {
                nearest = d;
            }
        }
        return nearest;
    }

    private static float WeaponFit(BotComponent bot, float dist)
    {
        var info = bot.PlayerComponent?.Equipment?.CurrentWeaponInfo;
        if (info == null)
        {
            return 0.5f;
        }
        switch (info.WeaponClass)
        {
            case EWeaponClass.shotgun:
            case EWeaponClass.pistol:
                return dist > 30f ? 0f : dist < 15f ? 1f : 0.5f;
            case EWeaponClass.smg:
                return dist > 45f ? 0.2f : 1f;
            case EWeaponClass.sniperRifle:
            case EWeaponClass.marksmanRifle:
                return dist < 10f ? 0.2f : 1f;
            default:
                return 1f;
        }
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

    public static void Clear()
    {
        _memory.Clear();
    }
}
