using System.Collections.Generic;
using System.Linq;
using System.Text;
using Comfort.Common;
using EFT;
using SAIN.Preset.Shared.GlobalSettings;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// Raid-level diagnostics for the zzap door tactics and freeze ambush, so a single LogOutput.log shows
/// (1) which F6 values were actually loaded, (2) how often each thing started and how it ended.
///   [Tactics] SETTINGS ...   once per raid, when the first SAIN bot of that raid is created
///   [Tactics] SUMMARY ...    every 5 minutes of raid time while something happened
/// Counters are only written when either Diagnostic Logs toggle is on.
/// </summary>
internal static class TacticDiagnostics
{
    private const float SUMMARY_INTERVAL = 300f;

    private static readonly Dictionary<string, int> _counts = new();
    private static GameWorld _raid;
    private static float _raidStartTime;
    private static float _nextSummaryTime;
    private static bool _changedSinceSummary;

    private static bool Enabled
    {
        get
        {
            var general = GlobalSettingsClass.Instance?.General;
            return general != null
                && (general.DoorTactics.DiagnosticLogs || general.FreezeAmbush.DiagnosticLogs || general.SquadCombat.DiagnosticLogs || general.Reposition.DiagnosticLogs);
        }
    }

    /// <summary>
    /// Called when a bot's DoorTacticClass is created. The first call in a new raid logs the settings snapshot.
    /// </summary>
    public static void OnBotCreated()
    {
        CheckNewRaid();
    }

    public static void Count(string key)
    {
        if (!Enabled)
        {
            return;
        }
        CheckNewRaid();
        _counts.TryGetValue(key, out int value);
        _counts[key] = value + 1;
        _changedSinceSummary = true;
        TrySummary();
    }

    private static void CheckNewRaid()
    {
        GameWorld world = Singleton<GameWorld>.Instance;
        if (world == null || ReferenceEquals(world, _raid))
        {
            return;
        }
        if (_raid != null && _changedSinceSummary)
        {
            LogSummary("previous raid, final");
        }
        _raid = world;
        _counts.Clear();
        _raidStartTime = Time.time;
        _nextSummaryTime = Time.time + SUMMARY_INTERVAL;
        _changedSinceSummary = false;
        if (Enabled)
        {
            LogSettings();
        }
    }

    private static void TrySummary()
    {
        if (Time.time < _nextSummaryTime)
        {
            return;
        }
        _nextSummaryTime = Time.time + SUMMARY_INTERVAL;
        if (_changedSinceSummary)
        {
            LogSummary($"raid time {(Time.time - _raidStartTime) / 60f:0} min");
            _changedSinceSummary = false;
        }
    }

    private static void LogSummary(string when)
    {
        var sb = new StringBuilder($"[Tactics] SUMMARY ({when}): ");
        if (_counts.Count == 0)
        {
            sb.Append("nothing happened");
        }
        else
        {
            sb.Append(string.Join(", ", _counts.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}")));
        }
        Logger.LogWarning(sb.ToString());
    }

    private static void LogSettings()
    {
        var general = GlobalSettingsClass.Instance.General;
        var d = general.DoorTactics;
        var f = general.FreezeAmbush;
        Logger.LogWarning(
            $"[Tactics] SETTINGS DoorTactics: enabled={d.Enabled} jumpPeek={d.JumpPeek} roomTrap={d.RoomTrap} fakeNade={d.FakeGrenade} fakeHeal={d.FakeHeal} retreatDist={d.EmergencyRetreatDistance:0}m "
                + $"trick%: giga={d.GigaChadFakeTrickChance:0} chad={d.ChadFakeTrickChance:0} trapHeal={d.GigaChadTrapFakeHealChance:0} "
                + $"chance%: giga={d.GigaChadChance:0} chad={d.ChadChance:0} turtle={d.SnappingTurtleChance:0} rat={d.RatChance:0} "
                + $"peekVsTrap={d.GigaChadPeekChance:0} runBy%={d.RunByChance:0} stepPeek={d.StepPeek} autoClose={d.AutoCloseDoors} x{d.ChanceMultiplier:0.0} "
                + $"TESTMODE={d.TestModeAllPmcGigaChad} logs={d.DiagnosticLogs}/{d.VerboseLogs}"
        );
        Logger.LogWarning(
            $"[Tactics] SETTINGS FreezeAmbush: maxDist={f.MaxDistance:0}m outdoors={f.AllowOutdoors} time={f.MinDuration:0}-{f.MaxDuration:0}s "
                + $"notSeenFor={f.MinTimeSinceSeen:0}s heardWithin={f.MaxTimeSinceHeard:0}s watchCorner={f.WatchApproachCorner} logs={f.DiagnosticLogs}"
        );
        var q = general.SquadCombat;
        Logger.LogWarning(
            $"[Tactics] SETTINGS SquadCombat: enabled={q.Enabled} crossfire={q.Crossfire} minAngle={q.CrossfireMinAngle:0} spacing={q.MinSpacing:0}m "
                + $"cover={q.CoverTeammate} coverDist={q.CoverMaxDistance:0}m trade={q.Trade} tradeWindow={q.TradeWindow:0}s "
                + $"maxEnemyDist={q.MaxEnemyDistance:0}m pmcCallouts={q.PmcSquadVoiceCallouts} logs={q.DiagnosticLogs}"
        );
        var r = general.Reposition;
        Logger.LogWarning(
            $"[Tactics] SETTINGS Reposition: TESTMODE={r.TestMode} enabled={r.Enabled} fuse={r.FuseSelection}(<{r.ShortFuseDistance:0}m short) "
                + $"relocate={r.Relocate}/{r.RelocateChance:0}% bait={r.BaitPeek}/{r.BaitPeekChance:0}% fakeReload={r.FakeReload}/{r.FakeReloadChance:0}% pmcOnly={r.PmcOnly} logs={r.DiagnosticLogs}"
        );
        var c = general.CloseCombat;
        Logger.LogWarning(
            $"[Tactics] SETTINGS CloseCombat: noBackTurning={c.NoBackTurning} dist={c.Distance:0}m seenWithin={c.SeenWithin:0.0}s dash<={c.SprintIfCoverWithin:0.0}m pmcOnly={c.PmcOnly} "
                + $"suppDiscipline={c.SuppressionDiscipline} within={c.SuppressMaxTimeSinceContact:0.0}s keepMag={c.SuppressMinAmmoRatio:0.00} "
                + $"burst={c.SuppressBurstRounds:0}/{c.SuppressBurstPause:0.0}s"
        );
    }
}
