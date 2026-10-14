using System.Text;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using SAIN.Helpers;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.SubComponents.CoverFinder;
using UnityEngine;

namespace SAIN.Layers.Combat.Solo.Cover;

internal class SeekCoverAction(BotOwner bot) : BotAction(bot, nameof(SeekCoverAction)), IBotAction
{
    public override void Update(CustomLayer.ActionData data)
    {
        Enemy enemy = Bot.GoalEnemy;
        Bot.Cover.UpdateCover(enemy);

        if (Bot.Cover.CoverInUse == null)
        {
            if (!Bot.Mover.Running && !Bot.Mover.ActivePath?.Crawling != true)
            {
                Bot.Mover.Pose.SetPoseToCover(enemy);
            }

            return;
        }

        if (Bot.Info.FileSettings.Move.PRONE_TOGGLE && GlobalSettingsClass.Instance.Move.PRONE_TOGGLE)
        {
            if (
                !Bot.Cover.TrySetProneConditional(enemy)
                && enemy != null
                && enemy.IsVisible
                && Bot.Player.MovementContext.CanProne
                && Bot.Player.PoseLevel <= 0.1
                && BotOwner.WeaponManager.Reload.Reloading
            )
            {
                Bot.Mover.Prone.SetProne(true);
            }
        }
        if (!Bot.Player.IsInPronePose)
        {
            Bot.Mover.Pose.SetPoseToCover(enemy);
        }
    }

    public override void OnSteeringTicked()
    {
        Enemy enemy = Bot.GoalEnemy;
        // zzap (10/14 sim F8 mark #7, user: "walks slowly sideways shooting, then dies"): walking (not sprinting - legs gone)
        // to cover with the gun out of action (reloading / meds in hand), the bot still faced the enemy and went backwards at
        // 1.1 m/s. It can't shoot anyway - face the way it walks (EFT walks forward faster than backward).
        if (CantShootWhileWalkingToCover())
        {
            Bot.Aim.LoseAimTarget();
            Bot.Steering.LookToMovingDirection();
            if (SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.CountOn && Time.time > _nextFaceCount)
            {
                _nextFaceCount = Time.time + 5f;
                SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count("cover.faceMoveWhileBusy");
            }
            return;
        }
        if (!Shoot.ShootAnyVisibleEnemies(enemy) && !Bot.Suppression.TrySuppressAnyEnemy(enemy, Bot.EnemyController.KnownEnemies))
        {
            Bot.Aim.LoseAimTarget();
            Bot.Suppression.ResetSuppressing();
            // zzap (10/14 21:07 sim, oddity backWalk 22 of 24 in SeekCover): walking (legs broken = no sprint, or the run not
            // started yet) with the enemy out of sight 40-200 m away, the bot kept looking at the last known spot and walked
            // backwards for seconds. The close-combat rule (enemy within 30 m seen in the last 4 s -> walk facing it) and
            // fresh fire still keep the eyes on the enemy; anything else faces the walk.
            if (FaceWalkNoThreat(enemy))
            {
                Bot.Steering.LookToMovingDirection();
                if (SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.CountOn && Time.time > _nextFaceCount)
                {
                    _nextFaceCount = Time.time + 5f;
                    SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count("cover.faceMoveNoThreat");
                }
                return;
            }
        }
        if (!Bot.Steering.SteerByPriority(enemy, false))
        {
            Bot.Steering.LookToLastKnownEnemyPosition(enemy);
        }
        checkSetLean();
    }

    private float _nextFaceCount;

    private bool CantShootWhileWalkingToCover()
    {
        var cover = Bot.Cover.CoverInUse;
        if (cover == null || cover.BotInThisCover || Bot.Mover.Running || Bot.Player.Velocity.magnitude < 0.5f)
        {
            return false;
        }
        if ((cover.Position - Bot.Position).sqrMagnitude < 2f * 2f)
        {
            return false;
        }
        return BotOwner.WeaponManager?.Reload?.Reloading == true || BotOwner.Medecine?.Using == true;
    }

    private bool FaceWalkNoThreat(Enemy enemy)
    {
        if (Bot.Mover.Running || !Bot.Mover.Moving || Bot.Player.Velocity.magnitude < 0.5f)
        {
            return false;
        }
        var cover = Bot.Cover.CoverInUse;
        if (cover != null && (cover.BotInThisCover || (cover.Position - Bot.Position).sqrMagnitude < 2f * 2f))
        {
            return false;
        }
        var memory = BotOwner.Memory;
        if (memory != null && memory.UnderFireTime > 0f && Time.time - memory.UnderFireTime < 3f)
        {
            return false;
        }
        if (enemy == null)
        {
            return true;
        }
        if (enemy.IsVisible)
        {
            return false;
        }
        var close = GlobalSettingsClass.Instance?.General?.CloseCombat;
        float seenWithin = close?.SeenWithin ?? 4f;
        float distance = close?.Distance ?? 30f;
        return !(enemy.Seen
            && enemy.TimeSinceSeen < seenWithin
            && enemy.KnownPlaces.LastKnownPosition is Vector3 known
            && (known - Bot.Position).sqrMagnitude < distance * distance);
    }

    private void checkSetLean()
    {
        if (
            !Bot.Info.FileSettings.Move.LEAN_INCOVER_TOGGLE
            || !GlobalSettingsClass.Instance.Move.LEAN_INCOVER_TOGGLE
            || Bot.Suppression.IsSuppressed
            || Bot.Decision.CurrentSelfDecision != ESelfActionType.None
        )
        {
            Bot.Mover.Lean.FastLean(LeanSetting.None);
            CurrentLean = LeanSetting.None;
            return;
        }

        if (CurrentLean != LeanSetting.None && ShallHoldLean())
        {
            Bot.Mover.Lean.FastLean(CurrentLean);
            ChangeLeanTimer = Time.time + 0.66f;
            return;
        }

        if (ChangeLeanTimer < Time.time)
        {
            setLean();
        }
    }

    private void setLean()
    {
        LeanSetting newLean;
        switch (CurrentLean)
        {
            case LeanSetting.Left:
            case LeanSetting.Right:
                newLean = LeanSetting.None;
                ChangeLeanTimer = Time.time + Random.Range(0.75f, 4f);
                break;

            default:
                newLean = Bot.Mover.Lean.FindLeanFromBlindCornerAngle(Bot.GoalEnemy);
                //if (newLean == LeanSetting.None)
                //{
                //    newLean = EFTMath.RandomBool() ? LeanSetting.Left : LeanSetting.Right;
                //}
                ChangeLeanTimer = Time.time + Random.Range(0.5f, 2f);
                break;
        }

        if (checkLeanIntoObject(newLean))
        {
            return;
        }
        CurrentLean = newLean;
        Bot.Mover.Lean.FastLean(newLean);
    }

    private const float RAYCAST_LEAN_HITOBJECT_DIST = 0.5f;

    private bool checkLeanIntoObject(LeanSetting lean)
    {
        Vector3 headPos = Bot.Transform.EyePosition;
        Vector3 rayEnd = lean == LeanSetting.Right ? Bot.Transform.Right() : Bot.Transform.Left();
        switch (lean)
        {
            case LeanSetting.Right:
            case LeanSetting.Left:
                return Vector.Raycast(headPos, rayEnd * RAYCAST_LEAN_HITOBJECT_DIST, LayersMaskController.HighPolyWithTerrainMask);

            default:
                return false;
        }
    }

    private bool ShallHoldLean()
    {
        if (Bot.Suppression.IsSuppressed)
        {
            return false;
        }
        Enemy enemy = Bot.GoalEnemy;
        if (enemy == null || !enemy.Seen)
        {
            return false;
        }
        if (enemy.IsVisible && enemy.CanShoot)
        {
            return true;
        }
        if (enemy.TimeSinceSeen < 3f)
        {
            return true;
        }
        return false;
    }

    private LeanSetting CurrentLean;
    private float ChangeLeanTimer;

    public override void Start()
    {
        base.Start();
        ChangeLeanTimer = Time.time + 2f;
        Bot.Mover.SetTargetMoveSpeed(1f);
    }

    public override void Stop()
    {
        base.Stop();
        Bot.Mover.DogFight.ResetDogFightStatus();
        Bot.Suppression.ResetSuppressing();
        Bot.Cover.StopSeekingCover();
    }

    public override void BuildDebugText(StringBuilder stringBuilder)
    {
        stringBuilder.AppendLine("Cover Info");
        if (Bot == null)
        {
            return;
        }

        var cover = Bot.Cover;
        stringBuilder.AppendLabeledValue("Cover Seeking State", $"{cover.CoverSeekingState}", Color.white, Color.yellow, true);
        stringBuilder.AppendLabeledValue("Running To Cover", $"{cover.SprintingToCover}", Color.white, Color.yellow, true);
        //stringBuilder.AppendLabeledValue("CoverFinder State", $"{cover.CurrentCoverFinderState}", Color.white, Color.yellow, true);
        stringBuilder.AppendLabeledValue("Cover Count", $"{cover.CoverPoints.Count}", Color.white, Color.yellow, true);
        DebugOverlay.AddMoveData(Bot, stringBuilder);
        CoverPoint point = Bot.Cover.CoverInUse;
        if (point != null)
        {
            stringBuilder.AppendLine($"Holding In Cover [{point.HardData.Id}]");
            stringBuilder.AppendLabeledValue("Path Length Status", $"{point.PathDistanceStatus}", Color.white, Color.yellow, true);
            stringBuilder.AppendLabeledValue(
                "Straight Distance Status",
                $"{point.StraightDistanceStatus}",
                Color.white,
                Color.yellow,
                true
            );
            stringBuilder.AppendLabeledValue(
                "Height / Value",
                $"{point.CoverHeight} {point.HardData.Value}",
                Color.white,
                Color.yellow,
                true
            );
            stringBuilder.AppendLabeledValue("Path Length", $"{point.PathData.PathLength}", Color.white, Color.yellow, true);
            stringBuilder.AppendLabeledValue(
                "Distance",
                $"{point.GetDistance(Bot.Transform.NavData.Position)}",
                Color.white,
                Color.yellow,
                true
            );
        }
        point = Bot.Cover.CoverPoint_MovingTo;
        if (point != null)
        {
            stringBuilder.AppendLine($"Moving To Cover [{point.HardData.Id}]");
            stringBuilder.AppendLabeledValue("Path Length Status", $"{point.PathDistanceStatus}", Color.white, Color.yellow, true);
            stringBuilder.AppendLabeledValue("Status", $"{point.StraightDistanceStatus}", Color.white, Color.yellow, true);
            stringBuilder.AppendLabeledValue(
                "Height / Value",
                $"{point.CoverHeight} {point.HardData.Value}",
                Color.white,
                Color.yellow,
                true
            );
            stringBuilder.AppendLabeledValue("Path Length", $"{point.PathData.PathLength}", Color.white, Color.yellow, true);
            stringBuilder.AppendLabeledValue(
                "Distance",
                $"{point.GetDistance(Bot.Transform.NavData.Position)}",
                Color.white,
                Color.yellow,
                true
            );
        }
    }
}
