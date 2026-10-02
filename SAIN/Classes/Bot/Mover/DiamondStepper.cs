using SAIN.Components;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.GlobalSettings.Categories.General;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SAIN.SAINComponent.Classes.EnemyClasses;
using SAIN.SAINComponent.Classes.Tactics;
using EFT;
using UnityEngine;
using UnityEngine.AI;

namespace SAIN.SAINComponent.Classes.Mover;

/// <summary>
/// zzap fork: diamond step, shared by StandAndShootAction and DogFightAction (2026-09-28 [Death] log: most close-range
/// deaths were in DogFight, whose back-up/strafe points are 1-2m away and crawl because of SAIN's arrival slowdown).
/// F6: General > Close Combat (zzap).
/// </summary>
public sealed class DiamondStepper(BotComponent bot, string owner)
{
    private readonly BotComponent Bot = bot;
    private readonly string _owner = owner;

    // zzap fork: diamond step (user: "tap-dance" - mash A/D, now and then W/S, while shooting).
    // v1 walked to points 0.5-1.2m away via the path system; SAIN's mover slows to ~5% speed inside 0.75m of the
    // destination, so it crept. Now the direction is held directly (like a key press) for a short tap, then switched.
    // The leash keeps it around the spot where the shooting started. Steering keeps aiming the whole time.
    private Vector3 _diamondCenter;
    private bool _diamondActive;

    /// <summary>Bots moving without inertia (Classic Movement, BotsUseOldMovement) get the slower, player-like tap.</summary>
    private static float TapTime(CloseCombatSettings settings)
    {
        return ClassicMovementInterop.BotsNoInertia ? settings.DiamondStepTapTimeNoInertia : settings.DiamondStepTapTime;
    }
    private bool _leanSpam;
    private Vector3 _tapDir;
    private float _tapEnd;
    private bool _tapPause;
    private bool _lastTapLateralLeft;

    // zzap (user 2026-09-29): at a corner / door frame, jiggle peek - A/D taps out of cover and back while shooting.
    // zzap: bots in a jiggle right now (last tick time). The decision keeps the shooting action through the jiggle's own
    // hide phase (EnemyDecisionClass.KeepCloseFight) instead of re-deciding "enemy hidden" and ending the jiggle.
    public const float JIGGLE_SIGHT_TOLERANCE = 1.2f;
    private static readonly System.Collections.Generic.Dictionary<string, float> _jiggling = new();

    public static bool JiggleActive(string profileId)
    {
        return _jiggling.TryGetValue(profileId, out float t) && Time.time - t < 0.3f;
    }

    public static void ClearAll()
    {
        _jiggling.Clear();
    }

    private const float JIGGLE_DELAY = 0.6f;
    private float _jiggleFrom;
    private bool _jiggleChecked;
    private bool _jiggle;
    private bool _jiggleHide;
    private Vector3 _jiggleCoverDir;

    /// <summary>Returns true while it is driving the bot's movement this frame.</summary>
    public bool Tick(Enemy enemy, float minDistance)
    {
        if (Bot.Player?.HealthController?.IsAlive != true)
        {
            Bot.Mover?.Lean?.SetLeanSpam(false, 0.13f);
            _diamondActive = false;
            return false;
        }
        var settings = GlobalSettingsClass.Instance?.General?.CloseCombat;
        if (settings == null || !settings.DiamondStep)
        {
            return Stop("off");
        }
        // Keep dancing through a sub-second loss of sight (enemy ducks behind a frame) instead of freezing in place.
        // In a jiggle the bot hides itself on purpose - a little longer before calling him gone.
        float sightTolerance = _diamondActive && _jiggle ? JIGGLE_SIGHT_TOLERANCE : 1f;
        if (enemy == null || (!enemy.IsVisible && !(enemy.Seen && enemy.TimeSinceSeen < sightTolerance)))
        {
            return Stop("enemyNotVisible");
        }
        if (!settings.DiamondStepTestMode)
        {
            if (settings.PmcOnly && !Bot.Info.Profile.IsPMC)
            {
                return Stop("notPmc");
            }
            EPersonality personality = Bot.Info.Personality;
            if (personality != EPersonality.GigaChad && personality != EPersonality.Chad && personality != EPersonality.Wreckless && personality != EPersonality.Normal)
            {
                return Stop("personality");
            }
        }
        float dist = enemy.RealDistance;
        if (dist > settings.DiamondStepMaxDistance)
        {
            return Stop("tooFar");
        }
        if (dist < minDistance)
        {
            return Stop("tooClose");
        }
        if (Bot.Player.IsInPronePose || Bot.BotOwner.Medecine?.Using == true)
        {
            return Stop("proneOrHealing");
        }
        if (!_diamondActive)
        {
            _diamondActive = true;
            float leanChance = settings.LeanSpamChance + SAIN.Components.BotControllerSpace.Classes.PlayerAdaptation.LeanSpamBonus(Bot, enemy);
            _leanSpam = settings.LeanSpam && Random.value * 100f < leanChance;
            // zzap (user 2026-10-02 19:46 sim: "the dance got monotone - every bot the same, some just stand there taking hits"):
            // planting is a style, rolled per engagement, not something every bot does on every burst.
            _plantStyle = settings.DiamondStepPlant && Random.value * 100f < settings.DiamondStepPlantChance;
            if (_leanSpam)
            {
                TacticDiagnostics.Count("diamond.leanSpam");
            }
            _diamondCenter = Bot.Position;
            _tapEnd = 0f;
            _plantUntil = 0f;
            _tapsSincePlant = 0;
            _jiggleChecked = false;
            _jiggle = false;
            Bot.Mover.Stop();
            _lastDiamondState = null;
            TacticDiagnostics.SetDiamond(Bot.ProfileId, "active");
            TacticDiagnostics.Count("diamond.start");
            if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat(
                $"[Diamond] [{Bot.name}] [{Bot.Info.Personality}] start ({_owner}): enemy {dist:0}m, tap {TapTime(settings):0.00}s ({(ClassicMovementInterop.BotsNoInertia ? "no-inertia bots" : "inertia")}), style: steps{(_leanSpam ? " + Q/E" : "")}{(_plantStyle ? " + plant to fire" : "")}"
            );
        }
        Bot.Mover.SetTargetPose(1f);
        Bot.Mover.SetTargetMoveSpeed(1f);
        // Without Classic Movement's quick tilt (it applies to bots too) a bot's lean has vanilla inertia and a 0.13s swing
        // never gets past half way - keep the swings at 0.25s+ then.
        float leanInterval = ClassicMovementInterop.BotsQuickTilt ? settings.LeanSpamInterval : Mathf.Max(settings.LeanSpamInterval, 0.25f);
        Bot.Mover.Lean.SetLeanSpam(_leanSpam && enemy.IsVisible, leanInterval);

        Vector3 forward = enemy.EnemyPosition - Bot.Position;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
        {
            return true;
        }
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (_leanSpam)
        {
            // Hold the lean toward the side the enemy is moving (as seen from the bot), Q/E mixed in - more up close.
            LeanSetting side = LeanSetting.None;
            Player enemyPlayer = enemy.EnemyPlayer;
            if (enemyPlayer != null)
            {
                float lateral = Vector3.Dot(enemyPlayer.Velocity, right);
                if (Mathf.Abs(lateral) > settings.LeanFollowEnemySpeed)
                {
                    side = lateral < 0f ? LeanSetting.Left : LeanSetting.Right;
                }
            }
            float rock = dist <= settings.LeanRockCloseDistance ? settings.LeanRockChanceClose : settings.LeanRockChanceFar;
            Bot.Mover.Lean.SetLeanPreference(side, rock / 100f, settings.LeanHoldMin, settings.LeanHoldMax);
        }

        if (!_jiggleChecked)
        {
            _jiggleChecked = true;
            _jiggle = settings.CornerJiggle && Random.value * 100f < settings.CornerJiggleChance && FindCoverSide(enemy, right, out _jiggleCoverDir);
            if (_jiggle)
            {
                // zzap (10/2 21:37 raid, user: "at a corner they look at me 1-2s late, then shoot"): the first jiggle tap was
                // a HIDE (toggle from false), so a bot that had just spotted him ducked behind the corner before its first
                // burst. Shoot first: plain taps for JIGGLE_DELAY, then the jiggle starts with a peek.
                _jiggleHide = true;
                _jiggleFrom = Time.time + JIGGLE_DELAY;
                TacticDiagnostics.Count("diamond.jiggle");
                if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat($"[Diamond] [{Bot.name}] corner jiggle peek: cover on the {(Vector3.Dot(_jiggleCoverDir, right) > 0f ? "right" : "left")}, enemy {dist:0}m");
            }
        }
        // zzap (10/2 14:38 sim, user: "peek-shooting accuracy is a bit off"): every tap is a moving shot - EFT gives
        // a moving bot 1.5x spread and 1.5x aim time (BotAimingData COEF_IF_MOVE / TIME_COEF_IF_MOVE). Firing a burst at
        // range -> let go of the keys for a split second like a player does, then back to tapping. Never up close (the
        // dance is the point there), never in a jiggle peek, at most one plant per two taps so it never turns into standing.
        if (Plant(settings, dist))
        {
            return true;
        }
        if (_jiggle)
        {
            _jiggling[Bot.ProfileId] = Time.time;
        }
        if (Time.time >= _tapEnd)
        {
            _tapsSincePlant++;
            if (_jiggle && Time.time >= _jiggleFrom)
            {
                PickJiggleTap();
            }
            else
            {
                PickTap(settings, forward, right);
            }
            TacticDiagnostics.Count("diamond.tap");
        }
        if (_tapPause)
        {
            return true;
        }
        // Never tap toward our own live grenade.
        if (OwnGrenadeTracker.Threatens(Bot.ProfileId, Bot.Position + _tapDir * 1f) && OwnGrenadeTracker.Live(Bot.ProfileId, out Vector3 nade)
            && Vector3.Dot(_tapDir, nade - Bot.Position) > 0f)
        {
            _tapDir = -_tapDir;
            TacticDiagnostics.Count("diamond.avoidOwnNade");
        }
        // Never walk off a ledge / into a wall mid-tap: if the next 0.5m is blocked, take the first open direction
        // (opposite, then across, then diagonal back). Before it only flipped and then re-rolled every frame, so in a
        // corridor the bot stood still re-picking (13th/14th sims: "blocked" 394-552 times against 340-521 starts).
        if (NavMesh.Raycast(Bot.Position, Bot.Position + _tapDir * 0.5f, out _, -1))
        {
            if (!TryOpenTapDirection(out Vector3 open))
            {
                // Boxed in: re-pick in 0.1s instead of every frame.
                _tapEnd = Time.time + 0.1f;
                TacticDiagnostics.Count("diamond.blocked");
                return true;
            }
            _tapDir = open;
        }
        Bot.PlayerComponent.CharacterController.SetWantToSprint(false);
        // Far-away "destination" so SAIN's arrival slowdown never kicks in during a tap.
        Bot.PlayerComponent.CharacterController.SetTargetMoveDirection(_tapDir, Bot.Position + _tapDir * 5f, Bot.PlayerComponent, 0f, 1f);
        return true;
    }

    private float _plantUntil;
    private bool _plantStyle;
    private int _tapsSincePlant;

    private bool Plant(CloseCombatSettings settings, float dist)
    {
        float time = Time.time;
        if (_plantUntil > 0f)
        {
            if (time < _plantUntil)
            {
                // Nothing else feeds BSG's aim its "moving" flag while SAIN steers (it's set from SetTargetMoveDirection),
                // so without this the stale "moving" from the last tap would keep the 1.5x spread through the plant.
                Bot.BotOwner.AimingManager?.CurrentAiming?.Move(0f);
                return true;
            }
            _plantUntil = 0f;
            _tapsSincePlant = 0;
            _tapEnd = 0f;
            return false;
        }
        if (!_plantStyle || _jiggle || _tapsSincePlant < 2 || dist < settings.DiamondStepPlantMinDistance
            || Bot.BotOwner.ShootData?.Shooting != true)
        {
            return false;
        }
        // Being hit: keep moving (standing still under fire was what it looked like - "just standing there taking hits").
        if (Bot.Medical.TimeSinceShot < 1f)
        {
            TacticDiagnostics.Count("diamond.plantSkippedHit");
            return false;
        }
        _plantUntil = time + settings.DiamondStepPlantTime * Random.Range(0.8f, 1.25f);
        Bot.Player.Move(Vector2.zero);
        Bot.BotOwner.AimingManager?.CurrentAiming?.Move(0f);
        TacticDiagnostics.Count("diamond.plant");
        return true;
    }

    private readonly Vector3[] _tapCandidates = new Vector3[5];

    private bool TryOpenTapDirection(out Vector3 open)
    {
        Vector3 across = Vector3.Cross(Vector3.up, _tapDir).normalized;
        if (Random.value < 0.5f)
        {
            across = -across;
        }
        _tapCandidates[0] = -_tapDir;
        _tapCandidates[1] = across;
        _tapCandidates[2] = -across;
        _tapCandidates[3] = (across - _tapDir).normalized;
        _tapCandidates[4] = (-across - _tapDir).normalized;
        Vector3 position = Bot.Position;
        for (int i = 0; i < _tapCandidates.Length; i++)
        {
            Vector3 dir = _tapCandidates[i];
            if (!NavMesh.Raycast(position, position + dir * 0.5f, out _, -1))
            {
                open = dir;
                if (i > 0)
                {
                    TacticDiagnostics.Count("diamond.redirected");
                }
                return true;
            }
        }
        open = default;
        return false;
    }

    /// <summary>A corner: one side step (0.8m, walkable) puts the bot's chest out of the enemy's sight.</summary>
    private bool FindCoverSide(Enemy enemy, Vector3 right, out Vector3 coverDir)
    {
        coverDir = default;
        Vector3 eye = enemy.EnemyPosition + Vector3.up * 1.5f;
        foreach (Vector3 side in new[] { right, -right })
        {
            Vector3 p = Bot.Position + side * 0.8f;
            if (NavMesh.Raycast(Bot.Position, p, out _, -1))
            {
                continue;
            }
            if (Physics.Linecast(eye, p + Vector3.up * 1.2f, LayersMaskController.HighPolyWithTerrainMask))
            {
                coverDir = side;
                return true;
            }
        }
        return false;
    }

    /// <summary>Alternate: step into cover (hidden, short) and back out (visible, a little longer - that's when it shoots).</summary>
    private void PickJiggleTap()
    {
        Vector3 offset = Bot.Position - _diamondCenter;
        offset.y = 0f;
        float along = Vector3.Dot(offset, _jiggleCoverDir);
        if (along > 0.9f)
        {
            _jiggleHide = false;
        }
        else if (along < -0.5f)
        {
            _jiggleHide = true;
        }
        else
        {
            _jiggleHide = !_jiggleHide;
        }
        _tapPause = false;
        _tapDir = _jiggleHide ? _jiggleCoverDir : -_jiggleCoverDir;
        _tapEnd = Time.time + (_jiggleHide ? Random.Range(0.18f, 0.32f) : Random.Range(0.25f, 0.45f));
        TacticDiagnostics.Count(_jiggleHide ? "diamond.jiggle.hide" : "diamond.jiggle.peek");
    }

    private void PickTap(CloseCombatSettings settings, Vector3 forward, Vector3 right)
    {
        float tap = TapTime(settings);
        _tapEnd = Time.time + Random.Range(tap * 0.65f, tap * 1.35f);
        // Very short stop now and then (a real player's rhythm isn't perfectly even).
        _tapPause = Random.value < 0.05f;
        if (_tapPause)
        {
            _tapEnd = Time.time + Random.Range(0.05f, 0.09f);
            return;
        }
        Vector3 offset = Bot.Position - _diamondCenter;
        offset.y = 0f;
        float leash = settings.DiamondStepSize;
        if (offset.magnitude > leash)
        {
            // Too far from the spot: tap back toward it.
            _tapDir = -offset.normalized;
            return;
        }
        if (Random.value < 0.6f)
        {
            // A/D, mostly alternating.
            bool left = Random.value < 0.8f ? !_lastTapLateralLeft : _lastTapLateralLeft;
            _lastTapLateralLeft = left;
            _tapDir = left ? -right : right;
        }
        else
        {
            _tapDir = Random.value < 0.5f ? forward : -forward;
        }
    }

    public bool Stop(string why = "actionStopped")
    {
        if (_jiggle)
        {
            _jiggle = false;
            if (Bot?.ProfileId != null)
            {
                _jiggling.Remove(Bot.ProfileId);
            }
        }
        if (Bot.Mover?.Lean != null && Bot.Mover.Lean.LeanSpamActive)
        {
            Bot.Mover.Lean.SetLeanSpam(false, 0.13f);
        }
        if (_diamondActive)
        {
            _diamondActive = false;
            if (Bot.Player?.HealthController?.IsAlive == true)
            {
                Bot.Player.Move(Vector2.zero);
            }
            if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat($"[Diamond] [{Bot.name}] stop: {why}");
        }
        // Stop runs every frame for a bot that doesn't qualify - only build the [Death]-line state when it changes and
        // something will print it (GC is off for the raid, per-frame strings pile up).
        if (TacticDiagnostics.LogOn && !ReferenceEquals(_lastDiamondState, why))
        {
            _lastDiamondState = why;
            TacticDiagnostics.SetDiamond(Bot.ProfileId, $"off({why})");
        }
        return false;
    }

    private string _lastDiamondState;

}
