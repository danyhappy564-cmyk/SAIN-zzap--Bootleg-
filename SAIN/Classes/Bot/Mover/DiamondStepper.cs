using SAIN.Components;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.GlobalSettings.Categories.General;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.Classes.Tactics;
using UnityEngine;
using UnityEngine.AI;

namespace SAIN.SAINComponent.Classes.Mover;

/// <summary>
/// zzap fork: diamond step, shared by StandAndShootAction and DogFightAction (2026-09-28 [Death] log: most close-range
/// deaths were in DogFight, whose back-up/strafe points are 1-2m away and crawl because of SAIN's arrival slowdown).
/// F6: General > Close Combat (zzap).
/// </summary>
public sealed class DiamondStepper(BotComponent bot, string owner)
{
    private readonly BotComponent Bot = bot;
    private readonly string _owner = owner;

    // zzap fork: diamond step (user: "tap-dance" - mash A/D, now and then W/S, while shooting).
    // v1 walked to points 0.5-1.2m away via the path system; SAIN's mover slows to ~5% speed inside 0.75m of the
    // destination, so it crept. Now the direction is held directly (like a key press) for a short tap, then switched.
    // The leash keeps it around the spot where the shooting started. Steering keeps aiming the whole time.
    private Vector3 _diamondCenter;
    private bool _diamondActive;
    private Vector3 _tapDir;
    private float _tapEnd;
    private bool _tapPause;
    private bool _lastTapLateralLeft;

    /// <summary>Returns true while it is driving the bot's movement this frame.</summary>
    public bool Tick(Enemy enemy, float minDistance)
    {
        if (Bot.Player?.HealthController?.IsAlive != true)
        {
            _diamondActive = false;
            return false;
        }
        var settings = GlobalSettingsClass.Instance?.General?.CloseCombat;
        if (settings == null || !settings.DiamondStep)
        {
            return Stop("off");
        }
        // Keep dancing through a sub-second loss of sight (enemy ducks behind a frame) instead of freezing in place.
        if (enemy == null || (!enemy.IsVisible && !(enemy.Seen && enemy.TimeSinceSeen < 1f)))
        {
            return Stop("enemyNotVisible");
        }
        if (!settings.DiamondStepTestMode)
        {
            if (settings.PmcOnly && !Bot.Info.Profile.IsPMC)
            {
                return Stop("notPmc");
            }
            EPersonality personality = Bot.Info.Personality;
            if (personality != EPersonality.GigaChad && personality != EPersonality.Chad && personality != EPersonality.Wreckless)
            {
                return Stop("personality");
            }
        }
        float dist = enemy.RealDistance;
        if (dist > settings.DiamondStepMaxDistance)
        {
            return Stop("tooFar");
        }
        if (dist < minDistance)
        {
            return Stop("tooClose");
        }
        if (Bot.Player.IsInPronePose || Bot.BotOwner.Medecine?.Using == true)
        {
            return Stop("proneOrHealing");
        }
        if (!_diamondActive)
        {
            _diamondActive = true;
            _diamondCenter = Bot.Position;
            _tapEnd = 0f;
            Bot.Mover.Stop();
            TacticDiagnostics.SetDiamond(Bot.ProfileId, "active");
            TacticDiagnostics.Count("diamond.start");
            TacticDiagnostics.LogCloseCombat($"[Diamond] [{Bot.name}] [{Bot.Info.Personality}] start ({_owner}): enemy {dist:0}m, tap {settings.DiamondStepTapTime:0.00}s");
        }
        Bot.Mover.SetTargetPose(1f);
        Bot.Mover.SetTargetMoveSpeed(1f);

        Vector3 forward = enemy.EnemyPosition - Bot.Position;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
        {
            return true;
        }
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        if (Time.time >= _tapEnd)
        {
            PickTap(settings, forward, right);
            TacticDiagnostics.Count("diamond.tap");
        }
        if (_tapPause)
        {
            return true;
        }
        // Never walk off a ledge / into a wall mid-tap: if the next 0.5m is blocked, flip the tap.
        if (NavMesh.Raycast(Bot.Position, Bot.Position + _tapDir * 0.5f, out _, -1))
        {
            _tapDir = -_tapDir;
            if (NavMesh.Raycast(Bot.Position, Bot.Position + _tapDir * 0.5f, out _, -1))
            {
                // Both ways blocked (narrow spot): force a new pick next frame instead of standing still.
                _tapEnd = 0f;
                TacticDiagnostics.Count("diamond.blocked");
                return true;
            }
        }
        Bot.PlayerComponent.CharacterController.SetWantToSprint(false);
        // Far-away "destination" so SAIN's arrival slowdown never kicks in during a tap.
        Bot.PlayerComponent.CharacterController.SetTargetMoveDirection(_tapDir, Bot.Position + _tapDir * 5f, Bot.PlayerComponent, 0f, 1f);
        return true;
    }

    private void PickTap(CloseCombatSettings settings, Vector3 forward, Vector3 right)
    {
        float tap = settings.DiamondStepTapTime;
        _tapEnd = Time.time + Random.Range(tap * 0.65f, tap * 1.35f);
        // Very short stop now and then (a real player's rhythm isn't perfectly even).
        _tapPause = Random.value < 0.05f;
        if (_tapPause)
        {
            _tapEnd = Time.time + Random.Range(0.05f, 0.09f);
            return;
        }
        Vector3 offset = Bot.Position - _diamondCenter;
        offset.y = 0f;
        float leash = settings.DiamondStepSize;
        if (offset.magnitude > leash)
        {
            // Too far from the spot: tap back toward it.
            _tapDir = -offset.normalized;
            return;
        }
        if (Random.value < 0.6f)
        {
            // A/D, mostly alternating.
            bool left = Random.value < 0.8f ? !_lastTapLateralLeft : _lastTapLateralLeft;
            _lastTapLateralLeft = left;
            _tapDir = left ? -right : right;
        }
        else
        {
            _tapDir = Random.value < 0.5f ? forward : -forward;
        }
    }

    public bool Stop(string why = "actionStopped")
    {
        if (_diamondActive)
        {
            _diamondActive = false;
            if (Bot.Player?.HealthController?.IsAlive == true)
            {
                Bot.Player.Move(Vector2.zero);
            }
            TacticDiagnostics.LogCloseCombat($"[Diamond] [{Bot.name}] stop: {why}");
        }
        TacticDiagnostics.SetDiamond(Bot.ProfileId, $"off({why})");
        return false;
    }

}
