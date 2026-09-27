using DrakiaXYZ.BigBrain.Brains;
using EFT;
using SAIN.Helpers;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.GlobalSettings.Categories.General;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SAIN.Models.PlayerData;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.Classes.Mover;
using SAIN.SAINComponent.Classes.Tactics;
using UnityEngine;
using UnityEngine.AI;

namespace SAIN.Layers.Combat.Solo;

public class StandAndShootAction(BotOwner bot) : BotAction(bot, nameof(StandAndShootAction)), IBotAction
{
    public override void Update(CustomLayer.ActionData data)
    {
        Enemy enemy = Bot.GoalEnemy;
        if (_diamond != null && _diamond.Tick(enemy, 3f))
        {
            return;
        }
        //if (!shallMoveShoot)
        //{
        Bot.Mover.Pose.SetPoseToCover(enemy);
        //}
    }

    private bool shallMoveShoot = false;
    private DiamondStepper _diamond;

    public override void Start()
    {
        const float STAND_AND_SHOOT_HOLDLEAN_DURATION = 0.66f;
        base.Start();
        _diamond ??= new DiamondStepper(Bot, "StandAndShoot");
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
        _diamond?.Stop();
    }
}
