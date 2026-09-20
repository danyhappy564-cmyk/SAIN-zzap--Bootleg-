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
    private const float DOOR_FINALIZE_WATCH_TIMEOUT = 3f;

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
        // completion target is Shut; anything else (Shut, Locked-being-breached) is an open.
        EDoorState targetState = prevState == EDoorState.Open ? EDoorState.Shut : EDoorState.Open;
        _pendingDoorWatches[door.GetInstanceID()] = new PendingDoorWatch(door, targetState, Time.time);
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
                continue;
            }
            if (time - watch.RequestedAt < DOOR_FINALIZE_WATCH_TIMEOUT)
            {
                continue;
            }
            if (watch.Door.DoorState == EDoorState.Interacting)
            {
                watch.Door.DoorState = watch.TargetState;
                watch.Door.CurrentAngle = watch.Door.GetAngle(watch.TargetState);
                GlobalEventsController.CreateEvent<InteractiveObjectInteractionResultEvent>().Invoke(watch.Door, watch.TargetState);
                Logger.LogWarning(
                    $"[DoorHandler] [{watch.Door.Id}] never left EDoorState.Interacting {time - watch.RequestedAt:F1}s "
                        + $"after a state change - force-finalized to {watch.TargetState} (BSG's completion callback "
                        + "only fires for player-driven animation events)"
                );
            }
            _resolvedDoorWatches.Add(kv.Key);
        }
        for (int i = 0; i < _resolvedDoorWatches.Count; i++)
        {
            _pendingDoorWatches.Remove(_resolvedDoorWatches[i]);
        }
    }
}
