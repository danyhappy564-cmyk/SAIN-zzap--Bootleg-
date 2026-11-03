# SAIN-zzap--Bootleg- Remix7.2

**태그:** `remix7.2` (master — 태그는 이 노트가 들어간 master 최신 커밋에)

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

**이전 릴리즈:** Remix7.1 (GitHub 릴리즈 `remix7.1`, 10/31 게시, 그 뒤 커밋 1개) → **이번: Remix7.2** (버그 수정)

> 레이드가 끝난 뒤 **SAIN이 지난 레이드를 메뉴에서까지 붙잡고 있던 메모리 누수**를 고친 판입니다.
> 봇 행동·설정 화면·zzap 프리셋 값은 Remix7.1과 같습니다.
> Remix7.1 노트의 알려진 점(문짝 장치를 켜면 멈칫이 늘었나?)도 같은 조건 비교로 확인했습니다 — 아래 2번.

---

**<설치 / 업데이트>**

- **Remix7.1에서 올라오는 경우:** `SAIN.REMIX.7.2.7z`를 풀어서 SPT 폴더(`EscapeFromTarkov.exe`가 있는 곳)에 덮어써 주세요. 클라이언트(`BepInEx`)와 서버 모드(`SPT_Runtime`)가 같이 들어 있습니다. 고친 코드는 클라이언트에만 있고, 서버 모드는 버전 이름(웹 업데이트 페이지)만 바뀝니다. 설정·프리셋은 그대로입니다.
- **Remix7.1보다 전 판에서 올라오는 경우:** 그 판 이후 노트의 설치 순서대로 한 뒤 위처럼 덮어써 주세요. 지우고 새로 설치할 때는 `Presets/` 안의 **이름에 `[Modified]`가 붙은 파일과 직접 만든 프리셋**을 먼저 따로 복사해 두세요.

---

**<변경점>**

**1. 레이드가 끝난 뒤 메뉴에서 지난 레이드 메모리가 남던 것**
- 레이드를 끝내고 메뉴로 돌아와도 지난 레이드의 맵·봇 데이터가 메모리에 그대로 남아, 다음 레이드를 시작할 때까지 풀리지 않았습니다. RAM 클리너의 누수 보고에서 SAIN이 '누수 확실' 1위로 나오던 원인입니다.
- SAIN의 봇 관리 부품 세 군데(관리자 자신, 봇 스폰 담당, 날씨 담당)가 '지금 레이드' 표시를 레이드가 끝나도 지우지 않아서, 그 표시를 따라 지난 레이드 전체가 붙잡혀 있었습니다.
- 이제 레이드가 끝날 때 그 표시를 지웁니다. 다음 레이드가 먼저 시작된 경우(정리가 늦게 도는 경우)에는 새 레이드 것을 지우지 않도록, 자기 것일 때만 지웁니다. 소리 듣기 연결도 자기 것만 끊고(전에는 새 레이드의 봇 청각까지 끊을 수 있었음), 봇이 먼저 사라진 경우에도 오류 없이 정리합니다.
- 확인: 게임 리저브 20분 시뮬 3판 — 레이드가 끝난 뒤 RAM 클리너 누수 목록에 SAIN이 없음(남은 것은 다른 모드: ORBIT·봇 배치 모드·LootNet), SAIN 오류 0. Codex 코드 검토 2회.

**2. 확인만 함: 열린 문짝 장치와 멈칫 (Remix7.1 알려진 점)**
- Remix7.1 노트에 '문짝 장치를 켠 판에서 짧은 멈칫 기록이 조금 많았다'고 적었는데, 그때는 끈 판과 같은 조건으로 비교하지 못했습니다(RAM 클리너는 게임 창이 맨 앞에 있을 때만 멈칫을 기록해서, 창이 뒤에 있던 판은 숫자가 빠졌습니다).
- 이번에 리저브 20분을 켬/끔 한 판씩 같은 조건으로 비교했습니다: SAIN 멈칫 켬 7회 / 끔 5회, 평균 프레임은 둘 다 14.8ms(약 67fps)로 같습니다.
- 차이는 봇이 처음 나온 뒤 약 15초 동안의 0.08초짜리 멈칫 2번뿐입니다 — 그때 열려 있는 문 200여 개에 장치를 한꺼번에 켭니다. 봇이 처음 나올 때의 큰 멈칫(약 0.3초)은 장치를 꺼도 똑같이 나옵니다.
- 그래서 기본값(켬)을 그대로 둡니다. 코드 변경 없음.

**알려진 점**
- 봇이 처음 나올 때 한 번 생기는 약 0.3초 멈칫은 문짝 장치와 상관없이 있습니다(원인 조사 중).
- 문제가 있으면 스크린샷과 `BepInEx/LogOutput.log`를 보내 주세요. Remix7.1로 되돌리려면 SAIN.REMIX.7.1.7z를 다시 덮어쓰면 됩니다.

------------------------------------------------------------------------------------------------------------------------------------------------------

**<Changes (English)>**

**Previous release:** Remix7.1 (`remix7.1`, published 10/31, 1 commit since) → **this: Remix7.2**, a bug fix. Bot behaviour, settings screens and zzap preset values are the same as Remix7.1.

**Install / update:** extract `SAIN.REMIX.7.2.7z` into your SPT folder (where `EscapeFromTarkov.exe` is) and overwrite - it holds both the client (`BepInEx`) and the server mod (`SPT_Runtime`). The fix is client-only; the server mod only changes the release name on the web updates page. Settings and presets are kept. On a clean install, keep your `[Modified]` and own preset files.

**1. The last raid stayed in memory in the menu** - after a raid, the last raid's map and bot data stayed alive in the menu until the next raid started; the RAM cleaner leak report listed SAIN as the top 'certain' holder. Three SAIN bot-manager parts (the manager itself, bot spawning, weather) kept their 'current raid' static after the raid ended, and the whole raid hung off it. They are cleared at raid end now, only by their owner, so a newer raid that already started keeps its own; the sound watcher detaches only its own hearing (it could detach the new raid's), and hearing / talk unsubscribe is null-safe. In game (Reserve 20 min sim, 3 raids): SAIN is gone from the RAM cleaner leak list after the raid (left: other mods - ORBIT, the bot placement mod, LootNet), 0 SAIN errors. Codex reviewed twice.

**2. Checked only: open door leaf carver and hitches (Remix7.1 known issue)** - the Remix7.1 note said raids with the carver on had a few more short hitches, without a like-for-like off run (the RAM cleaner records hitches only while the game window is focused, so unfocused raids were missing numbers). Reserve 20 min, one raid each, same setup: SAIN hitches on 7 / off 5, average frame 14.8 ms (about 67 fps) both. The only difference is two 0.08 s hitches in the first ~15 s after the first bots spawn, when the carver switches on for the ~200 doors open at that moment. The big hitch at the first bot spawn (about 0.3 s) happens with it off too. The default stays on; no code change.

**Known issues** - the one-time ~0.3 s hitch at the first bot spawn is there with or without the carver (under investigation). Send a screenshot and `BepInEx/LogOutput.log` if anything is off. To go back, overwrite with SAIN.REMIX.7.1.7z.

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
