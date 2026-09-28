using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace SAIN.Components.BotControllerSpace.Classes;

/// <summary>
/// zzap fork: records the player's game keys (press/release + time) during a raid. Polled from a MonoBehaviour Update,
/// i.e. every frame, by key state (edge detection), so nothing longer than one frame is missed. Only game control keys,
/// only while in a raid, and not while the cursor is visible (inventory / menus / chat).
///  - Timeline: BepInEx/config/SAIN-zzap/PlayerStyle/&lt;profile&gt;_&lt;date&gt;.keys.csv, appended every 2s so a crash loses
///    at most 2s. Columns: t (seconds since raid start), key, event (down/up), holdMs (on up).
///  - Stats (KeyStats): per key presses / hold time / taps, A-D switches, jump while sprinting, fire taps vs holds.
/// Default EFT binds are assumed (see Keys).
/// </summary>
public sealed class PlayerKeyRecorder
{
    // Action -> fallback key: the user's own Control.ini as of 2026-09-28 (crouch = LeftControl, prone = Z), used when
    // the file can't be read. At raid start the real binds are read from SPT's Control.ini (see LoadBinds).
    private static readonly (string name, string eftBind, string fallback)[] Actions =
    [
        ("W", "MoveY+", "W"),
        ("A", "MoveX-", "A"),
        ("S", "MoveY-", "S"),
        ("D", "MoveX+", "D"),
        ("Jump", "Jump", "Space"),
        ("Sprint", "Sprint", "LeftShift"),
        ("Crouch", "Duck", "LeftControl"),
        ("Prone", "Prone", "Z"),
        ("LeanL", "LeanLockLeft", "Q"),
        ("LeanR", "LeanLockRight", "E"),
        ("Alt", "-", "LeftAlt"),
        ("Reload", "ReloadWeapon", "R"),
        ("Grenade", "ThrowGrenade", "G"),
        ("Interact", "Interact", "F"),
        ("Melee", "Knife", "V"),
        ("Pistol", "SecondaryWeapon", "Alpha1"),
        ("Primary1", "PrimaryWeaponFirst", "Alpha2"),
        ("Primary2", "PrimaryWeaponSecond", "Alpha3"),
        ("Slot4", "Slot4", "Alpha4"),
        ("CheckAmmo", "CheckAmmo", "Mouse4"),
        ("Tactical", "Tactical", "T"),
        ("Fire", "Shoot", "Mouse0"),
        ("Aim", "Aim", "Mouse1"),
    ];

    private readonly (KeyCode code, string name)[] Keys;
    public string BindSource { get; private set; }

    private const float TAP_MS = 200f;
    private const float FIRE_TAP_MS = 150f;
    private const float AD_SWITCH_WINDOW = 0.6f;
    private const float FLUSH_INTERVAL = 2f;

    private readonly bool[] _down;
    private readonly float[] _downAt;
    private readonly StringBuilder _buffer = new();
    private readonly bool _timeline;
    private readonly float _t0;
    private string _file;
    private float _nextFlush;
    private bool _paused;
    private int _lastLateral = -1;
    private float _lastLateralTime = -10f;

    public KeyStats Stats { get; } = new();

    public PlayerKeyRecorder(string dir, string profileId, float raidStartTime, bool timeline)
    {
        _t0 = raidStartTime;
        _timeline = timeline;
        Keys = LoadBinds(out string source);
        BindSource = source;
        _down = new bool[Keys.Length];
        _downAt = new float[Keys.Length];
        foreach (var action in Actions)
        {
            Stats.Keys[action.name] = new KeyStat();
        }
        Stats.BindSource = source;
        if (_timeline)
        {
            _file = Path.Combine(dir, $"{profileId}_{DateTime.Now:yyyyMMdd_HHmmss}.keys.csv");
            var header = new StringBuilder("# binds (" + source + "): ");
            foreach (var key in Keys)
            {
                header.Append(key.name).Append('=').Append(key.code).Append(' ');
            }
            File.WriteAllText(_file, header.ToString().TrimEnd() + Environment.NewLine + "t,key,event,holdMs" + Environment.NewLine);
            Stats.TimelineFile = _file;
        }
    }

    /// <summary>
    /// The player's real binds from SPT's Control.ini (JSON despite the name): &lt;game&gt;/SPT_Runtime/user/sptSettings, or
    /// &lt;game&gt;/user/sptSettings. First single-key variant of each action; combos (Alt+Q etc.) are skipped.
    /// </summary>
    private static (KeyCode code, string name)[] LoadBinds(out string source)
    {
        var binds = new Dictionary<string, string>();
        source = "fallback";
        try
        {
            string[] candidates =
            [
                Path.Combine(BepInEx.Paths.GameRootPath, "SPT_Runtime", "user", "sptSettings", "Control.ini"),
                Path.Combine(BepInEx.Paths.GameRootPath, "user", "sptSettings", "Control.ini"),
            ];
            foreach (string path in candidates)
            {
                if (!File.Exists(path))
                {
                    continue;
                }
                var root = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
                foreach (var axis in root["axisBindings"] ?? new Newtonsoft.Json.Linq.JArray())
                {
                    string axisName = (string)axis["axisName"];
                    var pair = axis["pairs"]?[0];
                    if (pair == null || (axisName != "MoveX" && axisName != "MoveY"))
                    {
                        continue;
                    }
                    AddSingle(binds, axisName + "+", pair["positive"]?["keyCode"]);
                    AddSingle(binds, axisName + "-", pair["negative"]?["keyCode"]);
                }
                foreach (var bind in root["keyBindings"] ?? new Newtonsoft.Json.Linq.JArray())
                {
                    string keyName = (string)bind["keyName"];
                    var variants = bind["variants"];
                    if (keyName == null || variants == null || binds.ContainsKey(keyName))
                    {
                        continue;
                    }
                    foreach (var variant in variants)
                    {
                        if (AddSingle(binds, keyName, variant["keyCode"]))
                        {
                            break;
                        }
                    }
                }
                source = path;
                break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[PlayerStyle] could not read Control.ini, using fallback binds: {ex.Message}");
            binds.Clear();
            source = "fallback";
        }

        var result = new List<(KeyCode, string)>();
        foreach (var action in Actions)
        {
            string keyName = binds.TryGetValue(action.eftBind, out string bound) ? bound : action.fallback;
            if (Enum.TryParse(keyName, out KeyCode code) || Enum.TryParse(action.fallback, out code))
            {
                result.Add((code, action.name));
            }
        }
        return result.ToArray();
    }

    private static bool AddSingle(Dictionary<string, string> binds, string key, Newtonsoft.Json.Linq.JToken codes)
    {
        if (codes is Newtonsoft.Json.Linq.JArray array && array.Count == 1)
        {
            binds[key] = (string)array[0];
            return true;
        }
        return false;
    }

    public void Poll()
    {
        float now = Time.time;
        bool paused = Cursor.visible;
        if (paused != _paused)
        {
            _paused = paused;
            if (paused)
            {
                // Menu/inventory opened: close any held key so its hold time doesn't include the menu time.
                ReleaseAll(now, "menu");
            }
            Line(now, "-", paused ? "menuOpen" : "menuClosed", -1f);
        }
        if (!paused)
        {
            var input = UnityInput.Current;
            for (int i = 0; i < Keys.Length; i++)
            {
                bool isDown = input.GetKey(Keys[i].code);
                if (isDown == _down[i])
                {
                    continue;
                }
                if (isDown)
                {
                    Press(i, now);
                }
                else
                {
                    Release(i, now);
                }
            }
        }
        if (now >= _nextFlush)
        {
            _nextFlush = now + FLUSH_INTERVAL;
            Flush();
        }
    }

    private void Press(int i, float now)
    {
        _down[i] = true;
        _downAt[i] = now;
        string name = Keys[i].name;
        Stats.Keys[name].Presses++;
        Line(now, name, "down", -1f);

        if (name == "A" || name == "D")
        {
            if (_lastLateral >= 0 && _lastLateral != i && now - _lastLateralTime < AD_SWITCH_WINDOW)
            {
                Stats.AdSwitches++;
            }
            _lastLateral = i;
            _lastLateralTime = now;
        }
        else if (name == "Jump" && IsDown("Sprint"))
        {
            Stats.SprintJumps++;
        }
    }

    private void Release(int i, float now)
    {
        _down[i] = false;
        float holdMs = (now - _downAt[i]) * 1000f;
        string name = Keys[i].name;
        var stat = Stats.Keys[name];
        stat.HoldSumMs += holdMs;
        if (holdMs < TAP_MS)
        {
            stat.Taps++;
        }
        if (name == "A" || name == "D")
        {
            Stats.LateralPresses++;
            Stats.LateralHoldSumMs += holdMs;
            _lastLateralTime = now;
        }
        else if (name == "Fire")
        {
            if (holdMs < FIRE_TAP_MS) Stats.FireTaps++;
            else Stats.FireHolds++;
        }
        Line(now, name, "up", holdMs);
    }

    private bool IsDown(string name)
    {
        for (int i = 0; i < Keys.Length; i++)
        {
            if (Keys[i].name == name)
            {
                return _down[i];
            }
        }
        return false;
    }

    private void ReleaseAll(float now, string why)
    {
        for (int i = 0; i < Keys.Length; i++)
        {
            if (_down[i])
            {
                Release(i, now);
            }
        }
    }

    private void Line(float now, string key, string evt, float holdMs)
    {
        if (!_timeline)
        {
            return;
        }
        _buffer.Append((now - _t0).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture))
            .Append(',').Append(key).Append(',').Append(evt).Append(',')
            .Append(holdMs >= 0f ? holdMs.ToString("0", System.Globalization.CultureInfo.InvariantCulture) : "")
            .Append('\n');
    }

    public void Flush()
    {
        if (!_timeline || _buffer.Length == 0 || _file == null)
        {
            return;
        }
        try
        {
            File.AppendAllText(_file, _buffer.ToString());
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[PlayerStyle] key timeline write failed: {ex.Message}");
        }
        _buffer.Length = 0;
    }

    public void End()
    {
        float now = Time.time;
        ReleaseAll(now, "end");
        Line(now, "-", "raidEnd", -1f);
        Flush();
    }

    public string Summary(float seconds)
    {
        float min = Mathf.Max(seconds, 1f) / 60f;
        var s = Stats;
        float lateralAvg = s.LateralPresses > 0 ? s.LateralHoldSumMs / s.LateralPresses : 0f;
        string Avg(string key)
        {
            var k = s.Keys[key];
            return k.Presses > 0 ? $"{k.HoldSumMs / k.Presses:0}ms" : "-";
        }
        return $"keys: A/D switches/min={s.AdSwitches / min:0.0} A/D avgHold={lateralAvg:0}ms A/D taps={s.Keys["A"].Taps + s.Keys["D"].Taps} "
            + $"jump/min={s.Keys["Jump"].Presses / min:0.0} sprintJumps={s.SprintJumps} crouch/min={s.Keys["Crouch"].Presses / min:0.0} "
            + $"lean/min={(s.Keys["LeanL"].Presses + s.Keys["LeanR"].Presses) / min:0.0} leanHold L/R={Avg("LeanL")}/{Avg("LeanR")} "
            + $"fire taps/holds={s.FireTaps}/{s.FireHolds} aim hold={Avg("Aim")} reloadKey={s.Keys["Reload"].Presses} "
            + $"timeline={(s.TimelineFile != null ? Path.GetFileName(s.TimelineFile) : "off")}";
    }
}

public sealed class KeyStat
{
    public int Presses;
    public float HoldSumMs;
    public int Taps;
}

public sealed class KeyStats
{
    public Dictionary<string, KeyStat> Keys = new();
    public int AdSwitches;
    public int LateralPresses;
    public float LateralHoldSumMs;
    public int SprintJumps;
    public int FireTaps;
    public int FireHolds;
    public string TimelineFile;
    public string BindSource;
}
