using System.Collections.Generic;
using EFT;
using EFT.InventoryLogic;
using SAIN.Components;
using SAIN.Models.Enums;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.GlobalSettings.Categories.General;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.Classes.WeaponFunction;
using UnityEngine;
using UnityEngine.AI;

namespace SAIN.SAINComponent.Classes.Tactics;

/// <summary>
/// zzap fork: repositioning around grenades and contact (ECombatDecision.Reposition, SAIN combat layer, so ORBIT is
/// unaffected). From Korean veteran-player advice:
///   NadeFlank - indoors, after throwing a frag at the enemy, move to a flanking angle as it goes off (the blast
///               covers the footsteps), then hold that angle.
///   Relocate  - outdoors, the enemy got first contact: once in cover and patched up (SAIN heals first), throw a
///               frag at them if possible and move to an angle they can't see.
///   Disengage - outdoors, the bot opened the fight but emptied a magazine without the kill: after the reload,
///               break line of sight and move back instead of re-peeking the same spot.
///   BaitPeek  - from cover, aggressive personalities step out and straight back once or twice to draw fire; if the
///               enemy empties their gun, SAIN's own rush-on-reload (EnemyDecisionClass.shallRushEnemy) takes over.
/// Plus fuse selection for SAIN's normal frag throws (ApplyFuseChoice).
/// F6: General > Reposition (zzap). Logs: [Reposition] + SUMMARY keys repo.* / nade.fuse.*.
/// </summary>
public class RepositionClass : BotComponentClassBase
{
    public enum EMode
    {
        None,
        NadeFlank,
        Relocate,
        Disengage,
        BaitPeek,
        FakeReload,
        GhostFlank,
    }

    private enum EPhase
    {
        None,
        WaitThrow,
        WaitBlast,
        Move,
        BaitOut,
        BaitShow,
        BaitBack,
        BaitWait,
        MagCheck,
        GhostNoise,
        GhostNoiseBack,
        Sneak,
        Hold,
    }

    private const float DECISION_THROTTLE = 0.4f;
    private const float COOLDOWN_AFTER_END = 5f;
    private const float MOVE_TIMEOUT = 12f;
    private const float SESSION_MAX = 30f;
    private const float ARRIVE_DIST = 0.7f;
    private const float MAX_PATH = 26f;
    private const float BAIT_ROLL_INTERVAL = 20f;

    public RepositionClass(BotComponent bot)
        : base(bot)
    {
        CanEverTick = false;
    }

    private sealed class Session
    {
        public EMode Mode;
        public EPhase Phase;
        public Enemy Enemy;
        public string EnemyId;
        public Vector3 Target;
        public Vector3 Look;
        public Vector3 Home;
        public float StartTime;
        public float PhaseTime;
        public float NextMoveOrder;
        public float MoveAt;
        public float HoldTime;
        public float HoldPose = 1f;
        public int BaitLeft;
        public bool BaitCrouch;
        public bool BaitJump;
        public bool BaitJumped;
        public bool DrewFire;
        public Vector3 Noise;
        public int NoiseLeft = 2;
    }

    private Session _session;
    private float _nextDecisionTime;
    private float _cooldownUntil;
    private float _nextBaitRoll;

    public bool Active
    {
        get { return _session != null; }
    }

    public EMode Mode
    {
        get { return _session?.Mode ?? EMode.None; }
    }

    public Vector3? LookTarget
    {
        get { return _session?.Look; }
    }

    private static RepositionSettings Settings
    {
        get { return GlobalSettingsClass.Instance.General.Reposition; }
    }

    private bool Applies
    {
        get
        {
            var settings = GlobalSettingsClass.Instance?.General?.Reposition;
            return settings != null && settings.Enabled && (!settings.PmcOnly || Bot.Info.Profile.IsPMC);
        }
    }

    // ---------------------------------------------------------------- grenade tracking

    private GrenadeController _subscribedController;
    private float _expectOwnNadeUntil;
    private float _expectFuse;
    private Vector3 _expectTarget;
    private bool _expectFlank;

    public override void Init()
    {
        var controller = BotManagerComponent.Instance?.GrenadeController;
        if (controller != null)
        {
            controller.OnGrenadeThrown += OnAnyGrenadeThrown;
            _subscribedController = controller;
        }
        base.Init();
    }

    public override void Dispose()
    {
        End("disposed");
        if (_subscribedController != null)
        {
            _subscribedController.OnGrenadeThrown -= OnAnyGrenadeThrown;
            _subscribedController = null;
        }
        base.Dispose();
    }

    private void OnAnyGrenadeThrown(Grenade grenade, Vector3 dangerPoint, string profileId)
    {
        if (grenade == null || profileId != Bot.ProfileId || Time.time > _expectOwnNadeUntil)
        {
            return;
        }
        _expectOwnNadeUntil = 0f;
        Session s = _session;
        if (s != null && (s.Phase == EPhase.WaitThrow || s.Phase == EPhase.WaitBlast))
        {
            // Released now: the blast is fuse seconds away. Flank moves just before it (the bang masks the steps),
            // relocate moves right away (the fuse keeps their head down, the bang masks the end of the move).
            s.MoveAt = s.Mode == EMode.NadeFlank ? Time.time + Mathf.Max(0.3f, _expectFuse - 0.6f) : Time.time + 0.2f;
            SetPhase(s, s.Mode == EMode.NadeFlank ? EPhase.WaitBlast : EPhase.WaitBlast);
            Log($"{Who()} own frag released (fuse {_expectFuse:0.0}s) -> moving in {s.MoveAt - Time.time:0.0}s");
        }
    }

    /// <summary>
    /// Called by GrenadeThrowDecider right before SetThrowData for SAIN's normal throws: picks the fuse and returns
    /// its length (or -1 when nothing was changed).
    /// </summary>
    public float ApplyFuseChoice(ref AIGreanageThrowData data, Vector3 from, Vector3 target)
    {
        if (!Applies || !Settings.FuseSelection)
        {
            return -1f;
        }
        bool indoors = Bot.Memory.Location.IsIndoors;
        float dist = (target - from).magnitude;
        bool shortest = indoors || dist < Settings.ShortFuseDistance;
        ThrowWeap pick = PickFrag(shortest);
        if (pick == null)
        {
            return -1f;
        }
        var grenades = BotOwner.WeaponManager.Grenades;
        grenades.SetThrowParams(pick);
        // No GrenadeType: DoThrow would otherwise swap back to the first frag in the rig (CheckGrenadeWithType).
        data.GrenadeType = null;
        TacticDiagnostics.Count(shortest ? "nade.fuse.short" : "nade.fuse.long");
        Log($"{Who()} frag throw {dist:0}m {(indoors ? "indoors" : "outdoors")} -> {(shortest ? "SHORT" : "LONG")} fuse {pick.ShortName.Localized()} {pick.GetExplDelay:0.0}s");
        return pick.GetExplDelay;
    }

    /// <summary>
    /// Called by GrenadeThrowDecider after a successful DoThrow of SAIN's normal throw.
    /// </summary>
    public void OnSainThrowStarted(Enemy enemy, Vector3 target, float fuse)
    {
        if (!Applies || !Settings.NadeFlank || _session != null || enemy == null || !Bot.Memory.Location.IsIndoors)
        {
            return;
        }
        if (Bot.DoorTactic.Active || Bot.SquadCombat.Active)
        {
            return;
        }
        if (!Roll(Settings.NadeFlankChance))
        {
            TacticDiagnostics.Count("repo.rollFailed.NadeFlank");
            return;
        }
        Vector3? known = enemy.KnownPlaces.LastKnownPosition;
        Vector3 enemyPos = known ?? target;
        if (!FindPoint(EMode.NadeFlank, enemyPos, out Vector3 point, out string why))
        {
            TacticDiagnostics.Count($"repo.noPoint.NadeFlank.{why}");
            Log($"{Who()} grenade flank: no flanking spot ({why})");
            return;
        }
        _expectOwnNadeUntil = Time.time + 4f;
        _expectFuse = fuse > 0f ? fuse : 4f;
        Start(EMode.NadeFlank, enemy, point, enemyPos + Vector3.up * 1.3f, EPhase.WaitThrow, "own frag thrown indoors, flank as it goes off");
    }

    /// <summary>
    /// Percent roll; F6 TEST MODE makes every roll succeed.
    /// </summary>
    private static bool Roll(float chancePercent)
    {
        return Settings.TestMode || Random.value * 100f < chancePercent;
    }

    private ThrowWeap PickFrag(bool shortest)
    {
        var inventory = Player?.InventoryController?.Inventory;
        if (inventory == null)
        {
            return null;
        }
        ThrowWeap best = null;
        foreach (var item in inventory.GetPlayerItems(EPlayerItems.Equipment))
        {
            if (item is not ThrowWeap nade || nade.ThrowType != ThrowWeapType.frag_grenade)
            {
                continue;
            }
            // Impact grenades (VOG) and anything silly short are left to SAIN.
            if (nade.MinTimeToContactExplode >= 0f || nade.GetExplDelay < 2.5f)
            {
                continue;
            }
            if (best == null || (shortest ? nade.GetExplDelay < best.GetExplDelay : nade.GetExplDelay > best.GetExplDelay))
            {
                best = nade;
            }
        }
        return best;
    }

    // ---------------------------------------------------------------- contact / magazine tracking

    private enum EContact
    {
        BotFirst,
        EnemyFirst,
    }

    private readonly Dictionary<string, EContact> _contact = new();
    private readonly HashSet<string> _relocateRolled = new();
    private string _disengageArmedFor;
    private float _disengageArmedUntil;
    private bool _wasReloading;
    private float _lastAmmoRatio = 1f;

    /// <summary>
    /// Called by EnemyDecisionClass for the goal enemy before anything else (also while reloading).
    /// </summary>
    public void Observe(Enemy enemy)
    {
        if (enemy == null || !Applies)
        {
            return;
        }
        string id = enemy.EnemyProfileId;
        if (!_contact.ContainsKey(id))
        {
            var status = enemy.Status;
            if (status.ShotAtMe || status.ShotMe)
            {
                // Shot at before we saw them (or in the same moment) = they had first contact.
                bool weSawFirst = enemy.Seen && enemy.TimeSinceSeen > 0.5f;
                _contact[id] = weSawFirst ? EContact.BotFirst : EContact.EnemyFirst;
                TacticDiagnostics.Count($"repo.contact.{_contact[id]}");
            }
            else if (enemy.Seen)
            {
                _contact[id] = EContact.BotFirst;
                TacticDiagnostics.Count("repo.contact.BotFirst");
            }
        }

        bool reloading = BotOwner.WeaponManager?.Reload?.Reloading == true;
        if (!reloading)
        {
            _lastAmmoRatio = SAINBotSuppressClass.CalcAmmoRatio(BotOwner, out _);
        }
        if (reloading && !_wasReloading)
        {
            // Magazine run dry on an enemy this bot engaged first, still alive, seen just now, outdoors.
            bool emptied = _lastAmmoRatio < 0.2f;
            bool first = _contact.TryGetValue(id, out EContact c) && c == EContact.BotFirst;
            bool alive = enemy.EnemyPlayer?.HealthController?.IsAlive == true;
            if (Settings.Disengage && emptied && first && alive && enemy.Seen && enemy.TimeSinceSeen < 6f && !Bot.Memory.Location.IsIndoors)
            {
                _disengageArmedFor = id;
                _disengageArmedUntil = Time.time + 12f;
                TacticDiagnostics.Count("repo.disengage.armed");
                Log($"{Who()} emptied a magazine on {enemy.EnemyPlayer?.Profile?.Nickname} without the kill -> disengage after reload");
            }
        }
        _wasReloading = reloading;
    }

    // ---------------------------------------------------------------- decision

    /// <summary>
    /// Called from EnemyDecisionClass right before the shoot check.
    /// </summary>
    public bool ShallUse(Enemy enemy, EnemyList knownEnemies, out string reason)
    {
        Session s = _session;
        bool otherVisible = OtherEnemyVisible(enemy, knownEnemies);
        if (s != null)
        {
            // This check runs BEFORE SAIN's shoot decision, so it must never hold a bot that has something to
            // shoot or is being shot (second raid test: bots stood still in a hold and died / ignored the player).
            if (otherVisible)
            {
                End("otherEnemyVisible");
                reason = "otherEnemyVisible";
                return false;
            }
            bool movingAway = (s.Mode == EMode.Relocate || s.Mode == EMode.Disengage) && s.Phase == EPhase.Move;
            if (BotOwner.Memory.IsUnderFire && !movingAway)
            {
                End("underFire");
                reason = "underFire";
                return false;
            }
            if (enemy == null || enemy.EnemyProfileId != s.EnemyId)
            {
                End("goalEnemyChanged");
                reason = "goalEnemyChanged";
                return false;
            }
            if (BotOwner.WeaponManager?.Grenades?.ThrowindNow == true && s.Mode != EMode.Relocate)
            {
                // SAIN's own throw (grenade flank) finishes under SAIN's ThrowGrenade decision.
                reason = "throwingNow";
                return false;
            }
            if (enemy.IsVisible)
            {
                float dist = (enemy.EnemyPosition - Bot.Position).magnitude;
                bool keepMoving = (s.Mode == EMode.Relocate || s.Mode == EMode.Disengage) && s.Phase == EPhase.Move && dist > 20f;
                if (!keepMoving)
                {
                    End("enemySpotted");
                    reason = "enemySpotted";
                    return false;
                }
            }
            reason = $"active:{s.Mode}:{s.Phase}";
            return true;
        }

        float time = Time.time;
        if (_nextDecisionTime > time)
        {
            reason = "throttled";
            return false;
        }
        _nextDecisionTime = time + DECISION_THROTTLE;
        if (!Applies || _cooldownUntil > time)
        {
            reason = "disabledOrCooldown";
            return false;
        }
        if (enemy == null || enemy.IsVisible || otherVisible || BotOwner.Memory.IsUnderFire || Bot.DoorTactic.Active || Bot.SquadCombat.Active)
        {
            reason = "visibleOrOtherTactic";
            return false;
        }
        if (Bot.Decision.CurrentSelfDecision != ESelfActionType.None || BotOwner.WeaponManager?.Grenades?.ThrowindNow == true)
        {
            reason = "selfAction";
            return false;
        }
        Vector3? known = enemy.KnownPlaces.LastKnownPosition;
        if (known == null || enemy.TimeSinceLastKnownUpdated > 30f)
        {
            reason = "enemyInfoStale";
            return false;
        }
        Vector3 enemyPos = known.Value;
        float enemyDist = (enemyPos - Bot.Position).magnitude;

        if (TryStartDisengage(enemy, enemyPos, enemyDist, out reason)
            || TryStartRelocate(enemy, enemyPos, enemyDist, out reason)
            || TryStartCoverTrick(enemy, enemyPos, enemyDist, out reason))
        {
            return true;
        }
        return false;
    }

    private float _pullBackShootUntil;
    private float _pullBackUntil;
    private bool _pullBackLogged;

    /// <summary>
    /// Called from EnemyDecisionClass next to the emergency retreat: the short window after a bait peek's shot where
    /// the bot ducks back into cover (SAIN Retreat).
    /// </summary>
    public bool ShallPullBack(out string reason)
    {
        float time = Time.time;
        if (_pullBackUntil > time && time >= _pullBackShootUntil)
        {
            if (!_pullBackLogged)
            {
                _pullBackLogged = true;
                TacticDiagnostics.Count("repo.bait.pullBack");
                Log($"{Who()} bait peek shot done -> back into cover");
            }
            reason = "baitPullBack";
            return true;
        }
        reason = string.Empty;
        return false;
    }

    private static bool OtherEnemyVisible(Enemy goal, EnemyList knownEnemies)
    {
        if (knownEnemies == null)
        {
            return false;
        }
        foreach (Enemy e in knownEnemies)
        {
            if (e != null && e != goal && e.IsVisible)
            {
                return true;
            }
        }
        return false;
    }

    private bool TryStartDisengage(Enemy enemy, Vector3 enemyPos, float enemyDist, out string reason)
    {
        reason = "noDisengage";
        if (_disengageArmedFor != enemy.EnemyProfileId || Time.time > _disengageArmedUntil)
        {
            return false;
        }
        _disengageArmedFor = null;
        if (enemyDist < Settings.DisengageMinDistance)
        {
            TacticDiagnostics.Count("repo.disengage.tooClose");
            reason = "disengageTooClose";
            return false;
        }
        if (!Roll(Settings.DisengageChance))
        {
            TacticDiagnostics.Count("repo.rollFailed.Disengage");
            return false;
        }
        if (!FindPoint(EMode.Disengage, enemyPos, out Vector3 point, out string why))
        {
            TacticDiagnostics.Count($"repo.noPoint.Disengage.{why}");
            Log($"{Who()} disengage: no spot out of their sight ({why})");
            return false;
        }
        Start(EMode.Disengage, enemy, point, enemyPos + Vector3.up * 1.3f, EPhase.Move, "one magazine, no kill -> break line of sight and back off");
        reason = "disengage";
        return true;
    }

    private bool TryStartRelocate(Enemy enemy, Vector3 enemyPos, float enemyDist, out string reason)
    {
        reason = "noRelocate";
        if (!Settings.Relocate || Bot.Memory.Location.IsIndoors || Bot.Cover.CoverInUse == null)
        {
            return false;
        }
        string id = enemy.EnemyProfileId;
        if (!_contact.TryGetValue(id, out EContact c) || c != EContact.EnemyFirst || _relocateRolled.Contains(id))
        {
            return false;
        }
        if (enemy.Seen && enemy.TimeSinceSeen < 1.5f)
        {
            return false;
        }
        _relocateRolled.Add(id);
        if (!Roll(Settings.RelocateChance))
        {
            TacticDiagnostics.Count("repo.rollFailed.Relocate");
            return false;
        }
        if (!FindPoint(EMode.Relocate, enemyPos, out Vector3 point, out string why))
        {
            TacticDiagnostics.Count($"repo.noPoint.Relocate.{why}");
            Log($"{Who()} relocate: no spot out of their sight ({why})");
            return false;
        }
        bool threw = enemyDist > 8f && enemyDist < 45f && TryThrowAt(enemyPos, enemyDist);
        Start(EMode.Relocate, enemy, point, enemyPos + Vector3.up * 1.3f, threw ? EPhase.WaitThrow : EPhase.Move,
            threw ? "spotted first: frag at them, then move where they can't see" : "spotted first: no frag, move where they can't see");
        TacticDiagnostics.Count(threw ? "repo.relocate.withNade" : "repo.relocate.noNade");
        reason = "relocate";
        return true;
    }

    /// <summary>
    /// From cover, at most every 20s: bait peek (aggressive personalities), fake reload (mag check) or ghost flank,
    /// tried in random order, each with its own chance.
    /// </summary>
    private bool TryStartCoverTrick(Enemy enemy, Vector3 enemyPos, float enemyDist, out string reason)
    {
        reason = "noCoverTrick";
        if (Bot.Cover.CoverInUse == null || Time.time < _nextBaitRoll || enemyDist > 40f || enemyDist < 5f)
        {
            return false;
        }
        if (!enemy.Seen || enemy.TimeSinceSeen < 2f || enemy.TimeSinceSeen > 30f)
        {
            return false;
        }
        _nextBaitRoll = Time.time + (Settings.TestMode ? 8f : BAIT_ROLL_INTERVAL);
        int first = Random.Range(0, 3);
        for (int k = 0; k < 3; k++)
        {
            switch ((first + k) % 3)
            {
                case 0:
                    if (TryStartBait(enemy, enemyPos, out reason))
                    {
                        return true;
                    }
                    break;

                case 1:
                    if (TryStartFakeReload(enemy, enemyPos, out reason))
                    {
                        return true;
                    }
                    break;

                default:
                    if (TryStartGhost(enemy, enemyPos, enemyDist, out reason))
                    {
                        return true;
                    }
                    break;
            }
        }
        return false;
    }

    private bool TryStartBait(Enemy enemy, Vector3 enemyPos, out string reason)
    {
        reason = "noBait";
        if (!Settings.BaitPeek || (!Settings.TestMode && !Bot.Info.PersonalitySettings.Rush.CanRushEnemyReloadHeal))
        {
            return false;
        }
        if (!Roll(Settings.BaitPeekChance))
        {
            TacticDiagnostics.Count("repo.rollFailed.BaitPeek");
            return false;
        }
        if (!FindBaitPoint(enemyPos, out Vector3 point))
        {
            TacticDiagnostics.Count("repo.noPoint.BaitPeek");
            return false;
        }
        Start(EMode.BaitPeek, enemy, point, enemyPos + Vector3.up * 1.3f, EPhase.BaitOut, "fake peek to draw fire");
        _session.Home = Bot.Position;
        _session.BaitLeft = Random.value < 0.5f ? 1 : 2;
        _session.BaitCrouch = Random.value < 0.4f;
        _session.BaitJump = Random.value < 0.5f;
        reason = "bait";
        return true;
    }

    private bool TryStartFakeReload(Enemy enemy, Vector3 enemyPos, out string reason)
    {
        reason = "noFakeReload";
        if (!Settings.FakeReload || SAINBotSuppressClass.CalcAmmoRatio(BotOwner, out _) < 0.5f)
        {
            return false;
        }
        if (!Roll(Settings.FakeReloadChance))
        {
            TacticDiagnostics.Count("repo.rollFailed.FakeReload");
            return false;
        }
        if (Player.HandsController is not Player.FirearmController firearm)
        {
            return false;
        }
        // The angle they'd come through: the last corner between us, else their last known spot.
        Vector3 look = (enemy.VisiblePathPoint ?? enemyPos) + Vector3.up * 1.3f;
        Start(EMode.FakeReload, enemy, Bot.Position, look, EPhase.MagCheck, "mag check sound as a fake reload, then hold their push angle");
        bool ok = firearm.CheckAmmo();
        TacticDiagnostics.Count(ok ? "repo.fakeReload.magCheck" : "repo.fakeReload.refused");
        Log($"{Who()} fake reload: mag check {(ok ? "started" : "REFUSED by the weapon controller")}");
        if (!ok)
        {
            End("magCheckRefused");
            return false;
        }
        _session.HoldTime = Random.Range(4f, 7f);
        _session.HoldPose = 0.8f;
        reason = "fakeReload";
        return true;
    }

    private bool TryStartGhost(Enemy enemy, Vector3 enemyPos, float enemyDist, out string reason)
    {
        reason = "noGhost";
        if (!Settings.GhostFlank || enemyDist < 8f || enemyDist > 35f)
        {
            return false;
        }
        if (!Roll(Settings.GhostFlankChance))
        {
            TacticDiagnostics.Count("repo.rollFailed.GhostFlank");
            return false;
        }
        if (!FindPoint(EMode.GhostFlank, enemyPos, out Vector3 point, out string why))
        {
            TacticDiagnostics.Count($"repo.noPoint.GhostFlank.{why}");
            Log($"{Who()} ghost flank: no other angle on them ({why})");
            return false;
        }
        // Noise spot: 2-3m back and forth inside the current cover area.
        Vector3 away = Flat(Bot.Position - enemyPos).normalized;
        Vector3 noise = Bot.Position + away * 2.5f;
        if (NavMesh.SamplePosition(noise, out NavMeshHit hit, 1f, -1))
        {
            noise = hit.position;
        }
        else
        {
            noise = Bot.Position;
        }
        Start(EMode.GhostFlank, enemy, point, enemyPos + Vector3.up * 1.3f, EPhase.GhostNoise, "loud steps in cover, then creep to another angle");
        _session.Home = Bot.Position;
        _session.Noise = noise;
        _session.HoldTime = Random.Range(8f, 14f);
        _session.HoldPose = 0.8f;
        reason = "ghost";
        return true;
    }

    // ---------------------------------------------------------------- execution

    private void Start(EMode mode, Enemy enemy, Vector3 target, Vector3 look, EPhase phase, string why)
    {
        _session = new Session
        {
            Mode = mode,
            Phase = phase,
            Enemy = enemy,
            EnemyId = enemy.EnemyProfileId,
            Target = target,
            Look = look,
            Home = Bot.Position,
            StartTime = Time.time,
            PhaseTime = Time.time,
            MoveAt = Time.time + 6f,
            HoldTime = mode == EMode.BaitPeek ? 2.5f : Random.Range(5f, 10f),
            HoldPose = mode == EMode.Relocate || mode == EMode.Disengage ? 0.8f : 1f,
        };
        TacticDiagnostics.Count($"repo.start.{mode}");
        Log($"{Who()} START {mode}: {why} (spot {Flat(target - Bot.Position).magnitude:0.0}m away)");
    }

    private void SetPhase(Session s, EPhase phase)
    {
        s.Phase = phase;
        s.PhaseTime = Time.time;
        s.NextMoveOrder = 0f;
    }

    public void Tick()
    {
        Session s = _session;
        if (s == null)
        {
            return;
        }
        float time = Time.time;
        float phaseTime = time - s.PhaseTime;
        if (time - s.StartTime > SESSION_MAX)
        {
            End("sessionTimeout");
            return;
        }
        if (s.Mode == EMode.BaitPeek && !s.DrewFire && s.Enemy.Status.ShotAtMe && time - s.Enemy.Status.TimeLastShotAtMe < time - s.StartTime)
        {
            s.DrewFire = true;
            TacticDiagnostics.Count("repo.bait.drewFire");
            Log($"{Who()} bait peek drew fire");
        }

        switch (s.Phase)
        {
            case EPhase.WaitThrow:
                Bot.Mover.Stop();
                if (phaseTime > 4f)
                {
                    // Throw never happened (animation refused): just move.
                    SetPhase(s, EPhase.Move);
                }
                break;

            case EPhase.WaitBlast:
                Bot.Mover.Stop();
                if (time >= s.MoveAt)
                {
                    TacticDiagnostics.Count($"repo.moveOnBlast.{s.Mode}");
                    SetPhase(s, EPhase.Move);
                }
                break;

            case EPhase.Move:
                if (MoveTo(s, s.Target, !s.Enemy.IsVisible, phaseTime))
                {
                    TacticDiagnostics.Count($"repo.arrived.{s.Mode}");
                    Log($"{Who()} {s.Mode} in position after {time - s.StartTime:0.0}s");
                    SetPhase(s, EPhase.Hold);
                }
                break;

            case EPhase.BaitOut:
                Bot.Mover.SetTargetPose(s.BaitCrouch ? 0.55f : 1f);
                // Reference clip 3: hop out to see over/around cover, not just a shoulder.
                if (s.BaitJump && !s.BaitJumped)
                {
                    s.BaitJumped = Bot.Mover.TryJump();
                    if (s.BaitJumped)
                    {
                        TacticDiagnostics.Count("repo.bait.jump");
                    }
                }
                if (MoveTo(s, s.Target, false, phaseTime) || phaseTime > 0.9f)
                {
                    SetPhase(s, EPhase.BaitShow);
                }
                break;

            case EPhase.BaitShow:
                Bot.Mover.Stop();
                if (phaseTime > Random.Range(0.2f, 0.35f))
                {
                    SetPhase(s, EPhase.BaitBack);
                }
                break;

            case EPhase.BaitBack:
                if (MoveTo(s, s.Home, false, phaseTime) || phaseTime > 1.2f)
                {
                    s.BaitLeft--;
                    TacticDiagnostics.Count("repo.bait.peeked");
                    if (s.BaitLeft > 0)
                    {
                        // Vary it: other stance and the OTHER side next time so the head isn't where they pre-aimed.
                        s.BaitCrouch = !s.BaitCrouch;
                        s.BaitJump = !s.BaitJump;
                        s.BaitJumped = false;
                        Vector3 other = s.Home + (s.Home - s.Target);
                        if (NavMesh.SamplePosition(other, out NavMeshHit otherHit, 0.6f, -1)
                            && !Physics.Linecast(otherHit.position + Vector3.up * 1.4f, s.Look, LayersMaskController.HighPolyWithTerrainMask))
                        {
                            s.Target = otherHit.position;
                            TacticDiagnostics.Count("repo.bait.otherSide");
                        }
                        SetPhase(s, EPhase.BaitWait);
                    }
                    else
                    {
                        SetPhase(s, EPhase.Hold);
                    }
                }
                break;

            case EPhase.BaitWait:
                Bot.Mover.Stop();
                if (phaseTime > Random.Range(0.8f, 2.2f))
                {
                    SetPhase(s, EPhase.BaitOut);
                }
                break;

            case EPhase.MagCheck:
                // Stand still while the mag check plays (~1.5-2s), then hold the push angle.
                Bot.Mover.Stop();
                if (phaseTime > 1.8f)
                {
                    SetPhase(s, EPhase.Hold);
                }
                break;

            case EPhase.GhostNoise:
                // Sprinting is the loudest footstep sound: dash to the noise spot and back.
                if (MoveTo(s, s.Noise, true, phaseTime) || phaseTime > 1.5f)
                {
                    SetPhase(s, EPhase.GhostNoiseBack);
                }
                break;

            case EPhase.GhostNoiseBack:
                if (MoveTo(s, s.Home, true, phaseTime) || phaseTime > 1.5f)
                {
                    s.NoiseLeft--;
                    SetPhase(s, s.NoiseLeft > 0 ? EPhase.GhostNoise : EPhase.Sneak);
                    if (s.NoiseLeft <= 0)
                    {
                        TacticDiagnostics.Count("repo.ghost.noiseDone");
                        Log($"{Who()} ghost flank: noise made, creeping to the other angle");
                    }
                }
                break;

            case EPhase.Sneak:
                Bot.Mover.SetTargetPose(0.6f);
                if (MoveTo(s, s.Target, false, phaseTime, 0.2f))
                {
                    TacticDiagnostics.Count("repo.arrived.GhostFlank");
                    Log($"{Who()} ghost flank in position after {time - s.StartTime:0.0}s");
                    SetPhase(s, EPhase.Hold);
                }
                break;

            case EPhase.Hold:
                Bot.Mover.Stop();
                Bot.Mover.SetTargetPose(s.HoldPose);
                if (phaseTime > s.HoldTime)
                {
                    End(s.Mode == EMode.BaitPeek ? (s.DrewFire ? "baitDoneDrewFire" : "baitDoneNoFire") : "holdTimeout");
                }
                break;
        }
    }

    private bool MoveTo(Session s, Vector3 target, bool sprint, float phaseTime, float walkSpeed = 1f)
    {
        if (Flat(target - Bot.Position).magnitude < ARRIVE_DIST)
        {
            return true;
        }
        if (phaseTime > (walkSpeed < 0.5f ? MOVE_TIMEOUT * 2.5f : MOVE_TIMEOUT))
        {
            End("moveTimeout");
            return false;
        }
        if (s.NextMoveOrder < Time.time)
        {
            s.NextMoveOrder = Time.time + 0.6f;
            bool ok = sprint
                ? Bot.Mover.RunToPoint(target, true, ARRIVE_DIST * 0.7f, ESprintUrgency.High)
                : Bot.Mover.WalkToPoint(target, true, ARRIVE_DIST * 0.7f);
            if (!ok)
            {
                End("noPath");
                return false;
            }
        }
        if (!sprint)
        {
            Bot.Mover.SetTargetMoveSpeed(walkSpeed);
        }
        return false;
    }

    public void OnActionStopped()
    {
        if (_session == null)
        {
            return;
        }
        ECombatDecision next = Bot.Decision.CurrentCombatDecision;
        if (next == ECombatDecision.Reposition)
        {
            // SAINLayer restarts the action on any decision change (kb 2.14): still ours, keep going.
            TacticDiagnostics.Count("repo.actionRestartKept");
            return;
        }
        End(next == ECombatDecision.StandAndShoot ? "enemySpotted(StandAndShoot)" : $"interrupted({next})");
    }

    public void End(string result)
    {
        Session s = _session;
        if (s == null)
        {
            return;
        }
        _session = null;
        _cooldownUntil = Time.time + COOLDOWN_AFTER_END;
        Bot.Mover.SetTargetMoveSpeed(1f);
        Bot.Mover.SetTargetPose(1f);
        string key = result.Contains("(") ? result.Substring(0, result.IndexOf('(')) : result;
        TacticDiagnostics.Count($"repo.end.{s.Mode}.{key}");
        if (s.Mode == EMode.BaitPeek && Bot.Decision.CurrentCombatDecision == ECombatDecision.RushEnemy)
        {
            TacticDiagnostics.Count("repo.bait.rushAfter");
        }
        if (s.Mode == EMode.BaitPeek && result.StartsWith("enemySpotted"))
        {
            // Peek, shoot briefly, back into cover (reference clip 3) instead of standing out in the open.
            _pullBackShootUntil = Time.time + 0.9f;
            _pullBackUntil = _pullBackShootUntil + 1.6f;
            _pullBackLogged = false;
        }
        Log($"{Who()} END {s.Mode} result={result} after {Time.time - s.StartTime:0.0}s phase={s.Phase}");
    }

    // ---------------------------------------------------------------- own throw (relocate)

    private static readonly AIGreandeAng[] _throwAngles = [AIGreandeAng.ang15, AIGreandeAng.ang25, AIGreandeAng.ang35, AIGreandeAng.ang45];

    private bool TryThrowAt(Vector3 enemyPos, float dist)
    {
        var grenades = BotOwner.WeaponManager?.Grenades;
        if (grenades == null || grenades.ThrowindNow || !grenades.HaveGrenade || !grenades.ReadyToThrow)
        {
            return false;
        }
        foreach (var member in Bot.Squad.Members?.Values ?? (IEnumerable<BotComponent>)new List<BotComponent>())
        {
            if (member != null && !ReferenceEquals(member, Bot) && (member.Position - enemyPos).sqrMagnitude < 64f)
            {
                return false;
            }
        }
        ThrowWeap pick = PickFrag(dist < Settings.ShortFuseDistance);
        if (pick == null)
        {
            return false;
        }
        Vector3 from = Bot.Transform.WeaponData.WeaponRoot;
        Vector3 target = enemyPos + Vector3.up * 0.25f;
        foreach (AIGreandeAng angle in _throwAngles)
        {
            AIGreanageThrowData data = AIGrenadeHelper.CanThrowGrenade2(from, target, grenades.MaxPower * 0.9f, angle, -1f, 0.66f);
            if (!data.CanThrow)
            {
                continue;
            }
            data.GrenadeType = null;
            grenades.SetThrowParams(pick);
            if (grenades.SetThrowData(data) && grenades.DoThrow())
            {
                _expectOwnNadeUntil = Time.time + 4f;
                _expectFuse = pick.GetExplDelay;
                Log($"{Who()} relocate frag {pick.ShortName.Localized()} ({pick.GetExplDelay:0.0}s) at {dist:0}m");
                return true;
            }
        }
        return false;
    }

    // ---------------------------------------------------------------- spots

    private static readonly float[] RADII = { 5f, 8f, 12f, 17f, 23f };
    private readonly List<(Vector3 point, float score)> _candidates = new();

    /// <summary>
    /// NadeFlank: 4-12m away, bearing on the enemy changed >= 40 degrees, can see the enemy spot (a flanking angle).
    /// Relocate: 5-17m, bearing changed >= 25 degrees, the enemy spot can NOT see it, not much closer to them.
    /// Disengage: 8-23m, the enemy spot can NOT see it, at least 5m further from them.
    /// </summary>
    private bool FindPoint(EMode mode, Vector3 enemyPos, out Vector3 result, out string why)
    {
        result = default;
        Vector3 bot = Bot.Position;
        Vector3 fromEnemy = Flat(bot - enemyPos);
        float curDist = fromEnemy.magnitude;
        bool flank = mode == EMode.NadeFlank || mode == EMode.GhostFlank;
        float minR = mode == EMode.NadeFlank ? 4f : mode == EMode.Relocate || mode == EMode.GhostFlank ? 5f : 8f;
        float maxR = mode == EMode.NadeFlank ? 12f : mode == EMode.Relocate || mode == EMode.GhostFlank ? 17f : 23f;
        float minBearing = flank ? 40f : mode == EMode.Relocate ? 25f : 0f;
        Vector3 enemyEye = enemyPos + Vector3.up * 1.5f;

        _candidates.Clear();
        int sampled = 0;
        int losWrong = 0;
        int geometry = 0;
        foreach (float r in RADII)
        {
            if (r < minR || r > maxR)
            {
                continue;
            }
            for (int a = 0; a < 360; a += 30)
            {
                float rad = (a + r * 7f) * Mathf.Deg2Rad;
                Vector3 raw = bot + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * r;
                // Same floor only: a 2m sample / 4m height window picked spots under stairs and on the floor below,
                // and the bot ran at the wall trying to get there (field report).
                if (!NavMesh.SamplePosition(raw, out NavMeshHit hit, 1.2f, -1))
                {
                    continue;
                }
                Vector3 p = hit.position;
                if (Mathf.Abs(p.y - bot.y) > 1.2f || Flat(p - raw).magnitude > 1f)
                {
                    continue;
                }
                sampled++;
                Vector3 pe = Flat(p - enemyPos);
                float bearing = Vector3.Angle(pe, fromEnemy);
                if (bearing < minBearing || pe.magnitude < 4f)
                {
                    geometry++;
                    continue;
                }
                if (mode == EMode.Relocate && pe.magnitude < curDist * 0.7f)
                {
                    geometry++;
                    continue;
                }
                if (mode == EMode.Disengage && pe.magnitude < curDist + 5f)
                {
                    geometry++;
                    continue;
                }
                bool blocked = Physics.Linecast(enemyEye, p + Vector3.up * 1.3f, LayersMaskController.HighPolyWithTerrainMask);
                bool wantSeen = flank;
                if (wantSeen == blocked)
                {
                    losWrong++;
                    continue;
                }
                float score = Flat(p - bot).magnitude - (flank ? bearing * 0.05f : 0f);
                _candidates.Add((p, score));
            }
        }
        if (_candidates.Count == 0)
        {
            why = sampled == 0 ? "noNavmesh" : losWrong > 0 ? (flank ? "noAngleOnEnemy" : "allInTheirSight") : "geometry";
            return false;
        }
        _candidates.Sort((x, y) => x.score.CompareTo(y.score));
        int checks = 0;
        foreach (var c in _candidates)
        {
            if (++checks > 8)
            {
                break;
            }
            if (!Bot.Mover.CanGoToPoint(c.point, out NavMeshPath path, true))
            {
                continue;
            }
            if (PathLength(path) > MAX_PATH)
            {
                continue;
            }
            result = c.point;
            why = "ok";
            return true;
        }
        why = "noPath";
        return false;
    }

    /// <summary>
    /// 0.9-1.5m sideways out of cover to a spot that can see the enemy's position (so they see a shoulder).
    /// </summary>
    private bool FindBaitPoint(Vector3 enemyPos, out Vector3 result)
    {
        Vector3 bot = Bot.Position;
        Vector3 toEnemy = Flat(enemyPos - bot).normalized;
        Vector3 lateral = Vector3.Cross(Vector3.up, toEnemy);
        float first = Random.value < 0.5f ? 1f : -1f;
        foreach (float side in new[] { first, -first })
        {
            foreach (float d in new[] { 1.1f, 1.5f, 0.9f })
            {
                if (!NavMesh.SamplePosition(bot + lateral * side * d, out NavMeshHit hit, 0.6f, -1))
                {
                    continue;
                }
                if (!Physics.Linecast(hit.position + Vector3.up * 1.4f, enemyPos + Vector3.up * 1.3f, LayersMaskController.HighPolyWithTerrainMask))
                {
                    result = hit.position;
                    return true;
                }
            }
        }
        result = default;
        return false;
    }

    // ---------------------------------------------------------------- helpers

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    private static float PathLength(NavMeshPath path)
    {
        float length = 0f;
        var corners = path.corners;
        for (int i = 1; i < corners.Length; i++)
        {
            length += (corners[i] - corners[i - 1]).magnitude;
        }
        return length;
    }

    private string Who()
    {
        return $"[{Bot.name}] [{Bot.Info.Personality}]";
    }

    private static void Log(string message)
    {
        if (GlobalSettingsClass.Instance?.General?.Reposition?.DiagnosticLogs == true)
        {
            Logger.LogWarning($"[Reposition] {message}");
        }
    }
}
