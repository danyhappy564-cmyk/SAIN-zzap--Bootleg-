using System;
using System.Collections.Generic;
using System.Reflection;
using EFT;
using SAIN.Helpers;
using SAIN.Preset.Shared.GlobalSettings;

namespace SAIN.SimLab;

/// <summary>
/// zzap remake5 cross-check (user 2026-10-11: "I run the sim on the sim preset only and ship right after - what it tested must
/// hold for zzap"). The sim preset differs from zzap in test switches inside six behaviour sections (diamond test mode, all-PMC
/// GigaChad doors, run-by 100%, room clear 60, reposition test mode, no utility mistakes, no adaptation). zzap.json doesn't have
/// those sections at all, so zzap users get the code defaults there. For a cross-check sim raid every field of those sections is
/// set to its default - except the logging switches, which keep the sim preset's values - and put back when the raid ends.
/// The [Tactics] SETTINGS lines of the raid journal (written when the first bot spawns) show the values actually used.
/// </summary>
public static class SimLabZzapCheck
{
    private static readonly string[] KeepFields = { "DiagnosticLogs", "VerboseLogs", "RaidJournal", "RecordKeys" };

    private static readonly List<(object Live, object Backup)> _backups = new();

    public static bool Applied
    {
        get { return _backups.Count > 0; }
    }

    /// <summary>Sets the six sections to zzap's values. Returns what changed ("CloseCombat.DiamondStepTestMode True->False").</summary>
    public static List<string> Apply()
    {
        var changes = new List<string>();
        Restore();
        var general = GlobalSettingsClass.Instance?.General;
        if (general == null)
        {
            changes.Add("GlobalSettings 없음 - 적용 안 됨");
            return changes;
        }
        Reset("CloseCombat", general.CloseCombat, changes);
        Reset("DoorTactics", general.DoorTactics, changes);
        Reset("Reposition", general.Reposition, changes);
        Reset("PlayerStyle", general.PlayerStyle, changes);
        Reset("FreezeAmbush", general.FreezeAmbush, changes);
        Reset("SquadCombat", general.SquadCombat, changes);
        if (changes.Count == 0)
        {
            changes.Add("차이 없음 (시뮬 프리셋의 행동 값이 이미 zzap과 같음)");
        }
        Logger.LogWarning($"[SimLab] zzap cross-check: behaviour sections set to zzap values: {string.Join(", ", changes)}");
        return changes;
    }

    private static void Reset(string name, object live, List<string> changes)
    {
        if (live == null)
        {
            return;
        }
        Type type = live.GetType();
        object defaults = Activator.CreateInstance(type);
        object backup = Activator.CreateInstance(type);
        CloneSettingsClass.CopyFields(live, backup);
        _backups.Add((live, backup));
        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            if (field.IsInitOnly || field.FieldType == type || Array.IndexOf(KeepFields, field.Name) >= 0 || field.Name == "Defaults")
            {
                continue;
            }
            object now = field.GetValue(live);
            object want = field.GetValue(defaults);
            if (!Equals(now, want))
            {
                changes.Add($"{name}.{field.Name} {now}->{want}");
                field.SetValue(live, want);
            }
        }
    }

    /// <summary>Puts the sim preset's own values back (raid end, preset change). Safe to call any time.</summary>
    public static void Restore()
    {
        if (_backups.Count == 0)
        {
            return;
        }
        foreach (var (live, backup) in _backups)
        {
            foreach (FieldInfo field in live.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!field.IsInitOnly && field.FieldType != live.GetType() && field.Name != "Defaults")
                {
                    field.SetValue(live, field.GetValue(backup));
                }
            }
        }
        _backups.Clear();
        Logger.LogInfo("[SimLab] zzap cross-check: sim preset values restored");
    }
}

/// <summary>Faction group of a bot role, for the per-faction scoreboard and counters (normal raids too: [Battle] by faction).</summary>
public static class SimFaction
{
    public static string Of(WildSpawnType role)
    {
        int id = (int)role;
        if (id >= 848420 && id <= 848426)
        {
            return "BlackDivision";
        }
        if (role is WildSpawnType.pmcUSEC or WildSpawnType.pmcBEAR)
        {
            return "PMC";
        }
        if (role == WildSpawnType.pmcBot)
        {
            return "Raider";
        }
        if (role == WildSpawnType.exUsec)
        {
            return "Rogue";
        }
        if (EnumValues.WildSpawn.IsGoons(role))
        {
            return "Goons";
        }
        if (EnumValues.WildSpawn.IsNormalBoss(role))
        {
            return "Boss";
        }
        if (EnumValues.WildSpawn.IsNormalFollower(role))
        {
            return "Follower";
        }
        if (EnumValues.WildSpawn.IsScav(role))
        {
            return "Scav";
        }
        if (role.IsSectant())
        {
            return "Cultist";
        }
        return "Other";
    }
}
