using System;
using System.IO;
using System.Text;
using SAIN.Preset.Shared.GlobalSettings;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork: raid journal (user 2026-09-29: "make it so you can check everything as if you had played the raid").
/// One plain-text file per raid - BepInEx/config/SAIN-zzap/Journal/&lt;date&gt;_&lt;map&gt;.log - with raid time on every
/// line and only SAIN-zzap content (the BepInEx log is interleaved with every other mod and has no timestamps):
///   header      settings that matter (utilities, mistakes, adaptation, preset, Classic Movement)
///   [Decide]    every decision change of every bot: combat / squad / self decision, WHY (the decision chain's reason),
///               goal enemy + distance + visible, bot position
///   all tactic logs ([Utility] [UtilityV] [Reload] [Heal] [Nade] [Chase] [DoorTactic] [SquadCombat] [Reposition]
///               [Storm] [FireLane] [PreAim] [HeadDown] [Handoff] [PostCombat] [Death] [Adapt] [Learn] ...)
///   [Player]    player position / stance / health every 2s, [PlayerHit] who hit him where, [Kill] his kills
///   SUMMARY     the tactic counters at the end
/// Flushed every 2s. F6: General > Player Style Recorder (zzap) > Raid Journal.
/// </summary>
public static class RaidJournal
{
    private static StreamWriter _writer;
    private static float _startTime;
    private static float _nextFlush;
    private static readonly StringBuilder _buffer = new();

    public static string File { get; private set; }

    public static bool Enabled
    {
        get { return GlobalSettingsClass.Instance?.General?.PlayerStyle?.RaidJournal == true; }
    }

    public static void Start(string map, string header)
    {
        End("newRaid");
        if (!Enabled)
        {
            return;
        }
        try
        {
            string dir = Path.Combine(BepInEx.Paths.ConfigPath, "SAIN-zzap", "Journal");
            Directory.CreateDirectory(dir);
            File = Path.Combine(dir, $"{DateTime.Now:yyyyMMdd_HHmmss}_{map}.log");
            _writer = new StreamWriter(File, false, new UTF8Encoding(false));
            _startTime = Time.time;
            _writer.WriteLine($"# SAIN-zzap raid journal - {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {map}");
            _writer.WriteLine(header);
            _writer.WriteLine("# t = seconds since raid start. Lines: [Decide] decision changes with reasons, tactic logs, [Player]/[PlayerHit]/[Kill].");
            _writer.Flush();
            Logger.LogWarning($"[Journal] writing {File}");
        }
        catch (Exception ex)
        {
            _writer = null;
            Logger.LogWarning($"[Journal] could not open: {ex.Message}");
        }
    }

    public static void Line(string text)
    {
        if (_writer == null)
        {
            return;
        }
        _buffer.Append("t=").Append((Time.time - _startTime).ToString("0.0")).Append(' ').AppendLine(text);
        if (Time.time >= _nextFlush || _buffer.Length > 32000)
        {
            Flush();
        }
    }

    public static void Flush()
    {
        if (_writer == null || _buffer.Length == 0)
        {
            return;
        }
        _nextFlush = Time.time + 2f;
        try
        {
            _writer.Write(_buffer.ToString());
            _writer.Flush();
        }
        catch
        {
        }
        _buffer.Clear();
    }

    public static void End(string why)
    {
        if (_writer == null)
        {
            return;
        }
        Line($"# journal end ({why})");
        Flush();
        try
        {
            _writer.Dispose();
        }
        catch
        {
        }
        _writer = null;
        Logger.LogWarning($"[Journal] closed ({why}): {File}");
    }

    public static string Pos(Vector3 p)
    {
        return $"({p.x:0},{p.z:0})";
    }
}
