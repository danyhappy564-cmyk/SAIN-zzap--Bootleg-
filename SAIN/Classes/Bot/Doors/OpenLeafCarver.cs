using System.Collections.Generic;
using EFT;
using EFT.Interactive;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.SAINComponent.Classes.Tactics;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Mover;

/// <summary>
/// zzap (2026-10-31, 1st-stage 11-map stall probe + user screenshots of Reserve / Factory doors): bots ran into open door leaves
/// and stood there - Reserve's outside metal door (leaf across the sidewalk) alone 9.8 times a raid, the bunker / toilet doors (leaf
/// across the corridor / stair landing), Factory's basement bunker door. Every door link has an "opened" NavMeshObstacle
/// (Carver_Opened) that cuts the open leaf's spot out of the walkable mesh, but in this EFT build it never runs: every map's links
/// are baked ShallTryInteract = false (only the editor's NavGraph sets it), so NavMeshDoorLink.OnDoorStateChanged ignores the door,
/// and BotDoorsController.InitCarveStates switches every carver component off at raid start (SetAllCarvers(false) disables them,
/// then only sets carving = true). So the path ran through the leaf for every open door, scene-open or opened mid-raid.
/// This does what the link would do with its carver live: an open door's opened / breached carvers on, once a second and only
/// when nobody stands within 2 m of the open-leaf middle (EFT's TryCarve rule - carving under a body pushes it off the mesh);
/// off again the moment the door leaves Open. ShallTryInteract itself stays false: flipping it makes link.ShallInteract() false
/// for carved doors and SAIN would stop closing them (DoorOpener.CanInteract). A leaf turned the wrong way (ram / DSDB, see
/// ReversedLeafUnjam) waits until it is back at its own angle - the carver covers the normal open spot only.
/// Starts when BotsController has handed the links to BotDoorsController; the setting is read then (next raid on change).
/// Ticked from DoorHandler. Counters door.carve.*.
/// </summary>
public static class OpenLeafCarver
{
    private const float CHECK_INTERVAL = 1f;
    private const float CLEAR_RADIUS = 2f;
    private const int MAX_CARVES_PER_CHECK = 16;
    private const float FIND_LINKS_TIMEOUT = 60f;
    private const float CUT_MEMORY = 60f;
    private const float PATH_NEAR = 2.5f;

    private struct Cut
    {
        public Vector3 Mid;
        public float Time;
    }

    private static readonly List<Cut> _cuts = new();
    private static readonly List<NavMeshDoorLink> _gone = new();

    private static readonly List<NavMeshDoorLink> _links = new();
    private static readonly List<NavMeshDoorLink> _waiting = new();
    private static readonly HashSet<NavMeshDoorLink> _carved = new();
    private static readonly Dictionary<Door, List<NavMeshDoorLink>> _byDoor = new();
    private static bool _ready;
    private static float _firstTry = -1f;
    private static float _nextCheck;

    public static int CarvedCount => _carved.Count;

    private static bool SettingOn => GlobalSettingsClass.Instance?.General?.DoorTactics?.CarveOpenLeaves != false;

    /// <summary>Every frame (DoorHandler).</summary>
    public static void Tick(float time)
    {
        if (time < _nextCheck)
        {
            return;
        }
        _nextCheck = time + CHECK_INTERVAL;
        if (!_ready)
        {
            TryStart(time);
            return;
        }
        DropGone(time);
        int carved = 0;
        for (int i = _waiting.Count - 1; i >= 0 && carved < MAX_CARVES_PER_CHECK; i--)
        {
            NavMeshDoorLink link = _waiting[i];
            if (link == null || link.Door == null || link.Door.DoorState != EDoorState.Open)
            {
                _waiting.RemoveAt(i);
                continue;
            }
            if (!Live(link.Door) || ReversedLeafUnjam.IsReversed(link.Door))
            {
                continue;
            }
            if (SomeoneNear(link.MidOpen))
            {
                continue;
            }
            _waiting.RemoveAt(i);
            SetCarve(link, true);
            carved++;
        }
    }

    /// <summary>The door is in the level (F6 Disable All Doors switches doors off without a state change - DoorHandler.DisableDoor).</summary>
    private static bool Live(Door door) => door.enabled && door.gameObject.activeInHierarchy;

    /// <summary>A carved link whose door went away (disabled) loses its cut; it waits to be carved again if the door comes back
    /// still open. Old cut records past CUT_MEMORY are dropped.</summary>
    private static void DropGone(float time)
    {
        _gone.Clear();
        foreach (NavMeshDoorLink link in _carved)
        {
            if (link == null || link.Door == null || !Live(link.Door))
            {
                _gone.Add(link);
            }
        }
        foreach (NavMeshDoorLink link in _gone)
        {
            SetCarve(link, false);
            if (link != null && link.Door != null && link.Door.DoorState == EDoorState.Open && !_waiting.Contains(link))
            {
                _waiting.Add(link);
            }
        }
        _gone.Clear();
        _cuts.RemoveAll(c => time - c.Time > CUT_MEMORY);
    }

    /// <summary>
    /// A route made at <paramref name="pathTime"/> whose rest (from the bot through the corners from <paramref name="from"/>) runs
    /// within PATH_NEAR of a leaf cut out after the route was made. Mover paths are kept while the destination doesn't move
    /// (BotPathData.TryUpdatePath), so without this a bot keeps walking the old line through the leaf. Cuts count from the next
    /// frame on: Unity applies an obstacle's carve a frame later.
    /// </summary>
    public static bool CutAfter(float pathTime, List<BotPathCorner> corners, int from, Vector3 botPosition)
    {
        float now = Time.time;
        for (int c = 0; c < _cuts.Count; c++)
        {
            Cut cut = _cuts[c];
            if (cut.Time < pathTime || cut.Time >= now)
            {
                continue;
            }
            Vector3 prev = botPosition;
            for (int i = Mathf.Max(0, from); i < corners.Count; i++)
            {
                Vector3 next = corners[i].Position;
                if (DistanceToSegmentSqr(cut.Mid, prev, next) < PATH_NEAR * PATH_NEAR)
                {
                    return true;
                }
                prev = next;
            }
        }
        return false;
    }

    private static float DistanceToSegmentSqr(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float len = ab.sqrMagnitude;
        float t = len < 1e-6f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / len);
        return (a + ab * t - p).sqrMagnitude;
    }

    private static void TryStart(float time)
    {
        if (_firstTry < 0f)
        {
            _firstTry = time;
        }
        if (time - _firstTry > FIND_LINKS_TIMEOUT)
        {
            _ready = true;
            Logger.LogWarning($"[OpenLeafCarver] no door links after {FIND_LINKS_TIMEOUT:0}s - open leaves stay walkable this raid");
            return;
        }
        if (Comfort.Common.Singleton<IBotGame>.Instance == null)
        {
            return;
        }
        List<NavMeshDoorLink> links = BotDoorsController.CreateOrFind(doErrorMsg: false)?._navMeshDoorLinks;
        if (links == null)
        {
            return;
        }
        _ready = true;
        if (!SettingOn)
        {
            TacticDiagnostics.Count("door.carve.off");
            return;
        }
        foreach (NavMeshDoorLink link in links)
        {
            if (link == null || link.Door == null || link.Carver_Opened == null || link.ShallTryInteract)
            {
                continue;
            }
            _links.Add(link);
            if (!_byDoor.TryGetValue(link.Door, out var list))
            {
                _byDoor[link.Door] = list = new List<NavMeshDoorLink>();
                link.Door.OnDoorStateChanged += OnDoorStateChanged;
            }
            list.Add(link);
            if (link.Door.DoorState == EDoorState.Open)
            {
                _waiting.Add(link);
            }
        }
        Logger.LogDebug($"[OpenLeafCarver] {_links.Count} door links, {_waiting.Count} open at start");
        TacticDiagnostics.Count("door.carve.start");
    }

    private static void OnDoorStateChanged(WorldInteractiveObject obj, EDoorState prev, EDoorState next)
    {
        if (obj is not Door door || !_byDoor.TryGetValue(door, out var list))
        {
            return;
        }
        foreach (NavMeshDoorLink link in list)
        {
            if (next == EDoorState.Open)
            {
                if (!_carved.Contains(link) && !_waiting.Contains(link))
                {
                    _waiting.Add(link);
                }
                continue;
            }
            _waiting.Remove(link);
            SetCarve(link, false);
        }
    }

    private static void SetCarve(NavMeshDoorLink link, bool on)
    {
        if (ReferenceEquals(link, null))
        {
            _carved.Remove(link);
            return;
        }
        if (on == _carved.Contains(link))
        {
            return;
        }
        if (on)
        {
            _carved.Add(link);
            _cuts.Add(new Cut { Mid = link.MidOpen, Time = Time.time });
        }
        else
        {
            _carved.Remove(link);
        }
        Turn(link.Carver_Opened, on);
        Turn(link.Carver_Breached, on);
        TacticDiagnostics.Count(on ? "door.carve.on" : "door.carve.off.closed");
    }

    private static void Turn(UnityEngine.AI.NavMeshObstacle carver, bool on)
    {
        if (carver == null)
        {
            return;
        }
        carver.carving = on;
        carver.enabled = on;
    }

    private static bool SomeoneNear(Vector3 point)
    {
        var players = Comfort.Common.Singleton<GameWorld>.Instance?.AllAlivePlayersList;
        if (players == null)
        {
            return false;
        }
        for (int i = 0; i < players.Count; i++)
        {
            Player p = players[i];
            if (p != null && (p.Position - point).sqrMagnitude < CLEAR_RADIUS * CLEAR_RADIUS)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Raid end (DoorHandler.Dispose): unsubscribe and drop the raid's links (RamCleaner leak holders).</summary>
    public static void Clear()
    {
        foreach (var kv in _byDoor)
        {
            if (kv.Key != null)
            {
                kv.Key.OnDoorStateChanged -= OnDoorStateChanged;
            }
        }
        _byDoor.Clear();
        _links.Clear();
        _waiting.Clear();
        _carved.Clear();
        _cuts.Clear();
        _gone.Clear();
        _ready = false;
        _firstTry = -1f;
        _nextCheck = 0f;
    }
}
