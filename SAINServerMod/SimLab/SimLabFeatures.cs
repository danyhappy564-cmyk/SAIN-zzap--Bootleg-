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
    public sealed record Feature(string Group, string Name, string[] Fired, string[] Refused, string[] Bad, string Journal, string Note = "");

    private static string[] A(params string[] keys) => keys;

    public static readonly Feature[] All =
    {
        // README "추가한 기능"
        new("tactic", "점프 피킹", A("door.jumpOut", "door.jumpBack"), A("door.jumpUnsafe", "door.jumpFrameInTheWay", "door.jumpLeafTooClose"), A(), "[DoorTactic]"),
        new("tactic", "런바이 피킹", A("door.runBy.start"), A("door.runBy.noPoint", "door.runBy.noAnglePoint", "door.runBy.abortHeard"), A(), "[DoorTactic]"),
        new("tactic", "스텝 피킹", A("door.stepPeek"), A(), A(), "[DoorTactic]"),
        new("tactic", "페이크 수류탄", A("fakeNade.drawn"), A("fakeNade.putAwayRefused", "trick.skipped", "trick.cancelledOnSound"), A(), "[DoorTactic]"),
        new("tactic", "페이크 치료·스팀", A("fakeStim.started"), A("fakeStim.cancelled"), A(), "[DoorTactic]"),
        new("tactic", "방 진입(Clear)", A("door.pick.Clear", "door.breach."), A("door.planFailed"), A(), "[DoorTactic]"),
        new("tactic", "방 가두기(Trap)", A("door.pick.Trap"), A(), A(), "[DoorTactic]"),
        new("tactic", "지키기 = 문 지키기", A("door.start.hold."), A("door.holdReject."), A(), "[DoorTactic]", "holdReject는 매 판단마다 세서 큰 게 정상"),
        new("tactic", "문 옆 매복(Rat)", A("door.start.Rat."), A(), A(), "[DoorTactic]", "Rat 성격이 있어야 나옴"),
        new("tactic", "분대 역할(오버워치/후방)", A("door.role."), A(), A(), "[DoorTactic]"),
        new("tactic", "제3자 감지(문 전술 중단)", A("door.abort.otherEnemyActive", "door.end.Clear.otherEnemyActive", "door.end.Trap.otherEnemyActive", "door.end.Peek.otherEnemyActive"), A(), A(), "[DoorTactic]"),
        new("tactic", "다이아몬드 스텝", A("diamond.start", "diamond.tap"), A("diamond.blocked"), A(), "[Diamond]"),
        new("tactic", "근거리 가림 유지", A("decision.closeOcclusionKeep"), A(), A(), "[Decide]"),
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
        new("ai", "사람 같은 실수", A("utility.mistake."), A(), A(), "[Utility] (mistake)", "TEST 프리셋은 꺼 둠 → 0이 정상"),
        new("close", "근접 교전 우선", A("close.fightInsteadOfCover"), A(), A(), "[Decide]"),
        new("close", "개활지 질주 대신 교전", A("close.exposedCommit."), A(), A(), "[Decide]"),
        new("close", "코너 추격", A("chase.style."), A("chase.rollFailed"), A(), "[Chase]"),
        new("close", "후퇴 고개 숙이기 + 지그재그", A("retreat.headDown", "retreat.weave"), A("retreat.weaveBlocked"), A(), "[HeadDown]"),
        new("close", "빛 관리", A("light.dark."), A(), A(), "[Decide]", "어두운 맵/야간에서 주로"),
        new("close", "코너 선조준", A("corner.preAim"), A(), A(), "[PreAim]"),
        new("close", "위협 기준 목표 선택", A("threat.switch."), A(), A(), "[Threat]", "threat.keep은 유지 횟수"),
        new("close", "수류탄 판단", A("nade.thrown."), A("nade.blocked.", "nade.noArc", "nade.notReady"), A(), "[Nade]"),
        new("close", "자기 수류탄 계산", A("ownGrenade.tracked", "diamond.avoidOwnNade", "chase.waitOwnNade"), A(), A(), "[Nade]"),
        new("close", "권총 전환", A("weapon.pistolSwap"), A("weapon.pistolSwap.refused", "weapon.pistolSwap.noPistolOrAmmo"), A(), "[Weapon]"),
        new("close", "빠른 장전", A("weapon.quickReload"), A(), A(), "[Weapon]"),
        new("close", "장거리 무기 전환", A("weapon.longRange"), A(), A(), "[Weapon]", "2번 슬롯 DMR/저격 + 80m 넘는 적"),
        new("close", "등 안 돌리기", A("close.walkFacingEnemy", "close.shortDashToCover"), A(), A(), "[Decide]"),
        new("close", "제압사격 절제", A("suppress.burstPause"), A(), A(), "[Suppress]"),
        new("repo", "재배치(Relocate)", A("repo.start.Relocate"), A("repo.noPoint.Relocate"), A(), "[Reposition]", "실외 전용 — 공장처럼 지붕 아래는 제외"),
        new("repo", "미끼 피킹", A("repo.start.BaitPeek", "repo.bait."), A("repo.noPoint.BaitPeek"), A(), "[Reposition]"),
        new("repo", "가짜 재장전", A("repo.start.FakeReload"), A("repo.fakeReload.refused"), A(), "[Reposition]"),
        new("repo", "수류탄 신관 선택", A("nade.fuse."), A(), A(), "[Nade]"),
        new("squad", "분대 교전(교차·엄호·트레이드)", A("squad.start."), A("squad.crossfire.noPoint"), A(), "[SquadCombat]"),
        new("squad", "약한 적 일제 돌격", A("squad.storm.start"), A("squad.storm.notWeak"), A(), "[Storm]"),
        new("squad", "전투 후 정리", A("postCombat.start"), A(), A(), "[PostCombat]"),
        new("squad", "아군 사선 회피", A("squad.fireLane."), A(), A("death.teamKill."), "[FireLane]", "나쁜 신호 = 아군 사격 사망"),
        new("squad", "정지 매복(Freeze)", A("freeze.start."), A("freeze.broken."), A("death.Freeze"), "[Freeze]"),
        new("squad", "폭발음 반응", A("repo.moveOnBlast."), A(), A(), "[Reposition]"),
        new("squad", "플레이어 스타일 대응", A("adapt."), A(), A(), "[Adapt]", "시뮬은 사람 상대가 아니라 0이 정상"),
        new("zzap", "벽 보고 서 있지 않기", A("look.wallFix.corner", "look.wallFix.open"), A("look.wallFix.none"), A("look.wallFix.flip"), "-", "flip = 고른 방향이 반대로 뒤집힘(총구 까딱임), 0에 가까워야"),
        new("zzap", "봇끼리 발소리 듣기", A("hear.firstSeen.heard.move"), A(), A(), "-", "notHeard.close가 적어야"),
        new("zzap", "ORBIT 인계", A("handoff.toOrbit", "handoff.orbitToCombat"), A(), A("handoff.orbitFlipFlop"), "[Handoff]"),
        // README "버그 수정"
        new("fix", "문 낑김 / 아무도 못 여는 문", A("door.interactionRepaired."), A(), A(), "[DoorTactic]", "복구가 돌았다는 뜻 — 0이면 문제가 안 생긴 것"),
        new("fix", "무기 계속 바꾸기(핑퐁)", A("weapon.toggle.keptLoadedGun"), A(), A("weapon.pingPong"), "[Weapon]"),
        new("fix", "빠른 재장전 탄창 소실", A("weapon.quickReload.keptLastSpare"), A(), A(), "[Weapon]"),
        new("fix", "벽에 계속 박기(끼임 해제)", A("stuck.freed."), A(), A(), "[Stuck]", "stuck.stage만 있고 freed가 적으면 못 풀린 것"),
        new("fix", "죽은 채로 걷는 시체", A("deadBug.bodyOk"), A(), A("deadBug.bodyNotRagdoll", "deadBug.onDeadThrew", "deadBug.handlerThrew", "deadBug.unprocessedDeathRescued"), "[DeadBug]"),
        new("fix", "시체 피·총알 자국 텍스처", A("perf.corpseDecalsFreed"), A(), A(), "[Perf]"),
        new("fix", "전술장비 딸깍 반복", A("light.turnOnFailedBackoff"), A(), A(), "-"),
        new("fix", "치료하며 피킹", A("lean.stoppedForMeds"), A(), A(), "-"),
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
