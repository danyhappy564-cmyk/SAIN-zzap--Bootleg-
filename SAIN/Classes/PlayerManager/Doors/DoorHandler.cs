using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT.GlobalEvents;
using EFT.Interactive;
using SAIN.Preset.Shared.GlobalSettings;
using UnityEngine;

namespace SAIN.Components;

public class DoorHandler : GameWorldBase, IGameWorldClass
{
    public event Action<Door, EDoorState, bool> OnDoorStateChanged;

    public event Action<bool> OnDoorsDisabled;

    public DoorHandler(GameWorldComponent component)
        : base(component) { }

    public void Init() { }

    public void ManualUpdate(float currentTime, float deltaTime)
    {
        checkDoors();
        EnsureDoorFinalizeWatchdogInitialized(currentTime);
        TickDoorFinalizeWatches(currentTime);
        TickForcedDoorResync(currentTime);
    }

    // A forced door can still have BSG's smooth-open coroutine finishing its old interaction afterwards; re-check those
    // doors for 15s so the record ends up matching whatever state the door settles in.
    private readonly Dictionary<WorldInteractiveObject, float> _recentlyForced = new();
    private readonly List<WorldInteractiveObject> _forcedDone = new();
    private float _nextForcedResync;

    private void TickForcedDoorResync(float time)
    {
        if (_recentlyForced.Count == 0 || time < _nextForcedResync)
        {
            return;
        }
        _nextForcedResync = time + 1f;
        _forcedDone.Clear();
        foreach (var kv in _recentlyForced)
        {
            if (kv.Key == null || time - kv.Value > 15f)
            {
                _forcedDone.Add(kv.Key);
                continue;
            }
            SyncInteraction(kv.Key, "afterForce");
        }
        foreach (var door in _forcedDone)
        {
            _recentlyForced.Remove(door);
        }
    }

    public void Dispose()
    {
        foreach (var door in _doorsWithTriggers)
        {
            var collider = door.Value?.gameObject?.GetComponent<SphereCollider>();
            if (collider != null)
            {
                GameObject.Destroy(collider);
            }
            GameObject.Destroy(door.Value);
        }
        _doorsWithTriggers.Clear();

        DisposeDoorFinalizeWatchdog();
    }

    public void ChangeDoorState(Door door, EDoorState state, bool shallInvert)
    {
        if (shallInvert)
        {
            door.OpenAngle = -door.OpenAngle;
        }

        door.SetDoorState(state);
        OnDoorStateChanged?.Invoke(door, state, shallInvert);

        if (shallInvert)
        {
            door.OpenAngle = -door.OpenAngle;
        }
    }

    public void HostDisabledDoors(bool value)
    {
        _doorsDisabledByHost = value;
    }

    private void checkDoors()
    {
        if (Singleton<IBotGame>.Instance == null)
        {
            return;
        }

        bool shallDisable = _doorsDisabledByHost || GlobalSettingsClass.Instance.General.Doors.DisableAllDoors;

        if (!_doorsDisabled && shallDisable)
        {
            OnDoorsDisabled?.Invoke(true);
            _doorsDisabled = true;
            disableDoors();
            return;
        }
        if (_doorsDisabled && !shallDisable)
        {
            OnDoorsDisabled?.Invoke(false);
            _doorsDisabled = false;
            enableDoors();
            return;
        }
    }

    public bool DisableDoor(Door door)
    {
        // We don't support doors that don't start open/closed
        if (door.DoorState != EDoorState.Open && door.DoorState != EDoorState.Shut)
        {
            return false;
        }

        // We don't support non-operatable doors
        if (!door.Operatable || !door.enabled)
        {
            return false;
        }

        // We don't support doors that aren't on the "Interactive" layer
        if (door.gameObject.layer != LayersMaskController.InteractiveLayer)
        {
            return false;
        }

        door.gameObject.SmartDisable();
        door.enabled = false;
        _disabledDoors.Add(door.Id, door);
        return true;
    }

    private void disableDoors()
    {
        int doorCount = 0;
        // Code taken from Drakia's Door Randomizer Mod
        UnityEngine
            .Object.FindObjectsOfType<Door>()
            .ExecuteForEach(door =>
            {
                if (DisableDoor(door))
                {
                    doorCount++;
                }
            });

        _doorsDisabled = true;
        Logger.LogDebug($"Disabled Doors: {doorCount}");
    }

    private void enableDoors()
    {
        int doorCount = 0;
        foreach (var door in _disabledDoors)
        {
            door.Value.gameObject.SmartEnable();
            door.Value.enabled = true;
            doorCount++;
        }
        _disabledDoors.Clear();
        _doorsDisabled = false;
        Logger.LogDebug($"Enabled Doors: {doorCount}");
    }

    private bool _doorsDisabled;
    private readonly Dictionary<string, Door> _disabledDoors = new();
    private readonly Dictionary<int, GameObject> _doorsWithTriggers = new();

    private bool _doorsDisabledByHost;

    /// <summary>
    /// BSG's door-open completion callback (Interacting -> Open, or -> Shut on close) is wired to
    /// player-side animation events, which a bot never fires. A bot-driven Door.Interact +
    /// Player.ExecuteInteraction can therefore leave the door's real EDoorState stuck at
    /// Interacting forever, even though the leaf visually finished swinging. Code that only checks
    /// Open/Shut (DoorDataStruct.InRangeToInteract, DoorOpener.RaycastToDoors) then treats that
    /// door as unusable - not just for whoever opened it, for every bot.
    ///
    /// First attempt (2026-09-20) only started a watch from SAIN's own DoorOpener.TryInteractWithDoor,
    /// covering doors our own bots opened. A field log cross-referenced against a separate per-map
    /// door probe showed several doors stuck at Interacting for 12-24+ seconds with no corresponding
    /// watchdog fire - those doors were never opened through SAIN's own interaction call, so no
    /// watch was ever started for them (most likely vanilla BSG bot AI opening a door directly
    /// during a window where SAIN had ceded control - see SAINActivationClass/3.13 in the KB - which
    /// hits the exact same BSG completion-callback gap SAIN's own interactions do).
    ///
    /// Fix: watch every door on the map, not just the ones this mod's bots personally open, by
    /// subscribing to each door's own WorldInteractiveObject.OnDoorStateChanged (fired by the engine
    /// on every DoorState transition regardless of who caused it - the same mechanism ORBIT's
    /// DoorSystem uses for the identical problem). Doors, once placed in the level, don't get
    /// created or destroyed mid-raid, so a one-time FindObjectsOfType is enough - see
    /// EnsureDoorFinalizeWatchdogInitialized() below for when that scan actually has to happen
    /// (GameWorldComponent.Init() turned out to be too early, see the comment there).
    /// </summary>
    // 3s -> 1.2s (2026-09-27 video): NavMeshDoorLink only carves the open leaf out of the navmesh once the door
    // is Open, so for the 3s a bot-opened door sat in Interacting the path ran straight through the leaf and the bot
    // walked into the pocket behind it. A door swing takes ~1s.
    private const float DOOR_FINALIZE_WATCH_TIMEOUT = 1.2f;

    // zzap (2026-10-04, user report "kicking a door opens it instantly with no animation - even locked doors"): a kick
    // puts the door in Interacting when it STARTS (MovementContext.StartDoorBreak -> LockForInteraction), then the
    // player walks to the kick spot (Approach) and the foot lands at 45% of the BreachDoor animation - past 1.2s. The
    // watchdog snapped the door to Open with no swing, and on a failed kick (FailBreach puts a locked door back to Locked)
    // the leaf was left standing open. While someone is still in their own door animation for this door BSG finishes it
    // itself, so wait for them (players up to 10s, bots 4s, then the old force).
    private const float DOOR_FINALIZE_MAX_WAIT_PLAYER = 10f;
    private const float DOOR_FINALIZE_MAX_WAIT_BOT = 4f;

    // GameWorldComponent.Init() (where Init() above used to do this scan) runs as soon as the
    // GameWorld object exists, before the map's scene content - including every Door - has finished
    // loading, so a FindObjectsOfType<Door> there reliably finds 0 doors (confirmed via a field log:
    // "Watching 0 doors" while a separate per-map door probe found 40+ real doors the same raid,
    // several cycling through Interacting). findSpawnPointMarkers() below already works around the
    // identical timing problem by retrying every tick until the scene is ready instead of scanning
    // once too early - do the same here from ManualUpdate() instead of Init().
    private const float DOOR_WATCH_INIT_TIMEOUT = 15f;

    private readonly Dictionary<int, PendingDoorWatch> _pendingDoorWatches = new();
    private readonly List<int> _resolvedDoorWatches = new();
    private Door[] _watchedDoors;
    private float _doorWatchInitStartTime = -1f;

    private readonly struct PendingDoorWatch(Door door, EDoorState targetState, float requestedAt)
    {
        internal readonly Door Door = door;
        internal readonly EDoorState TargetState = targetState;
        internal readonly float RequestedAt = requestedAt;
    }

    private void EnsureDoorFinalizeWatchdogInitialized(float currentTime)
    {
        if (_watchedDoors != null)
        {
            return;
        }
        if (_doorWatchInitStartTime < 0f)
        {
            _doorWatchInitStartTime = currentTime;
        }

        Door[] doors = UnityEngine.Object.FindObjectsOfType<Door>();
        if (doors.Length == 0)
        {
            if (currentTime - _doorWatchInitStartTime > DOOR_WATCH_INIT_TIMEOUT)
            {
                _watchedDoors = doors;
                Logger.LogWarning(
                    $"[DoorHandler] Still found 0 doors after {DOOR_WATCH_INIT_TIMEOUT:F0}s - giving up on the "
                        + "stuck-Interacting watchdog for this raid (map may genuinely have none, or they never loaded)."
                );
            }
            return;
        }

        _watchedDoors = doors;
        foreach (Door door in _watchedDoors)
        {
            if (door != null)
            {
                door.OnDoorStateChanged += OnNativeDoorStateChanged;
            }
        }
        Logger.LogDebug($"[DoorHandler] Watching {_watchedDoors.Length} doors for stuck EDoorState.Interacting.");
    }

    private void DisposeDoorFinalizeWatchdog()
    {
        if (_watchedDoors != null)
        {
            foreach (Door door in _watchedDoors)
            {
                if (door != null)
                {
                    door.OnDoorStateChanged -= OnNativeDoorStateChanged;
                }
            }
            _watchedDoors = null;
        }
        _pendingDoorWatches.Clear();
        _doorWaitCounted.Clear();
    }

    // WorldInteractiveObject.OnDoorStateChanged(WorldInteractiveObject obj, EDoorState prevState, EDoorState nextState)
    private void OnNativeDoorStateChanged(WorldInteractiveObject obj, EDoorState prevState, EDoorState nextState)
    {
        if (nextState != EDoorState.Interacting || obj is not Door door)
        {
            return;
        }
        // The event only tells us the door started interacting, not which way it's headed - infer
        // that from what it was doing before: already Open means this transition is a close, so the
        // completion target is Shut; anything else (Shut, Locked-being-breached) is an open. A locked door is re-checked
        // when the force actually happens (ResolveForcedState) - only a kick that landed may open it.
        EDoorState targetState = prevState == EDoorState.Open ? EDoorState.Shut : EDoorState.Open;
        _pendingDoorWatches[door.GetInstanceID()] = new PendingDoorWatch(door, targetState, Time.time);
    }

    // zzap (2026-09-29, "a door nobody can open, not even the player"): besides DoorState, BSG keeps the target of the last
    // interaction in the protected _interaction.ResultState, and CanStartInteraction(Open) refuses while that still says
    // Open (_interaction.ResultState != state || _interaction.Break). Force-finalizing only DoorState (to Shut after an
    // earlier Open) left ResultState=Open forever -> every Open request silently dropped (9th sim: doors 00050/00008, bots
    // retried 26/30 times). Bring the interaction record in line with the forced state and end it.
    private static readonly System.Reflection.FieldInfo _interactionField =
        HarmonyLib.AccessTools.Field(typeof(WorldInteractiveObject), "_interaction");

    public static bool SyncInteraction(WorldInteractiveObject door, string why)
    {
        if (door == null || _interactionField == null || door.DoorState == EDoorState.Interacting)
        {
            return false;
        }
        try
        {
            if (_interactionField.GetValue(door) is not WorldInteractiveObject.InteractionState interaction || interaction.IsInProgress)
            {
                return false;
            }
            if (interaction.ResultState == door.DoorState && interaction.Break)
            {
                return false;
            }
            bool mismatch = interaction.ResultState != door.DoorState && !interaction.Break;
            interaction.ResultState = door.DoorState;
            interaction.Break = true;
            if (mismatch)
            {
                if (SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.CountOn) SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count($"door.interactionRepaired.{why}");
                Logger.LogWarning($"[DoorHandler] [{door.Id}] interaction record repaired ({why}) - it still pointed at another state, so the door refused to open/close");
            }
            return mismatch;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[DoorHandler] interaction sync failed: {ex.Message}");
            return false;
        }
    }

    private void TickDoorFinalizeWatches(float time)
    {
        if (_pendingDoorWatches.Count == 0)
        {
            return;
        }
        _resolvedDoorWatches.Clear();
        foreach (var kv in _pendingDoorWatches)
        {
            PendingDoorWatch watch = kv.Value;
            if (watch.Door == null)
            {
                _resolvedDoorWatches.Add(kv.Key);
                _doorWaitCounted.Remove(kv.Key);
                continue;
            }
            if (time - watch.RequestedAt < DOOR_FINALIZE_WATCH_TIMEOUT)
            {
                continue;
            }
            if (watch.Door.DoorState == EDoorState.Interacting)
            {
                if (time - watch.RequestedAt < DoorAnimationWait(watch.Door, out bool byPlayer))
                {
                    if (_doorWaitCounted.Add(kv.Key) && SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.CountOn)
                    {
                        SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count(byPlayer ? "door.finalizeWait.player" : "door.finalizeWait.bot");
                    }
                    continue;
                }
                EDoorState target = ResolveForcedState(watch.Door, watch.TargetState);
                watch.Door.DoorState = target;
                watch.Door.CurrentAngle = watch.Door.GetAngle(target);
                SyncInteraction(watch.Door, "force-finalize");
                _recentlyForced[watch.Door] = time;
                GlobalEventsController.CreateEvent<InteractiveObjectInteractionResultEvent>().Invoke(watch.Door, target);
                Logger.LogWarning(
                    $"[DoorHandler] [{watch.Door.Id}] never left EDoorState.Interacting {time - watch.RequestedAt:F1}s "
                        + $"after a state change - force-finalized to {target} (BSG's completion callback "
                        + "only fires for player-driven animation events)"
                );
            }
            _resolvedDoorWatches.Add(kv.Key);
            _doorWaitCounted.Remove(kv.Key);
        }
        for (int i = 0; i < _resolvedDoorWatches.Count; i++)
        {
            _pendingDoorWatches.Remove(_resolvedDoorWatches[i]);
        }
    }

    private readonly HashSet<int> _doorWaitCounted = new();

    // How long (seconds since the watch started) to leave this door in Interacting because a player is still in their
    // own door animation for it - walking to it (Approach), kicking it (BreachDoor) or turning the handle/key
    // (DoorInteraction). 0 = nobody is. MovementContext.InteractionInfo is set by StartInteraction/StartDoorBreak right
    // before the door goes Interacting; SAIN's own bot opens go through Player.ExecuteInteraction, which neither sets it
    // nor changes the bot's state, so they keep the 1.2s force.
    private static float DoorAnimationWait(Door door, out bool byPlayer)
    {
        byPlayer = false;
        var players = Singleton<EFT.GameWorld>.Instance?.AllAlivePlayersList;
        if (players == null)
        {
            return 0f;
        }
        float wait = 0f;
        for (int i = 0; i < players.Count; i++)
        {
            EFT.Player player = players[i];
            if (player == null || player.MovementContext?.CurrentState == null)
            {
                continue;
            }
            if (player.MovementContext.InteractionInfo.WorldInteractiveObject != door)
            {
                continue;
            }
            switch (player.CurrentStateName)
            {
                case EPlayerState.Approach:
                case EPlayerState.BreachDoor:
                case EPlayerState.DoorInteraction:
                case EPlayerState.DoorInteractionZombieState:
                    if (!player.IsAI)
                    {
                        byPlayer = true;
                    }
                    wait = Mathf.Max(wait, player.IsAI ? DOOR_FINALIZE_MAX_WAIT_BOT : DOOR_FINALIZE_MAX_WAIT_PLAYER);
                    break;
            }
        }
        return wait;
    }

    // A locked door only leaves Interacting three ways: a key turn (UnlockCoroutine -> Shut), a kick that landed (KickOpen
    // starts a Breaching swing that ends Open) or a kick that missed (FailBreach -> back to Locked). Guessing Open for it
    // opened locked doors for free - keep it Locked unless the kick actually landed, or the game is already swinging it
    // open itself (KeycardDoor with _openOnUnlock: card -> handle -> sound -> Interact(Open) while still Interacting;
    // forcing Locked there labelled an opening door Locked until the swing ended, or for good if the swing stalled).
    private static EDoorState ResolveForcedState(Door door, EDoorState inferred)
    {
        if (door.FallbackState != EDoorState.Locked)
        {
            return inferred;
        }
        WorldInteractiveObject.InteractionState interaction = null;
        try
        {
            interaction = _interactionField?.GetValue(door) as WorldInteractiveObject.InteractionState;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[DoorHandler] interaction read failed: {ex.Message}");
        }
        if (interaction != null
            && (interaction.ResultState == EDoorState.Breaching
                || (interaction.IsInProgress && interaction.ResultState == EDoorState.Open)))
        {
            return EDoorState.Open;
        }
        if (SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.CountOn) SAIN.SAINComponent.Classes.Tactics.TacticDiagnostics.Count("door.forceKeptLocked");
        return EDoorState.Locked;
    }
}
