using System.Collections.Generic;
using System.Linq;
using System.Text;
using EFT;
using SAIN.Components;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork: bot-vs-bot scoreboard for the raid journal (user 2026-09-29: "from now on I test by letting bots fight on
/// Factory for 30+ minutes as a simulation"). Every bot death is booked for the victim and, when a bot killed it, for the
/// killer: per personality (kills / deaths), per decision reason the bot was acting on (why= of the [Decide] line, e.g.
/// utilityVShoot / utilityVCover / utilityHold) and how long that decision had been running when the bot died (a death
/// within 1.5s of switching = "died switching"). Written as [Battle] lines every 5 minutes and at raid end, so a long sim
/// can be read without scrolling through thousands of lines.
/// </summary>
public static class BattleStats
{
    private sealed class Row
    {
        public int Kills;
        public int Deaths;
        public int DiedSwitching;
    }

    private static readonly Dictionary<string, Row> _personality = new();
    private static readonly Dictionary<string, Row> _reason = new();
    private static readonly Dictionary<string, Row> _combat = new();
    private static int _botKills;
    private static int _otherKills;
    private static int _teamKills;
    private static float _nextReport;
    private static int _perfFrame;
    private static float _perfTime;

    private static Row Get(Dictionary<string, Row> dict, string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            key = "-";
        }
        if (!dict.TryGetValue(key, out Row r))
        {
            r = new Row();
            dict[key] = r;
        }
        return r;
    }

    /// <summary>Strip numbers so reasons group (e.g. "default:MoveTo" stays, "hold 3s" -> "hold").</summary>
    private static string ReasonKey(string reason)
    {
        if (string.IsNullOrEmpty(reason))
        {
            return "-";
        }
        int cut = reason.IndexOfAny(new[] { ' ', '(' });
        return cut > 0 ? reason.Substring(0, cut) : reason;
    }

    public static void OnBotDied(BotComponent victim, IPlayer killer, float decisionAge, bool teamKill)
    {
        if (victim == null)
        {
            return;
        }
        string reason = ReasonKey(victim.Decision.EnemyDecisions?.LastReason);
        bool switching = decisionAge < 1.5f;
        var p = Get(_personality, victim.Info.Personality.ToString());
        var r = Get(_reason, reason);
        var c = Get(_combat, victim.Decision.CurrentCombatDecision.ToString());
        p.Deaths++;
        r.Deaths++;
        c.Deaths++;
        if (switching)
        {
            p.DiedSwitching++;
            r.DiedSwitching++;
            c.DiedSwitching++;
        }
        if (teamKill)
        {
            _teamKills++;
        }
        if (killer != null && killer.IsAI && SAINEnableClass.GetSAIN(killer.ProfileId, out BotComponent k) && k != null)
        {
            _botKills++;
            Get(_personality, k.Info.Personality.ToString()).Kills++;
            Get(_reason, ReasonKey(k.Decision.EnemyDecisions?.LastReason)).Kills++;
            Get(_combat, k.Decision.CurrentCombatDecision.ToString()).Kills++;
        }
        else
        {
            _otherKills++;
        }
    }

    /// <summary>Called from the journal tick; writes the table every 5 minutes.</summary>
    public static void Tick()
    {
        CorpseCleanup.Tick();
        float time = Time.time;
        if (_nextReport <= 0f)
        {
            _nextReport = time + 300f;
            _perfFrame = Time.frameCount;
            _perfTime = Time.realtimeSinceStartup;
            return;
        }
        if (time > _nextReport)
        {
            _nextReport = time + 300f;
            Report("running");
        }
    }

    public static void Report(string why)
    {
        int deaths = _personality.Values.Sum(x => x.Deaths);
        ReportPerf(why, deaths);
        if (deaths == 0)
        {
            RaidJournal.Line($"[Battle] ({why}) no bot deaths yet");
            return;
        }
        RaidJournal.Line(
            $"[Battle] ({why}) {deaths} bot deaths: {_botKills} by bots, {_otherKills} by player/other, {_teamKills} teamkills | "
                + "K/D, 'sw' = died <1.5s after switching decision");
        RaidJournal.Line("[Battle] by personality: " + Table(_personality, 0));
        RaidJournal.Line("[Battle] by combat action: " + Table(_combat, 0));
        RaidJournal.Line("[Battle] by decision reason: " + Table(_reason, 2));
    }

    /// <summary>
    /// Frame rate since the last report, managed heap and players, to tell "the log" from "corpses piling up" or "garbage
    /// piling up" when frames drop over a long sim (EFT turns the GC off for the whole raid, see kb 02 2.18).
    /// </summary>
    private static void ReportPerf(string why, int deaths)
    {
        int frame = Time.frameCount;
        float now = Time.realtimeSinceStartup;
        float fps = _perfTime > 0f && now > _perfTime ? (frame - _perfFrame) / (now - _perfTime) : 0f;
        _perfFrame = frame;
        _perfTime = now;
        long used = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / (1024 * 1024);
        long heap = UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong() / (1024 * 1024);
        int alive = Comfort.Common.Singleton<GameWorld>.Instance?.AllAlivePlayersList?.Count ?? -1;
        RaidJournal.Line(
            $"[Perf] ({why}) fps {fps:0} avg since last report | mono heap used {used} MB / reserved {heap} MB | GC {UnityEngine.Scripting.GarbageCollector.GCMode} | "
                + $"alive players {alive}, bot corpses so far {deaths} (hidden by the sim corpse limit: {CorpseCleanup.Hidden})"
        );
    }

    private static string Table(Dictionary<string, Row> dict, int minEvents)
    {
        var sb = new StringBuilder();
        foreach (var kv in dict.OrderByDescending(x => x.Value.Kills + x.Value.Deaths))
        {
            var r = kv.Value;
            if (r.Kills + r.Deaths < minEvents)
            {
                continue;
            }
            float kd = r.Deaths > 0 ? (float)r.Kills / r.Deaths : r.Kills;
            sb.Append($"{kv.Key} {r.Kills}/{r.Deaths} ({kd:0.00}{(r.DiedSwitching > 0 ? $", sw {r.DiedSwitching}" : "")}); ");
        }
        return sb.ToString();
    }

    public static void Clear()
    {
        _personality.Clear();
        _reason.Clear();
        _combat.Clear();
        _botKills = 0;
        _otherKills = 0;
        _teamKills = 0;
        _nextReport = 0f;
        _perfFrame = 0;
        _perfTime = 0f;
        CorpseCleanup.Clear();
        WeaponLog.Clear();
    }
}
