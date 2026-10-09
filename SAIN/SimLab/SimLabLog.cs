using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Logging;
using Newtonsoft.Json;
using SAIN.Preset.Shared.SimLab;

namespace SAIN.SimLab;

/// <summary>
/// zzap SimLab: crash-safe local record of each sim raid - BepInEx/config/SAIN-zzap/SimLab/&lt;run&gt;_&lt;map&gt;_&lt;time&gt;.jsonl, one full
/// report per line, flushed to the OS every 10 s (survives Alt+F4 and a killed process; only a power cut can lose the last lines).
/// A file without an "end" line is an interrupted map: the next game start sends its last line to the server as "recovered".
/// </summary>
public static class SimLabFile
{
    private static StreamWriter _writer;

    public static string Current { get; private set; }

    public static string Dir
    {
        get { return Path.Combine(BepInEx.Paths.ConfigPath, "SAIN-zzap", "SimLab"); }
    }

    public static void Open(string runId, string map)
    {
        Close(null);
        try
        {
            Directory.CreateDirectory(Dir);
            Current = Path.Combine(Dir, $"{San(runId)}_{San(map)}_{DateTime.Now:yyyyMMdd_HHmmss}.jsonl");
            _writer = new StreamWriter(Current, true, new UTF8Encoding(false));
            Logger.LogWarning($"[SimLab] recording {Current}");
        }
        catch (Exception ex)
        {
            _writer = null;
            Logger.LogWarning($"[SimLab] could not open the sim file: {ex.Message}");
        }
    }

    public static void Write(SimBeat beat)
    {
        if (_writer == null || beat == null)
        {
            return;
        }
        try
        {
            _writer.WriteLine(JsonConvert.SerializeObject(beat, Formatting.None));
            _writer.Flush();
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[SimLab] sim file write failed: {ex.Message}");
        }
    }

    public static void Close(SimBeat endBeat)
    {
        if (_writer == null)
        {
            return;
        }
        Write(endBeat);
        try
        {
            _writer.Dispose();
        }
        catch
        {
        }
        _writer = null;
    }

    /// <summary>Sim files whose last line is not an "end" report: send their last report as "recovered", then mark the file.</summary>
    public static void RecoverUnfinished()
    {
        try
        {
            if (!Directory.Exists(Dir))
            {
                return;
            }
            foreach (string file in Directory.GetFiles(Dir, "*.jsonl"))
            {
                if (file.EndsWith(".sent.jsonl", StringComparison.OrdinalIgnoreCase) || file == Current)
                {
                    continue;
                }
                string last = File.ReadLines(file).LastOrDefault(l => !string.IsNullOrWhiteSpace(l));
                SimBeat beat = null;
                try
                {
                    beat = last == null ? null : JsonConvert.DeserializeObject<SimBeat>(last);
                }
                catch
                {
                }
                if (beat != null && beat.Kind != "end")
                {
                    beat.Kind = "recovered";
                    beat.Notes ??= new List<string>();
                    beat.Notes.Add(
                        $"PC 기록 파일에서 복구: 마지막 기록 {beat.ClientTime} (t={beat.RaidSeconds / 60f:0.0}분). 이 뒤로 게임이 강제 종료됐거나 멈춤. 파일: {Path.GetFileName(file)}"
                    );
                    if (SimLab.Post<object>("/sain/sim/beat", beat) == null)
                    {
                        continue; // server not answering - try again next start
                    }
                    Logger.LogWarning($"[SimLab] recovered unfinished sim map {beat.Map} (last report t={beat.RaidSeconds:0}s) from {file}");
                }
                File.Move(file, file.Substring(0, file.Length - ".jsonl".Length) + ".sent.jsonl");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[SimLab] recovery of sim files failed: {ex.Message}");
        }
    }

    private static string San(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return "x";
        }
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            s = s.Replace(c, '_');
        }
        return s;
    }
}

/// <summary>
/// zzap SimLab: counts errors/exceptions written to the BepInEx log (every mod, and Unity's own log through BepInEx) during a
/// sim raid, grouped by their first line with numbers masked, so the analysis shows "this exception ×340 from SAIN" instead
/// of a 50 MB log. Only attached while a sim raid runs.
/// </summary>
public sealed class SimLogListener : ILogListener
{
    private const int MAX_KEYS = 80;
    private static readonly Regex _numbers = new(@"\d+", RegexOptions.Compiled);

    private readonly object _lock = new();
    private readonly Dictionary<string, SimError> _errors = new();
    private static SimLogListener _instance;

    /// <summary>Raid time for "first seen" (set by the runner every frame; read from any thread).</summary>
    public static volatile float RaidSeconds;

    /// <summary>Set once the sim ends the raid: from then on errors are the game tearing the raid down (user 2026-10-09:
    /// "the NullReference and the frame drop only happen when the raid ends - keep them out"), counted apart, not as raid errors.</summary>
    public static volatile bool Ending;
    public static int EndingErrors;

    public static void Attach()
    {
        if (_instance != null)
        {
            _instance.Clear();
            return;
        }
        _instance = new SimLogListener();
        BepInEx.Logging.Logger.Listeners.Add(_instance);
    }

    public static void Detach()
    {
        if (_instance == null)
        {
            return;
        }
        BepInEx.Logging.Logger.Listeners.Remove(_instance);
        _instance = null;
    }

    public static void CopyTo(List<SimError> to)
    {
        to.Clear();
        var listener = _instance;
        if (listener == null)
        {
            return;
        }
        lock (listener._lock)
        {
            foreach (var e in listener._errors.Values)
            {
                to.Add(new SimError { Key = e.Key, Level = e.Level, Source = e.Source, Count = e.Count, FirstRaidTime = e.FirstRaidTime, Sample = e.Sample });
            }
        }
    }

    private void Clear()
    {
        lock (_lock)
        {
            _errors.Clear();
        }
    }

    public void LogEvent(object sender, LogEventArgs eventArgs)
    {
        try
        {
            var level = eventArgs.Level;
            bool error = (level & (LogLevel.Error | LogLevel.Fatal)) != 0;
            string text = eventArgs.Data?.ToString();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            if (!error && !((level & LogLevel.Warning) != 0 && text.IndexOf("Exception", StringComparison.Ordinal) >= 0))
            {
                return;
            }
            if (Ending)
            {
                System.Threading.Interlocked.Increment(ref EndingErrors);
                return;
            }
            string first = text;
            int newline = first.IndexOf('\n');
            if (newline >= 0)
            {
                first = first.Substring(0, newline);
            }
            first = first.Trim();
            string key = _numbers.Replace(first, "#");
            if (key.Length > 180)
            {
                key = key.Substring(0, 180);
            }
            string source = eventArgs.Source?.SourceName ?? "?";
            lock (_lock)
            {
                if (_errors.TryGetValue(key, out SimError existing))
                {
                    existing.Count++;
                    return;
                }
                if (_errors.Count >= MAX_KEYS)
                {
                    return;
                }
                _errors[key] = new SimError
                {
                    Key = key,
                    Level = level.ToString(),
                    Source = source,
                    Count = 1,
                    FirstRaidTime = RaidSeconds,
                    Sample = text.Length > 1500 ? text.Substring(0, 1500) : text,
                };
            }
        }
        catch
        {
            // never throw from a log listener
        }
    }

    public void Dispose()
    {
    }
}
