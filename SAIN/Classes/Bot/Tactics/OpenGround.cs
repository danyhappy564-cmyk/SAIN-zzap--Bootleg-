using SAIN.Components;
using UnityEngine;
using UnityEngine.AI;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork: "am I standing in the open?" and "where's the nearest spot that isn't?" without SAIN's cover finder - that one
/// only runs while the bot has an enemy, so right after a fight its list is empty (sim round 3: post-combat "to cover" fired
/// 0 times; user: "after a fight they stand in wide open spaces and get shot first").
///   Enclosure(pos): of 8 directions at hip and chest height, how many hit something solid within 2.5m (0 = open field).
///   FindCover: navmesh points 3-12m around (4 rings x 12), reachable by a path under 20m, scored by enclosure +3 if it
///   breaks the line from the last threat. One-time cost at the start of the post-combat window.
/// </summary>
public static class OpenGround
{
    private const float ENCLOSE_DIST = 2.5f;

    public static int Enclosure(Vector3 pos, float height = 1.1f)
    {
        int n = 0;
        Vector3 origin = pos + Vector3.up * height;
        for (int i = 0; i < 8; i++)
        {
            Vector3 dir = Quaternion.AngleAxis(i * 45f, Vector3.up) * Vector3.forward;
            if (Physics.Raycast(origin, dir, ENCLOSE_DIST, LayersMaskController.HighPolyWithTerrainMask))
            {
                n++;
            }
        }
        return n;
    }

    public static bool IsOpen(Vector3 pos, Vector3? threat)
    {
        if (Enclosure(pos) >= 3)
        {
            return false;
        }
        // Something between me and where the fight was counts as cover too.
        return threat == null || !BlocksLine(pos, threat.Value);
    }

    private static bool BlocksLine(Vector3 pos, Vector3 threat)
    {
        return Physics.Linecast(threat + Vector3.up * 1.4f, pos + Vector3.up * 1.0f, LayersMaskController.HighPolyWithTerrainMask);
    }

    private static readonly float[] _near = { 3f, 6f, 9f, 12f };
    private static readonly float[] _far = { 16f, 20f, 25f };

    /// <summary>Near pass (3-12m, path 20m, needs a decent spot), then a far pass (16-25m, path 35m, anything that beats the
    /// open: walls on 2 sides or the line from the fight blocked) - in a wide hall, a long run to cover beats sitting in the open.</summary>
    public static Vector3? FindCover(BotComponent bot, Vector3? threat, out string why, out bool far)
    {
        far = false;
        Vector3? p = Search(bot, threat, _near, 20f, 2.5f, out why);
        if (p == null)
        {
            p = Search(bot, threat, _far, 35f, 1.5f, out why);
            far = p != null;
            if (p == null)
            {
                why = "none within 25m";
            }
        }
        return p;
    }

    private static Vector3? Search(BotComponent bot, Vector3? threat, float[] rings, float maxPath, float minScore, out string why)
    {
        why = null;
        Vector3 pos = bot.Position;
        Vector3? best = null;
        float bestScore = minScore;
        var path = new NavMeshPath();
        foreach (float r in rings)
        {
            for (int i = 0; i < 12; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(i * 30f, Vector3.up) * Vector3.forward;
                if (!NavMesh.SamplePosition(pos + dir * r, out NavMeshHit hit, 1.2f, -1))
                {
                    continue;
                }
                Vector3 p = hit.position;
                int enclosure = Enclosure(p, 1.0f);
                bool blocks = threat != null && BlocksLine(p, threat.Value);
                // Closer is better; toward the threat is worse (don't walk into his friends).
                float score = enclosure + (blocks ? 3f : 0f) - r * 0.08f;
                if (threat != null && Vector3.Dot((p - pos).normalized, (threat.Value - pos).normalized) > 0.5f)
                {
                    score -= 1f;
                }
                if (score <= bestScore)
                {
                    continue;
                }
                if (!NavMesh.CalculatePath(pos, p, -1, path) || path.status != NavMeshPathStatus.PathComplete || PathLength(path) > maxPath)
                {
                    continue;
                }
                bestScore = score;
                best = p;
                why = $"{(p - pos).magnitude:0}m, walls on {enclosure}/8 sides{(blocks ? ", blocks the line from the fight" : "")}";
            }
            if (best != null && bestScore >= 6f)
            {
                break; // good enough - don't walk further for a slightly better one
            }
        }
        return best;
    }

    private static float PathLength(NavMeshPath path)
    {
        float len = 0f;
        var corners = path.corners;
        for (int i = 1; i < corners.Length; i++)
        {
            len += (corners[i] - corners[i - 1]).magnitude;
        }
        return len;
    }
}
