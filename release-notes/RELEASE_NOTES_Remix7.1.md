# SAIN-zzap--Bootleg- Remix7.1

**태그:** `remix7.1` (master — 태그는 이 노트가 들어간 master 최신 커밋에)

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

**이전 릴리즈:** Remake7 (GitHub 릴리즈 `remake7`, 10/24 게시, 그 뒤 커밋 11개) → **이번: Remix7.1** (봇 길찾기·엄폐 버그 수정 + 열린 문짝 피하기)

> 봇이 **열린 문짝에 박고 서 있던 것**, **바닥 끝 밖에 서서 길을 못 찾고 몇 분씩 서 있던 것**, **분대 수색 때 분대가 통째로 서 있던 것**을 고친 판입니다.
> 설정 화면과 zzap 프리셋 값은 Remake7과 같고, 새 설정은 '봇이 열린 문짝을 돌아서 지나감'(기본 켬) 하나입니다.
> 2번(바닥 끝)은 게임 4맵 시뮬로 전/후를 확인했고, 1·3·4·5번은 게임 시뮬에서 작동과 오류 없음을 확인했습니다.
> 그림: `release-notes/images/remix7.1-open-door-leaf.png` (열린 문짝 전/후, 릴리즈에 첨부)

---

**<설치 / 업데이트>**

- **Remake7에서 올라오는 경우:** 클라이언트 `SAIN.dll`·`SAIN.Preset.Shared.dll`과 **서버 모드 둘 다** 덮어써 주세요. 서버 모드에도 설정 이름이 든 `SAIN.Preset.Shared.dll`이 들어 있어서, 한쪽만 바꾸면 웹 화면에 새 설정이 안 보입니다.
  - zzap 프리셋 값은 바뀌지 않았습니다. 새 설정은 서버가 시작할 때 기존 프리셋에 기본값(켬)으로 채워 넣습니다.
  - 지우고 새로 설치할 때는 `Presets/` 안의 **이름에 `[Modified]`가 붙은 파일과 직접 만든 프리셋**을 먼저 따로 복사해 두세요.
- **Remake7보다 전 판에서 올라오는 경우:** Remake7 노트의 설치 순서대로 한 뒤 위처럼 덮어써 주세요.

---

**<변경점>**

**1. 봇이 열린 문짝을 돌아서 지나감 (새 설정, 기본 켬)**
- 열린 문의 문짝이 서 있는 자리가 봇이 다니는 길에서 빠져 있지 않아서, 봇이 문짝을 향해 걷다가 박고 서 있거나 문짝에 비벼 대며 지나갔습니다.
- EFT에는 문마다 '열린 문짝 자리를 길에서 빼는 장치'가 들어 있는데, 게임이 레이드 시작 때 전부 꺼 둡니다.
- 이제 문이 열리고 문짝 옆 2m 안에 아무도 없으면 그 장치를 켜고, 문이 닫히면 바로 끕니다. 거꾸로 연 문짝은 내려앉을 때까지 기다립니다. 문짝 자리를 뺀 뒤 그 근처(2.5m 안)를 지나는 길로 가던 봇은 길을 다시 찾습니다. 'Disable All Doors'로 꺼진 문은 빼고, 다시 켜지면 다시 적용합니다.
- 설정: F6 `문 전술 (zzap)` › `봇이 열린 문짝을 돌아서 지나감` (`Bots Walk Around Open Door Leaves`). 바꾸면 다음 레이드부터 적용.
- 확인: 게임 리저브 20분 3판 — 판마다 장치가 220여 번 켜지고 꺼짐, SAIN 오류 0, 화면 프레임 차이 없음. 1차 시뮬(30판) 문짝 앞 정체: 공장 294→75, 세관 70→3, 리저브 552→217, 연구소 483→210. 켬/끔을 같은 조건으로 나란히 비교한 게임 판은 아직 없습니다(아래 알려진 점).

**2. 바닥 끝 밖에 선 봇이 길을 못 찾고 몇 분씩 서 있던 것**
- 봇이 걸어 다니는 바닥(내비메시) 가장자리에서 옆으로 0.35m 넘게 밖에 서면 — 위층 끝 난간 옆, 계단 사이 빈틈, 주차장 문턱 같은 곳 — 적에게 가는 길·엄폐로 가는 길·이동 길이 전부 실패했습니다. 봇은 '엄폐 없음 → 대기 → 다시 엄폐 찾기'를 되풀이하며 몇 분씩 그 자리에 서 있었습니다(게임 시뮬: 공장 644초, 세관 계단 321초, 연구소 주차장 문).
- SAIN이 봇 위치를 '높이만 바닥에 맞춘 그 자리'로 써서, 바닥 밖이면 길찾기 출발점이 바닥 위가 아니었습니다. 원작 SAIN부터 있던 문제입니다.
- 이제 바닥 밖이면 **길 출발점만** 가장 가까운 바닥 점으로 옮깁니다(0.25m 안으로 돌아오면 원래대로 — 가장자리에서 왔다 갔다 하지 않게). 적이 어느 쪽에 있는지 같은 다른 판단은 그대로입니다. 적에게 가는 길을 다시 계산할지 정할 때, 실제로 계산했을 때만 위치를 기억하게 했습니다(전에는 건너뛴 때도 기억해서 다음 계산이 늦어짐).
- 확인: 게임 4맵(공장·세관·리저브·연구소) 15분씩 — 길이 막혀 2분 넘게 서 있는 봇 0마리(고치기 전 같은 순회: 공장 1·세관 2), '엄폐도 길도 없음' 대기 공장 3308→78·세관 1258→45, 출발점을 옮긴 횟수 8·8·12·15, SAIN 오류 0. 1차 시뮬 3맵 100판은 전/후가 같았습니다(시뮬에선 바닥 밖 상황이 생기지 않음 — 다른 행동이 안 바뀐 것만 확인).

**3. 분대 수색 때 분대가 통째로 서 있던 것**
- 분대가 함께 수색할 때 리더가 '리더 따라가기'를 자기 자신에게 걸어 제자리에 서고, 분대원도 그 리더를 따라 다 같이 서 있었습니다.
- 이제 리더가 아닌 분대원만, 리더가 실제로 수색하고 있을 때만 따라갑니다. 리더는 직접 수색합니다.

**4. 엄폐도 길도 없으면 각 잡고 대기**
- 안 보이는 적에게 가는 길도 없고 숨을 엄폐물도 없을 때, 전에는 '엄폐 찾기' 상태로 아무것도 못 하고 서 있었습니다(게임 시뮬: 연구소 웨지, 리저브).
- 이제 그 자리에서 각을 잡고 기다립니다. 최대 30초, 그 뒤 20초는 원래 행동으로 돌아가 다시 길을 찾습니다. 총에 맞고 있거나 5초 안에 맞은 봇은 기다리지 않습니다.

**5. 문틀·문짝은 엄폐로 안 씀**
- 리저브 지하 문에서 봇이 문틀을 엄폐로 잡고 버티다가, 문이 움직이거나 다른 봇이 지나가 엄폐가 무너지면 그 자리에 멈춰 섰습니다.
- 이제 문틀·문짝은 엄폐 후보에서 뺍니다. 게임 측정: 엄폐 후보 중 문 2/319 → 0/344.

**6. 기타**
- 봇이 판단할 때마다 만들던 '왜 그렇게 정했나' 글을 일지(시뮬·디버그)를 켰을 때만 만듭니다 — 판단 쪽 메모리 쓰레기 4% 감소, 판단 결과는 같음.
- 끼임 로그에 SAIN 길의 남은 거리를 적음(게임 값은 SAIN이 길을 몰 때 늘 0m라 쓸모없었음).
- 시뮬 모드 전용: 봇 부품별 메모리 측정(평소 게임에는 영향 없음).

**알려진 점**
- 1번 문짝 장치를 켠 게임 판 4개에서 SAIN 끊김(짧은 멈칫) 기록이 판마다 5~11회였습니다(장치 없이 돈 이전 판 1개는 3회). 대부분 봇이 나올 때였고 원인은 아직 모릅니다. 끔과 같은 조건으로 비교한 판이 아직 없으니, 멈칫이 늘었다고 느끼면 F6 `봇이 열린 문짝을 돌아서 지나감`을 끄고 알려 주세요.
- 2번은 봇이 바닥 밖에 선 경우만 바뀝니다. 그 밖의 이유로 오래 서 있는 봇(겁쟁이 성격이 멀리 있는 적을 피해 숨어 있기, 크게 다쳐 치료하며 버티기)은 정상 행동입니다.
- 문제가 있으면 스크린샷과 `BepInEx/LogOutput.log`를 보내 주세요. Remake7로 되돌리려면 Remake7 zip을 다시 덮어쓰면 됩니다.

------------------------------------------------------------------------------------------------------------------------------------------------------

**<Changes (English)>**

**Previous release:** Remake7 (`remake7`, published 10/24, 11 commits since) → **this: Remix7.1**. Bot pathing and cover fixes plus open door leaves. Settings screens and zzap preset values are the same as Remake7; the one new setting is `Bots Walk Around Open Door Leaves` (default on). Item 2 was checked before/after in a 4-map in-game sim; items 1, 3, 4 and 5 were seen working without errors in in-game sims. Picture: `release-notes/images/remix7.1-open-door-leaf.png` (attached to the release).

**Install / update:** overwrite the client (`SAIN.dll`, `SAIN.Preset.Shared.dll`) **and** the server mod - the server mod carries `SAIN.Preset.Shared.dll` too, so the web UI shows the new setting only with both. Preset values did not change; the server fills the new setting (on) into existing presets. On a clean install, keep your `[Modified]` and own preset files.

**1. Bots walk around open door leaves (new, default on)** - an open leaf's spot stayed in the bots' walkable paths, so bots walked into the leaf and stood there or scraped along it. Every door carries EFT's own carver for that, which the game switches off at raid start. SAIN now turns it on once the door is open and nobody is within 2 m of the leaf, and off when the door closes (reverse-opened leaves wait until they settle); a bot whose kept route runs within 2.5 m of a new cut paths again; doors disabled by Disable All Doors are skipped and carved again when they come back. F6 `Door Tactics (zzap)` › `Bots Walk Around Open Door Leaves`, next raid. In game (Reserve 20 min, 3 raids): about 220 carves per raid, 0 SAIN errors, same frame rate. 1st-stage sim (30 raids) door leaf stalls: Factory 294→75, Customs 70→3, Reserve 552→217, Labs 483→210. No side-by-side on/off game run yet.

**2. Bots past the NavMesh edge stood for minutes** - a bot standing more than 0.35 m sideways past its floor's edge (next to an upper floor rail, in a stairwell gap, at the Labs parking gate) had every path fail - to the enemy, to cover, to move - and looped no cover → hold → seek cover for minutes (in-game sim: Factory 644 s, Customs stairs 321 s, Labs gate). SAIN used the bot's own x/z with the floor height as its position, which is no NavMesh point there - original SAIN behaviour. Now only the **path start** moves to the nearest NavMesh point (back below 0.25 m, so it doesn't flicker at the edge); which side the enemy is on still comes from the old position. The enemy path recalc remembers positions only when it really recalculates. In game, 4 maps × 15 min: 0 bots stuck 2 min+ with no path (before, same rotation: Factory 1, Customs 2), no cover + no path holds Factory 3308→78, Customs 1258→45, path start moved 8 / 8 / 12 / 15 times, 0 SAIN errors. 1st-stage sim, 3 maps × 100 raids: identical before/after (the sim never puts a bot off the edge - shows nothing else changed).

**3. Squad search stood the whole squad** - the leader on group search followed itself and stood, and the squad followed the standing leader. Now only non-leaders follow, and only while the leader is searching.

**4. No cover and no path → hold an angle** - with no path to a hidden enemy and no cover, the bot stood in seek cover doing nothing (in-game sim: Labs Wedge, Reserve). Now it holds an angle for at most 30 s, then 20 s of the old default to look for a path again; not while being shot or hit within 5 s.

**5. Door frames and leaves are no cover** - bots held a door frame as cover at a Reserve bunker door and stalled when the door moved or another bot walked through. Door colliders are out of the cover candidates (in game: doors among candidates 2/319 → 0/344).

**6. Other** - decision reason text only when the journal (sim / debug) is on (4 % less decision garbage, same decisions); stuck log shows SAIN's own path distance left (EFT's value is always 0 m while SAIN drives the path); sim mode only: garbage per bot part.

**Known issues** - with the leaf carver on, SAIN hitch records were 5-11 per raid in 4 raids (3 in one earlier raid without it), mostly at bot spawns, cause unknown, and no like-for-like off run yet: if you feel more hitches, turn `Bots Walk Around Open Door Leaves` off and tell me. Item 2 only changes bots off the floor edge; other long waits (a Coward hiding from far enemies, a badly hurt bot healing in cover) are normal. To go back, overwrite with the Remake7 zip.

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
