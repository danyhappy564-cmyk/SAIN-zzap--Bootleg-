using DrakiaXYZ.BigBrain.Brains;
using EFT;
using System.Collections.Generic;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.Classes.Tactics;
using SAIN.SAINComponent.Classes.WeaponFunction;
using UnityEngine;

namespace SAIN.Layers.Combat.Squad;

/// <summary>
/// zzap fork: post-combat tidy-up (ESquadDecision.PostCombat, SAIN squad layer 22). Fills the gap between the SAIN combat
/// layer ending and ORBIT taking over - ORBIT waits 15s after "SAIN : Combat Layer" goes inactive, and in between the bot
/// fell to BSG's vanilla layers. Runs in the SQUAD layer on purpose: ORBIT's 15s timer only watches the combat layer, so
/// this doesn't delay the handoff. For up to 14s: top the magazine up (under 70%), heal (bleeding/hurt first aid, then
/// surgery if a limb is blacked - stays until the meds are done; ORBIT waits for healing anyway), rejoin the squad leader if
/// 25m+ away, otherwise hold half-crouched with the gun on the last threat, glancing around it. Any enemy seen = combat.
/// </summary>
internal class PostCombatAction(BotOwner bot) : BotAction(bot, nameof(PostCombatAction)), IBotAction
{
    private bool _reloadTried;
    private float _nextHealTry;
    private float _nextMove;
    private float _nextGlance;
    private Vector3 _glanceOffset;
    private Vector3? _threat;
    private Vector3? _coverSpot;
    private bool _open;
    private bool _coverFar;
    private bool _proneInOpen;
    private bool _advance;
    private bool _heard;
    private bool _coming;
    private Vector3 _heardAt;
    private float _nextListen;
    private bool _finished;
    private float _watchUntil;

    // zzap (sim round 3, user: "bots still zone out after a fight"): 14s crouched in place looked like idling. A player
    // reloads, watches a few seconds, then - if he's the pushing type - moves up to check the kill spot. Chance per personality.
    private static readonly Dictionary<EPersonality, float> _advanceChance = new()
    {
        { EPersonality.GigaChad, 80f },
        { EPersonality.Wreckless, 85f },
        { EPersonality.Chad, 65f },
        { EPersonality.Normal, 40f },
        { EPersonality.Timmy, 30f },
        { EPersonality.SnappingTurtle, 10f },
        { EPersonality.Rat, 10f },
        { EPersonality.Coward, 0f },
    };

    public override void Start()
    {
        base.Start();
        _reloadTried = false;
        _nextMove = 0f;
        Enemy enemy = Bot.GoalEnemy;
        _threat = enemy?.KnownPlaces.LastKnownPosition ?? Bot.Decision.LastFightThreat;
        if (_threat == null && Bot.Memory.UnderFireFromPosition != Vector3.zero)
        {
            _threat = Bot.Memory.UnderFireFromPosition;
        }
        // In the open: get behind something FIRST (reload on the way), then heal / watch. SAIN's cover list is empty by now
        // (its finder only runs with an enemy), so OpenGround looks for a spot itself.
        _coverSpot = null;
        string coverWhy = "not in the open";
        _open = Bot.Cover.CoverInUse == null && OpenGround.IsOpen(Bot.Position, _threat);
        if (_open)
        {
            _coverSpot = OpenGround.FindCover(Bot, _threat, out coverWhy, out _coverFar);
            // No cover within 25m at all: get small - prone to heal / watch (a standing or crouching body in a wide hall is the
            // first thing anyone entering sees), up again to move on.
            _proneInOpen = _coverSpot == null;
            TacticDiagnostics.Count(_coverSpot != null ? (_coverFar ? "postCombat.open.toFarCover" : "postCombat.open.toCover") : "postCombat.open.noCoverProne");
            if (_proneInOpen)
            {
                coverWhy += " - going prone";
            }
        }
        _watchUntil = Time.time + Random.Range(3f, 6f);
        _heard = false;
        _finished = false;
        _nextListen = 0f;
        float chance = _advanceChance.TryGetValue(Bot.Info.Personality, out float c) ? c : 40f;
        var hs = Bot.Memory.Health.HealthStatus;
        if (hs == ETagStatus.BadlyInjured || hs == ETagStatus.Dying)
        {
            chance *= 0.2f;
        }
        float threatDist = _threat != null ? (_threat.Value - Bot.Position).magnitude : 0f;
        _advance = _threat != null && threatDist > 4f && threatDist < 40f && Random.value * 100f < chance;
        TacticDiagnostics.Count("postCombat.start");
        if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat(
            $"[PostCombat] [{Bot.name}] [{Bot.Info.Personality}] combat over -> tidy up until ORBIT takes over (ammo {SAINBotSuppressClass.CalcAmmoRatio(BotOwner, out _):P0}, "
                + $"leader {(Bot.Squad.IAmLeader || Bot.Squad.LeaderComponent == null ? "-" : $"{(Bot.Squad.LeaderComponent.Position - Bot.Position).magnitude:0}m")}, "
                + $"watching {(_threat != null ? $"last threat {(_threat.Value - Bot.Position).magnitude:0}m" : "around")}"
                + $"{(_open ? $", standing in the open -> cover: {coverWhy}" : "")}"
                + $"{(_advance ? $", then moves up to check the kill spot ({chance:0}% {Bot.Info.Personality})" : ", then holds")})"
        );
    }

    public override void Update(CustomLayer.ActionData data)
    {
        if (!_reloadTried && BotOwner.WeaponManager?.Reload?.Reloading != true)
        {
            _reloadTried = true;
            if (SAINBotSuppressClass.CalcAmmoRatio(BotOwner, out _) < 0.7f && Bot.Decision.SelfActionDecisions.ReloadNow())
            {
                TacticDiagnostics.Count("postCombat.reload");
            }
        }
        Listen();
        if (_coverSpot != null && (_coverSpot.Value - Bot.Position).magnitude > 1f && BotOwner.Medecine?.Using != true)
        {
            if (_nextMove < Time.time)
            {
                _nextMove = Time.time + 1f;
                if (!Bot.Mover.WalkToPoint(_coverSpot.Value))
                {
                    _coverSpot = null;
                }
            }
            // Quick and low - the open is where you get shot first. A far spot: run for it.
            Bot.Mover.SetTargetPose(_coverFar ? 1f : 0.8f);
            Bot.Mover.SetTargetMoveSpeed(_coverFar ? 1f : 0.85f);
            _watchUntil = Time.time + Random.Range(3f, 6f);
            return;
        }
        if (TickHeal())
        {
            return;
        }
        var leader = Bot.Squad.LeaderComponent;
        if (!Bot.Squad.IAmLeader && leader != null && !leader.IsDead && (leader.Position - Bot.Position).magnitude > 25f)
        {
            LieLow(false);
            if (_nextMove < Time.time)
            {
                _nextMove = Time.time + 1f;
                if (Bot.Mover.WalkToPoint(leader.Position))
                {
                    TacticDiagnostics.Count("postCombat.regroup");
                }
            }
            Bot.Mover.SetTargetPose(1f);
            Bot.Mover.SetTargetMoveSpeed(0.8f);
            return;
        }
        if (_advance && Time.time > _watchUntil && _threat != null && BotOwner.WeaponManager?.Reload?.Reloading != true)
        {
            if ((_threat.Value - Bot.Position).magnitude > 3f)
            {
                LieLow(false);
                if (_nextMove < Time.time)
                {
                    _nextMove = Time.time + 1f;
                    if (Bot.Mover.WalkToPoint(_threat.Value))
                    {
                        TacticDiagnostics.Count("postCombat.advance");
                    }
                    else
                    {
                        _advance = false;
                    }
                }
                Bot.Mover.SetTargetPose(1f);
                Bot.Mover.SetTargetMoveSpeed(0.55f);
                return;
            }
            _advance = false;
            _threat = null;
        }
        Bot.Mover.Stop();
        Bot.Mover.SetTargetPose(0.75f);
        LieLow(true);
        // Done: in cover (or nothing better), reloaded, healed, watched, checked - and nothing heard. No reason to keep the
        // bot here (user: "if there's nothing to heal or reload, why have it?").
        if (!_finished && !_heard && Time.time > _watchUntil && !Bot.Decision.MedsWorkLeft() && BotOwner.WeaponManager?.Reload?.Reloading != true)
        {
            _finished = true;
            Bot.Decision.PostCombatFinished = true;
            TacticDiagnostics.Count("postCombat.finished");
            if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat($"[PostCombat] [{Bot.name}] nothing left to do, nothing heard -> hands over");
        }
    }

    /// <summary>
    /// Gunfire nearby / someone coming (SoundWatch): stay on guard facing it - no moving up to the kill spot, no starting meds
    /// with footsteps close, and cancel a heal if they're within 15m. The decision manager keeps this action running meanwhile.
    /// </summary>
    private void Listen()
    {
        if (_nextListen > Time.time)
        {
            return;
        }
        _nextListen = Time.time + 0.4f;
        bool heard = SoundWatch.Heard(Bot, out Vector3 at, out bool coming, out string why);
        if (heard && !_heard)
        {
            TacticDiagnostics.Count(coming ? "postCombat.alert.coming" : "postCombat.alert.gunfire");
            if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat($"[PostCombat] [{Bot.name}] hears {why} -> stays on guard facing it{(_advance ? ", won't move up" : "")}");
        }
        _heard = heard;
        _coming = heard && coming;
        if (!heard)
        {
            return;
        }
        _heardAt = at;
        _advance = false;
        _watchUntil = Mathf.Max(_watchUntil, Time.time + 3f);
        if (_coming && BotOwner.Medecine?.Using == true && (at - Bot.Position).magnitude < 15f)
        {
            Bot.Medical.TryCancelHeal();
            TacticDiagnostics.Count("postCombat.healCancelled");
        }
    }

    /// <summary>Prone while still in the open with no cover in reach (see Start); standing up again to move.</summary>
    private void LieLow(bool still)
    {
        bool want = still && _proneInOpen;
        if (want != _prone)
        {
            _prone = want;
            Bot.Mover.Prone.SetProne(want);
        }
    }

    private bool _prone;

    public override void Stop()
    {
        if (_prone)
        {
            _prone = false;
            Bot.Mover.Prone.SetProne(false);
        }
        base.Stop();
    }

    /// <summary>Heal in place (crouched, gun still on the threat while the meds animation allows). True while healing.</summary>
    private bool TickHeal()
    {
        var med = BotOwner.Medecine;
        if (med == null || BotOwner.WeaponManager?.Reload?.Reloading == true)
        {
            return false;
        }
        if (med.Using)
        {
            Bot.Mover.Stop();
            Bot.Mover.SetTargetPose(0.6f);
            LieLow(true);
            return true;
        }
        if (_coming || _nextHealTry > Time.time || !SAIN.Preset.Shared.GlobalSettings.GlobalSettingsClass.Instance.General.SquadCombat.PostCombatHeal)
        {
            return false;
        }
        _nextHealTry = Time.time + 1f;
        bool started = false;
        string what = null;
        if (med.FirstAid?.ShallStartUse() == true)
        {
            started = Bot.SelfActions.DoFirstAid();
            what = "first aid";
        }
        else if (med.SurgicalKit?.ShallStartUse() == true)
        {
            started = Bot.SelfActions.DoSurgery();
            what = "surgery";
        }
        if (started)
        {
            Bot.Mover.Stop();
            Bot.Mover.SetTargetPose(0.6f);
            LieLow(true);
            if (TacticDiagnostics.CountOn) TacticDiagnostics.Count($"postCombat.heal.{(what == "surgery" ? "surgery" : "firstAid")}");
            if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat($"[PostCombat] [{Bot.name}] healing: {what} ({Bot.Memory.Health.HealthStatus})");
        }
        return started;
    }

    public override void OnSteeringTicked()
    {
        Enemy enemy = Bot.GoalEnemy;
        if (Shoot.ShootAnyVisibleEnemies(enemy))
        {
            return;
        }
        if (_heard)
        {
            // Gun on the sound.
            Bot.Steering.LookToPoint(SAIN.SAINComponent.Classes.Mover.SAINSteeringClass.ClampPitch(Bot.Transform.WeaponRoot, _heardAt + Vector3.up * 1.3f, Bot.Transform.LookDirection));
            return;
        }
        if (_advance && Bot.Mover.Moving && _threat != null)
        {
            // Moving up gun first, eyes on the spot (level).
            Bot.Steering.LookToPoint(SAIN.SAINComponent.Classes.Mover.SAINSteeringClass.ClampPitch(Bot.Transform.WeaponRoot, _threat.Value + Vector3.up * 1.3f, Bot.Transform.LookDirection));
            return;
        }
        if (Bot.Mover.Moving && (_coverSpot == null || _threat == null))
        {
            Bot.Steering.LookToMovingDirection();
            return;
        }
        if (_threat != null)
        {
            if (_nextGlance < Time.time)
            {
                // Gun on the last threat, now and then a glance a bit to either side of it.
                _nextGlance = Time.time + Random.Range(1.5f, 3f);
                Vector3 to = _threat.Value - Bot.Position;
                to.y = 0f;
                Vector3 side = Vector3.Cross(Vector3.up, to.normalized);
                _glanceOffset = Random.value < 0.5f ? Vector3.zero : side * Random.Range(-0.5f, 0.5f) * to.magnitude;
            }
            Bot.Steering.LookToPoint(_threat.Value + _glanceOffset + Vector3.up * 1.3f);
            return;
        }
        Bot.Steering.LookToRandomPosition();
    }
}
