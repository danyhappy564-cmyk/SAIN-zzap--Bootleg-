using System.Collections.Generic;
using System.Text;
using EFT;
using SAIN.Components;
using SAIN.Models.Enums;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.GlobalSettings.Categories.General;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.Classes.WeaponFunction;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork: utility decision for an enemy the bot knows about but can't see (user 2026-09-29: "compute which move pays
/// off more in this situation"). Every candidate stance gets an expected-gain score from the same situation features:
///   information (how fresh, heard), angles (is he holding one on us, is he coming), resources (health, magazine, grenade),
///   numbers (mates on him vs his friends near him), his weaknesses (gear, busy healing/reloading), time (a camping enemy
///   wins by the clock), distance, personality.
/// Stances: Push (rush / clear the room), Grenade, Hold (freeze on the angle he must come through), Flank (squad angle /
/// shift cover), Search (info is old - go find him), FallBack (to cover). EnemyDecisionClass tries them best-first and
/// maps each onto SAIN's existing actions; if none can run, the old decision chain decides as before.
/// A stance is kept for a few seconds (no flip-flopping) unless the enemy shows up or the bot gets shot.
/// Logged as [Utility] with the scores and the reasons; counted as utility.*.
/// </summary>
public static class HiddenEnemyUtility
{
    public enum EStance
    {
        Push,
        Grenade,
        Hold,
        Flank,
        Search,
        FallBack,
    }

    private sealed class Memory
    {
        public List<(EStance stance, float score)> Ranked;
        public float Until;
        public string EnemyId;
        public float HoldSince = -1f;
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

    // How freely a personality takes the second-best option when the scores are close (unpredictability).
    private static readonly Dictionary<EPersonality, float> _mixMargin = new()
    {
        { EPersonality.Wreckless, 0.2f },
        { EPersonality.GigaChad, 0.15f },
        { EPersonality.Chad, 0.12f },
        { EPersonality.Normal, 0.1f },
        { EPersonality.Timmy, 0.1f },
        { EPersonality.SnappingTurtle, 0.06f },
        { EPersonality.Rat, 0.05f },
        { EPersonality.Coward, 0.05f },
    };

    public static void Forget(BotComponent bot)
    {
        _memory.Remove(bot.ProfileId);
    }

    /// <summary>Stances best-first (kept for a few seconds once chosen). Null when the utility doesn't apply.</summary>
    public static List<(EStance stance, float score)> Rank(BotComponent bot, Enemy enemy)
    {
        var settings = GlobalSettingsClass.Instance?.General?.CloseCombat;
        if (settings == null || !settings.UtilityHiddenEnemy || enemy == null || enemy.IsVisible || !(enemy.Seen || enemy.Heard))
        {
            return null;
        }
        if (enemy.TimeSinceLastKnownUpdated > 60f || enemy.KnownPlaces.LastKnownPosition == null)
        {
            return null;
        }
        string id = bot.ProfileId;
        float time = Time.time;
        if (_memory.TryGetValue(id, out Memory m) && m.EnemyId == enemy.EnemyProfileId && time < m.Until && !bot.BotOwner.Memory.IsUnderFire)
        {
            return m.Ranked;
        }
        // How long the bot has already been holding against this enemy (hold fatigue, see Score).
        float heldFor = m != null && m.EnemyId == enemy.EnemyProfileId && m.HoldSince > 0f ? time - m.HoldSince : 0f;
        var ranked = Score(bot, enemy, settings, heldFor, out string why, out float hold);
        float holdSince = ranked[0].stance == EStance.Hold ? (heldFor > 0f ? m.HoldSince : time) : -1f;
        // Under fire it re-scores every tick (15:43 sim: the same line 9 times in 1s) - log on a change of pick or every 2s.
        bool sameTop = m != null && m.EnemyId == enemy.EnemyProfileId && m.LastTop == ranked[0].stance && time < m.NextLog;
        _memory[id] = new Memory
        {
            Ranked = ranked, Until = time + hold, EnemyId = enemy.EnemyProfileId, HoldSince = holdSince,
            LastTop = ranked[0].stance, NextLog = sameTop ? m.NextLog : time + 2f,
        };
        if (TacticDiagnostics.CountOn && !sameTop) TacticDiagnostics.Count($"utility.top.{ranked[0].stance}");
        if (TacticDiagnostics.LogOn && !sameTop) TacticDiagnostics.LogCloseCombat(
            $"[Utility] [{bot.name}] [{bot.Info.Personality}] enemy {enemy.EnemyPlayer?.Profile?.Nickname} hidden -> "
                + $"{ranked[0].stance} {ranked[0].score:0.00} > {ranked[1].stance} {ranked[1].score:0.00} > {ranked[2].stance} {ranked[2].score:0.00} | {why}"
        );
        return ranked;
    }

    private static List<(EStance stance, float score)> Score(BotComponent bot, Enemy enemy, CloseCombatSettings settings, float heldFor, out string why, out float commit)
    {
        var sb = new StringBuilder();
        float time = Time.time;
        var healthStatus = bot.Memory.Health.HealthStatus;
        float health = healthStatus switch
        {
            ETagStatus.Healthy => 1f,
            ETagStatus.Injured => 0.7f,
            ETagStatus.BadlyInjured => 0.35f,
            _ => 0.1f,
        };
        float ammo = SAINBotSuppressClass.CalcAmmoRatio(bot.BotOwner, out _);
        float weak = SquadStorm.Weakness(bot, enemy, out string weakWhy);
        bool busy = enemy.Status.VulnerableAction != EEnemyAction.None;
        float infoAge = enemy.TimeSinceLastKnownUpdated;
        float quiet = Mathf.Clamp01((infoAge - 8f) / 12f);
        float lastShot = enemy.Status.TimeLastShotAtMe;
        bool holdsAngle = lastShot > 0f && time - lastShot < 5f;
        var hearing = enemy.Hearing;
        Vector3 known = enemy.KnownPlaces.LastKnownPosition.Value;
        bool coming = hearing != null && time - hearing.LastHeardSoundTime < 3f
            && (hearing.LastHeardSoundType == SAINSoundType.FootStep || hearing.LastHeardSoundType == SAINSoundType.Sprint)
            && (hearing.LastHeardSoundPosition - bot.Position).magnitude < (known - bot.Position).magnitude + 1f;
        float numbers = Mathf.Clamp(MatesOnHim(bot, enemy) - FriendsNearHim(bot, enemy), -2, 2) / 2f;
        float path = enemy.Path.PathLength;
        float dist = Mathf.Clamp01((path - 5f) / 35f);
        float info = Mathf.Clamp01(infoAge / 30f);
        float aggr = _aggression.TryGetValue(bot.Info.Personality, out float a) ? a : 0.5f;
        bool inCover = bot.Cover.CoverInUse != null;
        bool indoor = enemy.EnemyPlayer?.Environment == EnvironmentType.Indoor;
        bool nadeReady = bot.BotOwner.WeaponManager?.Grenades?.HaveGrenade == true && path >= settings.GrenadeMinRushDistance
            && enemy.KnownPlaces.BotDistanceFromLastKnown > 8f && enemy.KnownPlaces.BotDistanceFromLastKnown < 45f
            && infoAge <= settings.GrenadeMaxInfoAge && healthStatus != ETagStatus.BadlyInjured && healthStatus != ETagStatus.Dying;
        // Just broke line of sight and he isn't holding an angle: momentum - follow him while he's still moving.
        // ...unless we were the ones breaking off: in sight we just picked Cover, so following him now undoes it.
        bool brokeOff = VisibleEnemyUtility.PickedCoverRecently(bot, enemy, 4f);
        bool justLost = infoAge < 3f && !holdsAngle && !brokeOff;
        bool pushyPlayer = SAIN.Components.BotControllerSpace.Classes.PlayerAdaptation.AgainstPlayer(enemy)
            && SAIN.Components.BotControllerSpace.Classes.PlayerAdaptation.Aggression > 0.6f;

        var load = LoadoutProfile.Of(bot);
        // A long gun wants distance: an enemy closing inside 40% of its ideal range is a reason to back off.
        bool closingOnLongGun = load.Long > 0.7f && path < load.IdealRange * 0.4f;
        float push = 0.15f * load.Cqb - 0.3f * load.Long + 0.25f + 0.3f * weak + (busy ? 0.25f : 0f) + 0.15f * numbers + 0.15f * aggr + 0.1f * quiet
            - (holdsAngle ? 0.35f : 0f) + (justLost ? 0.15f : 0f) - (brokeOff ? 0.2f : 0f) - 0.35f * (1f - health) - (ammo < 0.5f ? 0.3f : 0f) - 0.15f * dist;
        float nade = nadeReady
            ? 0.3f + 0.05f * Mathf.Min(load.Grenades, 3) + (load.Grenades >= 3 ? 0.1f : 0f) + 0.4f * quiet + (busy ? 0.15f : 0f) + (indoor ? 0.1f : 0f) + (holdsAngle ? 0.1f : 0f) - 0.25f * info
            : 0f;
        float hold = 0.2f * load.Long + 0.25f + (coming ? 0.35f : 0f) + 0.25f * (1f - health) + (holdsAngle ? 0.1f : 0f) + 0.15f * (1f - aggr) - (justLost ? 0.05f : 0f)
            + (inCover ? 0.1f : 0f) + (pushyPlayer ? 0.15f : 0f) - 0.3f * quiet;
        float flank = 0.2f + (holdsAngle ? 0.3f : 0f) + 0.15f * Mathf.Max(0f, numbers) + 0.1f * quiet - 0.2f * (1f - health);
        float search = -0.15f * load.Long + 0.15f + 0.35f * info + 0.1f * aggr - 0.2f * (1f - health);
        float fallBack = (closingOnLongGun ? 0.35f : 0f) + 0.05f + 0.45f * (1f - health) + (ammo < 0.25f ? 0.3f : 0f) + (numbers < 0f ? 0.25f * -numbers : 0f);

        // Fear (personality + mates dying, the player's streak, hurt, outnumbered, pinned): toward falling back / holding.
        float fear = FearModel.Fear(bot, enemy, health, numbers < 0f, out string fearWhy);
        push -= 0.4f * fear;
        flank -= 0.1f * fear;
        hold += 0.2f * fear;
        fallBack += 0.5f * fear + (fear > 0.75f ? 0.4f : 0f);
        // Hold fatigue: an ambush pays off in the first seconds; holding on and on with nothing coming just hands him the
        // clock (2nd sim 2026-09-29: bots camping for no reason - Hold was the pick ~70% of the time).
        float fatigue = Mathf.Clamp((heldFor - 10f) * 0.03f, 0f, 0.45f);
        // Holding an angle is done from cover. Frozen in the open (nothing within 2.5m on 3+ sides and nothing between us and
        // where he was) = first to be seen when he re-peeks (5th sim: Hold 3 kills / 25 deaths, mostly right after choosing it).
        bool openHold = !inCover && OpenGround.IsOpen(bot.Position, known);
        if (openHold)
        {
            hold -= 0.3f;
        }
        if (!coming)
        {
            hold -= fatigue;
        }
        // Too far to ambush (sims 2026-10-10: Hold = Freeze was 22-34% of combat time, mostly short 2-5 s holds while the
        // enemy was 50-130 m off - Shoreline 86% of freeze time beyond 50 m). A freeze ambush waits for him at a close angle
        // (the old chain only freezes within Freeze Ambush > Max Enemy Distance, 45 m); the utility had no distance at all.
        // Fades in from that distance to twice it; a long gun (DMR / sniper) keeps most of its overwatch.
        float freezeMax = GlobalSettingsClass.Instance?.General?.FreezeAmbush?.MaxDistance ?? 45f;
        float knownDist = enemy.KnownPlaces.BotDistanceFromLastKnown;
        float farHold = 0.35f * Mathf.Clamp01((knownDist - freezeMax) / freezeMax) * (1f - 0.6f * load.Long);
        hold -= farHold;
        if (fearWhy.Length > 0) sb.Append(fearWhy).Append("; ");
        if (fatigue > 0f && !coming) sb.Append($"held {heldFor:0}s already, nothing came; ");
        if (openHold) sb.Append("standing in the open - no place to hold from; ");
        if (farHold > 0.05f) sb.Append($"he's {knownDist:0}m off - too far to ambush; ");
        if (weak >= 0.3f) sb.Append($"he's weak ({weakWhy}); ");
        if (busy) sb.Append($"he's {enemy.Status.VulnerableAction}; ");
        if (holdsAngle) sb.Append("he's holding an angle on us; ");
        if (justLost) sb.Append("just lost sight (momentum); ");
        if (brokeOff)
        {
            sb.Append("I just broke off to cover - not running back at him; ");
            if (TacticDiagnostics.CountOn) TacticDiagnostics.Count("utility.brokeOff");
        }
        if (coming) sb.Append("he's coming our way; ");
        if (quiet > 0.3f) sb.Append($"he's been quiet {infoAge:0}s (clock favors him); ");
        if (info > 0.5f) sb.Append($"info {infoAge:0}s old; ");
        if (health < 1f) sb.Append($"I'm {healthStatus}; ");
        if (ammo < 0.5f) sb.Append($"mag {ammo:P0}; ");
        if (numbers > 0f) sb.Append("we outnumber him; ");
        if (numbers < 0f) sb.Append("he has friends near; ");
        if (pushyPlayer) sb.Append("this player pushes; ");
        if (closingOnLongGun) sb.Append($"he's closing on my long gun (ideal {load.IdealRange:0}m) - keep distance; ");
        sb.Append(path < 10000f ? $"path {path:0}m, " : "no path yet, ").Append($"{bot.Info.Personality} [{load.Summary}]");
        why = sb.ToString();

        var list = new List<(EStance stance, float score)>
        {
            (EStance.Push, push),
            (EStance.Grenade, nade),
            (EStance.Hold, hold),
            (EStance.Flank, flank),
            (EStance.Search, search),
            (EStance.FallBack, fallBack),
        };
        // Learned against this player (outcomes of past responses in this kind of situation), when adaptation is on.
        for (int i = 0; i < list.Count; i++)
        {
            float bonus = SAIN.Components.BotControllerSpace.Classes.PlayerOutcomeLearner.Bonus(bot, enemy,
                SAIN.Components.BotControllerSpace.Classes.PlayerOutcomeLearner.Key("H", list[i].stance.ToString(), path, bot));
            if (Mathf.Abs(bonus) > 0.02f)
            {
                list[i] = (list[i].stance, list[i].score + bonus);
                why += $" | learned {list[i].stance} {bonus:+0.00;-0.00}";
            }
        }
        list.Sort((x, y) => y.Item2.CompareTo(x.Item2));
        // Weighted dice over the top 3 (user 2026-09-29: "plain probability may have felt better" - scores alone made every
        // bot do the same thing): p ~ exp((score - best) / T), T per personality (Wreckless loose ... Rat/Coward tight) x the
        // F6 mix setting. A much worse option is still very unlikely; close ones really get mixed.
        // x0.6: at the raw personality values the 3rd sim took a lower-ranked option 37% of the time - too random.
        float temp = (_mixMargin.TryGetValue(bot.Info.Personality, out float mm) ? mm : 0.1f) * settings.UtilityMixMargin * 0.6f;
        if (temp > 0.001f)
        {
            int n = Mathf.Min(3, list.Count);
            float[] w = new float[n];
            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                w[i] = list[i].Item2 > 0.05f || i == 0 ? Mathf.Exp((list[i].Item2 - list[0].Item2) / temp) : 0f;
                sum += w[i];
            }
            float roll = Random.value * sum;
            int pick = 0;
            for (int i = 0; i < n; i++)
            {
                roll -= w[i];
                if (roll <= 0f)
                {
                    pick = i;
                    break;
                }
            }
            if (pick > 0)
            {
                var chosen = list[pick];
                list.RemoveAt(pick);
                list.Insert(0, chosen);
                why += $" | dice: took #{pick + 1} (p {w[pick] / sum:0.00})";
                if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"utility.dice.{pick + 1}");
            }
        }
        if (UtilityMistake.Apply(bot, list, "hidden"))
        {
            why += " | (mistake)";
        }
        commit = list[0].Item1 == EStance.Hold ? Random.Range(4f, 7f) : Random.Range(2.5f, 4f);
        return list;
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
            if (m == null || ReferenceEquals(m, bot) || m.IsDead || (m.Position - bot.Position).sqrMagnitude > 30f * 30f)
            {
                continue;
            }
            var goal = m.GoalEnemy;
            if (goal != null && goal.EnemyProfileId == enemy.EnemyProfileId)
            {
                n++;
            }
        }
        return n;
    }

    private static int FriendsNearHim(BotComponent bot, Enemy enemy)
    {
        int n = 0;
        var known = bot.EnemyController?.KnownEnemies;
        if (known == null)
        {
            return 0;
        }
        Vector3 pos = enemy.EnemyPosition;
        foreach (Enemy other in known)
        {
            if (other != null && !ReferenceEquals(other, enemy) && other.TimeSinceLastKnownUpdated < 20f
                && (other.EnemyPosition - pos).sqrMagnitude < 20f * 20f)
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
