using System.Collections.Generic;
using EFT;
using SAIN.Components;
using SAIN.Components.BotControllerSpace.Classes;
using SAIN.Components.PlayerComponentSpace;
using SAIN.Preset.Shared.Enums;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork: what a bot can hear around it after its fight (user 2026-09-29: "killing its target isn't a reason to hand over
/// to ORBIT - gunfire nearby or someone coming = stay in combat mode; they always kill the target, back off healing and die").
/// Listens to every AI sound SAIN broadcasts (BotHearingClass.AISoundPlayed) and keeps the last 10s of gunshots and approach
/// sounds (footsteps, sprint, jump, landing, doors). Heard(bot):
///   someone coming  - an approach sound within 25m in the last 3s (not the bot's squad)
///   gunfire nearby  - a shot within 70m in the last 6s (not the bot's squad)
/// </summary>
public static class SoundWatch
{
    private struct Sound
    {
        public float Time;
        public Vector3 Position;
        public string ProfileId;
        public Player Player;
        public bool Shot;
    }

    private static readonly List<Sound> _sounds = new();
    private static BotHearingClass _hearing;

    public static void Attach(BotHearingClass hearing)
    {
        Detach();
        _hearing = hearing;
        if (_hearing != null)
        {
            _hearing.AISoundPlayed += OnSound;
        }
    }

    public static void Detach()
    {
        if (_hearing != null)
        {
            _hearing.AISoundPlayed -= OnSound;
            _hearing = null;
        }
        _sounds.Clear();
    }

    private static void OnSound(SAINSoundType type, Vector3 position, PlayerComponent source, float range, float volume)
    {
        bool shot = type == SAINSoundType.Shot || type == SAINSoundType.SuppressedShot;
        bool approach = type == SAINSoundType.FootStep || type == SAINSoundType.Sprint || type == SAINSoundType.Jump
            || type == SAINSoundType.Land || type == SAINSoundType.Door || type == SAINSoundType.DoorBreach;
        if ((!shot && !approach) || source == null)
        {
            return;
        }
        float time = Time.time;
        while (_sounds.Count > 0 && (time - _sounds[0].Time > 10f || _sounds.Count > 256))
        {
            _sounds.RemoveAt(0);
        }
        _sounds.Add(new Sound { Time = time, Position = position, ProfileId = source.ProfileId, Player = source.Player, Shot = shot });
    }

    /// <summary>True when the bot hears someone coming or gunfire nearby. Where = the closest such sound (approach first).</summary>
    public static bool Heard(BotComponent bot, out Vector3 where, out bool coming, out string why)
    {
        where = default;
        coming = false;
        why = null;
        if (bot == null || _sounds.Count == 0)
        {
            return false;
        }
        float time = Time.time;
        Vector3 pos = bot.Position;
        var squad = bot.Squad?.Members;
        float bestApproach = 25f, bestShot = 70f;
        Vector3? approachAt = null, shotAt = null;
        float approachAge = 0f, shotAge = 0f;
        for (int i = _sounds.Count - 1; i >= 0; i--)
        {
            Sound s = _sounds[i];
            float age = time - s.Time;
            if (age > 6f)
            {
                break;
            }
            // Not our own squad, and not the dead - the enemy just killed was shooting a moment ago.
            if (s.ProfileId == bot.ProfileId || (squad != null && squad.ContainsKey(s.ProfileId))
                || s.Player == null || s.Player.HealthController?.IsAlive != true)
            {
                continue;
            }
            float d = (s.Position - pos).magnitude;
            if (!s.Shot && age < 3f && d < bestApproach)
            {
                bestApproach = d;
                approachAt = s.Position;
                approachAge = age;
            }
            else if (s.Shot && d < bestShot)
            {
                bestShot = d;
                shotAt = s.Position;
                shotAge = age;
            }
        }
        if (approachAt != null)
        {
            where = approachAt.Value;
            coming = true;
            why = $"someone moving {bestApproach:0}m away ({approachAge:0.0}s ago)";
            return true;
        }
        if (shotAt != null)
        {
            where = shotAt.Value;
            why = $"gunfire {bestShot:0}m away ({shotAge:0.0}s ago)";
            return true;
        }
        return false;
    }
}
