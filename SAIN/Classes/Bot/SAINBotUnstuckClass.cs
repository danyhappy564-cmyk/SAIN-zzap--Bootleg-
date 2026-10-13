using System.Collections;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using SAIN.Components;
using SAIN.Preset.Shared.GlobalSettings;
using UnityEngine;
using UnityEngine.AI;

namespace SAIN.SAINComponent.Classes.Debug;

public class SAINBotUnstuckClass : BotComponentClassBase
{
    public SAINBotUnstuckClass(BotComponent sain)
        : base(sain)
    {
        TickRequirement = ESAINTickState.OnlyBotActive;
    }

    public override void Init()
    {
        PathController = BotOwner.Mover.ActualPathController;
        DontUnstuckMe = DontUnstuckTheseTypes.Contains(Bot.Info.Profile.WildSpawnType);
        base.Init();
    }

    private void CheckIfPositionChanged()
    {
        if (_checkPositionTimer < Time.time)
        {
            _checkPositionTimer = Time.time + 0.5f;

            bool botChangedPositionLast = BotHasChangedPosition;

            const float DistThreshold = 0.1f;
            BotHasChangedPosition = (_lastPos - Bot.Position).sqrMagnitude > DistThreshold * DistThreshold;

            if (botChangedPositionLast && !BotHasChangedPosition)
            {
                TimeStartedChangingPosition = Time.time;
            }
            else if (BotHasChangedPosition)
            {
                TimeStartedChangingPosition = 0f;
            }

            _lastPos = Bot.Position;
        }
    }

    private void Teleport(Vector3 position)
    {
        Player.Teleport(position + Vector3.up * 0.25f);
#if DEBUG
        if (SAINPlugin.DebugMode)
        {
            Logger.LogDebug(
                $"{BotOwner.name} has teleported because they were stuck after vaulting, and no human players are visible to them, and no human players are close."
            );
        }
#endif
        BotOwner.Mover?.Stop();
        BotOwner.Mover?.RecalcWay();
    }

    private bool IsHumanVisible()
    {
        return Bot.EnemyController.HumanEnemyInLineofSight;
    }

    private bool IsHumanClose()
    {
        bool closeHuman = false;
        foreach (var player in Singleton<GameWorld>.Instance.AllAlivePlayersList)
        {
            if (
                player != null
                && !player.IsAI
                && player.HealthController.IsAlive
                && (player.Position - Bot.Position).sqrMagnitude < 50f * 50f
            )
            {
                closeHuman = true;
                break;
            }
        }
        return closeHuman;
    }

    private bool _botStuckAfterVault;

    private bool TryVault()
    {
        if (!Bot.Info.FileSettings.Move.VAULT_UNSTUCK_TOGGLE || !GlobalSettingsClass.Instance.Move.VAULT_UNSTUCK_TOGGLE)
        {
            return false;
        }
        if (Bot.Mover.TryVault())
        {
            _botVaultedTime = Time.time;
            return true;
        }
        return false;
    }

    private float _nextVaultCheckTime;
    private bool DontUnstuckMe;

    private static readonly List<WildSpawnType> DontUnstuckTheseTypes = new() { WildSpawnType.marksman, WildSpawnType.shooterBTR };

    private void checkResetPathFromVault()
    {
        if (_botVaulted && !_botStuckAfterVault && _botVaultedTime + 1f < Time.time)
        {
            _botVaulted = false;
            Bot.Mover.RecalcPath();
        }
    }

    private bool _botVaulted;
    private float _botVaultedTime;

    private void TryAutoVault()
    {
        if (!Bot.Info.FileSettings.Move.VAULT_TOGGLE || !GlobalSettingsClass.Instance.Move.VAULT_TOGGLE)
        {
            return;
        }
        if (_nextVaultCheckTime < Time.time && (BotOwner?.Mover?.IsMoving == true || Bot.Mover.Moving))
        {
            float timeAdd;
            Vector3 lookDir = Player.LookDirection.normalized;
            Vector3 targetDir = BotOwner.Mover.NormDirCurPoint;
            if (Vector3.Dot(lookDir, targetDir) > 0.85f && TryVault())
            {
                _botVaulted = true;
                timeAdd = 2f;
            }
            else
            {
                timeAdd = 0.5f;
            }
            _nextVaultCheckTime = Time.time + timeAdd;
        }
    }

    public override void ManualUpdate()
    {
        if (!DontUnstuckMe && !Bot.BotActivation.BotInStandBy)
        {
            Watchdog();
            GrenadeHandsWatchdog();
        }
        if (!DontUnstuckMe && !Bot.BotActivation.BotInStandBy)
        {
            StartCoroutine();
        }
        else if (_botUnstuckCoroutine != null)
        {
            Bot.StopCoroutine(_botUnstuckCoroutine);
        }
        base.ManualUpdate();
    }

    private void StartCoroutine()
    {
        if (_botUnstuckCoroutine == null)
        {
            _botUnstuckCoroutine = Bot.StartCoroutine(BotUnstuck());
        }
    }

    private Coroutine _botUnstuckCoroutine;

    private IEnumerator BotUnstuck()
    {
        while (true)
        {
            //if (Bot.BotActive
            //&& !Bot.GameEnding)
            //{
            //    tryAutoVault();
            //    checkResetPathFromVault();
            //}
            yield return null;
        }
    }

    // ---- zzap stuck watchdog ------------------------------------------------------------------------------------------
    // SAIN's own unstuck is switched off upstream (the coroutine body above is commented out, BotIsStuck is never set), so
    // a bot walking into a wall in ANY layer (ORBIT patrol, BSG exfil/patrol gap layers, SAIN search...) stayed there until an
    // enemy came close and the combat layer gave it a new path (user report, 3rd time 2026-09-29). Whatever layer is
    // driving: the mover has a path but the bot hasn't moved 0.6m in 3s -> recalc path, 5s -> vault/jump, 8s -> hop to the
    // next path corner if no human is close or sees it, 14s -> stop the mover so the layer plans again. [Stuck] logs
    // each step with the layer name.

    private Vector3 _wdAnchor;
    private float _wdAnchorTime = -1f;
    private int _wdStage;
    private float _wdNextCheck;
    private bool _wdUnjamTried;
    private bool _wdLedgeTried;

    private void Watchdog()
    {
        float now = Time.time;
        if (now < _wdNextCheck || Player == null || BotOwner?.Mover == null)
        {
            return;
        }
        _wdNextCheck = now + 0.5f;
        var mover = BotOwner.Mover;
        bool wantsMove = (mover.IsMoving && mover.HasPathAndNoComplete) || Bot.Mover.Moving;
        Vector3 pos = Bot.Position;
        if (!wantsMove || !Player.HealthController.IsAlive || Player.IsInPronePose)
        {
            ResetWatchdog(pos, now, "stoppedMoving");
            return;
        }
        if (_wdAnchorTime < 0f)
        {
            ResetWatchdog(pos, now, null);
            return;
        }
        Vector3 d = pos - _wdAnchor;
        d.y = 0f;
        if (d.sqrMagnitude > 0.6f * 0.6f)
        {
            ResetWatchdog(pos, now, "moving again");
            return;
        }
        float stuckFor = now - _wdAnchorTime;
        // 10/14 sim: a door leaf left at the reversed angle across the passage held every bot there for the rest of the
        // raid - recalc / jump / hop never cleared it. Push it back first (once per stop).
        if (stuckFor >= 1.5f && !_wdUnjamTried)
        {
            _wdUnjamTried = true;
            bool pushed = SAIN.SAINComponent.Classes.Mover.ReversedLeafUnjam.TryUnjam(Player, out string what);
            if (what != null && SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.LogOn)
            {
                SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.LogCloseCombat(
                    $"[Stuck] [{Bot.name}] no progress {stuckFor:0.0}s by a reversed door leaf -> {(pushed ? what : $"no push: {what}")} "
                        + $"| layer {BotOwner.Brain?.ActiveLayerName()} | at ({Bot.Position.x:0},{Bot.Position.z:0})");
            }
        }
        // 10/14 sim F8 mark #6 (user: "hits a ledge while running, jumps a bit late"): the vault/jump stage waited 5 s even
        // when a knee-high edge was right in front. Knee-high hit within 0.7 m with chest height clear -> vault/jump at 1 s.
        if (stuckFor >= 1f && _wdStage == 0 && !_wdLedgeTried && LowLedgeAhead(pos))
        {
            _wdLedgeTried = true;
            bool vaulted = Bot.Mover.TryVault();
            bool jumped = !vaulted && Bot.Mover.TryJump();
            if (SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.CountOn)
            {
                SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count(vaulted ? "stuck.ledge.vault" : jumped ? "stuck.ledge.jump" : "stuck.ledge.cant");
            }
            if (SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.LogOn)
            {
                SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.LogCloseCombat(
                    $"[Stuck] [{Bot.name}] no progress {stuckFor:0.0}s at a knee-high edge -> {(vaulted ? "vault" : jumped ? "jump" : "vault/jump not possible")} early "
                        + $"| layer {BotOwner.Brain?.ActiveLayerName()} | at ({pos.x:0},{pos.z:0})");
            }
        }
        if (stuckFor < 3f)
        {
            return;
        }
        if (_wdStage == 0)
        {
            _wdStage = 1;
            Bot.Mover.RecalcPath();
            mover.RecalcWay();
            LogStuck("recalc path", stuckFor);
        }
        else if (_wdStage == 1 && stuckFor >= 5f)
        {
            _wdStage = 2;
            bool vaulted = Bot.Mover.TryVault();
            bool jumped = !vaulted && Bot.Mover.TryJump();
            LogStuck(vaulted ? "vault" : jumped ? "jump" : "vault/jump not possible", stuckFor);
        }
        else if (_wdStage == 2 && stuckFor >= 8f)
        {
            _wdStage = 3;
            if (IsHumanClose() || IsHumanVisible())
            {
                LogStuck("human near/looking - no hop", stuckFor);
            }
            else if (NextCorner(pos, out Vector3 corner))
            {
                Player.Teleport(corner + Vector3.up * 0.25f);
                mover.RecalcWay();
                LogStuck($"hopped {(corner - pos).magnitude:0.0}m to the next path corner", stuckFor);
            }
            else
            {
                LogStuck("no path corner to hop to", stuckFor);
            }
        }
        else if (_wdStage == 3 && stuckFor >= 14f)
        {
            _wdStage = 4;
            mover.Stop();
            Bot.Mover.Stop();
            LogStuck("still stuck - stopped the mover so the layer plans again", stuckFor);
        }
    }

    // zzap (10/14 sim F8 mark #12, user: "suddenly an arm out, then the gun pulled out in a hurry and the muzzle stuck to the
    // sky"): the bot picked a grenade throw, the decision changed 1.8 s later (rush / dog fight) and the grenade was never
    // thrown - EFT only puts the gun back after a throw (BotGrenadeController.EndAll). DoorTactic had its own fix
    // (ForceRestoreWeapon); this one covers every layer: a grenade in hand for 2 s with no throw going on -> previous weapon.
    private float _grenadeHandsSince = -1f;
    private float _nextGrenadeHandsCheck;

    private void GrenadeHandsWatchdog()
    {
        float now = Time.time;
        if (now < _nextGrenadeHandsCheck || Player == null)
        {
            return;
        }
        _nextGrenadeHandsCheck = now + 0.5f;
        var wm = BotOwner?.WeaponManager;
        if (Player.HandsController is not IGrenadeController || wm?.Grenades == null || wm.Grenades.ThrowindNow
            || Bot.Decision.CurrentCombatDecision == SAIN.Preset.Shared.Enums.ECombatDecision.ThrowGrenade
            // DoorTactic shows a grenade on purpose (fake-nade bait) and restores the gun itself
            || Bot.Decision.CurrentCombatDecision == SAIN.Preset.Shared.Enums.ECombatDecision.DoorTactic)
        {
            _grenadeHandsSince = -1f;
            return;
        }
        if (_grenadeHandsSince < 0f)
        {
            _grenadeHandsSince = now;
            return;
        }
        if (now - _grenadeHandsSince < 2f || wm.Selector == null || wm.Selector.IsChanging)
        {
            return;
        }
        _grenadeHandsSince = now; // next try in 2 s at the earliest
        bool ok = wm.Selector.TakePrevWeapon();
        if (SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.CountOn)
        {
            SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count(ok ? "hands.grenadePutAway" : "hands.grenadePutAwayRefused");
        }
        if (SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.LogOn)
        {
            SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.LogCloseCombat(
                $"[Hands] [{Bot.name}] grenade in hand 2s+ with no throw (decision {Bot.Decision.CurrentCombatDecision}) -> TakePrevWeapon={ok}");
        }
    }

    private void ResetWatchdog(Vector3 pos, float now, string why)
    {
        if (_wdStage > 0 && why != null)
        {
            // 04:55 sim: "stoppedMoving" is the bot giving up the move (or dying / going prone), not getting free - counted
            // apart so the analyzer's freed-vs-stuck comparison is honest.
            if (SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.CountOn) SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count(
                why == "stoppedMoving" ? $"stuck.gaveUp.stage{_wdStage}" : $"stuck.freed.stage{_wdStage}");
            if (SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.LogOn) SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.LogCloseCombat(
                $"[Stuck] [{Bot.name}] freed after stage {_wdStage} ({why}) in layer {BotOwner.Brain?.ActiveLayerName()}"
            );
        }
        _wdAnchor = pos;
        _wdAnchorTime = now;
        _wdStage = 0;
        _wdUnjamTried = false;
        _wdLedgeTried = false;
    }

    private void LogStuck(string action, float stuckFor)
    {
        if (!SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.LogOn && !SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.CountOn)
        {
            return;
        }
        string layer = BotOwner.Brain?.ActiveLayerName() ?? "?";
        if (SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.CountOn) SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count($"stuck.stage{_wdStage}");
        if (SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.CountOn) SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count($"stuck.layer.{layer}");
        bool wallAhead = Physics.Raycast(Bot.Position + Vector3.up * 1.2f, Player.LookDirection, 0.8f, LayersMaskController.HighPolyWithTerrainMask);
        var mover = BotOwner.Mover;
        if (SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.LogOn) SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.LogCloseCombat(
            $"[Stuck] [{Bot.name}] [{Bot.Info.Personality}] no progress {stuckFor:0}s -> {action} | layer {layer} | combat={Bot.Decision.CurrentCombatDecision} "
                + $"squad={Bot.Decision.CurrentSquadDecision} | {mover.DistDestination:0}m to go, wall ahead={wallAhead} | at ({Bot.Position.x:0},{Bot.Position.z:0})"
        );
    }

    /// <summary>Something solid at knee height within 0.7 m toward the next path point (or the look direction), nothing at
    /// chest height - a step / curb / low pipe the bot runs into.</summary>
    private bool LowLedgeAhead(Vector3 pos)
    {
        Vector3 dir = Player.LookDirection;
        var path = BotOwner.Mover.ActualPathController?.CurPath;
        if (path != null && path.CurIndex < path.Length)
        {
            Vector3 to = path.GetPoint(path.CurIndex) - pos;
            to.y = 0f;
            if (to.sqrMagnitude > 0.04f)
            {
                dir = to;
            }
        }
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f)
        {
            return false;
        }
        dir.Normalize();
        int mask = LayersMaskController.HighPolyWithTerrainMask;
        return Physics.Raycast(pos + Vector3.up * 0.4f, dir, 0.7f, mask)
            && !Physics.Raycast(pos + Vector3.up * 1.3f, dir, 1.0f, mask);
    }

    private bool NextCorner(Vector3 pos, out Vector3 corner)
    {
        corner = default;
        var path = BotOwner.Mover.ActualPathController?.CurPath;
        if (path == null)
        {
            return false;
        }
        for (int i = path.CurIndex; i < path.Length; i++)
        {
            Vector3 p = path.GetPoint(i);
            float dist = (p - pos).magnitude;
            if (dist >= 1f && dist <= 4f && NavMesh.SamplePosition(p, out NavMeshHit hit, 0.5f, -1))
            {
                // The path can bend around the very wall/door the bot is stuck on - never hop through anything solid
                // (a closed door, a thin wall): the straight line to the corner must be clear at knee and chest height.
                if (Physics.Linecast(pos + Vector3.up * 0.5f, hit.position + Vector3.up * 0.5f, LayersMaskController.HighPolyWithTerrainMask)
                    || Physics.Linecast(pos + Vector3.up * 1.2f, hit.position + Vector3.up * 1.2f, LayersMaskController.HighPolyWithTerrainMask))
                {
                    SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count("stuck.hopBlocked");
                    continue;
                }
                corner = hit.position;
                return true;
            }
            if (dist > 4f)
            {
                break;
            }
        }
        return false;
    }

    private const float _minDistance = 100f;
    private const float _maxDistance = 300f;
    private const float _pathLengthCoef = 1.25f;
    private const float _minDistancePathLength = _minDistance * _pathLengthCoef;

    //private Coroutine TeleportCoroutine;

    private IEnumerator CheckIfTeleport()
    {
        bool shallTeleport = true;
        var humanPlayers = GetHumanPlayers();

        Vector3? teleportDestination = null;

        const float minTeleDist = 1f;
        if (BotOwner.Mover.HasPathAndNoComplete)
        {
            for (int i = PathController.CurPath.CurIndex; i < PathController.CurPath.Length - 1; i++)
            {
                Vector3 corner = PathController.CurPath.GetPoint(i);
                Vector3 cornerDirection = corner - Bot.Position;
                float cornerDistance = cornerDirection.sqrMagnitude;
                if (cornerDirection.sqrMagnitude >= minTeleDist * minTeleDist)
                {
                    teleportDestination = new Vector3?(corner);
                    break;
                }
                yield return null;
            }
        }

        Vector3 botPosition = Bot.Position;

        if (teleportDestination != null)
        {
            var allPlayers = Singleton<GameWorld>.Instance?.AllAlivePlayersList;
            if (allPlayers != null)
            {
                foreach (var player in allPlayers)
                {
                    if (ShallCheckPlayer(player))
                    {
                        if (!BotIsStuck)
                        {
                            shallTeleport = false;
                            yield break;
                        }

                        Vector3 playerPosition = player.Position;

                        // Makes sure the bot isn't too close to a human for them to hear
                        float sqrMag = (playerPosition - botPosition).sqrMagnitude;
                        if (sqrMag < _minDistance * _minDistance)
                        {
                            shallTeleport = false;
                            break;
                        }

                        // Checks the max distance to do a path calculation
                        if (sqrMag < _maxDistance * _maxDistance)
                        {
                            NavMeshPath path = CalcPath(botPosition, playerPosition, out float pathLength);
                            if (CheckPathLength(playerPosition, path, pathLength) == false)
                            {
                                shallTeleport = false;
                                break;
                            }
                        }

                        // Check next player on the next frame
                        yield return null;
                    }
                }
            }
        }

        IsTeleporting = BotIsStuck && shallTeleport && teleportDestination != null;

        if (IsTeleporting)
        {
            TeleportWithLimit(teleportDestination.Value + Vector3.up * 0.25f);
            float distance = (teleportDestination.Value - botPosition).magnitude;
            Logger.LogDebug($"Teleporting stuck bot: [{Player.name}] [{distance}] meters to the next corner they are trying to go to");
            _botStuckAfterVault = false;
            BotIsStuck = false;
        }

        yield return null;
    }

    private bool IsTeleporting;

    private bool ShallCheckPlayer(Player player)
    {
        if (Player == null || Player.HealthController == null || Player.AIData == null)
        {
            return false;
        }
        return Player.HealthController.IsAlive == true && Player.AIData.IsAI == false;
    }

    private void TeleportWithLimit(Vector3 position)
    {
        if (teleportTimer < Time.time)
        {
            teleportTimer = Time.time + 3f;
            Player.Teleport(position);
        }
    }

    private float teleportTimer;

    public BotPathController PathController { get; private set; }

    private static NavMeshPath CalcPath(Vector3 start, Vector3 end, out float pathLength)
    {
        if (_pathToPlayer == null)
        {
            _pathToPlayer = new NavMeshPath();
        }
        if (NavMesh.SamplePosition(end, out NavMeshHit hit, 1f, -1))
        {
            _pathToPlayer.ClearCorners();
            if (NavMesh.CalculatePath(start, hit.position, -1, _pathToPlayer))
            {
                pathLength = _pathToPlayer.CalculatePathLength();
                return _pathToPlayer;
            }
        }
        pathLength = 0f;
        return null;
    }

    private static bool CheckPathLength(Vector3 end, NavMeshPath path, float pathLength)
    {
        if (path == null)
        {
            return false;
        }
        if (path.status == NavMeshPathStatus.PathPartial)
        {
            Vector3[] corners = path.corners;
            Vector3 lastCorner = corners[corners.Length - 1];
            float sqrMag = (lastCorner - end).magnitude;
            float combinedLength = sqrMag + pathLength;
            if (combinedLength < _minDistancePathLength)
            {
                return false;
            }
        }
        if (path.status == NavMeshPathStatus.PathComplete && pathLength < _minDistancePathLength)
        {
            return false;
        }
        return path.status != NavMeshPathStatus.PathInvalid;
    }

    private List<Player> GetHumanPlayers()
    {
        _humanPlayers.Clear();
        var allPlayers = Singleton<GameWorld>.Instance?.AllAlivePlayersList;
        if (allPlayers != null)
        {
            foreach (var player in allPlayers)
            {
                if (player != null && player.AIData.IsAI == false && player.HealthController.IsAlive)
                {
                    _humanPlayers.Add(player);
                }
            }
        }
        return _humanPlayers;
    }

    private static NavMeshPath _pathToPlayer;
    private readonly List<Player> _humanPlayers = [];
    public float TimeSinceStuck
    {
        get { return Time.time - TimeStuck; }
    }

    public float TimeStuck { get; }

    private float _checkPositionTimer = 0f;

    private Vector3 _lastPos = Vector3.zero;

    public float TimeSpentNotMoving
    {
        get { return Time.time - TimeStartedChangingPosition; }
    }

    public float TimeStartedChangingPosition { get; private set; }

    public bool BotIsStuck { get; private set; }

    public bool BotHasChangedPosition { get; private set; }
}
