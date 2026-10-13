using System;
using System.Collections.Generic;
using EFT;
using EFT.Interactive;
using SAIN.SAINComponent.Classes.Tactics;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Mover;

/// <summary>
/// zzap (2026-10-14 sim, F8 marks #5/#8 + 8 doorStuck/stalledMove episodes at the same Factory door (9,35)): a ram turned by
/// DualSideDoorBreach (or a bot push-open) leaves the leaf at the negated open angle (-82). The level was built for the leaf at
/// its own open angle - turned the other way it stood across the passage, the NavMesh path still ran through it, and every bot
/// that came that way for the rest of the raid ran on the spot against it (recalc and jump didn't help; nobody ever closed it).
/// A bot held up for 1.5 s with such a leaf within 1.2 m pushes it back to the door's own open angle (a 0.5 s swing, like a
/// shove). Doors at their normal angle, sliding doors and doors a human player stands next to are left alone.
/// Called by SAINBotUnstuckClass's watchdog (every layer); swings ticked from DoorHandler. Counter door.unjam.
/// </summary>
public static class ReversedLeafUnjam
{
    private const float LEAF_RADIUS = 1.2f;
    private const float SWING_TIME = 0.5f;
    private const int MAX_SWINGS = 16;

    private struct Swing
    {
        public Door Door;
        public float From;
        public float To;
        public float Start;
    }

    private static readonly List<Swing> _swings = new();

    /// <summary>The leaf's current angle is on the other side of shut from the door's own open angle.</summary>
    public static bool IsReversed(Door door)
    {
        if (door == null || door.DoorState != EDoorState.Open || BotDoorBreacher.IsSliding(door))
        {
            return false;
        }
        float shut = door.GetAngle(EDoorState.Shut);
        float open = Mathf.DeltaAngle(shut, door.GetAngle(EDoorState.Open));
        float now = Mathf.DeltaAngle(shut, door.CurrentAngle);
        return Mathf.Abs(now) > 20f && Mathf.Abs(open) > 20f && Mathf.Sign(now) != Mathf.Sign(open);
    }

    /// <summary>True when a reversed leaf next to this stuck bot was swung back.</summary>
    public static bool TryUnjam(Player bot, out string what)
    {
        what = null;
        if (bot == null || _swings.Count >= MAX_SWINGS)
        {
            return false;
        }
        Door door = DoorLeafCheck.OpenLeafDoorNear(bot.Position, LEAF_RADIUS);
        if (door == null || !IsReversed(door) || Swinging(door))
        {
            return false;
        }
        if (HumanNear(door.transform.position, 2.5f))
        {
            what = $"door {door.GetInstanceID()} reversed at {door.CurrentAngle:0} but a player is next to it";
            TacticDiagnostics.Count("door.unjam.playerNear");
            return false;
        }
        float to = door.GetAngle(EDoorState.Open);
        what = $"pushed door {door.GetInstanceID()} leaf from {door.CurrentAngle:0} back to its own open angle {to:0}";
        _swings.Add(new Swing { Door = door, From = door.CurrentAngle, To = to, Start = Time.time });
        TacticDiagnostics.Count("door.unjam");
        return true;
    }

    private static bool Swinging(Door door)
    {
        for (int i = 0; i < _swings.Count; i++)
        {
            if (ReferenceEquals(_swings[i].Door, door))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HumanNear(Vector3 point, float radius)
    {
        var players = Comfort.Common.Singleton<GameWorld>.Instance?.AllAlivePlayersList;
        if (players == null)
        {
            return false;
        }
        for (int i = 0; i < players.Count; i++)
        {
            Player p = players[i];
            if (p != null && !p.IsAI && (p.Position - point).sqrMagnitude < radius * radius)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Every frame (DoorHandler). Each swing ends after SWING_TIME, or when the door is used / removed meanwhile.</summary>
    public static void Tick(float time)
    {
        for (int i = _swings.Count - 1; i >= 0; i--)
        {
            Swing s = _swings[i];
            try
            {
                if (s.Door == null || s.Door.DoorState != EDoorState.Open)
                {
                    _swings.RemoveAt(i);
                    continue;
                }
                float t = Mathf.Clamp01((time - s.Start) / SWING_TIME);
                s.Door.CurrentAngle = Mathf.Lerp(s.From, s.To, t);
                if (t >= 1f)
                {
                    _swings.RemoveAt(i);
                }
            }
            catch (Exception ex)
            {
                _swings.RemoveAt(i);
                Logger.LogWarning($"SAIN: door unjam swing failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    public static void Clear()
    {
        _swings.Clear();
    }
}
