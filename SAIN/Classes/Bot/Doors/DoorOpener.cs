using System.Collections.Generic;
using EFT;
using EFT.Interactive;
using SAIN.Components;
using SAIN.Helpers;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Mover;

public class DoorOpener : BotComponentClassBase
{
    public bool Interacting { get; private set; }

    public EInteractionType InteractionType { get; private set; }

    public DoorOpener(BotComponent sain)
        : base(sain)
    {
        TickRequirement = ESAINTickState.OnlyNoSleep;
    }

    private const float DOOR_UPDATE_INTERVAL = 0.5f;

    private List<DoorDataStruct> _interactionDoors { get; } = [];
    private List<DoorDataStruct> _allDoors { get; } = [];
    public NavGraphVoxelSimple CurrentVoxel { get; private set; }

    public bool TryInteractWithDoor(EInteractionType interactionType, float time, DoorDataStruct data)
    {
        if (!InteractWithDoor(ref data, interactionType))
        {
#if DEBUG
            Logger.LogDebug($"[{Bot.name}]:[{data.Door.Id}] failed to interact with door");
#endif
            Clear();
            return false;
        }
        _interactionDoors[_interactionDoorIndex] = data;
        RememberStamps(data);
        Interacting = true;
        ActiveDoor = data;
        InteractionType = interactionType;
        _doorInteractionEndTime = time + (IsDoorPullOpen(data, Bot.NavMeshPosition) ? 1.25f : 1f);
        Bot.Player.MovementContext.IgnoreInteractionCollision(data.Door.Collider, true);
        return true;
    }

    /// <summary>
    /// Bypasses the DOOR_UPDATE_INTERVAL poll and forces the next SelectDoor call to rescan for
    /// doors immediately. Used when a bot is physically jammed against a shut door faster than the
    /// throttled scan can register it (typical while sprinting straight at one).
    /// </summary>
    public void ForceRecheck()
    {
        _nextDoorUpdateTime = 0f;
    }

    /// <summary>
    /// Releases an in-progress door interaction right now instead of waiting for the next
    /// SelectDoor poll to notice _doorInteractionEndTime has passed. SelectDoor is only ever
    /// called from BotPathData.InteractWithDoor, which is only reached while SAINMoverClass
    /// keeps ticking the bot's active path - and that ticking stops the instant
    /// SAINActivationClass deactivates the bot's SAIN layers (player walks out of range, game
    /// ending, etc). If that happens mid-interaction, Interacting and the
    /// IgnoreInteractionCollision(true) set in TryInteractWithDoor were previously left in
    /// place indefinitely: the bot would sit wedged in the doorway with its own collision
    /// against that door disabled until something eventually re-ticked its path, at which
    /// point the stale timeout fired and collision snapped back on wherever the bot happened
    /// to be standing - or, if nothing ever re-ticked it, the bot could walk straight through
    /// the door the next time it was near it. Call this from anywhere that force-stops a bot's
    /// movement independent of the path tick loop (SAINActivationClass.SetActive/ManualUpdate).
    /// </summary>
    public void CancelInteraction()
    {
        if (!Interacting)
        {
            return;
        }
        Clear();
    }

    public DoorDataStruct GetActiveDoor()
    {
        if (_interactionDoors.Count > 0 && _interactionDoorIndex < _interactionDoors.Count)
        {
            return _interactionDoors[_interactionDoorIndex];
        }
        return ActiveDoor;
    }

    public bool SelectDoor(out EInteractionType interactionType, out DoorDataStruct currentDoor, IBotPathData pathData)
    {
        const float RAY_LENGTH = 3f;
        Vector3 botPosition = Bot.Position;
        float time = Time.time;

        if (Interacting)
        {
            if (_doorInteractionEndTime < time)
            {
                Clear();

                interactionType = EInteractionType.Open; // Default to open if interaction is done
                currentDoor = default;
                return false;
            }
            else
            {
                interactionType = InteractionType;
                currentDoor = _interactionDoors[_interactionDoorIndex];
                return true;
            }
        }

        SearchForDoors(botPosition, time);

        if (_interactionDoors.Count == 0)
        {
            interactionType = EInteractionType.Open; // Default to open if no doors found
            currentDoor = ActiveDoor;
            return false;
        }

        CornerMoveData moveData = pathData.CurrentCornerMoveData;
        Ray ray = new() { origin = botPosition + Vector3.up, direction = moveData.CornerDirectionFromBotNormal * RAY_LENGTH };

        DoorDataStruct data = ActiveDoor;
        if (!RaycastToDoors(out interactionType, ref data, out int index, RAY_LENGTH, ray, _interactionDoors))
        {
            currentDoor = ActiveDoor;
            return false;
        }
        _interactionDoorIndex = index;
        _interactionDoors[index] = data;
        //bool value = TryInteractWithDoor(interactionType, time, ref data);
        currentDoor = data;
        return true;
    }

    private void Clear()
    {
        Bot.Player.MovementContext.IgnoreInteractionCollision(ActiveDoor.Door?.Collider, false);
        Interacting = false;
        _interactionDoorIndex = 0;
        ActiveDoor = new();
        _doorInteractionEndTime = 0;
        InteractionType = EInteractionType.Open;
    }

    private int _interactionDoorIndex;

    private void SearchForDoors(Vector3 botPosition, float time)
    {
        if (_nextDoorUpdateTime < time)
        {
            _nextDoorUpdateTime = time + DOOR_UPDATE_INTERVAL;
            BotOwner.AIData.SetPosToVoxel(botPosition);

            var lastVoxel = CurrentVoxel;
            CurrentVoxel = BotOwner.VoxelesPersonalData.CurVoxel;

            if (lastVoxel != CurrentVoxel)
            {
                _allDoors.Clear();
                if (CurrentVoxel != null)
                {
                    //Logger.LogDebug($"{CurrentVoxel.DoorLinks.Count} doors in voxel");
                    foreach (var link in CurrentVoxel.DoorLinks)
                    {
                        if (IsDoorOpenable(link.Door))
                        {
                            _allDoors.Add(new DoorDataStruct(link));
                        }
                    }
                }
                //Logger.LogDebug($"{AllDoors.Count} door data created");
            }

            _interactionDoors.Clear();
            for (int i = 0; i < _allDoors.Count; i++)
            {
                DoorDataStruct door = _allDoors[i];
                RestoreStamps(ref door);
                door.ManualUpdate(botPosition);
                if (door.InRangeToInteract(door.Door) && door.CanInteractByTime(time))
                {
                    _interactionDoors.Add(door);
                }
                _allDoors[i] = door;
            }
        }
    }

    private static bool FindDoorFromCollider(Collider collider, out DoorDataStruct data, out int index, List<DoorDataStruct> doors)
    {
        if (collider == null)
        {
            data = default;
            index = -1;
            return false;
        }
        data = default;
        for (index = 0; index < doors.Count; index++)
        {
            data = doors[index];
            if (data.Door.Collider == collider)
            {
                return true;
            }
            if (collider.GetComponent<NavMeshDoorLink>() == data.Link)
            {
                Logger.LogDebug($"Found NavMeshDoorLink from collider");
                return true;
            }
        }
        return false;
    }

    private static bool RaycastToDoors(
        out EInteractionType interactionType,
        ref DoorDataStruct data,
        out int index,
        float RAY_LENGTH,
        Ray ray,
        List<DoorDataStruct> doors
    )
    {
        // Widened from 0.15f: a bot sprinting/retreating under combat steering smoothing doesn't always
        // face squarely at a door it's jammed against, and this narrow a cast can miss it every tick.
        const float SPHERECAST_RADIUS = 0.3f;
        const float SPHERECAST_DISTANCE = 1.5f;
        if (Physics.SphereCast(ray, SPHERECAST_RADIUS, out RaycastHit hit, SPHERECAST_DISTANCE, LayersMaskController.PlayerStaticDoorMask))
        {
#if DEBUG
            DebugGizmos.DrawLine(ray.origin, hit.point, Color.red, 0.25f, 30f, true);
#endif
            if (FindDoorFromCollider(hit.collider, out data, out index, doors))
            {
#if DEBUG
                Logger.LogDebug($"Found door from hit collider [PlayerStaticDoorMask] [{hit.collider.name}]");
#endif
                if (data.Door.DoorState == EDoorState.Open && !RecentlySelfOpened(data))
                {
                    interactionType = EInteractionType.Close;
                    return true;
                }
                if (data.Door.DoorState == EDoorState.Shut)
                {
                    interactionType = EInteractionType.Open;
                    return true;
                }
            }
            else
            {
                //Logger.LogDebug($"Failed to find door, but we hit something on [PlayerStaticDoorMask] [{hit.collider.name}]");
            }
        }
        if (Physics.SphereCast(ray, SPHERECAST_RADIUS, out hit, SPHERECAST_DISTANCE, LayersMaskController.DoorLayer))
        {
            DebugGizmos.DrawLine(ray.origin, hit.point, Color.red, 0.25f, 30f, true);
            if (FindDoorFromCollider(hit.collider, out data, out index, doors))
            {
#if DEBUG
                Logger.LogDebug($"Found door from hit collider [DoorLayer] [{hit.collider.name}]");
#endif
                if (data.Door.DoorState == EDoorState.Open && !RecentlySelfOpened(data))
                {
                    interactionType = EInteractionType.Close;
                    return true;
                }
                if (data.Door.DoorState == EDoorState.Shut)
                {
                    interactionType = EInteractionType.Open;
                    return true;
                }
            }
            else
            {
                //Logger.LogDebug($"Failed to find door, but we hit something on [DoorLayer] [{hit.collider.name}]");
            }
        }
        for (index = 0; index < doors.Count; index++)
        {
            data = doors[index];
            if (data.CurrentSqrMagnitude > RAY_LENGTH * RAY_LENGTH)
            {
                continue;
            }

            if (!CanInteract(data.Link))
            {
                continue;
            }

            Collider doorCollider = data.Door.Collider;
            if (doorCollider == null)
            {
                continue;
            }

            if (doorCollider.Raycast(ray, out hit, SPHERECAST_DISTANCE))
            {
#if DEBUG
                Logger.LogDebug($"hit door  [Door.collider.Raycast]");
                DebugGizmos.DrawLine(ray.origin, hit.point, Color.red, 0.25f, 30f, true);
#endif
                if (data.Door.DoorState == EDoorState.Open && !RecentlySelfOpened(data))
                {
                    interactionType = EInteractionType.Close;
                    return true;
                }
                if (data.Door.DoorState == EDoorState.Shut)
                {
                    interactionType = EInteractionType.Open;
                    return true;
                }
            }
        }

        // Proximity fallback: every directional cast above missed. All candidates in `doors` were already
        // filtered to interaction range by DoorDataStruct.InRangeToInteract before reaching here (see
        // DoorOpener.SearchForDoors), so if a bot is jammed against a door but not squarely facing it
        // (typical while sprinting/retreating), just grab the nearest one instead of leaving it wedged
        // until its facing happens to line up.
        index = -1;
        var closestSqr = float.MaxValue;
        for (var i = 0; i < doors.Count; i++)
        {
            var candidate = doors[i];
            if (!CanInteract(candidate.Link)) continue;
            if (candidate.Door.DoorState != EDoorState.Shut && candidate.Door.DoorState != EDoorState.Open) continue;
            if (candidate.Door.DoorState == EDoorState.Open && RecentlySelfOpened(candidate)) continue;
            if (candidate.CurrentSqrMagnitude >= closestSqr) continue;
            closestSqr = candidate.CurrentSqrMagnitude;
            index = i;
        }
        if (index >= 0)
        {
            data = doors[index];
#if DEBUG
            Logger.LogDebug($"[{data.Door.Id}] no directional door hit — using closest in-range door as fallback");
#endif
            interactionType = data.Door.DoorState == EDoorState.Open ? EInteractionType.Close : EInteractionType.Open;
            return true;
        }

        interactionType = EInteractionType.Open; // Default to open if no doors found
        return false;
    }

    // (08-29 field report, Korean discord: a bot fleeing/fighting through a doorway
    // sometimes ran face-first into the SAME door it had just opened, for ~15s until
    // it "gave up" and reopened it.) every branch above treats ANY Open door in range
    // as something to close, with no notion of "I'm the one who just opened this and
    // haven't finished walking through it yet". a bot that lingers at the threshold
    // under combat steering (backpedaling, reacting to fire, group holdup) easily
    // takes longer than a clean walk-through, so the moment it re-evaluates doors it
    // decides to shut the one it's still standing in — then paths straight at the now-
    // closed door it thinks is open. LastCloseTime is stamped (InteractWithDoor) the
    // instant we start opening a Shut door, so a short grace window on it is a direct,
    // minimal fix: don't reconsider closing a door for a few seconds after WE opened
    // it, regardless of how long the bot dawdles at the threshold.
    private const float JUST_OPENED_GRACE = 6f;

    private static bool RecentlySelfOpened(in DoorDataStruct data)
        => Time.time - data.LastCloseTime < JUST_OPENED_GRACE;

    // ...except the grace above could not actually work, and neither could
    // DoorDataStruct.CanInteractByTime, because nothing kept the timestamps alive.
    //
    // DoorDataStruct is a STRUCT held in two Lists. InteractWithDoor stamps LastOpenTime
    // / LastCloseTime / LastInteractTime on its `ref data`, and TryInteractWithDoor
    // writes that back into _interactionDoors - but never into _allDoors. SearchForDoors
    // then does _interactionDoors.Clear() and rebuilds the whole list out of _allDoors
    // every DOOR_UPDATE_INTERVAL, so every stamp is thrown away within 0.5s. _allDoors
    // itself is also rebuilt from scratch whenever the bot's voxel changes - which, at a
    // doorway, is exactly when it is walking through.
    //
    // So RecentlySelfOpened read a LastCloseTime of 0 on essentially every call and
    // answered "no, I did not just open this", and the bot went right back to closing
    // the door it had opened a second ago. That is the open-close-open-close loop in the
    // 2026-09-15 report ("the door opening and closing animation is what happens first,
    // then it is stuck"), and it is why the 08-29 grace window never changed anything.
    //
    // Keep the three stamps here instead, keyed by the door link id, where neither list
    // rebuild can reach them. One small dictionary per bot, entries the size of three
    // floats, and it dies with the bot.
    private readonly Dictionary<int, DoorStamps> _stamps = [];

    private readonly struct DoorStamps(float interact, float open, float close)
    {
        internal readonly float Interact = interact;
        internal readonly float Open = open;
        internal readonly float Close = close;
    }

    private void RememberStamps(in DoorDataStruct data)
    {
        _stamps[data.Id] = new DoorStamps(data.LastInteractTime, data.LastOpenTime, data.LastCloseTime);
    }

    private void RestoreStamps(ref DoorDataStruct data)
    {
        if (!_stamps.TryGetValue(data.Id, out var stamps)) return;
        // Only ever move them forward. A struct that still carries this tick's stamps
        // must not be rolled back to an older remembered value.
        if (stamps.Interact > data.LastInteractTime) data.LastInteractTime = stamps.Interact;
        if (stamps.Open > data.LastOpenTime) data.LastOpenTime = stamps.Open;
        if (stamps.Close > data.LastCloseTime) data.LastCloseTime = stamps.Close;
    }

    private static bool IsDoorOpenable(Door door)
    {
        if (!door.enabled || !door.gameObject.activeInHierarchy || !door.Operatable)
        {
            return false;
        }
        //if (!ModDetection.ProjectFikaLoaded &&
        //    GlobalSettingsClass.Instance.General.Doors.DisableAllDoors &&
        //    GameWorldComponent.Instance.Doors.DisableDoor(door))
        //{
        //    return false;
        //}
        return true;
    }

    private float _nextDoorUpdateTime;

    private static bool CanInteract(NavMeshDoorLink link)
    {
        if (!link.ShallInteract())
        {
            return false;
        }
        if (!link.Door.enabled || !link.Door.gameObject.activeInHierarchy || !link.Door.Operatable)
        {
            return false;
        }
        return true;
    }

    private bool InteractWithDoor(ref DoorDataStruct data, EInteractionType type)
    {
        if (data.Door == null)
        {
            return false;
        }

        switch (data.Door.DoorState)
        {
            case EDoorState.Shut:
                data.LastCloseTime = Time.time;
                break;

            case EDoorState.Open:
                data.LastOpenTime = Time.time;
                break;

            default:
                return false;
        }
        data.LastInteractTime = Time.time;
        Player.MovementContext.ResetCanUsePropState();
        var interactionResult = Door.Interact(Player, type);
        if (interactionResult.Succeeded)
        {
            //Logger.LogDebug("Success");
            switch (type)
            {
                case EInteractionType.Breach:
                    Player.StartInteraction(data.Door, interactionResult.Value, null);
                    break;

                default:
                    Player.ExecuteInteraction(data.Door, interactionResult.Value);
                    break;
            }
            return true;
        }
        return false;
    }

    public bool ShallKickOpen(Door door, EInteractionType Etype)
    {
        if (Etype != EInteractionType.Open)
        {
            return false;
        }
        if (!WantToKick())
        {
            return false;
        }
        var breakInParameters = door.GetBreakInParameters(Bot.Position);
        return door.BreachSuccessRoll(breakInParameters.InteractionPosition);
    }

    private bool WantToKick()
    {
        var enemy = Bot.GoalEnemy;
        if (enemy != null)
        {
            if (Bot.Info.PersonalitySettings.General.KickOpenAllDoors)
            {
                return true;
            }
            if (BotOwner.Memory.IsUnderFire)
            {
                return true;
            }
            float? timeSinceSeen = enemy.TimeSinceSeen;
            if (timeSinceSeen != null)
            {
                if (timeSinceSeen.Value < 3f)
                {
                    return true;
                }
                if (timeSinceSeen.Value < 5f && enemy.InLineOfSight)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static bool IsDoorPullOpen(DoorDataStruct doorData, Vector3 botPosition)
    {
        Vector3 doorOpenPos = doorData.Link.Open2;
        Vector3 doorPos = doorData.Link.transform.position;
        Vector3 openDirection = (doorOpenPos - doorPos).normalized;
        Vector3 botDirection = (botPosition - doorPos).normalized;
        float dotProduct = Vector3.Dot(openDirection, botDirection);
        return dotProduct > 0;
    }

    public DoorDataStruct ActiveDoor = new();
    private float _doorInteractionEndTime;
}
