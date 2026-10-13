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

    private static readonly Queue<(Player player, float time, string weaponId)> _corpses = new();

    // Every death is checked once, 3s later, by what the body looks like - whatever the cause (user 2026-09-29: "you never
    // find the walking corpses in the log"): no ragdoll corpse built, character controller still on, or the body moved
    // 1.5m+ since it died -> [DeadBug] "body still standing/moving".
    private static readonly List<(Player player, float time, Vector3 pos, string name)> _toInspect = new();

    public static int Hidden { get; private set; }

    private static bool TestPreset
    {
        get
        {
            string name = SAINPlugin.LoadedPreset?.Info?.Name;
            return !string.IsNullOrEmpty(name) && name.IndexOf("TEST", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    private static int Limit
    {
        get
        {
            float limit = GlobalSettingsClass.Instance?.General?.Performance?.SimCorpseLimit ?? 0f;
            return limit >= 1f ? Mathf.RoundToInt(limit) : 0;
        }
    }

    // 2026-10-12 (user: "delete corpses every 5 minutes - otherwise it can't be checked quickly without a 30-min sim"): the
    // limit (TEST 40) is only reached after ~25 min, so also clear every corpse that is MIN_AGE+ old every N minutes.
    private static float SweepSeconds
    {
        get
        {
            float minutes = GlobalSettingsClass.Instance?.General?.Performance?.SimCorpseSweepMinutes ?? 0f;
            return minutes > 0f ? minutes * 60f : 0f;
        }
    }

    private static float _nextSweep;

    public static void OnBotDied(Player player)
    {
        if (player != null)
        {
            // OnPlayerDead fires before HandsController.OnPlayerDead drops the gun - the item in hand is the one that lands
            string weaponId = null;
            try
            {
                weaponId = player.HandsController?.Item is EFT.InventoryLogic.Weapon w ? w.Id : null;
            }
            catch
            {
            }
            _corpses.Enqueue((player, Time.time, weaponId));
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
        if (!TestPreset)
        {
            return;
        }
        int limit = Limit;
        bool sweep = false;
        float sweepEvery = SweepSeconds;
        if (sweepEvery > 0f)
        {
            if (_nextSweep <= 0f)
            {
                _nextSweep = Time.time + sweepEvery;
            }
            else if (Time.time >= _nextSweep)
            {
                _nextSweep = Time.time + sweepEvery;
                sweep = true;
                TacticDiagnostics.Count("sim.corpseSweep");
            }
        }
        // Each pass dequeues one corpse or returns, so this ends.
        while (_corpses.Count > 0 && (sweep || (limit > 0 && _corpses.Count > limit)))
        {
            var (player, time, weaponId) = _corpses.Peek();
            if (Time.time - time < MIN_AGE)
            {
                return;
            }
            _corpses.Dequeue();
            if (player == null || player.gameObject == null || !player.gameObject.activeSelf || player.HealthController?.IsAlive == true)
            {
                continue;
            }
            Vector3 bodyPos = player.Position;
            if (Delete && TryDelete(player))
            {
                DeleteDroppedWeapon(weaponId, bodyPos);
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
            ForgetDecals(player);
            ForgetPlayer(world, player);
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

    // 2026-10-12 (sim crash 03:59): the game's TextureDecalsPainter (blood marks on bodies) works a queue of marks to paint
    // and only removes an entry after painting it. A queued mark on a deleted body jammed the queue (exception every frame
    // -> out of memory, see DecalPainterGuardPatch). Remove this body's queued marks before it is destroyed. Its painted
    // textures are left to CorpseDecalRelease (frees destroyed renderers' textures back to the pool every 5s).
    private static void ForgetDecals(Player player)
    {
        if (!Comfort.Common.Singleton<Systems.Effects.Effects>.Instantiated)
        {
            return;
        }
        TextureDecalsPainter painter = Comfort.Common.Singleton<Systems.Effects.Effects>.Instance.TexDecals;
        if (painter == null)
        {
            return;
        }
        try
        {
            var queue = SAIN.Patches.Generic.DecalPainterGuardPatch.Queue(painter);
            if (queue == null)
            {
                return;
            }
            Transform root = player.gameObject.transform;
            for (int i = queue.Count - 1; i >= 0; i--)
            {
                Renderer r = queue[i].Renderer;
                if (r == null || r.transform.IsChildOf(root))
                {
                    queue.RemoveAt(i);
                    TacticDiagnostics.Count("sim.corpseDecalQueueDropped");
                }
            }
        }
        catch (System.Exception ex)
        {
            TacticDiagnostics.Count("sim.corpseDecalCleanupFailed");
            Logger.LogWarning($"[SAIN zzap] clearing queued blood marks of a deleted corpse failed: {ex.Message}");
        }
    }

    // 04:55 sim (DynamicMaps CorpseMarkerProvider error when the map was opened): the destroyed body stayed in GameWorld's
    // player lists (RegisteredPlayers, _allPlayersEverExisted), and other mods reading Player.Position from them hit the
    // destroyed transform. Take it out of those lists first (what EFT does when a bot despawns).
    private static readonly AccessTools.FieldRef<GameWorld, Dictionary<string, Player>> _everExisted =
        AccessTools.FieldRefAccess<GameWorld, Dictionary<string, Player>>("_allPlayersEverExisted");

    private static void ForgetPlayer(GameWorld world, Player player)
    {
        try
        {
            if (world.RegisteredPlayers.Contains(player))
            {
                world.UnregisterPlayer(player);
            }
            _everExisted(world)?.Remove(player.ProfileId);
        }
        catch (System.Exception ex)
        {
            TacticDiagnostics.Count("sim.corpseUnregisterFailed");
            Logger.LogWarning($"[SAIN zzap] removing a deleted corpse from the player lists failed: {ex.Message}");
        }
    }

    // 10/14 (user screenshots: "died, and the body vanished leaving only the gun"): the gun a bot holds when it dies is
    // dropped as its own loot item, apart from the corpse - deleting the corpse left it lying there. Deleted with it, unless
    // someone picked it up (then it is no LootItem in the world any more) or it lies 10 m+ from the body.
    private static void DeleteDroppedWeapon(string weaponId, Vector3 body)
    {
        var world = Comfort.Common.Singleton<GameWorld>.Instance;
        if (string.IsNullOrEmpty(weaponId) || world == null)
        {
            return;
        }
        try
        {
            var list = world.LootList;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is not LootItem loot || loot.ItemId != weaponId)
                {
                    continue;
                }
                if ((loot.transform.position - body).sqrMagnitude > 10f * 10f)
                {
                    return;
                }
                world.DestroyLoot(loot);
                TacticDiagnostics.Count("sim.corpseWeaponDeleted");
                return;
            }
        }
        catch (System.Exception ex)
        {
            Logger.LogWarning($"[SAIN zzap] deleting a deleted corpse's dropped gun failed: {ex.Message}");
        }
    }

    public static void Clear()
    {
        _corpses.Clear();
        _toInspect.Clear();
        Hidden = 0;
        Deleted = 0;
        _nextSweep = 0f;
    }
}
