# SAIN-zzap--Bootleg- Remix6.1

**태그:** `Remix6.1` (master — 코드 기준 커밋 `84ce562f`, 태그는 이 노트가 들어간 master 최신 커밋에)

**원작:**

1. **Solarint** — SAIN (Solarint's AI Modifications)
   - Repository: [Solarint/SAIN](https://github.com/Solarint/SAIN)
   - License: 원본 저장소 참고
2. **ArchangelWTF** — SAIN 4.x 유지보수판 (이 포크의 기준 v4.5.1, SPT 4.1)
   - Repository: [ArchangelWTF/SAIN](https://github.com/ArchangelWTF/SAIN)
   - License: 원본 저장소 참고
3. **bmpq (tarkin)** — doordash (봇 몸통박치기 코드를 봇 전용으로 이식)
   - Repository: [bmpq/spt-doordash](https://github.com/bmpq/spt-doordash)
   - License: 라이선스 파일 없음
4. **maschine34675** — DualSideDoorBreach 2.0.2 (문 양쪽 박치기 방식 이식)
   - Repository: [maschine34675/DualSideDoorBreach](https://github.com/maschine34675/DualSideDoorBreach)
   - License: MIT, Copyright (c) 2026 maschine34675

**이전 릴리즈:** remake6 (GitHub 릴리즈 `remake6`, 10/18 게시, 그 뒤 커밋 26개) → **이번: Remix6.1** (봇 행동 버그 수정·조정)

> remake6은 설정 화면을 새로 만든 판이었고, 이번 판은 **봇이 싸우는 방식의 버그 수정과 조정** 판입니다.
> 게임 안 시뮬레이션(봇끼리 30분 판) 일지를 보고 고친 것과, 새로 만든 오프라인 시뮬레이터(게임 없이 맵 데이터로 봇 코드를 수백 판 돌려 보는 도구)로 찾은 원래 SAIN 버그를 고친 것이 섞여 있습니다.
> 항목마다 확인 상태를 적었습니다. **마지막에 고친 4가지(무기 핑퐁·치료 점수·수술·시뮬 시체 정리)는 아직 게임에서 확인하지 못했습니다.**
> SAIN 내부 버전 숫자는 프리셋 호환 때문에 원본 기준 4.5.1 그대로이고, F6 왼쪽 위와 로그 첫 줄에 릴리즈 이름(Remix6.1)이 나옵니다.

---

**<설치 / 업데이트>**

- **remake6에서 올라오는 경우:** 클라이언트 `SAIN.dll`과 **서버 모드 둘 다** 덮어써 주세요.
  - 서버 모드 `Presets/`의 zzap 프리셋 5개도 바뀌었습니다(Wreckless 코너 점프 100% → 90%). 같이 덮어써 주세요.
  - 웹이나 F6에서 zzap 프리셋을 고쳐 저장했다면 그건 `[Modified]` 복사본이라 덮어써도 남습니다.
- **Remix5.1 이하에서 올라오는 경우:** remake6 노트의 설치 순서대로 한 뒤 위처럼 덮어써 주세요.

---

**<변경점>**

**1. 레이저·손전등이 켜진 채 남던 것**
- 전투가 끝나도 총의 레이저가 계속 켜져 있거나, 권총집에 넣은 권총의 레이저가 켜진 채 다니는 봇이 있었습니다.
- 원인: 게임의 봇 조명 코드는 총에 달린 장비 중 **첫 번째 하나만** 켜고 껐습니다. 레이저와 손전등이 같이 달리면 하나는 그대로 남았습니다. 손에 들지 않은 총의 장비는 아예 건드리지 않았습니다.
- 고친 것: 총에 달린 장비를 전부 한 번에 켜고 끕니다. 손에 없는 총(권총집·등)의 장비는 5초마다 끕니다.
- 확인: 1차 시뮬에는 조명이 없어 측정 불가. 게임 2차 시뮬에서 동작 횟수는 기록됨, 눈으로는 아직 확인 못 함.

**2. 제자리에서 위로만 뛰는 점프**
- 코너에서 점프 샷, 점프 피킹, 돌격 중 버니합을 할 때 옆으로 튀어나가지 않고 제자리에서 위로만 뛰는 봇이 있었습니다.
- 원인: 게임 점프는 뛰는 순간의 속도를 그대로 이어 가는데, 봇이 움직이기 시작하기 전에(속도 0) 점프를 눌렀습니다.
- 고친 것: 점프 피킹·코너 점프 샷·문 피킹·미끼 점프는 이미 그쪽으로 움직이는 중일 때만, 돌격 점프(버니합 포함)는 2m/s 이상으로 달리는 중에만 뜁니다.
- 확인: 1차 시뮬 제자리 점프 5건 → 0건, **게임 2차 시뮬 30분 0건**.

**3. 전투 뒤 멍하니 서 있는 봇 줄이기**
- 싸움이 조용해진 뒤 한참 서 있거나, 적의 마지막 위치를 수색하고도 그 자리에 계속 서 있는 봇이 있었습니다.
- 고친 것:
  - 소리로만 들은 적도 15초 넘게 조용하면 걸어서 조심스럽게 찾으러 갑니다(Coward는 원래대로 수색하지 않음). 사용자 결정.
  - 이미 수색한 마지막 위치의 적은 20초 소식이 없으면 잊고, 평소 흐름(정리·이동)으로 넘어갑니다. 다시 보이거나 들리면 다시 압니다.
  - 엄폐로 물러난 뒤 15초 넘게 소식이 없으면 '물러나기' 점수가 점점 약해집니다.
- 확인: 게임 2차 시뮬 30분에서 '전투 뒤 오래 제자리' 13건이 남아 있습니다(그중 하나가 6번 다리 파괴 봇). 줄었는지는 아직 판단 못 함, 계속 보는 중.

**4. 매복(지키기)이 끝없이 이어지던 것**
- 안 보이는 적을 기다리며 매복하는 시간이 몇 분씩 이어지는 경우가 있었습니다.
- 원인(원래 SAIN): 매복 '피로' 시계가 봇이 실제로 하는 행동이 아니라 1순위 선택지를 따라가서, 1순위가 실행 불가로 매복에 떨어지면 시계가 매번 0으로 돌아갔습니다. 목표 적이 바뀌어도 0으로 돌아갔습니다.
- 고친 것: 봇이 실제로 매복 중이면 시계가 계속 흐릅니다. 새 소식 없는 같은 적에게는 40초 상한(그 적이 다시 보이거나 들리면 풀림). 사용자 결정.
- 확인: 1차 시뮬 45초 넘는 매복 500판 39건 → 1건, **게임 2차 시뮬 30분 0건**.

**5. 적이 근처에 있는데 치료하던 것**
- 발소리가 들리고 적이 있는 걸 아는데 치료하거나, 적 쪽으로 걸어가며 치료하는 봇이 있었습니다.
- 고친 것: 가깝고 최근인 적이 있거나 15m 안에서 발소리가 들리면 치료를 미루고, 치료를 시작한 뒤 발소리가 들리면 취소합니다(그 뒤 4초는 다시 안 함). 적 쪽으로 걷는 중·돌격 30초 40m 안에서는 치료하지 않습니다.
- 약을 든 채 죽은 봇 옆에 구급상자가 공중에 떠 있던 것: 죽을 때 손을 정리합니다(게임 코드 분석 기준, 눈으로는 미확인).
- 확인: 게임 2차 시뮬에서 치료 취소가 실제로 일어남(발소리 25회·위협 212회). 체감은 미확인.

**6. 다리가 파괴된 봇이 엄폐에 서 있다 죽던 것 (아직 게임 미확인)**
- 두 다리가 파괴된 빈사 봇이 약을 13개나 갖고도 70초 동안 엄폐에 서 있다가 죽었습니다.
- 원인: 게임 구급상자는 파괴된 부위를 건너뛰고(수술 키트만 고침), 싸움 중 수술은 '아는 적 전부 60초 조용 + 80m 밖'일 때만 허락돼서 공장 같은 맵에선 30분 동안 0번이었습니다. 그런데도 '엄폐로 가서 치료하라' 점수는 계속 붙었습니다. 게다가 시작한 수술이 다음 순간 바로 취소되는 원래 버그가 있었습니다.
- 고친 것:
  - 고칠 수 있는 게 있을 때만 '치료하러 엄폐' 점수를 줍니다.
  - 싸움 중 수술 기준을 하나 더 둡니다: 엄폐 안, 보이는 적 없음, 아는 적이 성격별 시간 동안 조용하고 충분히 멀면 성격별 확률로 수술(사용자 결정).

    | 성격 | 확률 | 조용한 시간 | 거리 |
    |---|---|---|---|
    | Rat | 80% | 20초 | 30m |
    | Coward | 80% | 20초 | 35m |
    | SnappingTurtle | 70% | 20초 | 30m |
    | Timmy | 60% | 25초 | 40m |
    | Normal | 50% | 25초 | 40m |
    | Chad | 35% | 30초 | 45m |
    | GigaChad | 25% | 30초 | 50m |
    | Wreckless | 15% | 35초 | 50m |

    적이 보이거나, 맞거나, 적이 그 거리의 절반 안으로 오면 수술을 멈춥니다.
  - 시작한 수술이 바로 취소되던 버그를 고쳤습니다.
- 확인: 빌드만 확인, 게임 미검증.

**7. 무기를 계속 바꾸는 핑퐁 (아직 게임 미확인)**
- 탄창이 빈 봇이 권총을 꺼냈다가 2초 뒤 다시 주무기로 바꾸는 일이 있었습니다.
- 원인: 재장전을 바로 시작하지 못하면, 적이 안 보여서 점수 0인 '권총으로 바꾸기'까지 넘어가 권총을 꺼냈습니다.
- 고친 것: 권총은 적이 보이고 30m 안일 때(점수가 있을 때)만 꺼냅니다. 나머지는 들고 있는 총을 재장전합니다.
- 확인: 빌드만 확인, 게임 미검증.

**8. 원래 SAIN 버그 (오프라인 시뮬레이터 + 코드 리뷰로 찾음)**
- 성격의 '버니합 가능' 설정이 무시되던 것 — SnappingTurtle처럼 꺼진 성격도 버니합을 했습니다.
- 성격 배정 확률이 설정한 최대치를 넘어 100%까지 가던 것, 꺼 둔 성격이 대체 성격으로 뽑히던 것.
- 재장전 판단이 아직 달려가는 중인 엄폐를 '엄폐 안'으로 보던 것.
- 사선이 막혔을 때 방금 맞은 것을 다시 판단하지 않고 이전 선택을 그대로 쓰던 것.
- 플레이어 대응 코너 지키기가 맞는 중에도 늦게 풀리던 것.
- 확인: 1차 시뮬로 확인(버니합 0회 등). 게임 체감은 미확인.

**9. 성격 스타일 조정 (사용자 결정)**
- Coward는 돌격 검사가 거절하면 다른 길로 돌격하지 않습니다(1차 시뮬 Coward 돌격 3% → 0%).
- 늘 100%이던 굴림을 90%로: Rat·SnappingTurtle·Coward의 조용한 접근, Wreckless의 돌격 코너 점프(번들 프리셋). '스타일은 확률' 원칙.
- 코너 점프 샷·점프 피킹도 적마다 공중 사격 굴림을 따릅니다(전에는 공중에서 늘 사격 참음).

**10. 이동·문**
- 엄폐로 걸어갈 때 실제로 못 뛰는 봇은 가는 방향을 보고, 마지막 3m만 적 쪽을 봅니다(뒷걸음 줄이기).
- 약을 손에 든 동안 다이아몬드 스텝·기울이기를 하지 않습니다.
- 끼임 탈출이 길을 막은 사람 위로 넘거나 뛰지 않습니다.
- 달리다 문 몸통박치기는 가려는 곳이 문 너머일 때만 합니다.
- 봇 박치기가 봇에게 피해를 주지 않습니다(사용자 결정). F6의 '팔 피해' 설정은 없어졌습니다.

**11. 시뮬레이션 랩**
- 웹 '시뮬 정지'가 진행 중인 시뮬 레이드를 바로 끝내고, 정지 상태에서 직접 시작한 레이드는 일반 레이드가 됩니다. 시작 전 카운트다운도 3초마다 정지를 확인합니다.
- 웹에서 zzap 기본 프리셋을 저장해 생긴 `[Modified]` 복사본은 그 프리셋을 쓰던 게임이 자동으로 따라갑니다(레이드 중이면 끝난 뒤).
- F8 장면 표시는 화면 가운데 20도·50m 안의 보이는 봇만, 죽은 봇은 죽기 1~3초 전 상태와 함께 남깁니다.
- 시뮬 전용 시체 정리가 누가 뒤지는 중인 시체를 지워 ORBIT 오류가 나던 것: 3m 안에 산 사람이 있는 시체는 다음 정리로 미룹니다(게임 미검증).
- 시뮬 요약에 새 감지(제자리 점프, 맞으면서 매복, 45초 넘는 매복)와 확률 굴림 점검 표가 생겼습니다.

**12. 기타**
- 진단 카운터가 꺼져 있을 때는 자주 도는 확률 기록이 글자를 만들지 않게 정리했습니다(성능).
- 빌드: 클라이언트 오류 0(경고 3), 서버 오류 0.

**알려진 점**
- 6·7번과 시뮬 시체 정리는 아직 게임에서 확인하지 못했습니다. 이상하면 스크린샷과 `BepInEx/LogOutput.log`, 레이드 일지를 보내 주세요.
- 수술 확률·거리는 처음 정한 값입니다. 너무 자주 또는 너무 드물게 하면 알려 주세요.
- 교전 후 ORBIT으로 넘어가기 전 15초 빈틈은 일부러 그대로 두었습니다.

------------------------------------------------------------------------------------------------------------------------------------------------------

**<Changes (English)>**

**Previous release:** remake6 (`remake6`, published 10/18, 26 commits since) → **this: Remix6.1** (bot behaviour fixes and tuning). The last four fixes (weapon ping-pong, heal score, surgery, sim corpse cleanup) are not checked in game yet.

**Install / update:** overwrite both the client `SAIN.dll` and the server mod, including the 5 bundled zzap presets in `Presets/` (Wreckless corner jumps 100 % → 90 %). Your own `[Modified]` copies are kept.

**1. Lasers / flashlights left on** — EFT's bot light code only toggled the first device on a gun; now every device goes off/on together, and guns not in hands (holster, back) get their devices switched off every 5 s. Not checked visually in game.

**2. Jumping straight up on the spot** — jump peeks, corner jump shots, door peeks and bait hops only jump once the bot already moves that way; rush jumps and bunny hops only from a 2 m/s+ run. 1st stage 5 → 0, in-game sim 0 in 30 min.

**3. Standing still after a fight** — a heard-only enemy is searched (walking) after 15 s of quiet (Coward still never searches); an enemy whose last known spot was already searched is forgotten after 20 s without news; fall-back fades after 15 s of quiet.

**4. Endless ambushes** — the hold fatigue clock now follows the freeze the bot is actually in (it reset whenever the top pick fell through to Hold or the target changed), plus a 40 s cap against one enemy with no news. 1st stage 39 → 1 per 500 raids, in-game sim 0.

**5. Healing with enemies close** — heals wait for close recent enemies and footsteps within 15 m, a started first aid is cancelled by footsteps, no meds on a walk toward the enemy or within 30 s / 40 m of a push. A body that died holding meds clears its hands (no floating med kit).

**6. Destroyed legs, standing in cover (not checked in game)** — EFT first aid skips destroyed parts and the old surgery rule (every enemy quiet 60 s, 80 m+) never held on Factory. No "go to cover to heal" bonus when nothing can be healed; a second per-personality surgery rule (in cover, nobody in sight, enemies quiet T s and D m+ away, then a roll: Rat 80 %/20 s/30 m, Coward 80/20/35, SnappingTurtle 70/20/30, Timmy 60/25/40, Normal 50/25/40, Chad 35/30/45, GigaChad 25/30/50, Wreckless 15/35/50), stopped if an enemy is seen, the bot is hit or one comes within half that distance. A started surgery is no longer cancelled right away.

**7. Weapon ping-pong (not checked in game)** — a failed reload no longer falls through to drawing the pistol when the pistol scored 0 (no enemy in sight within 30 m).

**8. Original SAIN bugs** — CanBunnyHop was never read; personality assignment chance went past its max and could pick a disabled fallback; reload timing counted cover still being run to; a blocked muzzle line skipped the hit re-evaluation; the player-adaptation corner hold broke late under fire.

**9. Style tuning (user decisions)** — Coward never rushes past a refused rush check; fixed 100 % rolls are now 90 %; corner jump shots and jump peeks follow the per-enemy air-shot roll.

**10. Movement / doors** — face the walk to cover when the bot can't really sprint (threat for the last 3 m); no diamond step / lean with meds in hands; unstuck never vaults over a player; move rams only when the destination lies past the door; bot rams deal no damage (F6 arm-damage setting removed).

**11. Sim lab** — web stop ends a running sim raid; `[Modified]` preset copies are followed automatically; F8 scene marks 20 deg / 50 m in sight with dead bots' last state; sim corpse cleanup skips bodies someone stands at (ORBIT loot error); new oddity counters and a dice audit in the summary.

**Known issues** — items 6, 7 and the sim corpse fix are untested in game; surgery chances are first values; the 15 s gap before ORBIT after a fight is kept on purpose.

------------------------------------------------------------------------------------------------------------------------------------------------------

**!!!!!!!!!!!PLEASE READ THIS!!!!!!!!!!!**

Hi everyone,

I'd like to share a few thoughts and important warnings regarding the process of porting mods from version 4.0 to 4.1.

Following the recent "ECOT" mod incident, I've had a lot of time to reflect on my actions.
I’m not here to lecture anyone—I’m writing this simply in the hope that you’ll keep these points in mind.


**1. I am not a professional programmer; I am mostly a "vibe coder" who relies heavily on AI.**

By that, I mean I’m not someone who spends days designing and painstakingly crafting mods by hand from scratch.
At the same time, having tinkered with simple mod edits back in the TS/JS (SPT 2.3.1) days, I know firsthand how much time, effort, and care goes into this process.
Therefore, even if a mod is covered under an MIT License, if the original author requests that I delete the source code and stop distribution, I will willingly comply.


**2. Please DO NOT re-distribute or re-upload these modified mods to 3rd/4th party places.**

Even if my ported version has some fixes or improvements, please do not treat it as a long-term solution. Unlike official releases on "THE FORGE," I do not perform thorough debugging or full security scans.
Furthermore, imagine how disruptive it would be if someone mistook my ported build for an "official release" and pestered the original author about bugs in my version. It would be terrible.
Moving forward, I intend to approach any porting work with the utmost respect for original modders.
If I had spent days pouring my heart into making a mod, only to have someone take my code and modify it however they pleased, I wouldn't feel good about it either.
Therefore, when writing README.md files on GitHub, I will always credit the original creators, and if it's not a mod I built from scratch, I will fully respect the original modder's decisions and opinions.


**3. Please respect the original creators.**

If people ignore warning 2. and act as if my port is the "real deal," I would feel immense guilt.
Please do not do that. As the saying goes, "When in Rome, do as the Romans do"—and showing proper respect and courtesy is essential.
Instead, please visit the original creators' comment sections on The Forge to leave warm words of appreciation, encouragement, or even buy them a coffee/leave a tip if you can


**Thank you for reading this long post.**
**Stay safe and stay happy!**

**^^7**

------------------------------------------------------------------------------------------------------------------------------------------------------

**!!!!!!!!!!!꼭 읽어주세요!!!!!!!!!!**

안녕하세요 여러분

4.0 -> 4.1로 포팅하는 과정에 대해 드릴 말씀과 주의사항을 전달하고자 합니다.

이번 "ECOT" 모드 사건 이후로, 많은 반성의 계기를 가지게 되었습니다

뭔가, 설교를 드리는게 아니라 참고 해주셨으면 하는 마음에 글을 작성합니다.


**1. 저는 전문 프로그래머가 아니고, AI를 주로 이용하는 바이브코더입니다.**

해당말의 뜻은, 저는 매일 구상하고 힘들게 한땀한땀 손으로 모드를 만드는 사람이 아니고

또한, TS/JS (SPT 2.3.1) 시절에 간단하게 모드 수정을 거치며 이 과정이 얼마나 노력을 들이고, 시간이 필요한지 잘 알고 있습니다.

그렇기에, 해당 모드를 수정하면서 아무리 MIT 라이선스를 받았더라도 원 모드 제작자가 소스코드 삭제 및 배포를 중단해달라고 하면 중단할 의향이있습니다.


**2. 무단으로 수정한 모드를 "3차 4차 배포하지 마십쇼"**

제가 만든 버전이 개선점이 있어도, 오래 사용하지 마세요 저는 "FORGE" 에 올린 것 처럼 바이러스 검사나 디버깅을 완벽하게 수행하지 않습니다.

또한, 제가 포팅한 버전을 "진짜 릴리즈된 버전" 이라고 생각하고, 제 포팅 모드를 들고 원작자에게 가면 얼마나 민폐일까요? 끔찍합니다

저는 모더들을 최대한 존중하는 방향으로 포팅할 계획입니다.

아마, 제가 모드를 제작할 때 하루를 갈아넣어 만들었는데 누군가가 멋대로 코드를 뺏겨 지 멋대로 작성했다면 기분을 썩 좋지않을 것 같습니다.

고로 GITHUB에 README.MD 작성 시, 원작자 표기 및 제가 실제로 제작하지 않은 모드라면 최대한 해당 모드를 제작한 모더의 의견을 존중하는 방향으로 나아갈 것입니다.

**3. 원작자를 존중해주세요**

만일 2번 주의사항을 어겼을 시, 제가 포팅한 모드가 "진짜 모드다!" 이런 느낌이면 진짜 죄책감에 휩싸일 것 같습니다.

그런 짓은 하지말아주고, 로마에 왔으면 로마법을 지키듯이 한국에서는 예의를 갖추는 것이 미덕입니다.

해당 모드 제작자의 FORGE COMMENT 창에 들어가 따뜻한 위로와 감사하다는 격려 또는 그들에게 TIP을 건네주세요


**긴 글 읽어주셔서 감사합니다.** 

**행복하세요! ^^7**
