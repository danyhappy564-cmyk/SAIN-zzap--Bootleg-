using static SAINServerMod.Web.Services.WebText;

namespace SAINServerMod.SimLab;

/// <summary>
/// zzap fork: what the sim counters, decision reasons and personalities mean (user 2026-10-09: "explain in Korean what each one
/// is - one explanation for reading the analysis, a separate one for Claude's log analysis"). Plain text (Korean / English) is
/// shown on the analysis page; <see cref="Entry.Code"/> (where in SAIN it is counted) goes only into the Claude summary file.
/// Counters are matched by the longest prefix, so "door.reject.noDoorBetween" uses the "door.reject." entry.
/// </summary>
public static class SimLabGlossary
{
    public sealed record Entry(string Key, string Ko, string En, string Code);

    public static readonly Entry[] Counters =
    [
        // door tactics
        new("door.start.", "문 전술 시작 (성격.종류: Clear=방 정리 진입, Peek=문틀 피킹, Trap=문 함정 매복)", "Door tactic started (personality.kind: Clear = room clear, Peek = door-frame peek, Trap = door ambush)", "DoorTacticClass start"),
        new("door.pick.", "문 전술 후보 중 고른 종류", "Door tactic kind chosen", "DoorTacticClass pick"),
        new("door.end.", "문 전술이 끝난 이유 (종류.이유)", "Why a door tactic ended (kind.reason)", "DoorTacticClass End(reason)"),
        new("door.reject.", "문 전술을 '쓸까?' 검사했다가 조건이 안 맞아 안 씀 (예: noDoorBetween=적과 사이에 문 없음). 매 판단마다 세서 숫자가 큰 게 정상", "Door tactic checked and not used (e.g. noDoorBetween = no door between). Counted every check, big numbers are normal", "DoorTacticClass CanStart reject reasons"),
        new("door.holdReject.", "문 지키기(엄폐 대신 문 지키기) 검사 후 안 씀", "Door hold checked and not used", "DoorTacticClass hold reject"),
        new("door.searchReject.", "수색 중 방 정리 검사 후 안 씀", "Room clear while searching checked and not used", "DoorTacticClass search reject"),
        new("door.clear.", "방 정리 동작 (dash=달려 들어감, entered=진입 완료, fake=페이크 후 진입)", "Room clear steps (dash, entered, fake)", "DoorTacticClass Clear"),
        new("door.role.", "분대 문 역할 나누기 (solo=혼자, rearGuard=뒤 지키기)", "Squad door roles (solo, rear guard)", "DoorTacticClass roles"),
        new("door.abort.", "진행 중이던 문 전술을 중간에 그만둠", "Door tactic aborted midway", "DoorTacticClass abort"),
        new("door.stalemateBoost", "문 앞 대치가 길어져 문 전술 확률을 올림", "Door stalemate: door tactic chance raised", "DoorTacticClass stalemate"),
        new("door.keptOverChase", "코너 추격 대신 진행 중이던 문 전술을 유지", "Kept the door tactic over a corner chase", "DoorTacticClass"),
        new("door.", "문 전술 관련 기타", "Other door-tactic events", "DoorTacticClass / DoorHandler"),
        // close combat
        new("diamond.tap", "다이아몬드 스텝: 좌우로 한 번 디딤 (근거리 교전 중 몸을 흔드는 동작 한 번)", "Diamond step: one side step (body shuffle in a close fight)", "DiamondStepper tap"),
        new("diamond.start", "다이아몬드 스텝 시작", "Diamond step started", "DiamondStepper"),
        new("diamond.jiggle", "코너에서 몸을 내밀었다 숨는 흔들기 (peek=내밂, hide=숨음)", "Corner jiggle peek (peek / hide)", "DiamondStepper jiggle"),
        new("diamond.leanSpam", "기울이기(Q/E) 연타", "Lean spam (Q/E)", "DiamondStepper lean spam"),
        new("diamond.plant", "쏘는 순간 발을 멈춰 정확도 확보", "Planted feet to shoot accurately", "DiamondStepper plant"),
        new("diamond.", "다이아몬드 스텝 기타 (blocked=막힘, redirected=방향 바꿈)", "Other diamond-step events", "DiamondStepper"),
        new("chase.", "코너 추격: 숨은 적을 쫓아 코너를 도는 방식 (Pie=조금씩 각 넓히기, LeanIn=기울여 들어가기, JumpShot=점프하며 쏘기)", "Corner chase styles (Pie, LeanIn, JumpShot)", "CornerChase"),
        new("corner.preAim", "코너를 돌기 전에 미리 조준", "Pre-aimed before rounding a corner", "SAINSteeringClass pre-aim"),
        new("close.", "근거리 이동 (짧게 달려 엄폐, 적을 보며 걷기)", "Close-range movement (short dash to cover, walk facing enemy)", "SAINCoverClass / EnemyDecisionClass"),
        new("threat.keep", "목표 적을 그대로 유지 (너무 자주 바꾸지 않게)", "Kept the current target enemy", "ThreatPicker keep"),
        new("threat.switch.", "목표 적을 바꿈 (flanker=옆으로 온 적, seen=보이는 적, unseen=안 보이지만 가까운 적)", "Switched target (flanker, seen, unseen)", "ThreatPicker switch"),
        // decision
        new("utility.do.", "적이 안 보일 때 고른 행동 (Freeze=얼음 매복, Search=수색, RushEnemy=돌격, SeekCover=엄폐 찾기, DoorTactic=문 전술...). 판단할 때마다 셈", "Action chosen while the enemy is hidden (Freeze, Search, RushEnemy, SeekCover, DoorTactic...). Counted every decision", "HiddenEnemyUtility"),
        new("utility.top.", "안 보이는 적 상대로 점수가 가장 높았던 선택지", "Highest-scored option against a hidden enemy", "HiddenEnemyUtility scores"),
        new("utility.fellTo.", "1순위를 못 써서 다음 선택지로 넘어감", "Fell back to the next option", "HiddenEnemyUtility fallback"),
        new("utility.noneRunnable", "쓸 수 있는 선택지가 하나도 없어 기본 판단에 맡김", "No option was runnable - left to the default decision", "HiddenEnemyUtility"),
        new("utility.hold.skippedUnderFire", "맞는 중이라 '지키기'를 건너뜀", "Skipped 'hold' while under fire", "HiddenEnemyUtility"),
        new("utility.keepOnBlip", "적 정보가 잠깐 끊겨도 하던 행동 유지", "Kept the action through a short info blip", "HiddenEnemyUtility"),
        new("utility.brokeOff", "하던 행동을 끊고 다시 판단", "Broke off the action and re-decided", "HiddenEnemyUtility"),
        new("utility.dice.", "점수가 비슷한 선택지 중 무작위로 고름 (2개/3개 중)", "Random pick between close scores (2 / 3 options)", "HiddenEnemyUtility dice"),
        new("utility.", "안 보이는 적 판단 기타", "Other hidden-enemy decision events", "HiddenEnemyUtility / UtilityMistake"),
        new("utilityV.do.", "적이 보일 때 고른 행동 (StandAndShoot=서서 쏘기, RushEnemy=돌격, SeekCover=엄폐)", "Action chosen with the enemy visible (StandAndShoot, RushEnemy, SeekCover)", "VisibleEnemyUtility"),
        new("utilityV.top.", "보이는 적 상대로 점수가 가장 높았던 선택지 (Shoot/Push/Cover)", "Highest-scored option against a visible enemy", "VisibleEnemyUtility scores"),
        new("decision.", "판단 유지 규칙 (closeOcclusionKeep=가까운 적이 잠깐 가려져도 유지)", "Decision keep rules", "EnemyDecisionClass"),
        new("death.", "봇이 죽을 때 하고 있던 전투 행동 (DogFight=근접 난전, SeekCover=엄폐 이동, StandAndShoot=서서 쏘기, Freeze=얼음 매복...)", "Combat action the bot was doing when it died", "DoorTacticClass death log"),
        // reload / heal / weapons
        new("reload.pick.", "장전 판단: Reload=지금 장전, Hold=버티고 나중에 (판단할 때마다 셈)", "Reload decision: Reload now / Hold (counted every decision)", "ReloadUtility"),
        new("reload.justDucked.", "적이 방금 숨은 순간이라 장전 보류", "Held the reload: enemy just ducked", "ReloadUtility"),
        new("reload.cantReload", "장전하려 했지만 못 함 (탄창 없음 등)", "Wanted to reload but couldn't", "SelfActionDecisionClass"),
        new("reload.noAmmo.", "탄이 아예 없을 때 대응 (melee=칼, nothing=할 수 있는 게 없음)", "Out of ammo response", "ReloadUtility"),
        new("heal.pick.", "치료 판단 (FirstAid=구급상자, Stim=주사기, Wait=나중에)", "Heal decision (FirstAid, Stim, Wait)", "HealUtility"),
        new("heal.cancel.", "위협 때문에 치료 취소", "Heal cancelled by a threat", "HealUtility"),
        new("weapon.", "무기 교체·빠른 장전 등 무기 관련", "Weapon swaps, quick reloads", "CombatWeaponClass / WeaponLog"),
        // squad
        new("squad.start.", "분대 전술 시작 (CoverMate=장전·치료 중인 동료 엄호, Crossfire=교차 사격 자리, TradeAngle=복수 각도, TradePush=쓰러진 동료 복수 돌격)", "Squad tactic started (CoverMate, Crossfire, TradeAngle, TradePush)", "SquadCombatClass"),
        new("squad.arrived.", "분대 전술 자리에 도착", "Reached the squad-tactic spot", "SquadCombatClass"),
        new("squad.end.", "분대 전술 끝난 이유", "Why a squad tactic ended", "SquadCombatClass"),
        new("squad.crossfire.", "교차 사격 자리 찾기 결과 (noPoint=못 찾음)", "Crossfire spot search (noPoint = none found)", "SquadCombatClass crossfire"),
        new("squad.fireLane.", "동료 사격선 가리지 않기 (shooterHeld=동료가 앞을 막아 쏘기 보류)", "Fire lanes (shooterHeld = a mate blocked the shot)", "FireLaneGuard"),
        new("squad.storm.", "분대 일제 돌격", "Squad storm (all push)", "SquadStorm"),
        new("squad.teammateDown", "동료가 쓰러짐을 봄", "Saw a teammate go down", "SquadCombatClass"),
        new("squad.", "분대 교전 기타", "Other squad events", "SquadCombatClass"),
        // reposition / grenades
        new("repo.contact.", "첫 접촉 누가 먼저? BotFirst=내가 먼저 봄, EnemyFirst=적이 먼저 쏨", "First contact: BotFirst = I saw first, EnemyFirst = enemy shot first", "RepositionClass.Observe"),
        new("repo.start.", "위치 바꾸기 계열 시작 (Relocate=자리 옮기기, BaitPeek=미끼 피킹, FakeReload=가짜 장전)", "Reposition started (Relocate, BaitPeek, FakeReload)", "RepositionClass"),
        new("repo.end.", "위치 바꾸기 끝난 이유", "Why a reposition ended", "RepositionClass"),
        new("repo.noPoint.", "옮길 자리를 못 찾음", "No spot found", "RepositionClass"),
        new("repo.", "위치 바꾸기 기타", "Other reposition events", "RepositionClass"),
        new("nade.judge.", "수류탄 판단 (throw=던짐, hold=참음)", "Grenade judgement (throw / hold)", "GrenadeThrowDecider"),
        new("nade.thrown.", "수류탄을 던진 대상 (lastKnown=마지막 위치, blindCorner=안 보이는 코너, throughDoor=문 너머)", "Grenade target (lastKnown, blindCorner, throughDoor)", "GrenadeThrowDecider"),
        new("nade.noArc", "던질 궤적을 못 찾음 (벽·천장에 막힘)", "No throw arc found", "GrenadeThrowDecider"),
        new("nade.", "수류탄 기타 (fuse=짧게/길게 쥐었다 던짐, blocked=조건상 금지)", "Other grenade events", "GrenadeThrowDecider"),
        new("fakeNade.", "가짜 수류탄 (꺼냈다 다시 넣기)", "Fake grenade", "DoorTacticClass"),
        new("ownGrenade.", "내가 던진 수류탄 위치 추적", "Tracked own grenade", "OwnGrenadeTracker"),
        new("trick.", "속임수 동작 취소·건너뜀", "Tricks cancelled / skipped", "DoorTacticClass"),
        // freeze / search / post combat
        new("freeze.", "얼음 매복: 가까이 들린 적을 기다림 (start=시작, end=끝난 이유, cornerWatch=코너 주시)", "Freeze ambush (start, end reason, corner watch)", "FreezeAction"),
        new("peekSpot.", "피킹 자리 찾기 (none=못 찾음, step=한 걸음 옮겨 자리 잡음)", "Peek spot search (none / step)", "PeekSpot"),
        new("postCombat.", "교전이 끝난 뒤 행동 (regroup=모이기, heal=치료, advance=전진, reload=장전)", "After-fight actions (regroup, heal, advance, reload)", "PostCombatAction"),
        new("lean.", "기울이기 (hold=기울인 채 유지, rock=좌우 흔들기, stoppedForMeds=치료 때문에 멈춤)", "Leaning (hold, rock, stopped for meds)", "LeanClass"),
        new("retreat.", "후퇴 동작 (weave=지그재그, headDown=고개 숙여 뛰기, weaveJump=꺾을 때 점프, landingJump=엄폐 앞 착지 점프)", "Retreat (weave, head down, switch hop, landing jump)", "RetreatWeave / SAINSteeringClass"),
        new("jump.airShot.", "점프 중 사격 굴림 (allowed=공중에서도 쏨, held=착지까지 참음; 봇·적마다 한 번)", "Air-shot roll per bot and enemy (allowed / held until landing)", "SAINMoverClass.TryJump"),
        new("suppress.", "제압 사격 (burstPause=점사 사이 쉼)", "Suppression (burst pause)", "SAINBotSuppressClass"),
        new("light.", "손전등 켜기/끄기", "Flashlight use", "BotLightController"),
        // ORBIT handoff / stuck / sim
        new("handoff.toOrbit", "교전이 끝나 ORBIT(맵 이동 모드)로 넘어감 (gap=끝나고 몇 초 뒤)", "Fight ended -> handed to ORBIT (gap = seconds after)", "LayerHandoff"),
        new("handoff.orbitToCombat", "ORBIT 중에 적을 만나 SAIN 전투로 돌아옴", "ORBIT -> back to SAIN combat", "LayerHandoff"),
        new("handoff.orbitFlipFlop", "ORBIT로 갔다가 금방(5초 안) 전투로 되돌아옴 — 많으면 ORBIT와 SAIN이 서로 가져감", "ORBIT then back to combat within 5 s (flip-flop)", "LayerHandoff"),
        new("handoff.combatResumed", "교전이 끝나고 ORBIT가 받기 전에 전투 재개", "Combat resumed before ORBIT took over", "LayerHandoff"),
        new("handoff.gapLayer.", "교전 끝~ORBIT 사이에 잠깐 잡은 다른 레이어 (바닐라 순찰 등)", "Other layer in the gap between combat and ORBIT", "LayerHandoff"),
        new("handoff.", "레이어 넘기기 기타", "Other handoff events", "LayerHandoff"),
        new("stuck.", "막힘 감지·탈출 단계 (stage1~4, freed=풀려남, layer=막힌 레이어)", "Stuck detection / escape stages", "SAINBotUnstuckClass"),
        new("oddity.", "이상 행동 자동 감지 (noShoot=보이는 적에게 안 쏨, backTurned=가까운 적에게 등 돌림, hitNoReact=맞고도 반응 없음, flipFlop=판단이 계속 뒤집힘, bunched=아군과 몸이 겹침, stalledMove=움직여야 하는데 제자리). 한 번 걸린 상태가 풀릴 때까지 1회", "Automatic oddity detector (noShoot / backTurned / hitNoReact / flipFlop / bunched / stalledMove), one per episode", "SimLabOddity"),
        new("look.wallFix.", "벽을 보고 있던 봇이 시선을 고침 (corner=적이 나올 코너, open=트인 방향, none=고칠 곳 없음)", "Wall stare corrected (corner / open / none)", "SAINSteeringClass.AvoidWallStare"),
        new("hear.firstSeen.", "처음 적을 봤을 때 10초 안에 소리로 먼저 들었는지 (heard.move=발소리·문·덤불, heard.gun=총소리, notHeard=못 들음) · 거리 close<15m mid<40m far", "On first sight: was the enemy heard in the 10 s before (move / gun / not heard) and range", "EnemyEvents.CountFirstSeen"),
        new("sim.", "시뮬 전용 (시체 정리 등)", "Sim only (corpse cleanup)", "CorpseCleanup"),
        new("perf.", "성능 정리 (시체 피 자국 해제)", "Performance cleanup", "CorpseDecalRelease"),
        new("deadBug.", "죽음 처리 버그 점검 (bodyOk=정상)", "Death-handling check", "SafeDeathPatch / CorpseCleanup"),
    ];

    public static readonly Entry[] Reasons =
    [
        new("utilityVShoot", "적이 보여서 쏘는 중", "Enemy visible - shooting", "VisibleEnemyUtility Shoot"),
        new("utilityVPush", "적이 보이는데 밀고 들어가는 중", "Enemy visible - pushing", "VisibleEnemyUtility Push"),
        new("utilityVCover", "적이 보이는데 엄폐로 이동 중", "Enemy visible - moving to cover", "VisibleEnemyUtility Cover"),
        new("utilityFallBack", "안 보이는 적에게서 물러나는 중", "Falling back from a hidden enemy", "HiddenEnemyUtility FallBack"),
        new("utilityHold", "안 보이는 적을 기다리며 지키는 중", "Holding against a hidden enemy", "HiddenEnemyUtility Hold"),
        new("utilityPush", "안 보이는 적에게 밀고 가는 중", "Pushing a hidden enemy", "HiddenEnemyUtility Push"),
        new("utilitySearch", "안 보이는 적을 찾는 중", "Searching for a hidden enemy", "HiddenEnemyUtility Search"),
        new("utilityFlank", "안 보이는 적의 옆으로 도는 중", "Flanking a hidden enemy", "HiddenEnemyUtility Flank"),
        new("noBulletsOrReloading", "탄이 없거나 장전 중", "No bullets / reloading", "EnemyDecisionClass"),
        new("closeOcclusionKeep", "가까운 적이 잠깐 가려졌지만 하던 행동 유지", "Close enemy briefly hidden - kept the action", "EnemyDecisionClass"),
    ];

    public static readonly Entry[] Personalities =
    [
        new("GigaChad", "가장 공격적. 문 전술·돌격을 가장 자주", "Most aggressive", ""),
        new("Chad", "공격적. 밀고 들어가는 편", "Aggressive, pushes", ""),
        new("Normal", "보통", "Average", ""),
        new("Rat", "숨어서 기다리는 편", "Hides and waits", ""),
        new("SnappingTurtle", "자리를 지키다 가까이 오면 반격", "Holds, snaps back when close", ""),
        new("Coward", "겁이 많아 자주 물러남", "Cowardly, falls back", ""),
        new("Timmy", "초보 흉내 (실수 많음)", "Newbie-like", ""),
        new("Wreckless", "무모하게 돌진", "Reckless rusher", ""),
    ];

    public static Entry? Find(Entry[] table, string key)
    {
        Entry? best = null;
        foreach (var e in table)
        {
            if (key.StartsWith(e.Key, StringComparison.Ordinal) && (best == null || e.Key.Length > best.Key.Length))
            {
                best = e;
            }
        }
        return best;
    }

    public static string Explain(Entry[] table, string key)
    {
        var e = Find(table, key);
        return e == null ? string.Empty : T(e.Ko, e.En);
    }
}
