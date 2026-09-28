using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using SAIN.Components;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.GlobalSettings.Categories.General;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.Classes.Tactics;
using UnityEngine;

namespace SAIN.Components.BotControllerSpace.Classes;

/// <summary>
/// zzap fork, stage 2 of "bots learn the player's style". At raid start the player's recorded raids (PlayerStyleRecorder
/// jsonl) are turned into style scores (0-1), and bots adapt - only against the human player, scaled by how much data
/// there is (confidence), the F6 strength, and per personality:
///   Aggression (keeps moving, shoots on the move) / BunnyHop -> hold the approach corner when he is heard running/jumping
///       close by (Freeze decision, aims at the corner), and fight close instead of walking to cover (larger range).
///   LeanPeek (Q/E rocking) -> bots rock their lean back more often.
///   Grenade (many grenades) -> squadmates keep more spacing.
/// F6: General > Player Style Recorder (zzap) > Adapt Bots To Player Style. Logged as [Adapt].
/// </summary>
public static class PlayerAdaptation
{
    public enum ECounter
    {
        HoldCorner,
        CloseFight,
        LeanSpam,
        Spacing,
    }

    public static bool Active { get; private set; }
    public static string ProfileId { get; private set; }
    public static float Aggression { get; private set; }
    public static float BunnyHop { get; private set; }
    public static float LeanPeek { get; private set; }
    public static float Grenade { get; private set; }
    public static float Camper { get; private set; }
    public static float Confidence { get; private set; }
    public static float Minutes { get; private set; }

    private static readonly Dictionary<string, float> _holdUntil = new();
    private static readonly Dictionary<string, float> _nextRoll = new();

    // How much each personality uses each counter (0-1). Campers hold, pushers fight, cowards barely adapt.
    private static readonly Dictionary<EPersonality, float[]> _factors = new()
    {
        //                                  HoldCorner, CloseFight, LeanSpam, Spacing
        { EPersonality.GigaChad, new[] { 0.6f, 1.0f, 1.0f, 1.0f } },
        { EPersonality.Chad, new[] { 0.3f, 1.0f, 0.8f, 0.8f } },
        { EPersonality.Wreckless, new[] { 0.0f, 1.0f, 0.5f, 0.3f } },
        { EPersonality.Normal, new[] { 0.5f, 0.6f, 0.5f, 1.0f } },
        { EPersonality.Rat, new[] { 1.0f, 0.4f, 0.3f, 1.0f } },
        { EPersonality.SnappingTurtle, new[] { 1.0f, 0.5f, 0.3f, 1.0f } },
        { EPersonality.Coward, new[] { 0.4f, 0.0f, 0.0f, 1.0f } },
        { EPersonality.Timmy, new[] { 0.2f, 0.2f, 0.0f, 0.5f } },
    };

    private static PlayerStyleSettings Settings
    {
        get { return GlobalSettingsClass.Instance?.General?.PlayerStyle; }
    }

    /// <summary>Called by PlayerStyleRecorder once the main player is known (raid start).</summary>
    public static void Load(string dir, string profileId)
    {
        Active = false;
        ProfileId = profileId;
        _holdUntil.Clear();
        _nextRoll.Clear();
        var settings = Settings;
        if (settings == null || !settings.AdaptEnabled)
        {
            Logger.LogWarning("[Adapt] off (F6 Player Style Recorder > Adapt Bots To Player Style)");
            return;
        }
        try
        {
            string file = Path.Combine(dir, $"{profileId}.jsonl");
            if (!File.Exists(file))
            {
                Logger.LogWarning($"[Adapt] no recorded raids yet for this profile ({file}) - bots play normally this raid");
                return;
            }
            var lines = new List<string>();
            foreach (string line in File.ReadAllLines(file))
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line);
                }
            }
            int take = Mathf.Max(1, Mathf.RoundToInt(settings.AdaptRaids));
            int testRaids = 0;
            float sec = 0f, moving = 0f, shots = 0f, shotsMoving = 0f, jumps = 0f, leanSw = 0f, leanPeeks = 0f, nades = 0f, camp = 0f;
            int used = 0;
            for (int i = lines.Count - 1; i >= 0 && used < take; i--)
            {
                JObject r;
                try
                {
                    r = JObject.Parse(lines[i]);
                }
                catch
                {
                    continue;
                }
                float s = (float?)r["Seconds"] ?? 0f;
                if (s < 60f)
                {
                    continue;
                }
                // Test raids (god mode / infinite ammo) are the same movement and fighting style - used unless turned off.
                if ((bool?)r["TestSession"] == true)
                {
                    if (!settings.AdaptUseTestRaids)
                    {
                        continue;
                    }
                    testRaids++;
                }
                used++;
                sec += s;
                moving += (float?)r["MovingSec"] ?? 0f;
                shots += (float?)r["Shots"] ?? 0f;
                shotsMoving += (float?)r["ShotsMoving"] ?? 0f;
                nades += (float?)r["Grenades"] ?? 0f;
                camp += (float?)r["CampSec"] ?? 0f;
                leanPeeks += (float?)r["LeanPeeks"] ?? 0f;
                var keys = r["Keys"];
                float keyJumps = (float?)keys?["Keys"]?["Jump"]?["Presses"] ?? -1f;
                jumps += keyJumps >= 0f ? keyJumps : ((float?)r["Jumps"] ?? 0f);
                leanSw += (float?)keys?["LeanSwitches"] ?? 0f;
            }
            if (used == 0 || sec < 60f)
            {
                Logger.LogWarning("[Adapt] recorded raids too short to judge a style - bots play normally this raid");
                return;
            }
            float min = sec / 60f;
            Minutes = min;
            float moveRatio = moving / sec;
            float movingShots = shots > 0f ? shotsMoving / shots : 0f;
            Aggression = 0.5f * Mathf.Clamp01((moveRatio - 0.55f) / 0.35f) + 0.5f * Mathf.Clamp01((movingShots - 0.5f) / 0.4f);
            BunnyHop = Mathf.Clamp01((jumps / min - 2f) / 12f);
            LeanPeek = Mathf.Max(Mathf.Clamp01(leanSw / min / 6f), Mathf.Clamp01(leanPeeks / min / 8f));
            Grenade = Mathf.Clamp01(nades / min * 10f / 10f);
            Camper = Mathf.Clamp01(camp / sec / 0.3f);
            Confidence = min < 10f ? 0f : Mathf.Max(0.25f, Mathf.Clamp01(min / settings.AdaptFullConfidenceMinutes));
            Active = Confidence > 0f;
            Logger.LogWarning(
                $"[Adapt] {(Active ? "ON" : "OFF (under 10 min recorded)")}: {used} raids ({testRaids} tagged test) / {min:0} min, confidence {Confidence:0.00}, strength {settings.AdaptStrength:0.00} | "
                    + $"aggression {Aggression:0.00} (moving {moveRatio:P0}, shots on the move {movingShots:P0}) bunnyHop {BunnyHop:0.00} ({jumps / min:0.0}/min) "
                    + $"leanPeek {LeanPeek:0.00} grenade {Grenade:0.00} ({nades / min * 10f:0.0}/10min) camper {Camper:0.00} | "
                    + $"effects at full personality factor: hold corner {Chance(Mathf.Max(Aggression, BunnyHop)):P0}, close fight +{6f * Weight(Aggression):0.0}m, "
                    + $"lean spam +{30f * Weight(LeanPeek):0}%, spacing +{3f * Weight(Grenade):0.0}m | movement: player no-inertia {ClassicMovementInterop.PlayerNoInertia}, "
                    + $"bots no-inertia {ClassicMovementInterop.BotsNoInertia} -> corner hold x{MovementAdvantage:0.00}"
            );
        }
        catch (Exception ex)
        {
            Active = false;
            Logger.LogWarning($"[Adapt] could not read the style record: {ex.Message}");
        }
    }

    /// <summary>
    /// Classic Movement gives the player inertia-less strafing while the bots keep vanilla inertia: moving gunfights favour
    /// him, holding an angle takes that away - so corner holds are weighted up (x1.25) in that setup.
    /// </summary>
    private static float MovementAdvantage
    {
        get { return ClassicMovementInterop.PlayerNoInertia && !ClassicMovementInterop.BotsNoInertia ? 1.25f : 1f; }
    }

    private static float Weight(float score)
    {
        return score * Confidence * (Settings?.AdaptStrength ?? 0f);
    }

    private static float Chance(float score)
    {
        return Mathf.Clamp01(Weight(score) * 0.8f);
    }

    private static float Factor(BotComponent bot, ECounter counter)
    {
        return _factors.TryGetValue(bot.Info.Personality, out float[] f) ? f[(int)counter] : 0.5f;
    }

    /// <summary>True when this enemy is the recorded human player and adaptation is on.</summary>
    public static bool AgainstPlayer(Enemy enemy)
    {
        return Active && Settings?.AdaptEnabled == true && enemy != null && !enemy.IsAI && enemy.EnemyProfileId == ProfileId;
    }

    public static float CloseFightBonus(BotComponent bot, Enemy enemy)
    {
        return AgainstPlayer(enemy) ? 6f * Weight(Aggression) * Factor(bot, ECounter.CloseFight) : 0f;
    }

    public static float LeanSpamBonus(BotComponent bot, Enemy enemy)
    {
        return AgainstPlayer(enemy) ? 30f * Weight(LeanPeek) * Factor(bot, ECounter.LeanSpam) : 0f;
    }

    public static float SpacingBonus(BotComponent bot, Enemy enemy)
    {
        return AgainstPlayer(enemy) ? 3f * Weight(Grenade) * Factor(bot, ECounter.Spacing) : 0f;
    }

    /// <summary>
    /// The player (not visible) was just heard running or jumping within 25m: hold the corner he'll come around instead
    /// of moving (3-6s, Freeze decision). Rolled at most every 6s per bot; chance = style x confidence x strength x
    /// personality. Ends at once if the bot is shot at or the player shows up (StandAndShoot is decided before this).
    /// </summary>
    public static bool ShallHoldCorner(BotComponent bot, Enemy enemy, out string reason)
    {
        reason = string.Empty;
        if (!AgainstPlayer(enemy) || enemy.IsVisible)
        {
            return false;
        }
        string id = bot.ProfileId;
        float time = Time.time;
        if (bot.BotOwner.Memory.IsUnderFire || bot.Decision.CurrentSelfDecision != ESelfActionType.None)
        {
            _holdUntil.Remove(id);
            return false;
        }
        if (_holdUntil.TryGetValue(id, out float until))
        {
            if (time < until)
            {
                reason = "adaptHoldCorner";
                return true;
            }
            _holdUntil.Remove(id);
        }
        if (_nextRoll.TryGetValue(id, out float next) && time < next)
        {
            return false;
        }
        var hearing = enemy.Hearing;
        if (hearing == null || time - hearing.LastHeardSoundTime > 2.5f)
        {
            return false;
        }
        var type = hearing.LastHeardSoundType;
        if (type != SAINSoundType.Sprint && type != SAINSoundType.Jump && type != SAINSoundType.Land && type != SAINSoundType.FootStep)
        {
            return false;
        }
        float dist = (hearing.LastHeardSoundPosition - bot.Position).magnitude;
        if (dist > 25f)
        {
            return false;
        }
        _nextRoll[id] = time + 6f;
        float chance = Chance(Mathf.Max(Aggression, BunnyHop)) * Factor(bot, ECounter.HoldCorner) * MovementAdvantage;
        if (UnityEngine.Random.value >= chance)
        {
            TacticDiagnostics.Count("adapt.holdCorner.rollFailed");
            return false;
        }
        float hold = UnityEngine.Random.Range(3f, 6f);
        _holdUntil[id] = time + hold;
        TacticDiagnostics.Count($"adapt.holdCorner.{bot.Info.Personality}");
        TacticDiagnostics.LogCloseCombat(
            $"[Adapt] [{bot.name}] [{bot.Info.Personality}] heard the player ({type}, {dist:0}m) -> hold the approach corner {hold:0.0}s (chance {chance:P0})"
        );
        reason = "adaptHoldCorner";
        return true;
    }
}
