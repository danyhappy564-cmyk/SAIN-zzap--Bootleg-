using System.Collections.Generic;
using EFT;
using SAIN.Components;
using SAIN.Models.Enums;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SAIN.SAINComponent.Classes.EnemyClasses;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap remake5 (user 2026-10-11: "bot vs bot they hardly rush a reloading / healing enemy - why?" → "make them act on what the
/// sound means"). SAIN rushed a busy enemy only when the path to him was under 10 m (20 m for surgery) - bots fight at 15-40 m,
/// so the rule almost never fired between bots. Now the sound's meaning decides:
///   reload / dry fire  ~2.5 s left   rush if he can be reached before it ends (sprint ~5 m/s), else stay on the angle
///   heal               ~5 s           rush from further
///   surgery            ~10 s          further still
///   looting            ~10 s          rush, or a quiet approach (FootstepDiscipline)
///   grenade out / pin  he throws soon never rush - SAIN's utilities lean to cover instead (Hidden/VisibleEnemyUtility)
/// The window is scaled by personality (GigaChad/Wreckless take more risk), reduced by the time since the sound, and nothing
/// starts while the bot is being shot or another enemy is in sight. Results go to counters vuln.* and [Vuln] journal lines:
///   vuln.heard.{action} → vuln.reach.{action}.{yes|no} → vuln.rush.{action}.start → arrivedBusy / arrivedLate / died / killed
/// </summary>
public static class VulnerableRush
{
    private const float SPRINT_SPEED = 5f;
    private const float MAX_RUSH_DISTANCE = 40f;
    private const float RECORD_TIME = 12f;

    private sealed class Record
    {
        public string EnemyId;
        public EEnemyAction Action;
        public float Start;
        public float Distance;
        public bool Arrived;
    }

    private static readonly Dictionary<string, Record> _rushes = new();
    private static readonly Dictionary<string, float> _heardBooked = new();
    private static readonly Dictionary<string, float> _noBooked = new();

    public static float Window(EEnemyAction action)
    {
        return action switch
        {
            EEnemyAction.Reloading => 2.5f,
            EEnemyAction.Healing => 5f,
            EEnemyAction.UsingSurgery => 10f,
            EEnemyAction.Looting => 10f,
            _ => 0f,
        };
    }

    private static float Nerve(EPersonality p)
    {
        return p switch
        {
            EPersonality.Wreckless => 1.4f,
            EPersonality.GigaChad => 1.3f,
            EPersonality.Chad => 1.15f,
            EPersonality.Normal => 1f,
            _ => 0.85f,
        };
    }

    /// <summary>EnemyHearing heard a reload / heal / grenade / loot sound from this enemy.</summary>
    public static void OnHeard(BotComponent bot, Enemy enemy, EEnemyAction action, Vector3 at)
    {
        if (bot == null || enemy == null || !TacticDiagnostics.CountOn)
        {
            return;
        }
        // one count per bot / enemy / action episode (the same sound is reported again by squadmates and repeats)
        string key = bot.ProfileId + enemy.EnemyProfileId + (int)action;
        float time = Time.time;
        if (_heardBooked.TryGetValue(key, out float last) && time - last < 4f)
        {
            return;
        }
        _heardBooked[key] = time;
        if (_heardBooked.Count > 512)
        {
            _heardBooked.Clear();
        }
        TacticDiagnostics.Count($"vuln.heard.{action}");
        if (action == EEnemyAction.HasGrenade)
        {
            TacticDiagnostics.Count("vuln.grenade.backOff");
        }
        if (TacticDiagnostics.LogOn)
        {
            TacticDiagnostics.LogCloseCombat($"[Vuln] [{bot.name}] [{bot.Info.Personality}] heard {enemy.EnemyName} {action} {(at - bot.Position).magnitude:0}m away{(enemy.IsAI ? "" : " (player)")}");
        }
    }

    /// <summary>
    /// EnemyDecisionClass.shallRushEnemy: can this bot get to the busy enemy before the busy moment ends? inRange = SAIN's old
    /// rule (path under 10 m / 20 m surgery) already says yes. Grenade out never qualifies.
    /// </summary>
    public static bool CanReach(BotComponent bot, Enemy enemy, EEnemyAction action, bool inRange, out string why)
    {
        why = string.Empty;
        if (action == EEnemyAction.HasGrenade)
        {
            why = "enemyHasGrenade";
            return false;
        }
        float window = Window(action);
        if (window <= 0f)
        {
            why = "notBusy";
            return false;
        }
        float path = enemy.Path.PathLength;
        // time of the busy sound itself; a squadmate who only got the shared status falls back to his last heard sound
        var hearing = enemy.Hearing;
        float heardAt = hearing == null ? Time.time : hearing.LastVulnerableSoundTime > 0f ? hearing.LastVulnerableSoundTime : hearing.LastHeardSoundTime;
        float heardAgo = Time.time - heardAt;
        float left = window * Nerve(bot.Info.Personality) - Mathf.Max(0f, heardAgo);
        float need = path / SPRINT_SPEED;
        bool underFire = bot.BotOwner?.Memory?.IsUnderFire == true;
        bool otherInSight = false;
        var known = bot.EnemyController.KnownEnemies;
        for (int i = 0; i < known.Count; i++)
        {
            var other = known[i];
            if (other != null && !ReferenceEquals(other, enemy) && other.IsVisible)
            {
                otherInSight = true;
                break;
            }
        }
        bool yes = inRange || (!underFire && !otherInSight && path <= MAX_RUSH_DISTANCE && need <= left);
        // constant strings: this runs every decision tick while an enemy is busy (EFT keeps the GC off in raids)
        why = inRange ? "close" : underFire ? "underFire" : otherInSight ? "otherEnemyInSight" : path > MAX_RUSH_DISTANCE ? "tooFar" : need > left ? "tooSlow" : "inTime";
        return yes;
    }

    /// <summary>Booked once per decision that picked the rush (the decision repeats every 0.1 s while it holds).</summary>
    public static void OnDecision(BotComponent bot, Enemy enemy, EEnemyAction action, bool rush, string why)
    {
        if (!TacticDiagnostics.CountOn || bot == null || enemy == null)
        {
            return;
        }
        string id = bot.ProfileId;
        if (rush)
        {
            if (_rushes.TryGetValue(id, out var r) && r.EnemyId == enemy.EnemyProfileId && Time.time - r.Start < RECORD_TIME)
            {
                return;
            }
            _rushes[id] = new Record { EnemyId = enemy.EnemyProfileId, Action = action, Start = Time.time, Distance = enemy.Path.PathLength };
            TacticDiagnostics.Count($"vuln.reach.{action}.yes");
            TacticDiagnostics.Count($"vuln.rush.{action}.start");
            if (TacticDiagnostics.LogOn)
            {
                TacticDiagnostics.LogCloseCombat($"[Vuln] [{bot.name}] rush {enemy.EnemyName} ({action}, path {enemy.Path.PathLength:0}m, {why})");
            }
            return;
        }
        // per bot, at most once every 4 s (no key strings built per decision tick)
        if (_noBooked.TryGetValue(id, out float last) && Time.time - last < 4f)
        {
            return;
        }
        _noBooked[id] = Time.time;
        TacticDiagnostics.Count($"vuln.reach.{action}.no.{why}");
    }

    /// <summary>RushEnemyAction tick: when the rushing bot first gets close / sees him, was he still busy?</summary>
    public static void TickRush(BotComponent bot, Enemy enemy)
    {
        if (bot == null || enemy == null || !_rushes.TryGetValue(bot.ProfileId, out var r) || r.Arrived || r.EnemyId != enemy.EnemyProfileId)
        {
            return;
        }
        if (Time.time - r.Start > RECORD_TIME)
        {
            _rushes.Remove(bot.ProfileId);
            return;
        }
        if (!(enemy.IsVisible && enemy.RealDistance < 15f) && enemy.RealDistance > 4f)
        {
            return;
        }
        r.Arrived = true;
        bool stillBusy = enemy.Status.VulnerableAction == r.Action;
        TacticDiagnostics.Count($"vuln.rush.{r.Action}.{(stillBusy ? "arrivedBusy" : "arrivedLate")}");
        if (TacticDiagnostics.LogOn)
        {
            TacticDiagnostics.LogCloseCombat($"[Vuln] [{bot.name}] reached {enemy.EnemyName} after {Time.time - r.Start:0.0}s ({r.Distance:0}m) - {(stillBusy ? "still " + r.Action : "too late")}");
        }
    }

    /// <summary>BattleStats death hook: a rush that ended in a death on either side within 12 s.</summary>
    public static void OnDeath(string victimId, string killerId)
    {
        if (victimId != null && _rushes.TryGetValue(victimId, out var mine))
        {
            if (Time.time - mine.Start < RECORD_TIME)
            {
                TacticDiagnostics.Count($"vuln.rush.{mine.Action}.died");
            }
            _rushes.Remove(victimId);
        }
        if (killerId != null && _rushes.TryGetValue(killerId, out var theirs) && theirs.EnemyId == victimId)
        {
            if (Time.time - theirs.Start < RECORD_TIME)
            {
                TacticDiagnostics.Count($"vuln.rush.{theirs.Action}.killed");
            }
            _rushes.Remove(killerId);
        }
    }

    public static void Clear()
    {
        _rushes.Clear();
        _heardBooked.Clear();
        _noBooked.Clear();
    }
}
