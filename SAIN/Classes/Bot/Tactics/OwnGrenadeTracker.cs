using System.Collections.Generic;
using EFT;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork: each bot's own live grenades (SAIN only tracked enemy grenades - a bot ignored its own). Used so a bot times
/// its room dash to the real blast and never steps / chases / dashes into its own grenade (user: "calculate when your own
/// grenade goes off, and stay out of its range"). A grenade counts as live until it is destroyed (exploded) or 8s old.
/// </summary>
public static class OwnGrenadeTracker
{
    public const float DANGER_RADIUS = 6f;
    private const float MAX_AGE = 8f;

    private sealed class Entry
    {
        public Grenade Grenade;
        public Vector3 DangerPoint;
        public float ThrowTime;
    }

    private static readonly Dictionary<string, List<Entry>> _live = new();

    /// <summary>From GrenadeReactionClass when the thrower is the bot itself.</summary>
    public static void Add(string profileId, Grenade grenade, Vector3 dangerPoint)
    {
        if (string.IsNullOrEmpty(profileId) || grenade == null)
        {
            return;
        }
        if (!_live.TryGetValue(profileId, out var list))
        {
            list = new List<Entry>();
            _live[profileId] = list;
        }
        list.Add(new Entry { Grenade = grenade, DangerPoint = dangerPoint, ThrowTime = Time.time });
        TacticDiagnostics.Count("ownGrenade.tracked");
    }

    /// <summary>Position of the bot's newest live grenade (where it lies/flies now).</summary>
    public static bool Live(string profileId, out Vector3 position)
    {
        position = Vector3.zero;
        if (profileId == null || !_live.TryGetValue(profileId, out var list))
        {
            return false;
        }
        for (int i = list.Count - 1; i >= 0; i--)
        {
            Entry e = list[i];
            if (e.Grenade == null || Time.time - e.ThrowTime > MAX_AGE)
            {
                list.RemoveAt(i);
            }
        }
        if (list.Count == 0)
        {
            return false;
        }
        Entry last = list[list.Count - 1];
        position = last.Grenade.transform.position;
        return true;
    }

    /// <summary>
    /// True when one of the bot's own live grenades could hit this point: within the radius and nothing solid between the
    /// grenade and chest height at the point.
    /// </summary>
    public static bool Threatens(string profileId, Vector3 point, float radius = DANGER_RADIUS)
    {
        if (!Live(profileId, out _))
        {
            return false;
        }
        foreach (Entry e in _live[profileId])
        {
            Vector3 g = e.Grenade.transform.position;
            if ((g - point).sqrMagnitude > radius * radius)
            {
                continue;
            }
            if (!Physics.Linecast(g + Vector3.up * 0.3f, point + Vector3.up * 1.2f, LayersMaskController.HighPolyWithTerrainMask))
            {
                return true;
            }
        }
        return false;
    }

    public static void Clear()
    {
        _live.Clear();
    }
}
