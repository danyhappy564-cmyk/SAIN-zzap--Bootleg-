using EFT;
using SAIN.Components;
using SAIN.Preset.Shared.Enums;
using UnityEngine;

namespace SAIN.SAINComponent.Classes;

public class SAINFriendlyFireClass : BotComponentClassBase
{
    public bool ClearShot
    {
        get { return FriendlyFireStatus != FriendlyFireStatus.FriendlyBlock; }
    }

    public FriendlyFireStatus FriendlyFireStatus { get; private set; }

    public SAINFriendlyFireClass(BotComponent sain)
        : base(sain)
    {
        TickRequirement = ESAINTickState.OnlyBotInCombat;
    }

    public override void ManualUpdate()
    {
        // zzap: re-check every tick while the trigger is down - a teammate running into the line mid-burst used to get
        // the rest of the burst (the check only ran when the aim updated).
        if (BotOwner.ShootData?.Shooting == true && Bot.Squad?.Members?.Count > 1)
        {
            float dist = BotOwner.AimingManager?.CurrentAiming?.LastDist2Target ?? 50f;
            UpdateFriendlyFireStatus(Mathf.Max(dist, 5f), Bot.Transform.WeaponData.FirePort, Bot.Transform.WeaponData.PointDirection, Bot);
        }
        if (FriendlyFireStatus == FriendlyFireStatus.FriendlyBlock)
        {
            BotOwner.ShootData?.EndShoot();
        }
        base.ManualUpdate();
    }

    public bool UpdateFriendlyFireStatus(Vector3 target, Vector3 weaponFirePort, Vector3 weaponPointDirection, BotComponent bot)
    {
        FriendlyFireStatus = CheckFriendlyFireStatus(target, weaponFirePort, weaponPointDirection, bot);
        return FriendlyFireStatus != FriendlyFireStatus.FriendlyBlock;
    }

    public bool UpdateFriendlyFireStatus(float distance, Vector3 weaponFirePort, Vector3 weaponPointDirection, BotComponent bot)
    {
        FriendlyFireStatus = CheckFriendlyFireStatus(distance, weaponFirePort, weaponPointDirection, bot);
        return FriendlyFireStatus != FriendlyFireStatus.FriendlyBlock;
    }

    public static FriendlyFireStatus CheckFriendlyFireStatus(
        float distance,
        Vector3 weaponFirePort,
        Vector3 weaponPointDirection,
        BotComponent bot
    )
    {
        var members = bot.Squad?.Members;
        if (members == null || members.Count <= 1)
        {
            return FriendlyFireStatus.None;
        }
        return CheckFriendlyFire(weaponFirePort, distance, weaponPointDirection, bot);
    }

    public static FriendlyFireStatus CheckFriendlyFireStatus(
        Vector3 target,
        Vector3 weaponFirePort,
        Vector3 weaponPointDirection,
        BotComponent bot
    )
    {
        var members = bot.Squad?.Members;
        if (members == null || members.Count <= 1)
        {
            return FriendlyFireStatus.None;
        }
        return CheckFriendlyFire(weaponFirePort, (weaponFirePort - target).magnitude, weaponPointDirection, bot);
    }

    public static FriendlyFireStatus CheckFriendlyFire(
        Vector3 weaponFirePort,
        float distance,
        Vector3 weaponPointDirection,
        BotComponent bot
    )
    {
        // zzap: squadmates about to cross the line (where they'll be in 0.3s) block too, not only bodies touching the ray.
        if (TeammateCrossing(weaponFirePort, distance, weaponPointDirection, bot))
        {
            return FriendlyFireStatus.FriendlyBlock;
        }
        int count = SphereCastNonAlloc(weaponFirePort, distance, weaponPointDirection);
        if (count == 0)
        {
            return FriendlyFireStatus.None;
        }
        RaycastHit[] hits = _sphereCastHits;

        for (int i = 0; i < count; i++)
        {
            var hit = hits[i];
            if (hit.collider == null)
            {
                continue;
            }

            Player player = GameWorldComponent.Instance.GameWorld.GetPlayerByCollider(hit.collider);
            if (player == null)
            {
                continue;
            }

            if (player.ProfileId == bot.ProfileId)
            {
                continue;
            }

            if (!bot.EnemyController.IsPlayerAnEnemy(player.ProfileId))
            {
                return FriendlyFireStatus.FriendlyBlock;
            }
        }
        return FriendlyFireStatus.Clear;
    }

    private static bool TeammateCrossing(Vector3 firePort, float distance, Vector3 direction, BotComponent bot)
    {
        var members = bot.Squad?.Members;
        if (members == null || direction.sqrMagnitude < 0.0001f)
        {
            return false;
        }
        Vector3 dir = direction.normalized;
        foreach (var member in members.Values)
        {
            if (member == null || ReferenceEquals(member, bot) || member.IsDead || member.Player == null)
            {
                continue;
            }
            Vector3 predicted = member.Position + member.Player.Velocity * 0.3f;
            foreach (float height in _bodyHeights)
            {
                Vector3 p = predicted + Vector3.up * height;
                float along = Vector3.Dot(p - firePort, dir);
                if (along < 0.3f || along > distance)
                {
                    continue;
                }
                float off = (p - (firePort + dir * along)).magnitude;
                if (off < 0.55f)
                {
                    SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count("squad.fireLane.shooterHeld");
                    return true;
                }
            }
        }
        return false;
    }

    private static readonly float[] _bodyHeights = [0.5f, 1.0f, 1.5f];

    private static readonly RaycastHit[] _sphereCastHits = new RaycastHit[32];

    private static int SphereCastNonAlloc(Vector3 weaponFirePort, float targetDistance, Vector3 weaponPointDirection)
    {
        const float sphereCastRadius = 0.2f;
        return Physics.SphereCastNonAlloc(
            weaponFirePort,
            sphereCastRadius,
            weaponPointDirection,
            _sphereCastHits,
            targetDistance,
            LayersMaskController.PlayerMask
        );
    }
}
