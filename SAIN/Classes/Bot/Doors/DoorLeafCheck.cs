using EFT.Interactive;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Mover;

/// <summary>
/// zzap fork (2026-10-13, user screenshots: bots wedged between an open door leaf and the wall, or between the two leaves of
/// a double door, while aiming at a visible enemy - fine again once the enemy was gone): the moves a bot makes while it fights
/// (dogfight back-off to a random point, diamond-step side taps) only ask the NavMesh, and the pocket behind an open leaf is
/// walkable NavMesh - the leaf is a physics collider only. These checks see the leaf itself.
/// </summary>
public static class DoorLeafCheck
{
    private static int _mask;
    private static readonly Collider[] _buffer = new Collider[32];

    // Door leaves sit on the door layer or on (Door)LowPolyCollider - doordash uses the same two for the collider the player
    // capsule hits. Everything found is filtered down to colliders that belong to a Door.
    private static int Mask
    {
        get
        {
            if (_mask == 0)
            {
                _mask = (1 << LayersMaskController.DoorLayer) | LayerMask.GetMask("DoorLowPolyCollider", "LowPolyCollider");
            }
            return _mask;
        }
    }

    private static bool OpenLeaf(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }
        Door door = collider.GetComponentInParent<Door>();
        return door != null && door.DoorState != EDoorState.Shut && door.DoorState != EDoorState.Locked;
    }

    /// <summary>An open / opening door leaf within <paramref name="radius"/> of a standing body at <paramref name="point"/> (feet).</summary>
    public static bool OpenLeafNear(Vector3 point, float radius)
    {
        int count = Physics.OverlapSphereNonAlloc(point + Vector3.up * 1f, radius, _buffer, Mask, QueryTriggerInteraction.Ignore);
        bool found = false;
        for (int i = 0; i < count && i < _buffer.Length; i++)
        {
            if (OpenLeaf(_buffer[i]))
            {
                found = true;
                break;
            }
        }
        // static buffer: don't keep the raid's colliders alive into the menu (RamCleaner [leak holders] 10/14)
        System.Array.Clear(_buffer, 0, _buffer.Length);
        return found;
    }

    /// <summary>The door of the nearest open (not mid-swing) leaf within <paramref name="radius"/> of a standing body, or null.</summary>
    public static Door OpenLeafDoorNear(Vector3 point, float radius)
    {
        int count = Physics.OverlapSphereNonAlloc(point + Vector3.up * 1f, radius, _buffer, Mask, QueryTriggerInteraction.Ignore);
        Door best = null;
        float bestSq = float.MaxValue;
        for (int i = 0; i < count && i < _buffer.Length; i++)
        {
            Collider c = _buffer[i];
            Door door = c != null ? c.GetComponentInParent<Door>() : null;
            if (door == null || door.DoorState != EDoorState.Open)
            {
                continue;
            }
            float sq = (c.bounds.ClosestPoint(point + Vector3.up * 1f) - (point + Vector3.up * 1f)).sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = door;
            }
        }
        System.Array.Clear(_buffer, 0, _buffer.Length);
        return best;
    }

    /// <summary>An open / opening door leaf within <paramref name="distance"/> straight ahead along <paramref name="dir"/> (knee and chest height).</summary>
    public static bool OpenLeafAhead(Vector3 feet, Vector3 dir, float distance)
    {
        int mask = Mask;
        return (Physics.Raycast(feet + Vector3.up * 0.5f, dir, out RaycastHit low, distance, mask, QueryTriggerInteraction.Ignore) && OpenLeaf(low.collider))
            || (Physics.Raycast(feet + Vector3.up * 1.2f, dir, out RaycastHit high, distance, mask, QueryTriggerInteraction.Ignore) && OpenLeaf(high.collider));
    }
}
