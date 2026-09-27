using DrakiaXYZ.BigBrain.Brains;
using EFT;
using SAIN.Helpers;
using SAIN.Preset.Shared.GlobalSettings;
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

    // zzap fork: diamond step (user's definition: while shooting, keep stepping left/right/forward/back).
    // Vertices of a small diamond around the spot where the bot started shooting; mostly lateral (ADAD),
    // sometimes a W/S step. Walk (not sprint) so the gun stays up and on target; steering keeps aiming.
    private Vector3 _diamondCenter;
    private bool _diamondActive;
    private int _diamondVertex = -1;
    private float _nextDiamondStep;

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
            _diamondVertex = -1;
            _nextDiamondStep = 0f;
        }
        Bot.Mover.SetTargetPose(1f);
        Bot.Mover.SetTargetMoveSpeed(1f);
        if (Time.time < _nextDiamondStep)
        {
            return true;
        }
        _nextDiamondStep = Time.time + Random.Range(0.3f, 0.55f);

        Vector3 forward = enemy.EnemyPosition - _diamondCenter;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
        {
            return true;
        }
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        float size = settings.DiamondStepSize;
        // 0 = left, 1 = right, 2 = forward, 3 = back. Mostly the other side (ADAD), 30% a W/S step.
        int next;
        if (_diamondVertex == 0 || _diamondVertex == 1)
        {
            next = Random.value < 0.7f ? 1 - _diamondVertex : (Random.value < 0.5f ? 2 : 3);
        }
        else
        {
            next = Random.value < 0.5f ? 0 : 1;
        }
        for (int attempt = 0; attempt < 4; attempt++)
        {
            int vertex = (next + attempt) % 4;
            Vector3 offset = vertex switch
            {
                0 => -right * size,
                1 => right * size,
                2 => forward * size * 0.6f,
                _ => -forward * size * 0.6f,
            };
            if (!NavMesh.SamplePosition(_diamondCenter + offset, out NavMeshHit hit, 0.5f, -1))
            {
                continue;
            }
            if (Mathf.Abs(hit.position.y - Bot.Position.y) > 0.4f || NavMesh.Raycast(Bot.Position, hit.position, out _, -1))
            {
                continue;
            }
            if (Bot.Mover.WalkToPoint(hit.position, false, 0.3f))
            {
                _diamondVertex = vertex;
                return true;
            }
        }
        return true;
    }

    private bool StopDiamond()
    {
        _diamondActive = false;
        return false;
    }

    private bool shallMoveShoot = false;

    public override void Start()
    {
        const float STAND_AND_SHOOT_HOLDLEAN_DURATION = 0.66f;
        base.Start();
        shallMoveShoot = moveShoot(Bot.GoalEnemy);
        if (!shallMoveShoot)
        {
            Bot.Mover.Stop();
        }
        Bot.Mover.Lean.HoldLean(STAND_AND_SHOOT_HOLDLEAN_DURATION);
    }

    private bool moveShoot(Enemy enemy)
    {
        if (Bot.Player.IsInPronePose)
        {
            return false;
        }
        if (FindSwingMovePosition(Bot.Transform.NavData, enemy, out Vector3 movePosition))
        {
            return Bot.Mover.WalkToPoint(movePosition, false);
        }
        return false;
    }

    private static bool FindSwingMovePosition(PlayerNavData navData, Enemy enemy, out Vector3 movePosition)
    {
        movePosition = Vector3.zero;
        if (enemy != null && navData.IsOnNavMesh && enemy.RealDistance < 50)
        {
            float angle = UnityEngine.Random.Range(70, 110);
            if (EFTMath.RandomBool())
            {
                angle *= -1;
            }

            Vector3 directionToEnemy = enemy.EnemyDirection.normalized;
            Vector3 rotated = Vector.Rotate(directionToEnemy, 0, angle, 0);
            rotated.y = 0;
            rotated *= 6f;
            rotated += Random.insideUnitSphere;
            if (NavMesh.SamplePosition(navData.Position + rotated, out var hit, 3f, -1))
            {
                movePosition = hit.position;
                if (NavMesh.Raycast(navData.Position, movePosition, out var rayHit, -1))
                {
                    movePosition = rayHit.position;
                }
                if ((movePosition - navData.Position).sqrMagnitude > 0.75f)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public override void Stop()
    {
        base.Stop();
        StopDiamond();
    }
}
