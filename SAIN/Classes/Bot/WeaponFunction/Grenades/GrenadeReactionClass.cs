using System.Collections.Generic;
using System.Reflection;
using EFT;
using HarmonyLib;
using SAIN.Components;
using SAIN.Models.Enums;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.SubComponents;
using UnityEngine;

namespace SAIN.SAINComponent.Classes.WeaponFunction;

public class GrenadeVelocityTracker : MonoBehaviour
{
    public Vector3 Velocity { get; private set; }
    public float VelocityMagnitude { get; private set; }

    private const float GRENADE_UPDATE_FREQUENCY = 0.5f;

    public void Awake()
    {
        _grenade = this.GetComponent<Grenade>();
        _grenade.DestroyEvent += GrenadeDestroyed;
        _rigidBody = (Rigidbody)_rigidBodyField.GetValue(_grenade);
    }

    public void Update()
    {
        if (_grenade == null)
        {
            return;
        }

        if (_rigidBody == null)
        {
            GrenadeDestroyed(_grenade);
            return;
        }
        if (_nextUpdateTime < Time.time)
        {
            _nextUpdateTime = Time.time + GRENADE_UPDATE_FREQUENCY;
            Velocity = _rigidBody.velocity;
            VelocityMagnitude = Velocity.magnitude;
            //Logger.LogInfo($"Grenade {_grenade.Id} Velocity [{Velocity}] Magnitude: [{VelocityMagnitude}]");
        }
    }

    private Rigidbody _rigidBody;
    private Grenade _grenade;

    static GrenadeVelocityTracker()
    {
        _rigidBodyField = AccessTools.Field(typeof(Throwable), "Rigidbody");
    }

    private void GrenadeDestroyed(Throwable grenade)
    {
        if (grenade != null)
        {
            grenade.DestroyEvent -= GrenadeDestroyed;
        }
        Destroy(this);
    }

    private static FieldInfo _rigidBodyField;
    private float _nextUpdateTime;
}

public class GrenadeReactionClass : BotSubClass<BotGrenadeManager>, IBotClass
{
    /// <summary>
    /// Distance at which a grenade is tracked at all. Whether the bot reacts is governed separately
    /// by GRENADE_REACT_DISTANCE.
    /// </summary>
    private const float MAX_ENEMY_GRENADE_DIST_TOCARE = 125f;

    /// <summary>
    /// Distance at which a tracked grenade is worth reacting to.
    /// </summary>
    private const float GRENADE_REACT_DISTANCE = 18f;

    /// <summary>
    /// Beyond this range, cover between the bot and the blast is enough to justify holding position.
    /// Closer than this a frag still reaches around most things, so the bot moves regardless.
    /// </summary>
    private const float OCCLUSION_TRUST_DISTANCE = 7f;

    /// <summary>
    /// Inside this range the blast is the only thing that matters, so every personality breaks
    /// straight out of it. Pushing through a grenade at your feet is suicide, not aggression.
    /// </summary>
    private const float BLAST_OVERRIDE_DISTANCE = 6f;

    /// <summary>
    /// Angle squadmates are fanned apart by, so a group does not pile into one escape route.
    /// </summary>
    private const float SQUAD_FAN_ANGLE = 35f;

    /// <summary>
    /// Base chance a bot notices a given grenade, before difficulty scaling.
    /// </summary>
    private const float BASE_NOTICE_CHANCE = 0.8f;

    /// <summary>
    /// How long a CS gas canister still gets the normal Scatter/Push dodge after being thrown,
    /// before bots give up trying to outrun something that lingers far longer than a frag and just
    /// accept it instead.
    /// </summary>
    private const float GAS_DODGE_WINDOW = 4f;

    /// <summary>
    /// Radius counted as "standing in the cloud" for TickGasExposure below. Matches Manimal-CSGas's
    /// own Plugin.GasMaxRadius default (Volumetric.MaxRadiusM = 8, "the clamp 1.0 applies to
    /// volumetric throwables" per that mod's own comment) rather than a guess.
    /// </summary>
    private const float GAS_EXPOSURE_RADIUS = 8f;

    /// <summary>
    /// How long after being thrown a CS gas canister is still treated as actively affecting anyone
    /// standing in it. Matches the item's own EmitTime override (30) in Manimal-CSGas's
    /// ServerModFiles/db/CustomItems/cs_gas_grenade.json, not a guess - and, critically, not tied to
    /// the actual canister object's lifetime the way the old Scatter/Push reaction was (that
    /// coupling is what caused the stuck-forever bug this whole feature replaces).
    /// </summary>
    private const float GAS_EXPOSURE_WINDOW = 30f;

    private const float GAS_FLASH_REFRESH_INTERVAL = 1f;
    private const float GAS_FLASH_DURATION = 1.5f;

    /// <summary>
    /// BlackDiv-zzap--Bootleg-'s six custom WildSpawnType roles (WildSpawnTypeExtensions.BDTypeEnums
    /// in that mod's source - blackDivLead/Assault/Breacher/Support, bossWedge, blackDivIb). Black
    /// Division's concept mandates a full gas mask, so they neither dodge nor get the exposure
    /// debuff from CS gas at all - everyone else still does. Hardcoded here rather than referencing
    /// BlackDiv's assembly directly, since SAIN has no dependency on it and must keep working
    /// without it installed.
    /// </summary>
    private static readonly HashSet<int> BLACK_DIVISION_ROLES = [848420, 848421, 848422, 848423, 848424, 848426];

    private bool IsBlackDivision => BLACK_DIVISION_ROLES.Contains((int)Bot.Info.Profile.WildSpawnType);

    public GrenadeTrackerClass DangerGrenade { get; private set; }
    public Vector3? GrenadeDangerPoint
    {
        get { return DangerGrenade?.DangerPoint; }
    }

    public Dictionary<Throwable, GrenadeTrackerClass> EnemyGrenadesList { get; private set; } = [];

    public GrenadeReactionClass(BotGrenadeManager ThrowWeap)
        : base(ThrowWeap) { }

    public override void Init()
    {
        var grenadeController = BotManagerComponent.Instance.GrenadeController;
        grenadeController.OnGrenadeCollision += GrenadeCollision;
        grenadeController.OnGrenadeThrown += EnemyGrenadeThrown;
        grenadeController.OnGrenadeDangerUpdated += GrenadeDangerUpdated;
        CatchUpOnActiveGasGrenades(grenadeController);
        base.Init();
    }

    /// <summary>
    /// A bot that spawns mid-raid (wave spawns, reinforcement calls, APBS-style continuous
    /// spawning) never receives OnGrenadeThrown for a CS gas canister thrown before it existed -
    /// that event fires exactly once, at the moment of the throw, to whichever bots already exist
    /// then. Since a CS gas cloud lingers for up to GAS_EXPOSURE_WINDOW seconds (vs. a frag's
    /// ~3.5s), a newly-spawned bot walking into an already-active cloud is a real, likely gap -
    /// unlike a frag, which is long gone by the time reinforcements would matter. Runs once here at
    /// Init, not per tick, so the cost is one scan of the map's currently-live grenades (typically
    /// 0-5) per bot spawn, not a per-frame cost.
    /// </summary>
    private void CatchUpOnActiveGasGrenades(GrenadeController grenadeController)
    {
        // Everything this method tracks is CS gas by definition (see the IsCsGasGrenade filter
        // below) - Black Division is fully immune to it, so there is nothing for them to catch up
        // on here at all.
        if (IsBlackDivision)
        {
            return;
        }
        foreach (Throwable throwable in grenadeController.ActiveGrenades.Keys)
        {
            if (
                throwable is not Grenade grenade
                || grenade.ProfileId == Bot.ProfileId
                || !IsCsGasGrenade(grenade)
                || EnemyGrenadesList.ContainsKey(grenade)
            )
            {
                continue;
            }
            // No thrower/landing-distance gate here (unlike EnemyGrenadeThrown) - if it's still
            // live and it's gas, just track it; ManualUpdate/UpdateDangerGrenade already ignore
            // anything outside GRENADE_REACT_DISTANCE, so a tracker for a canister on the far side
            // of the map is inert, not wrong.
            EnemyGrenadesList.Add(
                grenade,
                new GrenadeTrackerClass(Bot, grenade, grenade.transform.position, GetReactionTime(), RollWillNotice())
            );
            grenade.DestroyEvent += RemoveGrenade;
        }
    }

    public override void ManualUpdate()
    {
        foreach (var tracker in EnemyGrenadesList.Values)
        {
            tracker?.Update();
        }
        UpdateDangerGrenade();
        TickGasExposure();
        base.ManualUpdate();
    }

    /// <summary>
    /// Standing inside an active gas canister's cloud degrades the bot the same way a flashbang
    /// does (accuracy/vision/hearing, via the existing BotFlashedClass) instead of the old
    /// approach of treating the canister as a live blast threat for its whole lifetime. This is
    /// bounded by construction - ApplyFlash sets a hard end-time, so if the bot leaves the cloud
    /// or GAS_EXPOSURE_WINDOW runs out, the effect simply stops getting refreshed and expires on
    /// its own. No dependency on the canister object ever actually being destroyed.
    ///
    /// Scans every tracked grenade (EnemyGrenadesList), not just DangerGrenade (the single "closest
    /// current threat" slot the Scatter/Push reaction uses) - with several grenades in flight at
    /// once, DangerGrenade keeps getting reassigned to whichever one is momentarily closest, which
    /// starved this of a refresh whenever a nearer frag briefly took the slot and made the exposure
    /// debuff flicker on and off despite the bot never actually leaving the gas (2026-09-21 field
    /// report). Checking the whole list instead means a closer frag no longer interrupts it.
    /// </summary>
    private void TickGasExposure()
    {
        if (IsBlackDivision || _nextGasFlashTime > Time.time)
        {
            return;
        }
        GrenadeTrackerClass danger = null;
        foreach (var tracker in EnemyGrenadesList.Values)
        {
            if (
                tracker == null
                || !IsCsGasGrenade(tracker.Grenade)
                || tracker.TimeSinceThrown > GAS_EXPOSURE_WINDOW
                || (Bot.Position - tracker.DangerPoint).sqrMagnitude > GAS_EXPOSURE_RADIUS * GAS_EXPOSURE_RADIUS
            )
            {
                continue;
            }
            danger = tracker;
            break;
        }
        if (danger == null)
        {
            return;
        }
        _nextGasFlashTime = Time.time + GAS_FLASH_REFRESH_INTERVAL;
        Bot.Flashed.ApplyFlash(GAS_FLASH_DURATION, danger.DangerPoint);
    }

    private float _nextGasFlashTime;

    private void UpdateDangerGrenade()
    {
        GrenadeTrackerClass closest = null;
        float closestSqrDist = GRENADE_REACT_DISTANCE * GRENADE_REACT_DISTANCE;
        Vector3 botPosition = Bot.Position;

        foreach (var tracker in EnemyGrenadesList.Values)
        {
            if (tracker?.Grenade == null || !tracker.CanReact)
            {
                continue;
            }
            float sqrDist = (tracker.DangerPoint - botPosition).sqrMagnitude;
            if (sqrDist < closestSqrDist)
            {
                closestSqrDist = sqrDist;
                closest = tracker;
            }
        }

        if (closest == null)
        {
            // Confirms the avoid-grenade reaction actually clears once the grenade is gone, instead
            // of leaving the bot stuck not-shooting/prone indefinitely - only logs when there was
            // something to clear, so this stays silent on every ordinary tick with no grenade around.
            if (DangerGrenade != null)
            {
                Logger.LogWarning($"[GrenadeReaction] [{Bot.name}] danger cleared after [{Reaction}] - resuming normal behavior.");
            }
            DangerGrenade = null;
            Reaction = EGrenadeReaction.None;
            return;
        }

        if (DangerGrenade != closest)
        {
            DangerGrenade = closest;
            SetReaction(GetReaction(), closestSqrDist);
            return;
        }

        // Always route through SetReaction, including a None result, even while DangerGrenade is
        // unchanged - SetReaction already no-ops when the value hasn't actually changed, but the old
        // "only call it for a non-None result" guard here meant a same-object transition INTO None
        // (e.g. the gas dodge window in GetReaction expiring) never got applied and Reaction stayed
        // stuck at whatever it was last set to. Found via the same stuck-reaction investigation that
        // added the gas dodge window in the first place.
        SetReaction(GetReaction(), closestSqrDist);
    }

    private void SetReaction(EGrenadeReaction reaction, float sqrDistance)
    {
        if (Reaction == reaction)
        {
            return;
        }
        Reaction = reaction;
        Logger.LogWarning($"[GrenadeReaction] [{Bot.name}] reaction [{reaction}] at [{Mathf.Sqrt(sqrDistance)}m].");
    }

    public EGrenadeReaction Reaction { get; private set; }

    public bool ShallAvoidGrenade()
    {
        return DangerGrenade != null && Reaction != EGrenadeReaction.None;
    }

    /// <summary>
    /// True when the currently tracked danger is CS gas rather than a real explosive. AvoidGrenadeAction
    /// uses this to skip its "go prone, can't outrun the blast" fallback - that logic is timed off
    /// EstimatedTimeRemaining's frag-tuned fuse constant, which reads 0 (i.e. "no time left") within a
    /// few seconds of a CS gas canister landing since gas has no real fuse to count down. Without this
    /// check, any bot within BLAST_URGENT_DISTANCE would drop prone almost immediately instead of
    /// visibly scattering during its GAS_DODGE_WINDOW (2026-09-21 field report: looked like bots weren't
    /// reacting to gas at all, when they were actually just flopping down in place).
    /// </summary>
    public bool DangerIsCsGas => IsCsGasGrenade(DangerGrenade?.Grenade);

    /// <summary>
    /// Seconds before the current danger grenade is expected to go off, or zero when there is none.
    /// </summary>
    public float EstimatedTimeRemaining
    {
        get { return DangerGrenade?.EstimatedTimeRemaining ?? 0f; }
    }

    /// <summary>
    /// True when solid geometry sits between the bot and the blast. Fragments do not turn corners, so
    /// a bot already behind something has far less reason to abandon its position than the raw
    /// distance suggests.
    /// </summary>
    public bool BlastIsOccluded()
    {
        Vector3? danger = GrenadeDangerPoint;
        if (danger == null)
        {
            return false;
        }

        Vector3 from = Bot.Transform.WeaponRoot;
        Vector3 to = danger.Value + (Vector3.up * 0.1f);
        Vector3 direction = to - from;
        float distance = direction.magnitude;
        if (distance < 0.1f)
        {
            return false;
        }
        return Physics.Raycast(from, direction.normalized, distance, LayersMaskController.HighPolyWithTerrainMask);
    }

    /// <summary>
    /// Identifies Manimal-CSGas's canister as precisely as SAIN can from just the world Grenade
    /// object it tracks (no TemplateId/Item reference reachable from there - see the long comment
    /// on IsCsGasGrenade's only call sites for why). SmokeGrenade + not flagged as real smoke is, in
    /// practice, unique to this specific item in the current modlist: vanilla smoke always reports
    /// CollisionSound == smoke, and nothing else installed shares this combination.
    /// </summary>
    private static bool IsCsGasGrenade(Grenade grenade)
    {
        return grenade is SmokeGrenade && grenade.GrenadeSettings.CollisionSound != GrenadeSettings.CollisionSounds.smoke;
    }

    private EGrenadeReaction GetReaction()
    {
        // Real smoke (CollisionSound == smoke) is never a threat - no reaction, ever.
        if (DangerGrenade.Grenade?.GrenadeSettings.CollisionSound == GrenadeSettings.CollisionSounds.smoke)
        {
            return EGrenadeReaction.None;
        }

        if (IsCsGasGrenade(DangerGrenade.Grenade))
        {
            // Black Division's concept mandates a full gas mask (BLACK_DIVISION_ROLES) - they don't
            // even dodge, let alone get the exposure debuff in TickGasExposure.
            if (IsBlackDivision)
            {
                return EGrenadeReaction.None;
            }

            // Confirmed via a field log (2026-09-21): the canister's CollisionSound isn't "smoke"
            // (it's a modded item, not vanilla smoke), so it fell through as a live frag-type threat
            // and, because it sits and keeps emitting far longer than a frag's near-instant
            // destruction, bots got stuck re-triggering Scatter/Push against it for the rest of its
            // lifetime - fleeing/going prone and not shooting the whole time. Give it the normal
            // short dodge (2026-09-21 follow-up: an outright ignore made bots stand still and eat it
            // instead) for a few seconds after the throw, then accept it - standing there dodging
            // something that lingers for tens of seconds isn't realistic either. Actual exposure
            // while standing in the cloud is handled separately by TickGasExposure/BotFlashedClass,
            // bounded on its own terms and not tied to this reaction at all.
            return DangerGrenade.TimeSinceThrown < GAS_DODGE_WINDOW ? EGrenadeReaction.Scatter : EGrenadeReaction.None;
        }

        // Any other SmokeGrenade-based item (a variant this modlist doesn't have yet, or real smoke
        // that somehow isn't flagged smoke) - treat like real smoke, never a threat. Only
        // IsCsGasGrenade's specific combination gets the dodge+exposure handling above by design;
        // falling into the frag-panic path below for anything SmokeGrenade-based is exactly the bug
        // class this whole feature exists to avoid.
        if (DangerGrenade.Grenade is SmokeGrenade)
        {
            return EGrenadeReaction.None;
        }

        float distance = (Bot.Position - (GrenadeDangerPoint ?? Bot.Position)).magnitude;
        if (distance > OCCLUSION_TRUST_DISTANCE && BlastIsOccluded())
        {
            return EGrenadeReaction.None;
        }

        if (distance < BLAST_OVERRIDE_DISTANCE)
        {
            return EGrenadeReaction.Scatter;
        }

        switch (Bot.Info.Personality)
        {
            case EPersonality.Wreckless:
            case EPersonality.GigaChad:
            case EPersonality.Chad:
                return Bot.GoalEnemy != null ? EGrenadeReaction.Push : EGrenadeReaction.Scatter;

            case EPersonality.Rat:
            case EPersonality.Coward:
            case EPersonality.Timmy:
                return EGrenadeReaction.Retreat;

            case EPersonality.SnappingTurtle:
                return EGrenadeReaction.Relocate;

            default:
                return EGrenadeReaction.Scatter;
        }
    }

    /// <summary>
    /// Direction the bot should break in, pointing away from the blast. Squadmates reacting to the
    /// same grenade are fanned apart so a group does not pile into one escape route and eat the next one.
    /// </summary>
    public Vector3 GetEscapeDirection()
    {
        Vector3 dangerPoint = GrenadeDangerPoint ?? Bot.Position;
        Vector3 away = Bot.Position - dangerPoint;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
        {
            // Standing on top of it, so any direction beats staying put.
            away = -Bot.LookDirection;
            away.y = 0f;
        }
        away = away.normalized;

        // Aggressive bots advance on the thrower, but only once clear of the blast, and only if the
        // thrower is not on the far side of the grenade.
        if (
            Reaction == EGrenadeReaction.Push
            && (Bot.Position - dangerPoint).sqrMagnitude > BLAST_OVERRIDE_DISTANCE * BLAST_OVERRIDE_DISTANCE
        )
        {
            Enemy goalEnemy = Bot.GoalEnemy;
            if (goalEnemy != null)
            {
                Vector3 toEnemy = goalEnemy.EnemyPosition - Bot.Position;
                toEnemy.y = 0f;
                if (toEnemy.sqrMagnitude > 0.01f)
                {
                    toEnemy = toEnemy.normalized;
                    if (Vector3.Dot(away, toEnemy) > 0f)
                    {
                        away = (away + toEnemy).normalized;
                    }
                }
            }
        }

        int fanIndex = GetSquadFanIndex();
        if (fanIndex == 0)
        {
            return away;
        }

        // Alternate left/right of the escape vector, widening with each additional member.
        float angle = SQUAD_FAN_ANGLE * ((fanIndex + 1) / 2) * (fanIndex % 2 == 0 ? 1f : -1f);
        return Quaternion.Euler(0f, angle, 0f) * away;
    }

    /// <summary>
    /// Stable per-bot slot within its squad, so the fan direction does not flip between frames.
    /// </summary>
    private int GetSquadFanIndex()
    {
        var squad = Bot.Squad;
        if (squad?.BotInGroup != true)
        {
            return 0;
        }

        int index = 0;
        string myId = Bot.ProfileId;
        foreach (string memberId in squad.Members.Keys)
        {
            if (memberId == myId)
            {
                return index;
            }
            index++;
        }
        return 0;
    }

    public override void Dispose()
    {
        var grenadeController = BotManagerComponent.Instance.GrenadeController;
        grenadeController.OnGrenadeCollision -= GrenadeCollision;
        grenadeController.OnGrenadeThrown -= EnemyGrenadeThrown;
        grenadeController.OnGrenadeDangerUpdated -= GrenadeDangerUpdated;

        foreach (var tracker in EnemyGrenadesList.Values)
        {
            if (tracker?.Grenade != null)
            {
                tracker.Grenade.DestroyEvent -= RemoveGrenade;
            }
        }

        EnemyGrenadesList.Clear();
        base.Dispose();
    }

    public void EnemyGrenadeThrown(Grenade grenade, Vector3 dangerPoint, string profileId)
    {
        if (Bot == null || profileId == Bot.ProfileId || !Bot.BotActive)
        {
            return;
        }
        // Black Division is fully immune to CS gas (gas mask) - never worth tracking it for them at
        // all. Every other grenade type still gets tracked normally.
        if (IsCsGasGrenade(grenade) && IsBlackDivision)
        {
            return;
        }

        Enemy enemy = Bot.EnemyController.GetEnemy(profileId, false);
        bool throwerIsKnownAndClose = enemy != null && enemy.RealDistance <= MAX_ENEMY_GRENADE_DIST_TOCARE;
        bool landingNearMe = (dangerPoint - Bot.Position).sqrMagnitude <= MAX_ENEMY_GRENADE_DIST_TOCARE * MAX_ENEMY_GRENADE_DIST_TOCARE;

        if (!EnemyGrenadesList.ContainsKey(grenade) && (throwerIsKnownAndClose || landingNearMe))
        {
            EnemyGrenadesList.Add(grenade, new GrenadeTrackerClass(Bot, grenade, dangerPoint, GetReactionTime(), RollWillNotice()));
            grenade.DestroyEvent += RemoveGrenade;
            return;
        }
        BotOwner.BewareGrenade.AddGrenadeDanger(dangerPoint, grenade);
    }

    private void GrenadeCollision(Grenade grenade, float maxRange)
    {
        if (EnemyGrenadesList.TryGetValue(grenade, out var Tracker))
        {
            Tracker?.CheckHeardGrenadeCollision(maxRange);
        }
    }

    private void GrenadeDangerUpdated(Grenade grenade, Vector3 Danger)
    {
        if (EnemyGrenadesList.TryGetValue(grenade, out var Tracker))
        {
            Tracker.UpdateGrenadeDanger(Danger);
        }
    }

    private void RemoveGrenade(Throwable grenade)
    {
        if (grenade != null)
        {
            grenade.DestroyEvent -= RemoveGrenade;
            EnemyGrenadesList.Remove(grenade);
        }
        // Confirms the tracker actually gets cleaned up when the grenade object is destroyed - if a
        // bot's reaction never clears (2026-09-21 field report), the first thing to rule out is
        // whether Grenade.DestroyEvent fires at all for an enemy-thrown grenade, or the tracker
        // outlives the grenade and keeps feeding UpdateDangerGrenade() a stale DangerGrenade forever.
        Logger.LogWarning($"[GrenadeReaction] [{Bot.name}] grenade removed from tracker, [{EnemyGrenadesList.Count}] remaining.");
    }

    private bool RollWillNotice()
    {
        float chance = Mathf.Clamp01(BASE_NOTICE_CHANCE * Bot.Info.Profile.DifficultyModifier);
        return Random.value <= chance;
    }

    public float GetReactionTime()
    {
        float reactionTime = 0.25f;
        reactionTime /= Bot.Info.Profile.DifficultyModifier;
        reactionTime *= Random.Range(0.75f, 1.25f);
        return Mathf.Clamp(reactionTime, 0.2f, 1f);
    }
}
