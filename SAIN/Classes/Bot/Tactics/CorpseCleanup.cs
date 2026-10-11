using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using EFT;
using EFT.Interactive;
using HarmonyLib;
using SAIN.Plugin;
using SAIN.Preset.Shared.GlobalSettings;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork, simulation only: hide the oldest bot corpses once there are more than the limit (F6 General > Performance,
/// only in a preset whose name contains "TEST"). 7th sim: 64-67 fps up to ~86 corpses, then 47 -> 32 fps at 104 -> 170
/// corpses while the managed heap only went up and down - this tests whether the bodies are what costs the frames.
/// A hidden corpse is deactivated (no render/physics), not destroyed, so nothing that still references it breaks.
/// </summary>
public static class CorpseCleanup
{
    private const float MIN_AGE = 20f;

    private static readonly Queue<(Player player, float time)> _corpses = new();

    // Every death is checked once, 3s later, by what the body looks like - whatever the cause (user 2026-09-29: "you never
    // find the walking corpses in the log"): no ragdoll corpse built, character controller still on, or the body moved
    // 1.5m+ since it died -> [DeadBug] "body still standing/moving".
    private static readonly List<(Player player, float time, Vector3 pos, string name)> _toInspect = new();

    public static int Hidden { get; private set; }

    private static int Limit
    {
        get
        {
            string name = SAINPlugin.LoadedPreset?.Info?.Name;
            if (string.IsNullOrEmpty(name) || name.IndexOf("TEST", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                return 0;
            }
            float limit = GlobalSettingsClass.Instance?.General?.Performance?.SimCorpseLimit ?? 0f;
            return limit >= 1f ? Mathf.RoundToInt(limit) : 0;
        }
    }

    public static void OnBotDied(Player player)
    {
        if (player != null)
        {
            _corpses.Enqueue((player, Time.time));
            _toInspect.Add((player, Time.time, player.Position, player.name));
        }
    }

    private static void Inspect()
    {
        for (int i = _toInspect.Count - 1; i >= 0; i--)
        {
            var (player, time, pos, name) = _toInspect[i];
            if (Time.time - time < 3f)
            {
                continue;
            }
            _toInspect.RemoveAt(i);
            if (player == null || player.gameObject == null || !player.gameObject.activeSelf)
            {
                continue;
            }
            bool noCorpse = SAIN.Patches.Generic.DeathRescuePatch.CorpseRef(player) == null;
            bool controllerOn = false;
            try
            {
                controllerOn = player._characterController != null && player._characterController.isEnabled;
            }
            catch
            {
            }
            Vector3 d = player.Position - pos;
            d.y = 0f;
            float moved = d.magnitude;
            if (noCorpse || controllerOn || moved > 1.5f)
            {
                TacticDiagnostics.Count("deadBug.bodyNotRagdoll");
                if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat(
                    $"[DeadBug] {name} 3s after death: ragdoll corpse built={!noCorpse}, character controller on={controllerOn}, body moved {moved:0.0}m "
                        + $"at ({player.Position.x:0},{player.Position.z:0}) - WALKING/STANDING CORPSE (see [DeadBug] handler lines above and BepInEx LogOutput.log)"
                );
            }
            else
            {
                TacticDiagnostics.Count("deadBug.bodyOk");
            }
        }
    }

    public static void Tick()
    {
        Inspect();
        int limit = Limit;
        if (limit <= 0)
        {
            return;
        }
        while (_corpses.Count > limit)
        {
            var (player, time) = _corpses.Peek();
            if (Time.time - time < MIN_AGE)
            {
                return;
            }
            _corpses.Dequeue();
            if (player == null || player.gameObject == null || !player.gameObject.activeSelf || player.HealthController?.IsAlive == true)
            {
                continue;
            }
            if (Delete && TryDelete(player))
            {
                continue;
            }
            player.gameObject.SetActive(false);
            Hidden++;
            TacticDiagnostics.Count("sim.corpseHidden");
        }
    }

    // zzap, 11th sim (user 2026-10-01): hiding keeps the whole body in memory (83 corpses, 43 hidden, RAM still +293 MB per
    // death) - so old corpses are really removed. TEST preset only (SimCorpseDelete).
    // 2026-10-12 (user: "once deleting starts, bots that die right then vanish", and living bots that can be searched with
    // someone else's gear): this used GameWorld.DestroyLoot, which ends in Corpse.Kill -> LootItem.Kill ->
    // AssetPoolObject.ReturnToPool(gameObject). The corpse lives on the player's own GameObject (Corpse.CreateCorpse adds
    // it there) and bots are pooled, so the dead player went back to the bot pool with the old Corpse still on it and the
    // next spawned bot reused it: alive but searchable (the old body's inventory), and on its own death the reused, already
    // killed Corpse took over. Now done like RemoveTheDead (jbs4bmx, MIT): DestroyLoot's list bookkeeping without Kill, then
    // Object.Destroy the player - it never goes back to the pool.
    private static bool Delete
    {
        get { return GlobalSettingsClass.Instance?.General?.Performance?.SimCorpseDelete == true; }
    }

    public static int Deleted { get; private set; }

    private static readonly AccessTools.FieldRef<GameWorld, System.Action<IKillable>> _onLootItemDestroyed =
        AccessTools.FieldRefAccess<GameWorld, System.Action<IKillable>>("_onLootItemDestroyed");

    private static bool TryDelete(Player player)
    {
        var corpse = SAIN.Patches.Generic.DeathRescuePatch.CorpseRef(player);
        var world = Comfort.Common.Singleton<GameWorld>.Instance;
        if (corpse == null || world == null)
        {
            return false;
        }
        try
        {
            // 10/10 Factory+Labs sim: 57x "KeyNotFoundException 'Body'" from CorpseRagdoll's settle coroutine polling a
            // disposed body - stop the corpse's coroutines first.
            corpse.StopAllCoroutines();
            // GameWorld.DestroyLoot(IKillable) minus the final loot.Kill() (= return to pool).
            _onLootItemDestroyed(world)?.Invoke(corpse);
            world.LootList.Remove(corpse);
            if (corpse.ItemOwner != null)
            {
                world.ItemOwners.Remove(corpse.ItemOwner);
            }
            world.LootItems.Remove(corpse.GetNetId());
            foreach (IPlayer registered in world.RegisteredPlayers)
            {
                world.GetAlivePlayerByProfileID(registered.ProfileId)?.ResetInteractionRaycast(corpse);
            }
            DropOrbitCorpseWaypoint(corpse);
            Object.Destroy(player.gameObject);
            Deleted++;
            TacticDiagnostics.Count("sim.corpseDeleted");
            return true;
        }
        catch (System.Exception ex)
        {
            TacticDiagnostics.Count("sim.corpseDeleteFailed");
            Logger.LogWarning($"[SAIN zzap] sim corpse delete failed for {player.name}, hiding instead: {ex.Message}");
            return false;
        }
    }

    // 2026-10-12: ORBIT registers every corpse as a loot waypoint (CorpseRegistrationPatch, Target = the Corpse) and only
    // drops loose-loot waypoints when loot is killed - corpse waypoints stay for the raid. Its loot start checks
    // Target == null (Unity null after Destroy), so a deleted corpse can't be looted, but squads could still be sent to
    // the empty spot and stand there. Drop it through ORBIT's public WaypointSystem (Cells / Waypoint.Target /
    // RemoveWaypoint) by reflection - ORBIT itself is not changed.
    private static PropertyInfo _orbitManager;
    private static bool _orbitLooked;

    private static void DropOrbitCorpseWaypoint(Corpse corpse)
    {
        try
        {
            if (!_orbitLooked)
            {
                _orbitLooked = true;
                System.Type type = AccessTools.TypeByName("Orbit.Core.OrbitManager");
                if (type != null)
                {
                    _orbitManager = AccessTools.Property(typeof(Comfort.Common.Singleton<>).MakeGenericType(type), "Instance");
                }
            }
            object manager = _orbitManager?.GetValue(null);
            object waypoints = manager == null ? null : Traverse.Create(manager).Field("WaypointSystem").GetValue();
            if (waypoints == null || Traverse.Create(waypoints).Property("Cells").GetValue() is not System.Array cells)
            {
                return;
            }
            foreach (object cell in cells)
            {
                if (Traverse.Create(cell).Field("Waypoints").GetValue() is not IList list)
                {
                    continue;
                }
                for (int i = 0; i < list.Count; i++)
                {
                    object waypoint = list[i];
                    if (!ReferenceEquals(Traverse.Create(waypoint).Field("Target").GetValue(), corpse))
                    {
                        continue;
                    }
                    int id = Traverse.Create(waypoint).Property("Id").GetValue<int>();
                    Traverse.Create(waypoints).Method("RemoveWaypoint", id).GetValue();
                    TacticDiagnostics.Count("sim.corpseOrbitWaypointDropped");
                    return;
                }
            }
        }
        catch (System.Exception ex)
        {
            TacticDiagnostics.Count("sim.corpseOrbitWaypointFailed");
            Logger.LogWarning($"[SAIN zzap] dropping ORBIT's waypoint for a deleted corpse failed: {ex.Message}");
        }
    }

    public static void Clear()
    {
        _corpses.Clear();
        _toInspect.Clear();
        Hidden = 0;
        Deleted = 0;
    }
}
