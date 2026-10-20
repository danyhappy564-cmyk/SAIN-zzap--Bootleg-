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
            if (settings.PmcOnly && Bot.Info.Profile.SkippedByPmcOnly)
            {
                return Stop("scav");
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
        // 2026-10-20 (user, 2nd sim: "steps the diamond while still healing instead of cancelling and shooting"): Medecine.Using
        // goes false when the heal is cancelled or times out, but the meds stay in the hands for the put-away (0.6 s wait + outro)
        // - no stepping while anything but the gun is out of the meds controller either.
        if (Bot.Player.IsInPronePose || Bot.BotOwner.Medecine?.Using == true || Bot.Player.HandsController is IMedsController)
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
        // 10/12 sim (muzzle wobble while peeking): Q/E spam went off on every sub-second sight blip (lean reset to none, then
        // corner pre-aim leaned, then spam picked a side again) - keep it through the same tolerance the step keeps.
        Bot.Mover.Lean.SetLeanSpam(_leanSpam && (enemy.IsVisible || (enemy.Seen && enemy.TimeSinceSeen < sightTolerance)), leanInterval);

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
            // 15:34 sim: the diamond step restarted within 0.3 s 132 times (shoot <-> dog fight) and every restart re-rolled the
            // peek. One roll per engagement: same enemy within 3 s of the last stop keeps the last roll and style.
            bool sameFight = enemy.EnemyProfileId == _rollEnemyId && Time.time < _rollKeepUntil;
            if (!sameFight)
            {
                _rollEnemyId = enemy.EnemyProfileId;
                _rollWantsJiggle = settings.CornerJiggle && Random.value * 100f < settings.CornerJiggleChance;
                _peekStyle = PickPeekStyle(settings);
                _jumpPeeks = 0;
                _runByDone = false;
            }
            _jiggle = _rollWantsJiggle && FindCoverSide(enemy, right, out _jiggleCoverDir);
            if (_jiggle)
            {
                // zzap (10/2 21:37 raid, user: "at a corner they look at me 1-2s late, then shoot"): the first jiggle tap was
                // a HIDE (toggle from false), so a bot that had just spotted him ducked behind the corner before its first
                // burst. Shoot first: plain taps for JIGGLE_DELAY, then the jiggle starts with a peek.
                _jiggleHide = true;
                _jiggleFrom = Time.time + JIGGLE_DELAY;
                TacticDiagnostics.Count("diamond.jiggle");
                if (!sameFight)
                {
                    TacticDiagnostics.Count($"diamond.peekStyle.{_peekStyle}");
                }
                if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat($"[Diamond] [{Bot.name}] corner {_peekStyle} peek: cover on the {(Vector3.Dot(_jiggleCoverDir, right) > 0f ? "right" : "left")}, enemy {dist:0}m{(sameFight ? " (same fight, kept)" : "")}");
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
        if (_runByActive)
        {
            return TickRunBy(enemy, right);
        }
        if (Time.time >= _tapEnd)
        {
            _tapsSincePlant++;
            if (_jiggle && Time.time >= _jiggleFrom)
            {
                if (_peekStyle == EPeekStyle.RunBy && !_runByDone)
                {
                    _runByDone = true; // one try per engagement, whether a crossing point is found or not
                    if (TryStartRunBy(enemy))
                    {
                        return TickRunBy(enemy, right);
                    }
                }
                PickJiggleTap();
                if (!_jiggleHide && _peekStyle == EPeekStyle.Jump)
                {
                    TryJumpPeek();
                }
            }
            else
            {
                PickTap(settings, forward, right);
            }
            // 10/12 sim (bunched): two mates diamond-stepping and reloading on top of each other - don't tap into a mate.
            // Checked once per tap, not every frame.
            if (MateWithin(1.2f, out Vector3 matePos) && Vector3.Dot(_tapDir, matePos - Bot.Position) > 0f)
            {
                _tapDir = -_tapDir;
                TacticDiagnostics.Count("diamond.avoidMate");
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
        // zzap (2026-10-13 screenshots): an open door leaf counts as a wall - the NavMesh runs on behind it (the pocket between
        // leaf and wall), so taps used to walk the bot into it and wedge it there while it aimed at the enemy.
        if (NavMesh.Raycast(Bot.Position, Bot.Position + _tapDir * 0.5f, out _, -1) || DoorLeafCheck.OpenLeafAhead(Bot.Position, _tapDir, 0.65f))
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
        TickJumpPeek();
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
            if (!NavMesh.Raycast(position, position + dir * 0.5f, out _, -1) && !DoorLeafCheck.OpenLeafAhead(position, dir, 0.65f))
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

    // ---------------------------------------------------------------- corner peek styles
    // zzap (user 2026-10-12): jump / run-by / step peeking are fighting peeks - with the enemy in sight, from a corner or door
    // frame, hide and show again and again so he can't land his shots. They only existed in the door tactic (enemy hidden
    // 1.5 s+ behind a door), so they practically never ran. Now the corner jiggle (= step peek) has two more styles, rolled
    // once per engagement: step 60 / jump 25 / run-by 15 (F6). Jump and run-by: Chad / GigaChad and half of the PMC Normals.
    public enum EPeekStyle
    {
        Step,
        Jump,
        RunBy,
    }

    private string _rollEnemyId;
    private float _rollKeepUntil;
    private bool _rollWantsJiggle;
    private EPeekStyle _peekStyle;
    private int _jumpPeeks;
    private float _nextJumpPeek;
    private bool _runByDone;
    private bool _runByActive;
    private Vector3 _runByTarget;
    private float _runByUntil;

    private const int MAX_JUMP_PEEKS = 3;

    private bool BoldPeeker()
    {
        var personality = Bot.Info.Personality;
        if (personality == EPersonality.Chad || personality == EPersonality.GigaChad)
        {
            return true;
        }
        // half of the PMC Normals, the same half all raid (by profile id)
        return personality == EPersonality.Normal && Bot.Info.Profile.IsPMC && (StableHash(Bot.ProfileId) & 1) == 0;
    }

    private static int StableHash(string s)
    {
        int h = 17;
        for (int i = 0; i < s.Length; i++)
        {
            h = h * 31 + s[i];
        }
        return h;
    }

    private EPeekStyle PickPeekStyle(CloseCombatSettings settings)
    {
        if (!BoldPeeker())
        {
            return EPeekStyle.Step;
        }
        float roll = Random.value * 100f;
        if (roll < settings.CornerPeekJumpShare)
        {
            return EPeekStyle.Jump;
        }
        if (roll < settings.CornerPeekJumpShare + settings.CornerPeekRunByShare)
        {
            return EPeekStyle.RunBy;
        }
        return EPeekStyle.Step;
    }

    /// <summary>On a peek tap: hop out instead of stepping (headroom, level landing on the navmesh, 2.5-4 s apart, 3 per fight).</summary>
    private void TryJumpPeek()
    {
        float time = Time.time;
        if (_jumpPeeks >= MAX_JUMP_PEEKS || time < _nextJumpPeek || !Bot.Player.MovementContext.IsGrounded)
        {
            return;
        }
        if (!JumpPeekSafe())
        {
            TacticDiagnostics.Count("diamond.jumpPeek.unsafe");
            _nextJumpPeek = time + 1f;
            return;
        }
        // zzap (user 2026-10-20: "they jump on the spot instead of out to the side"): the peek tap starts from standing (or
        // from the hide tap's other direction), and EFT's jump keeps the take-off speed - jumping here went straight up. Walk
        // out first and hop once the body moves that way (TickJumpPeek, up to 0.45 s); no speed by then = a plain step peek.
        _jumpPeekPendingUntil = time + 0.45f;
        _tapEnd = Mathf.Max(_tapEnd, time + 0.45f);
    }

    private float _jumpPeekPendingUntil;

    private bool JumpPeekSafe()
    {
        Vector3 pos = Bot.Position;
        // a hop from a walk carries ~1.5 m: land, headroom and a clear lane that far
        Vector3 land = pos + _tapDir * 1.6f;
        int mask = LayersMaskController.HighPolyWithTerrainMask;
        return !(NavMesh.Raycast(pos, land, out _, -1)
            || Physics.Raycast(pos + Vector3.up * 1.7f, Vector3.up, 0.7f, mask)
            || Physics.Raycast(pos + Vector3.up * 0.5f, _tapDir, 1.8f, mask)
            || Physics.Raycast(pos + Vector3.up * 1.2f, _tapDir, 1.8f, mask));
    }

    /// <summary>Every tick of a peek tap with a hop pending: jump once the bot moves out at walking speed.</summary>
    private void TickJumpPeek()
    {
        if (_jumpPeekPendingUntil <= 0f)
        {
            return;
        }
        float time = Time.time;
        if (time > _jumpPeekPendingUntil || _jiggleHide)
        {
            _jumpPeekPendingUntil = 0f;
            TacticDiagnostics.Count("diamond.jumpPeek.noRunUp");
            return;
        }
        Vector3 v = Bot.Player.Velocity;
        v.y = 0f;
        if (!Bot.Player.MovementContext.IsGrounded || Vector3.Dot(v, _tapDir) < 1.1f)
        {
            return;
        }
        // the tap may have turned since the hop was planned (mate / own grenade / blocked lane): check where it goes now
        if (!JumpPeekSafe())
        {
            _jumpPeekPendingUntil = 0f;
            TacticDiagnostics.Count("diamond.jumpPeek.unsafe");
            return;
        }
        if (!Bot.Mover.TryJumpMoving(_tapDir, 1.1f))
        {
            return;
        }
        _jumpPeekPendingUntil = 0f;
        _jumpPeeks++;
        _nextJumpPeek = time + Random.Range(2.5f, 4f);
        _tapEnd = time + 0.55f; // the hop is this peek
        Bot.BotOwner.ShootData?.BlockFor(0.4f); // look in the air, shoot on landing
        Bot.Mover.MarkJump("jumpPeek");
        TacticDiagnostics.Count("diamond.jumpPeek");
    }

    /// <summary>
    /// Run-by: a point 2.5-4.5 m along the open side that is hidden from him again (cover on the far side of the opening),
    /// with the way there open on the navmesh and the middle of the run in his sight (it is an opening, not a wall).
    /// </summary>
    private bool TryStartRunBy(Enemy enemy)
    {
        Vector3 pos = Bot.Position;
        Vector3 across = -_jiggleCoverDir;
        Vector3 eye = enemy.EnemyPosition + Vector3.up * 1.5f;
        int mask = LayersMaskController.HighPolyWithTerrainMask;
        for (int i = 0; i < _runByDistances.Length; i++)
        {
            Vector3 raw = pos + across * _runByDistances[i];
            if (!NavMesh.SamplePosition(raw, out NavMeshHit hit, 0.5f, -1) || NavMesh.Raycast(pos, hit.position, out _, -1))
            {
                continue;
            }
            Vector3 mid = (pos + hit.position) * 0.5f;
            bool farHidden = Physics.Linecast(eye, hit.position + Vector3.up * 1.2f, mask);
            bool midOpen = !Physics.Linecast(eye, mid + Vector3.up * 1.2f, mask);
            if (!farHidden || !midOpen)
            {
                continue;
            }
            _runByTarget = hit.position;
            _runByUntil = Time.time + _runByDistances[i] / 4f + 0.6f;
            _runByActive = true;
            TacticDiagnostics.Count("diamond.runBy.start");
            if (TacticDiagnostics.LogOn) TacticDiagnostics.LogCloseCombat($"[Diamond] [{Bot.name}] run-by peek: crossing {_runByDistances[i]:0.0}m to cover on the far side");
            return true;
        }
        TacticDiagnostics.Count("diamond.runBy.noPoint");
        return false;
    }

    private static readonly float[] _runByDistances = { 3.5f, 2.5f, 4.5f };

    private bool TickRunBy(Enemy enemy, Vector3 right)
    {
        Vector3 to = _runByTarget - Bot.Position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.4f * 0.4f || Time.time > _runByUntil)
        {
            _runByActive = false;
            Bot.PlayerComponent.CharacterController.SetWantToSprint(false);
            bool arrived = to.sqrMagnitude < 0.4f * 0.4f;
            TacticDiagnostics.Count(arrived ? "diamond.runBy.done" : "diamond.runBy.timeout");
            // keep peeking from the new side if it is a corner too, else plain diamond steps
            _jiggle = FindCoverSide(enemy, right, out _jiggleCoverDir);
            _jiggleHide = true;
            _jiggleFrom = Time.time + 0.2f;
            _tapEnd = 0f;
            return true;
        }
        Bot.PlayerComponent.CharacterController.SetWantToSprint(true);
        Bot.PlayerComponent.CharacterController.SetTargetMoveDirection(to.normalized, _runByTarget, Bot.PlayerComponent, 0f, 1f);
        return true;
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
    private bool MateWithin(float meters, out Vector3 matePos)
    {
        matePos = default;
        var group = Bot.BotOwner?.BotsGroup;
        if (group == null)
        {
            return false;
        }
        Vector3 pos = Bot.Position;
        for (int i = 0; i < group.MembersCount; i++)
        {
            var mate = group.Member(i);
            if (mate == null || mate == Bot.BotOwner || mate.IsDead || mate.GetPlayer == null)
            {
                continue;
            }
            Vector3 p = mate.GetPlayer.Position;
            if ((p - pos).sqrMagnitude < meters * meters)
            {
                matePos = p;
                return true;
            }
        }
        return false;
    }

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
        _rollKeepUntil = Time.time + 3f;
        _jumpPeekPendingUntil = 0f;
        if (_runByActive)
        {
            _runByActive = false;
            Bot.PlayerComponent?.CharacterController?.SetWantToSprint(false);
        }
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
