using SAIN.Preset.Shared.SimLab;

namespace SAINServerMod.SimLab;

/// <summary>
/// zzap fork: did every feature we added actually run in this sim map (user 2026-10-09: "the sim log must show precisely whether
/// ALL the features we added work - the README's added features + bug fixes")? One row per README row: the counters that prove
/// it fired, the ones that mean it was tried and refused, a "bad" counter that should stay 0 for a bug fix, and the journal tag
/// to read for the details. Counters are prefixes of TacticDiagnostics keys (SimBeat.Counters).
/// When a README row is added, add its row here too.
/// </summary>
public static class SimLabFeatures
{
    public sealed record Feature(string Group, string Name, string[] Fired, string[] Refused, string[] Bad, string Journal, string Note = "", bool ZeroOk = false, bool ZeroOnlyInTestPreset = false)
    {
        /// <summary>0 is fine for this map - except a "0 only because of the TEST preset's switches" row in a zzap cross-check raid
        /// (remake5), which then must fire like for zzap users.</summary>
        public bool ZeroFine(SimMapRecord? m) => ZeroOk && !(ZeroOnlyInTestPreset && m?.Raid?.ZzapBehaviour == true);
    }

    private static string[] A(params string[] keys) => keys;

    /// <summary>English feature names for the web page's English mode (the Korean name stays the key and the md text).</summary>
    private static readonly Dictionary<string, string> NamesEn = new()
    {
        ["점프 피킹"] = "Jump peek",
        ["런바이 피킹"] = "Run-by peek",
        ["스텝 피킹"] = "Step peek",
        ["페이크 수류탄"] = "Fake grenade",
        ["페이크 치료·스팀"] = "Fake heal / stim",
        ["방 진입(Clear)"] = "Room clear (Clear)",
        ["방 가두기(Trap)"] = "Room trap (Trap)",
        ["지키기 = 문 지키기"] = "Hold = door hold",
        ["문 옆 매복(Rat)"] = "Ambush beside a door (Rat)",
        ["분대 역할(오버워치/후방)"] = "Squad roles (overwatch / rear)",
        ["제3자 감지(문 전술 중단)"] = "Third party noticed (door tactic aborted)",
        ["다이아몬드 스텝"] = "Diamond step",
        ["근거리 가림 유지"] = "Keep close-range cover",
        ["코너 흔들기 피킹"] = "Corner jiggle peek",
        ["기울이기(사격 중)"] = "Lean (while firing)",
        ["안 보이는 적 판단(유틸리티)"] = "Unseen enemy decision (utility)",
        ["보이는 적 판단(유틸리티)"] = "Visible enemy decision (utility)",
        ["재장전 타이밍(유틸리티)"] = "Reload timing (utility)",
        ["치료 타이밍(유틸리티)"] = "Heal timing (utility)",
        ["장비 기반 교전 방식"] = "Gear-based fighting style",
        ["두려움"] = "Fear",
        ["결과로 배우기"] = "Learn from outcomes",
        ["레이드 일지"] = "Raid journal",
        ["사람 같은 실수"] = "Human-like mistakes",
        ["근접 교전 우선"] = "Close-range fight first",
        ["개활지 질주 대신 교전"] = "Fight instead of sprinting across open ground",
        ["코너 추격"] = "Corner chase",
        ["후퇴 고개 숙이기 + 지그재그"] = "Retreat ducking + zigzag",
        ["빛 관리"] = "Light discipline",
        ["코너 선조준"] = "Corner pre-aim",
        ["위협 기준 목표 선택"] = "Threat-based target choice",
        ["수류탄 판단"] = "Grenade decision",
        ["자기 수류탄 계산"] = "Own grenade math",
        ["권총 전환"] = "Pistol switch",
        ["빠른 장전"] = "Quick reload",
        ["장거리 무기 전환"] = "Long-range weapon switch",
        ["등 안 돌리기"] = "Don't turn your back",
        ["제압사격 절제"] = "Suppression restraint",
        ["재배치(Relocate)"] = "Relocate",
        ["미끼 피킹"] = "Bait peek",
        ["가짜 재장전"] = "Fake reload",
        ["수류탄 신관 선택"] = "Grenade fuse choice",
        ["분대 교전(교차·엄호·트레이드)"] = "Squad combat (crossfire / cover / trade)",
        ["분대 협동(제압+측면)"] = "Squad teamwork (suppress + flank)",
        ["약한 적 일제 돌격"] = "Squad storm on a weak enemy",
        ["전투 후 정리"] = "Post-combat clean-up",
        ["아군 사선 회피"] = "Avoid friendly fire lanes",
        ["정지 및 매복(Freeze)"] = "Freeze ambush",
        ["폭발음 반응"] = "Explosion reaction",
        ["플레이어 스타일 대응"] = "Player style response",
        ["벽 보고 서 있지 않기"] = "Don't stand staring at walls",
        ["봇끼리 발소리 듣기"] = "Bots hear bot footsteps",
        ["ORBIT 인계"] = "ORBIT hand-over",
        ["문 낑김 / 아무도 못 여는 문"] = "Stuck doors / doors nobody can open",
        ["주운 총 장전·무기 교체"] = "Loading a picked-up gun / weapon swap",
        ["무기 계속 바꾸기(핑퐁)"] = "Weapon ping-pong",
        ["빠른 재장전 탄창 소실"] = "Quick reload losing the magazine",
        ["벽에 계속 박기(끼임 해제)"] = "Running into walls (unstick)",
        ["죽은 채로 걷는 시체"] = "Corpses walking after death",
        ["시체 피·총알 자국 텍스처"] = "Corpse blood / bullet hole textures",
        ["전술장비 딸깍 반복"] = "Tactical device click spam",
        ["치료하며 피킹"] = "Peeking while healing",
        ["팩션 분대 섞기(로그·레이더)"] = "Faction squad mix (rogues / raiders)",
        ["판마다 보스 무작위"] = "Random boss per raid",
        ["연막 오투척 방지"] = "No mis-thrown smoke",
        ["층 차이 소리 감쇠"] = "Sound damping between floors",
        ["문 전술 제3자: 다른 층 제외"] = "Door tactic third party: other floors ignored",
        ["먼 총소리 3단계(고개만/무시)"] = "Far gunfire in 3 steps (look only / ignore)",
        ["나를 쏜 원거리 적은 거리 무시하고 반응"] = "Far enemy who shot me: react regardless of distance",
        ["전투 후 정리가 총소리를 들음"] = "Post-combat clean-up hears gunfire",
        ["저사양: 봇끼리 미끼 안 낚임"] = "Low-spec: bots don't take bot bait",
        ["취약 순간 돌격(소리 종류별 남은 시간)"] = "Vulnerable-moment rush (time left per sound)",
        ["수류탄 꺼낸 적에게 안 달려듦"] = "No charging an enemy holding a grenade",
        ["발소리 관리(30m 걷기·12m 숙여 걷기)"] = "Footstep discipline (walk 30 m, crouch-walk 12 m)",
        ["봇 박치기 문 부숨 (SAIN 자체)"] = "Bot ram breaks doors (SAIN's own)",
        ["몸통박치기 방 진입"] = "Shoulder-ram room entry",
        ["달리다가 문 몸통박치기 (도망·돌격·수색·그 밖의 전투 이동)"] = "Sprint door ram (flee / rush / search / other combat moves)",
    };

    public static string DisplayName(Feature f)
    {
        return !Web.Services.WebText.Korean && NamesEn.TryGetValue(f.Name, out string? en) ? en : f.Name;
    }

    public static readonly Feature[] All =
    {
        // README "추가한 기능"
        new("tactic", "점프 피킹", A("diamond.jumpPeek", "door.jumpOut", "door.jumpBack"), A("diamond.jumpPeek.unsafe", "door.jumpUnsafe", "door.jumpFrameInTheWay", "door.jumpLeafTooClose"), A(), "[Diamond] / [DoorTactic]", "교전 중 코너 피킹(diamond.*) + 문 전술(door.*). 점프 비율 25%, Chad·GigaChad·PMC Normal 절반"),
        new("tactic", "런바이 피킹", A("diamond.runBy.start", "door.runBy.start"), A("diamond.runBy.noPoint", "door.runBy.noPoint", "door.runBy.noAnglePoint", "door.runBy.abortHeard"), A(), "[Diamond] / [DoorTactic]", "교전 중 코너 피킹(diamond.*) + 문 전술(door.*). 런바이 비율 15%, 반대편 엄폐가 있는 트인 곳만"),
        new("tactic", "스텝 피킹", A("diamond.peekStyle.Step", "door.stepPeek", "door.peek.plain"), A(), A(), "[Diamond] / [DoorTactic]", "교전 중 코너 흔들기(A/D) = 스텝 피킹"),
        new("tactic", "페이크 수류탄", A("fakeNade.drawn"), A("fakeNade.putAwayRefused", "trick.skipped", "trick.cancelledOnSound"), A(), "[DoorTactic]"),
        new("tactic", "페이크 치료·스팀", A("fakeStim.started", "fakeHeal.started"), A("fakeStim.cancelled", "fakeHeal.failedToStart"), A(), "[DoorTactic]", "문 전술(가두기·피킹)이 문틀까지 가야 결정됨 — 문 전술 시작이 적으면 0이 흔함"),
        new("tactic", "방 진입(Clear)", A("door.pick.Clear", "door.breach."), A("door.planFailed"), A(), "[DoorTactic]"),
        new("tactic", "방 가두기(Trap)", A("door.pick.Trap", "door.start.hold.Trap"), A(), A(), "[DoorTactic]", "밀고 들어가는 가두기는 GigaChad 전용, 지키기 중 가두기(hold.Trap)도 셈"),
        new("tactic", "지키기 = 문 지키기", A("door.start.hold."), A("door.holdReject."), A(), "[DoorTactic]", "holdReject는 매 판단마다 세서 큰 게 정상"),
        new("tactic", "문 옆 매복(Rat)", A("door.start.Rat.", "door.start.hold.Ambush"), A(), A(), "[DoorTactic]", "Rat 성격이 문 8m·적 12m 안에 있어야 나옴 — Rat이 적거나 일찍 죽으면 0이 흔함. TEST 프리셋(교차 검증 없이)은 PMC 전원을 GigaChad로 취급해 0이 정상", ZeroOk: true, ZeroOnlyInTestPreset: true),
        new("tactic", "분대 역할(오버워치/후방)", A("door.role."), A(), A(), "[DoorTactic]"),
        new("tactic", "제3자 감지(문 전술 중단)", A("door.abort.otherEnemyActive", "door.end.Clear.otherEnemyActive", "door.end.Trap.otherEnemyActive", "door.end.Peek.otherEnemyActive"), A(), A(), "[DoorTactic]"),
        new("tactic", "다이아몬드 스텝", A("diamond.start", "diamond.tap"), A("diamond.blocked"), A(), "[Diamond]"),
        new("tactic", "근거리 가림 유지", A("decision.closeOcclusionKeep", "decision.midOcclusionKeep"), A(), A(), "[Decide]"),
        new("tactic", "코너 흔들기 피킹", A("diamond.jiggle"), A(), A(), "[Diamond]"),
        new("tactic", "기울이기(사격 중)", A("lean.hold.", "lean.rock"), A(), A(), "[Diamond]"),
        new("ai", "안 보이는 적 판단(유틸리티)", A("utility.do."), A("utility.noneRunnable"), A(), "[Utility]"),
        new("ai", "보이는 적 판단(유틸리티)", A("utilityV.do."), A(), A(), "[UtilityV]"),
        new("ai", "재장전 타이밍(유틸리티)", A("reload.pick."), A("reload.cantReload"), A(), "[Reload]"),
        new("ai", "치료 타이밍(유틸리티)", A("heal.pick."), A("heal.cancel."), A(), "[Heal]"),
        new("ai", "장비 기반 교전 방식", A(), A(), A(), "[Utility]", "별도 카운터 없음 — 유틸리티 점수 안에 포함"),
        new("ai", "두려움", A(), A(), A(), "[Utility]", "별도 카운터 없음 — 유틸리티 점수 안에 포함"),
        new("ai", "결과로 배우기", A(), A(), A(), "[Learn]", "사람 플레이어 상대 기록이라 봇끼리 시뮬에선 측정 안 됨"),
        new("ai", "레이드 일지", A(), A(), A(), "-", "일지 파일이 생겼는지로 확인"),
        new("ai", "사람 같은 실수", A("utility.mistake."), A(), A(), "[Utility] (mistake)", "TEST 프리셋은 꺼 둠 → 0이 정상. zzap 교차 검증 판에선 나와야 함", ZeroOk: true, ZeroOnlyInTestPreset: true),
        new("close", "근접 교전 우선", A("close.fightInsteadOfCover"), A(), A(), "[Decide]", "보이는 적 유틸리티가 켜져 있으면(기본) 유틸리티 점수가 같은 판단을 대신함 → 0이 정상", ZeroOk: true),
        new("close", "개활지 질주 대신 교전", A("close.exposedCommit."), A(), A(), "[Decide]", "보이는 적 유틸리티가 켜져 있으면(기본) 유틸리티 점수가 같은 판단을 대신함 → 0이 정상", ZeroOk: true),
        new("close", "코너 추격", A("chase.style."), A("chase.rollFailed"), A(), "[Chase]"),
        new("close", "후퇴 고개 숙이기 + 지그재그", A("retreat.headDown", "retreat.weave"), A("retreat.weaveBlocked"), A(), "[HeadDown]"),
        new("close", "빛 관리", A("light.dark."), A(), A(), "[Decide]", "어두운 맵/야간에서 주로"),
        new("close", "코너 선조준", A("corner.preAim"), A(), A(), "[PreAim]"),
        new("close", "위협 기준 목표 선택", A("threat.switch."), A(), A(), "[Threat]", "threat.keep은 유지 횟수"),
        new("close", "수류탄 판단", A("nade.thrown."), A("nade.blocked.", "nade.noArc", "nade.notReady"), A(), "[Nade]"),
        new("close", "자기 수류탄 계산", A("ownGrenade.tracked", "diamond.avoidOwnNade", "chase.waitOwnNade"), A(), A(), "[Nade]"),
        new("close", "권총 전환", A("weapon.pistolSwap"), A("weapon.pistolSwap.refused", "weapon.pistolSwap.noPistolOrAmmo"), A(), "[Weapon]"),
        new("close", "빠른 장전", A("weapon.quickReload"), A(), A(), "[Weapon]"),
        new("close", "장거리 무기 전환", A("weapon.longRange"), A(), A(), "[Weapon]", "2번 슬롯 DMR/저격 + 80m 넘는 적 — 시뮬 싸움 구역은 대부분 그보다 좁음", ZeroOk: true),
        new("close", "등 안 돌리기", A("close.walkFacingEnemy", "close.shortDashToCover"), A(), A(), "[Decide]"),
        new("close", "제압사격 절제", A("suppress.burstPause"), A(), A(), "[Suppress]"),
        new("repo", "재배치(Relocate)", A("repo.start.Relocate"), A("repo.noPoint.Relocate"), A(), "[Reposition]", "실외 전용 — 공장처럼 지붕 아래는 제외"),
        new("repo", "미끼 피킹", A("repo.start.BaitPeek", "repo.bait."), A("repo.noPoint.BaitPeek", "repo.rollFailed.BaitPeek"), A(), "[Reposition]", "Chad·GigaChad·SnappingTurtle·Wreckless만 (다른 성격은 repo.baitSkipped.personality)"),
        new("repo", "가짜 재장전", A("repo.start.FakeReload"), A("repo.fakeReload.refused"), A(), "[Reposition]"),
        new("repo", "수류탄 신관 선택", A("nade.fuse."), A(), A(), "[Nade]"),
        new("squad", "분대 교전(교차·엄호·트레이드)", A("squad.start."), A("squad.crossfire.noPoint"), A(), "[SquadCombat]"),
        new("squad", "분대 협동(제압+측면)", A("squad.maneuver.start"), A("squad.maneuver.noFlanker", "squad.maneuver.noSuppressor", "squad.maneuver.noFlankPoint."), A("squad.maneuver.end.flanker"), "[Maneuver] / [SquadCombat]", "flankArrived = 측면조 도착, holdFireFlanker = 측면조가 사선 근처라 사격 멈춤. 나쁜 신호 = 측면조가 도착 전에 끝남(맞음·길 없음 등)"),
        new("squad", "약한 적 일제 돌격", A("squad.storm.start"), A("squad.storm.notWeak"), A(), "[Storm]"),
        new("squad", "전투 후 정리", A("postCombat.start"), A(), A(), "[PostCombat]"),
        new("squad", "아군 사선 회피", A("squad.fireLane."), A(), A("death.teamKill."), "[FireLane]", "나쁜 신호 = 아군 사격 사망"),
        new("squad", "정지 및 매복(Freeze)", A("freeze.start.", "freeze.end."), A("freeze.broken."), A("death.Freeze"), "[Freeze]", "start = 옛 판단 체인, end = 유틸리티 Freeze 포함 전부"),
        new("squad", "폭발음 반응", A("blast.newEnemy"), A("blast.addToGroupFailed"), A(), "[BlastHeard] (LogOutput)", "25m 안에서 던진 사람을 모르던 봇이 있을 때만. blast.notHostile(같은 역할끼리 등 적이 아닌 경우)은 대상 아님", ZeroOk: true),
        new("squad", "플레이어 스타일 대응", A("adapt."), A(), A(), "[Adapt]", "시뮬은 사람 상대가 아니라 0이 정상", ZeroOk: true),
        new("zzap", "벽 보고 서 있지 않기", A("look.wallFix.corner", "look.wallFix.open"), A("look.wallFix.none"), A("look.wallFix.flip"), "-", "flip = 고른 방향이 반대로 뒤집힘(총구 까딱임), 0에 가까워야"),
        new("zzap", "봇끼리 발소리 듣기", A("hear.firstSeen.heard.move"), A(), A(), "-", "notHeard.close가 적어야"),
        new("zzap", "ORBIT 인계", A("handoff.toOrbit", "handoff.orbitToCombat"), A(), A("handoff.orbitFlipFlop"), "[Handoff]"),
        // README "버그 수정"
        new("fix", "문 낑김 / 아무도 못 여는 문", A("door.interactionRepaired."), A(), A(), "[DoorTactic]", "복구가 돌았다는 뜻 — 0이면 문제가 안 생긴 것", ZeroOk: true),
        new("fix", "주운 총 장전·무기 교체", A("weapon.infoSync.replaced", "weapon.infoSync.added", "weapon.infoSync.current"), A(), A("weapon.infoSync.threw"), "[Weapon]", "added는 안 든 슬롯 표 추가라 흔함, replaced = 주운 총 반영"),
        new("fix", "무기 계속 바꾸기(핑퐁)", A("weapon.toggle.keptLoadedGun", "weapon.keptOffEmptyMain", "weapon.pistolBackHeld", "weapon.pistolBackByEft", "weapon.keptLongRange.reload", "weapon.keptLongRange.otherEnemy", "weapon.pistolAwayByEft.holdOff", "reload.noAmmo.waitReloadInHand"), A(), A("weapon.pingPong"), "[Weapon]", "발동 = 막은 횟수(0이면 막을 일이 없었음). 나쁜 신호 pingPong이 0이어야 정상", ZeroOk: true),
        new("fix", "빠른 재장전 탄창 소실", A("weapon.quickReload.keptLastSpare"), A(), A(), "[Weapon]"),
        new("fix", "벽에 계속 박기(끼임 해제)", A("stuck.freed."), A("stuck.gaveUp."), A(), "[Stuck]", "stuck.stage만 있고 freed가 적으면 못 풀린 것. 거부 칸 = 이동을 포기해서 끝난 막힘(gaveUp)"),
        new("fix", "죽은 채로 걷는 시체", A("deadBug.bodyOk"), A(), A("deadBug.bodyNotRagdoll", "deadBug.onDeadThrew", "deadBug.handlerThrew", "deadBug.unprocessedDeathRescued"), "[DeadBug]"),
        new("fix", "시체 피·총알 자국 텍스처", A("perf.corpseDecalsFreed"), A(), A(), "[Perf]"),
        new("fix", "전술장비 딸깍 반복", A("light.turnOnFailedBackoff"), A(), A(), "-"),
        new("fix", "치료하며 피킹", A("lean.stoppedForMeds"), A(), A(), "-"),

        // remake5 (2026-10-11)
        new("remake5", "팩션 분대 섞기(로그·레이더)", A("sim.faction.called."), A("sim.faction.callFailed"), A(), "[SimLab] faction squad", "시뮬 설정 '팩션 분대 섞기'가 꺼져 있으면 0"),
        new("remake5", "판마다 보스 무작위", A("sim.boss.seen"), A(), A("sim.boss.notSeen", "sim.boss.waveMissing", "sim.boss.callFailed"), "[SimLab] boss"),
        new("remake5", "연막 오투척 방지", A("nade.skip.vanillaSmoke"), A(), A(), "[Nade]", "연막만 든 봇이 있을 때만 — APBS PMC는 2~3%라 0이 흔함", ZeroOk: true),
        new("remake5", "층 차이 소리 감쇠", A("sound.floor.applied"), A("sound.floor.cut"), A(), "-", "거부 칸 = 층 때문에 못 들은 횟수"),
        new("remake5", "문 전술 제3자: 다른 층 제외", A("door.thirdParty.otherFloorIgnored"), A(), A(), "[DoorTactic]", "여러 층 맵(연구소 등)에서만", ZeroOk: true),
        new("remake5", "먼 총소리 3단계(고개만/무시)", A("sound.gun.mid", "sound.gun.far", "sound.gun.glance"), A(), A(), "-", "싸움 구역이 좁으면 100m 넘는 총소리가 드묾", ZeroOk: true),
        new("remake5", "나를 쏜 원거리 적은 거리 무시하고 반응", A("sound.gun.shotAtMe"), A(), A(), "-", "100m 넘는 곳에서 쏜 경우만", ZeroOk: true),
        new("remake5", "전투 후 정리가 총소리를 들음", A("postCombat.alert.gunfire"), A(), A(), "[PostCombat]"),
        new("remake5", "저사양: 봇끼리 미끼 안 낚임", A("sound.bait.ignoredBot"), A(), A(), "-", "저사양 프리셋에서만 켜짐", ZeroOk: true),
        new("remake5", "취약 순간 돌격(소리 종류별 남은 시간)", A("vuln.rush."), A("vuln.reach."), A(), "[Vuln]", "거부 칸에 yes도 섞여 있음 — md '취약 순간 반응' 절 참고"),
        new("remake5", "수류탄 꺼낸 적에게 안 달려듦", A("vuln.grenade.backOff"), A(), A(), "[Vuln]", "", ZeroOk: true),
        new("remake5", "발소리 관리(30m 걷기·12m 숙여 걷기)", A("quiet.start"), A("quiet.skip.", "quiet.timeout"), A(), "[Quiet]"),
        // zzap bot sprint ram (2026-10-13): SAIN's own (BotDoorBreacher, ported from doordash + DualSideDoorBreach), no mod needed.
        // The sim preset "문 전술·박치기 점검" (door check switch) forces the rams (100%). Door Overhaul support was dropped.
        new("doormod", "봇 박치기 문 부숨 (SAIN 자체)", A("doorram.breach", "doorram.breach.flipped", "doorram.sideFixed"), A("doorram.skip.someoneBehind"), A("doorram.breachError"), "[DoorMod]", "breach=봇 반대쪽으로 열리는 문, breach.flipped=봇 쪽 문을 반대로 돌려 엶(DualSide 방식), sideFixed=옛 판정(DualSide와 같은 식)이면 반대로 열렸을 문(10/14 수정), 거부=문짝 자리에 사람 있음. breachError가 있으면 버그", ZeroOk: true),
        new("doormod", "몸통박치기 방 진입", A("doormod.ram.burst"), A("doormod.ram.noRunUp.", "doormod.ram.fallback."), A(), "[DoorTactic] door mod", "문 전술(방 진입)이 드물면 0일 수 있음. fallback.noBurst가 많으면 봇이 문 앞에서 F6 박치기 속도(기본 2.8m/s)보다 느린 것", ZeroOk: true),
        new("doormod", "달리다가 문 몸통박치기 (도망·돌격·수색·그 밖의 전투 이동)", A("doormod.move.burst."), A("doormod.move.fallback.", "doormod.move.skip."), A(), "[DoorRam]", "move.start.<상황>=시작, burst=문 박차 열림, fallback.noBurst=문 앞 속도 부족", ZeroOk: true),
    };

    /// <summary>Counters under <paramref name="prefixes"/> that are not under any of <paramref name="except"/>.</summary>
    public static int Sum(SimBeat b, string[] prefixes, string[] except)
    {
        int n = 0;
        if (b.Counters == null)
        {
            return 0;
        }
        foreach (var kv in b.Counters)
        {
            if (Array.Exists(except, e => kv.Key.StartsWith(e, StringComparison.Ordinal)))
            {
                continue;
            }
            if (Array.Exists(prefixes, p => kv.Key.StartsWith(p, StringComparison.Ordinal)))
            {
                n += kv.Value;
            }
        }
        return n;
    }

    public static (int Fired, int Refused, int Bad) Measure(Feature f, SimBeat b)
    {
        var except = f.Refused.Concat(f.Bad).ToArray();
        return (Sum(b, f.Fired, except), Sum(b, f.Refused, Array.Empty<string>()), Sum(b, f.Bad, Array.Empty<string>()));
    }

    public static int Sum(SimBeat b, string[] prefixes)
    {
        int n = 0;
        if (b.Counters == null)
        {
            return 0;
        }
        foreach (var kv in b.Counters)
        {
            foreach (string p in prefixes)
            {
                if (kv.Key.StartsWith(p, StringComparison.Ordinal))
                {
                    n += kv.Value;
                    break;
                }
            }
        }
        return n;
    }
}
