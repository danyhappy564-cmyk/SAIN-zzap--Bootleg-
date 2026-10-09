using System;
using System.Collections.Generic;
using EFT;
using SAIN.Components;
using SAIN.Preset.Shared.SimLab;
using SAIN.SAINComponent.Classes.Tactics;
using SAIN.SAINComponent.Classes.WeaponFunction;
using UnityEngine;

namespace SAIN.SimLab;

/// <summary>
/// zzap SimLab automatic oddity detector (user 2026-10-09: "I saw unnatural things - can't you measure them yourself instead of
/// me describing each one?"). Instead of one counter per known scene, it watches every SAIN-combat bot on the runner's 2 s tick
/// for states that look wrong to a player, whatever caused them, and records the moment (who, decision, enemy distance, where,
/// when) so a scene the user saw can be found by its raid time:
///   noShoot      - goal enemy visible and shootable within 40 m, gun loaded, no shot for 4 s+
///   backTurned   - known enemy within 12 m, bot standing still facing more than 120 deg away for 4 s+
///   hitNoReact   - shot by the enemy, then 4 s+ without moving or shooting
///   flipFlop     - combat decision changed on 4 of the last 5 samples (dithering)
///   bunched      - a squadmate within 0.8 m for 6 s+ (bodies blocking each other)
///   stalledMove  - a moving decision (cover, rush, search...) but under 0.5 m moved in 6 s
/// One episode per bot per kind until the state clears; sim raids only.
/// </summary>
public static class SimLabOddity
{
    private const int MAX_EXAMPLES_PER_KIND = 8;

    private sealed class State
    {
        public Vector3 LastPos;
        public int LastBullets = -1;
        public float LastShotOrReload = -1f;
        public readonly Queue<string> Decisions = new();
        public readonly Dictionary<string, float> Since = new();
        public readonly HashSet<string> Flagged = new();
        public bool HasPos;
        public float LastSample = -1f;
    }

    private static readonly Dictionary<string, State> _states = new();
    private static readonly Dictionary<string, int> _counts = new();
    private static readonly List<SimOddity> _examples = new();
    private static readonly Dictionary<string, int> _examplesPerKind = new();

    public static void Reset()
    {
        _states.Clear();
        _counts.Clear();
        _examples.Clear();
        _examplesPerKind.Clear();
    }

    public static void Fill(SimBeat beat)
    {
        beat.OddityCounts = new Dictionary<string, int>(_counts);
        beat.Oddities = new List<SimOddity>(_examples);
    }

    /// <summary>Every 2 s for each living sim bot (already in SAIN's combat layer, brain built).</summary>
    public static void Sample(Player player, BotOwner owner, BotComponent bot, string layer, float dt)
    {
        float now = Time.time;
        if (!_states.TryGetValue(player.ProfileId, out State s))
        {
            s = new State();
            _states[player.ProfileId] = s;
        }
        if (s.LastSample >= 0f && now - s.LastSample > 3f)
        {
            // was out of SAIN's combat layer in between: start over, nothing carries across the gap
            s.Since.Clear();
            s.Flagged.Clear();
            s.Decisions.Clear();
            s.HasPos = false;
            s.LastBullets = -1;
        }
        s.LastSample = now;
        Vector3 pos = player.Position;
        float speed = s.HasPos && dt > 0f ? Vector3.Distance(pos, s.LastPos) / dt : 0f;
        s.LastPos = pos;
        s.HasPos = true;

        SAINBotSuppressClass.CalcAmmoRatio(owner, out int bullets);
        if (s.LastBullets >= 0 && bullets != s.LastBullets)
        {
            s.LastShotOrReload = now; // fewer = shot, more = reloaded: either way the bot did something with its gun
        }
        s.LastBullets = bullets;

        string decision = bot.Decision.CurrentCombatDecision.ToString();
        string reason = bot.Decision.EnemyDecisions?.LastReason ?? "-";
        s.Decisions.Enqueue(decision);
        while (s.Decisions.Count > 5)
        {
            s.Decisions.Dequeue();
        }

        var enemy = bot.GoalEnemy;
        float enemyDist = enemy != null ? enemy.RealDistance : -1f;
        bool visible = enemy != null && enemy.IsVisible;
        float angle = 0f;
        if (enemy != null)
        {
            Vector3 to = enemy.EnemyPosition - pos;
            to.y = 0f;
            Vector3 look = bot.Transform.LookDirection;
            look.y = 0f;
            angle = to.sqrMagnitude > 0.01f && look.sqrMagnitude > 0.01f ? Vector3.Angle(look, to) : 0f;
        }
        bool gunIdle = s.LastShotOrReload < 0f || now - s.LastShotOrReload >= 4f;

        Check(s, "noShoot", enemy != null && visible && enemy.CanShoot && enemyDist < 40f && bullets > 0 && gunIdle, 4f, now);
        Check(s, "backTurned", enemy != null && enemy.EnemyKnown && enemyDist < 12f && angle > 120f && speed < 0.7f, 4f, now);
        Check(s, "hitNoReact", enemy != null && enemy.Status.TimeLastShotMe > 0f && now - enemy.Status.TimeLastShotMe < 6f && speed < 0.25f && gunIdle, 4f, now);
        Check(s, "flipFlop", Changes(s.Decisions) >= 4, 0f, now);
        Check(s, "bunched", MateWithin(owner, player, 0.8f), 6f, now);
        // holding in cover is a SeekCover decision too - only a decision that should be moving counts
        Check(s, "stalledMove", IsMovingDecision(decision) && reason.IndexOf("Hold", StringComparison.OrdinalIgnoreCase) < 0 && speed < 0.25f, 6f, now);

        foreach (string kind in KINDS)
        {
            if (s.Flagged.Contains(kind) || !s.Since.TryGetValue(kind, out float since) || now - since < Need(kind))
            {
                continue;
            }
            s.Flagged.Add(kind);
            Record(kind, player, bot, decision, reason, layer, enemyDist, visible, angle, speed, bullets, now - since);
        }
    }

    private static readonly string[] KINDS = { "noShoot", "backTurned", "hitNoReact", "flipFlop", "bunched", "stalledMove" };

    private static float Need(string kind)
    {
        return kind switch
        {
            "flipFlop" => 0f,
            "bunched" or "stalledMove" => 6f,
            _ => 4f,
        };
    }

    private static void Check(State s, string kind, bool condition, float need, float now)
    {
        if (!condition)
        {
            s.Since.Remove(kind);
            s.Flagged.Remove(kind); // state cleared: the next time is a new episode
            return;
        }
        if (!s.Since.ContainsKey(kind))
        {
            s.Since[kind] = now;
        }
    }

    private static int Changes(Queue<string> decisions)
    {
        int changes = 0;
        string last = null;
        foreach (string d in decisions)
        {
            if (last != null && d != last)
            {
                changes++;
            }
            last = d;
        }
        return changes;
    }

    private static bool IsMovingDecision(string decision)
    {
        return decision is "SeekCover" or "ShiftCover" or "RushEnemy" or "Search" or "MoveToEngage" or "Retreat" or "RunAway" or "CreepOnEnemy";
    }

    private static bool MateWithin(BotOwner owner, Player player, float meters)
    {
        var group = owner.BotsGroup;
        if (group == null)
        {
            return false;
        }
        for (int i = 0; i < group.MembersCount; i++)
        {
            var mate = group.Member(i);
            if (mate == null || mate == owner || mate.IsDead || mate.GetPlayer == null)
            {
                continue;
            }
            if (Vector3.Distance(mate.GetPlayer.Position, player.Position) < meters)
            {
                return true;
            }
        }
        return false;
    }

    private static void Record(string kind, Player player, BotComponent bot, string decision, string reason, string layer, float enemyDist, bool visible, float angle, float speed, int bullets, float seconds)
    {
        _counts.TryGetValue(kind, out int n);
        _counts[kind] = n + 1;
        TacticDiagnostics.Count("oddity." + kind);
        if (TacticDiagnostics.LogOn)
        {
            RaidJournal.Line($"[Oddity] {kind} [{player.Profile?.Nickname}] [{bot.Info.Personality}] {decision}/{reason} layer={layer} enemy={enemyDist:0.0}m visible={visible} angle={angle:0} speed={speed:0.0} bullets={bullets} for {seconds:0}s at ({player.Position.x:0},{player.Position.y:0},{player.Position.z:0})");
        }
        _examplesPerKind.TryGetValue(kind, out int shown);
        if (shown >= MAX_EXAMPLES_PER_KIND)
        {
            return;
        }
        _examplesPerKind[kind] = shown + 1;
        Vector3 pos = player.Position;
        _examples.Add(new SimOddity
        {
            Kind = kind,
            Name = player.Profile?.Nickname,
            Role = player.Profile?.Info?.Settings?.Role.ToString(),
            Personality = bot.Info.Personality.ToString(),
            Decision = decision,
            Reason = reason,
            Layer = layer,
            EnemyDistance = enemyDist,
            EnemyVisible = visible,
            EnemyAngle = angle,
            Speed = speed,
            Bullets = bullets,
            Seconds = seconds,
            X = pos.x,
            Y = pos.y,
            Z = pos.z,
            RaidTime = SimLogListener.RaidSeconds,
        });
    }
}
