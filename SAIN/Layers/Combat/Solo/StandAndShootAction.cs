using DrakiaXYZ.BigBrain.Brains;
using EFT;
using SAIN.Helpers;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.GlobalSettings.Categories.General;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SAIN.Models.PlayerData;
using SAIN.SAINComponent.Classes.EnemyClasses;
using UnityEngine;
using UnityEngine.AI;

namespace SAIN.Layers.Combat.Solo;

public class StandAndShootAction(BotOwner bot) : BotAction(bot, nameof(StandAndShootAction)), IBotAction
{
    public override void Update(CustomLayer.ActionData data)
    {
        Enemy enemy = Bot.GoalEnemy;
        if (DiamondStep(enemy))
        {
            return;
        }
        //if (!shallMoveShoot)
        //{
        Bot.Mover.Pose.SetPoseToCover(enemy);
        //}
    }

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

    private bool DiamondStep(Enemy enemy)
    {
        var settings = GlobalSettingsClass.Instance?.General?.CloseCombat;
        if (settings == null || !settings.DiamondStep || enemy == null || !enemy.IsVisible)
        {
            return StopDiamond();
        }
        if (!settings.DiamondStepTestMode)
        {
            if (settings.PmcOnly && !Bot.Info.Profile.IsPMC)
            {
                return StopDiamond();
            }
            EPersonality personality = Bot.Info.Personality;
            if (personality != EPersonality.GigaChad && personality != EPersonality.Chad && personality != EPersonality.Wreckless)
            {
                return StopDiamond();
            }
        }
        float dist = enemy.RealDistance;
        if (dist > settings.DiamondStepMaxDistance || dist < 3f || Bot.Player.IsInPronePose || BotOwner.Medecine?.Using == true)
        {
            return StopDiamond();
        }
        if (!_diamondActive)
        {
            _diamondActive = true;
            _diamondCenter = Bot.Position;
            _tapEnd = 0f;
            Bot.Mover.Stop();
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
        // Short stop now and then (a real player's rhythm isn't perfectly even).
        _tapPause = Random.value < 0.1f;
        if (_tapPause)
        {
            _tapEnd = Time.time + Random.Range(0.06f, 0.12f);
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
        if (Random.value < 0.75f)
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

    private bool StopDiamond()
    {
        if (_diamondActive)
        {
            _diamondActive = false;
            Bot.Player?.Move(Vector2.zero);
        }
        return false;
    }

    public override void Stop()
    {
        base.Stop();
        StopDiamond();
    }
}
