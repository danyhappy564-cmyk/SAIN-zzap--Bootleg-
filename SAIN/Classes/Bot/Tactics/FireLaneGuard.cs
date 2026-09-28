using System.Collections.Generic;
using EFT;
using SAIN.Components;
using SAIN.Models.Enums;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.GlobalSettings;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork: squadmates' firing lanes (user 2026-09-29: bots hanging back ran up toward the enemy right through the
/// teammates' fire and got team-killed). SAIN only checked friendly fire from the shooter, along the barrel with a 0.2m
/// ray at aim time. Now the mover side too: a bot never steps deeper into a lane a squadmate is firing (or about to fire,
/// enemy visible) down - it holds just outside, and a bot already in a lane steps out sideways first.
/// F6: General > Squad Combat (zzap) > Fire Lane Guard. Counted as squad.fireLane.*.
/// </summary>
public static class FireLaneGuard
{
    private const float CHECK_INTERVAL = 0.1f;

    private struct Cached
    {
        public float Until;
        public bool Changed;
        public Vector3 Direction;
    }

    private static readonly Dictionary<string, Cached> _cache = new();
    private static readonly Dictionary<string, float> _nextLog = new();
    private static readonly Dictionary<string, float> _heldSince = new();
    private static readonly Dictionary<string, float> _freeUntil = new();

    /// <summary>
    /// Called with every move direction (PlayerMovementController.SetTargetMoveDirection). Returns false to hold still
    /// this frame; may bend the direction to step out of a lane.
    /// </summary>
    public static bool Filter(BotComponent bot, ref Vector3 direction)
    {
        var settings = GlobalSettingsClass.Instance?.General?.SquadCombat;
        if (bot == null || settings == null || !settings.FireLaneGuard)
        {
            return true;
        }
        var members = bot.Squad?.Members;
        if (members == null || members.Count <= 1)
        {
            return true;
        }
        // Never hold a bot that is getting away from a grenade.
        if (bot.Decision.CurrentCombatDecision == ECombatDecision.AvoidGrenade)
        {
            return true;
        }
        string id = bot.ProfileId;
        float time = Time.time;
        if (_freeUntil.TryGetValue(id, out float free) && time < free)
        {
            return true;
        }
        if (_cache.TryGetValue(id, out Cached c) && time < c.Until)
        {
            if (!c.Changed)
            {
                return true;
            }
            direction = c.Direction;
            return direction != Vector3.zero;
        }
        Vector3 result = direction;
        bool changed = Check(bot, members, settings.FireLaneWidth, ref result, out string what);
        _cache[id] = new Cached { Until = time + CHECK_INTERVAL, Changed = changed, Direction = result };
        if (changed && result == Vector3.zero)
        {
            // Deadlock guard (narrow corridor, teammate firing down it for long): after 1.5s held, let it through for 1s.
            if (!_heldSince.TryGetValue(id, out float since))
            {
                _heldSince[id] = since = time;
            }
            if (time - since > 1.5f)
            {
                _heldSince.Remove(id);
                _freeUntil[id] = time + 1f;
                TacticDiagnostics.Count("squad.fireLane.giveUp");
                _cache.Remove(id);
                return true;
            }
        }
        else
        {
            _heldSince.Remove(id);
        }
        if (changed)
        {
            TacticDiagnostics.Count(result == Vector3.zero ? "squad.fireLane.held" : "squad.fireLane.stepOut");
            if (!_nextLog.TryGetValue(id, out float next) || time > next)
            {
                _nextLog[id] = time + 3f;
                TacticDiagnostics.LogCloseCombat($"[FireLane] [{bot.name}] {what}");
            }
            direction = result;
            return result != Vector3.zero;
        }
        return true;
    }

    private static bool Check(BotComponent bot, Dictionary<string, BotComponent> members, float width, ref Vector3 direction, out string what)
    {
        what = null;
        Vector3 chest = bot.Position + Vector3.up * 1.2f;
        Vector3 dir = direction;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
        {
            return false;
        }
        dir.Normalize();
        Vector3 next = chest + dir * 1f;
        foreach (var member in members.Values)
        {
            if (member == null || ReferenceEquals(member, bot) || member.IsDead || member.Player?.HealthController?.IsAlive != true)
            {
                continue;
            }
            if (!Lane(member, out Vector3 from, out Vector3 to))
            {
                continue;
            }
            float now = DistanceToSegment(chest, from, to, out float tNow);
            if (tNow <= 0.02f || tNow >= 0.98f)
            {
                // Behind the shooter or past the target: not in the line of fire.
                continue;
            }
            float ahead = DistanceToSegment(next, from, to, out _);
            if (now < width)
            {
                // Already in the lane: step straight out, to whichever side we're already on.
                Vector3 axis = to - from;
                axis.y = 0f;
                Vector3 side = Vector3.Cross(Vector3.up, axis.normalized);
                Vector3 offset = chest - (from + (to - from) * tNow);
                offset.y = 0f;
                if (Vector3.Dot(offset, side) < 0f)
                {
                    side = -side;
                }
                direction = side;
                what = $"in {member.name}'s line of fire ({now:0.0}m) -> step out sideways";
                return true;
            }
            if (ahead < width && ahead < now - 0.05f)
            {
                direction = Vector3.zero;
                what = $"would walk into {member.name}'s line of fire ({now:0.0}m away) -> hold";
                return true;
            }
        }
        return false;
    }

    /// <summary>The member's current firing line: shooting now, manual/suppression fire, or a visible enemy it aims at.</summary>
    private static bool Lane(BotComponent member, out Vector3 from, out Vector3 to)
    {
        from = member.Transform.WeaponData.FirePort;
        to = default;
        var manual = member.ManualShoot;
        if (manual != null && manual.Reason != EShootReason.None && manual.ShootPosition != Vector3.zero)
        {
            to = manual.ShootPosition;
            return true;
        }
        var enemy = member.GoalEnemy;
        if (enemy == null)
        {
            return false;
        }
        bool shooting = member.BotOwner?.ShootData?.Shooting == true;
        if (!shooting && !enemy.IsVisible)
        {
            return false;
        }
        to = enemy.EnemyPosition + Vector3.up * 1.2f;
        return (to - from).sqrMagnitude > 1f;
    }

    private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b, out float t)
    {
        Vector3 ab = b - a;
        float len = ab.sqrMagnitude;
        t = len > 0f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len) : 0f;
        return (p - (a + ab * t)).magnitude;
    }

    public static void Clear()
    {
        _cache.Clear();
        _nextLog.Clear();
        _heldSince.Clear();
        _freeUntil.Clear();
    }
}
