# SAIN-zzap--Bootleg- Remix6.2

**태그:** `Remix6.2` (master — 코드 기준 커밋 `9d40ba6e`, 태그는 이 노트가 들어간 master 최신 커밋에)

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

**이전 릴리즈:** Remix6.1 (GitHub 릴리즈 `remix6.1`, 10/21 게시, 그 뒤 커밋 12개) → **이번: Remix6.2** (모드 봇 지원)

> 이번 판은 **다른 봇 모드(블랙 디비전·RUAF·UNTAR)의 봇이 SAIN으로 싸우게 하는** 판입니다. 원래 SAIN 봇들의 행동은 Remix6.1과 같습니다.
> 블랙 디비전은 그동안 SAIN과 같이 쓰려면 zzap 포크판이 필요했는데, 이제 **원본 모드 그대로** 쓰면 됩니다.
> 핵심 수정(1번)은 게임 안 시뮬레이션(공장 20분, 모드 봇 25명)에서 확인했습니다.
> SAIN 내부 버전 숫자는 프리셋 호환 때문에 원본 기준 4.5.1 그대로이고, F6 왼쪽 위와 로그 첫 줄에 릴리즈 이름(Remix6.2)이 나옵니다.

---

**<설치 / 업데이트>**

- **Remix6.1에서 올라오는 경우:** 클라이언트 `SAIN.dll`·`SAIN.Preset.Shared.dll`과 **서버 모드 둘 다** 덮어써 주세요.
  - zzap 프리셋 값은 바뀌지 않았습니다. 새 설정(성격 지정)은 서버가 시작할 때 기존 프리셋에 기본값으로 채워 넣습니다.
- **블랙 디비전을 zzap 포크판으로 쓰고 있었다면:** 원본 블랙 디비전 모드로 바꿔 주세요. 포크판의 SAIN 연동 부분과 이번 SAIN이 같은 일을 두 번 하게 됩니다.
- **remake6 이하에서 올라오는 경우:** 그 판 노트의 설치 순서대로 한 뒤 위처럼 덮어써 주세요.

---

**<변경점>**

**1. 모드 봇이 SAIN이 아니라 게임 기본 AI로 싸우던 것**
- 블랙 디비전·RUAF·UNTAR 봇이 나오기는 하는데, 싸울 때 SAIN 행동(엄폐·문 전술·다이아몬드 스텝 등)을 하지 않고 게임 기본 레이더 AI처럼 움직였습니다.
- 원인: SAIN 전투 판단은 게임 기본 전투 판단을 빼 줘야 차례가 옵니다. 이 모드들은 MoreBotsAPI로 봇이 나올 때 두뇌를 통째로 새로 만드는데, 그 뒤에는 기본 전투 판단 3개(Pmc·AssaultHaveEnemy·AdvAssaultTarget)가 다시 켜진 채로 남아 있었습니다.
- 고친 것: 모드 봇이 나와 두뇌가 정해진 **맨 마지막**에 그 봇의 기본 전투 판단을 다시 뺍니다(블랙 디비전 zzap 포크가 쓰던 방법을 SAIN 쪽으로 옮김). 레이드 시작 때도 모드 봇 종류마다 SAIN 전투 판단이 붙어 있는지 확인해서 빠진 것만 채웁니다. 같은 판단이 판마다 한 벌씩 더 쌓이던 것도 막았습니다.
- 확인: **게임 시뮬 공장 20분** — 남아 있던 기본 판단 3개 → 0개(블랙 디비전·RUAF·Remnant·UNTAR 모두). 블랙 디비전 SAIN 판단 비율 0% → 67%, RUAF·UNTAR 73%(나머지는 싸움이 없을 때 순찰 시간이라 정상).
- 로그: 레이드마다 `[SAIN ModdedBots]` 줄이 몇 개 남습니다(봇 종류마다 한 번). 문제가 있을 때 보내 주시면 원인을 바로 볼 수 있습니다.

**2. 모르는 적에게 맞으면 그냥 맞고만 있던 것 (모드 봇)**
- 모드 봇이 보지도 듣지도 못한 적에게 맞으면 반응 없이 서 있는 경우가 있었습니다.
- 고친 것: 모르는 적에게 맞으면 그 적을 알아챈 것으로 등록하고 반격합니다(블랙 디비전 포크의 기능을 옮김). 분대 적 목록에 아직 없는 적이면 분대에 먼저 넣고 다시 시도합니다.
- 확인: 게임 시뮬에서 3번 동작한 기록이 있습니다.

**3. F6 봇 종류별 "성격 지정 (모드 봇용)"**
- F6 봇 종류 설정의 성격(Mind) 쪽에 난이도마다 "성격 지정 (모드 봇용)"이 생겼습니다. 정하면 그 봇 종류는 그 난이도에서 항상 그 성격이 됩니다. "지정 안 함"이면 평소처럼 확률로 정합니다. 꺼진 성격을 고르면 무시합니다.
- 기본값: 블랙 디비전 6종(웨지 포함)은 GigaChad, 나머지는 지정 안 함. 블랙 디비전 포크의 F12 설정을 대신합니다.
- 기존 프리셋에는 서버가 시작할 때 빈 칸만 기본값으로 채웁니다. 이미 "지정 안 함"으로 저장한 칸은 그대로 둡니다.
- 확인: 게임 시뮬에서 블랙 디비전에 GigaChad가 지정된 기록이 있습니다.

**4. 레이드가 끝나도 지난 판의 플레이어·봇 목록이 남아 있던 것**
- 레이드가 끝나면 살아 있는 플레이어 목록과 봇 목록을 비웁니다. 전에는 일부가 다음 레이드까지 남아 지난 판 데이터를 붙잡고 있었습니다.
- 확인: 레이드 뒤 메모리를 붙잡은 모드 목록(램 클리너 기록)에 SAIN이 더 이상 나오지 않습니다. 메모리가 얼마나 줄었는지는 따로 재지 않았습니다.

**5. 시뮬레이션 랩**
- 시뮬 프리셋 **"모드 봇 SAIN 점검 (블디·RUAF·UNTAR)"** — 보충 분대의 절반을 설치된 모드 봇으로 채우고(보스 제외), 같은 모드끼리는 아군입니다. 모드마다 번갈아 분대를 부릅니다(전에는 블랙 디비전만 차례가 옴).
- 시뮬 설정의 "팩션 분대 섞기"를 꺼도 모드 봇 점검은 따로 돕니다(전에는 같이 꺼져 모드 봇이 0명).
- 분석 md: 모드 봇 점검인데 모드 봇이 0명이면 [high]로 알립니다. 다른 모드 DLL이 필요한 파일 없이 깔려 찍히는 경고는 SAIN 오류로 세지 않고 참고 줄로 따로 적습니다.
- F8 장면 기록이 봇 상태를 못 읽어도 한 줄은 남깁니다.

**6. 그 밖의 봇 모드**
- ISB·Scourge는 원작 설계대로 자체 AI로 움직입니다(ISB는 SAIN에 등록하지 않고 능력치만 씀, Scourge는 스스로 SAIN에서 빠짐). 이번 판과 상관없습니다.

**알려진 점**
- 같은 시뮬에서 겁쟁이(Coward) 성격의 빈사 모드 봇이 엄폐 자리에서 지키기와 물러나기를 오가며 3분 가까이 서 있었습니다. 고쳐 봤더니 봇들이 엄폐에서 더 오래 버티는 쪽으로 바뀌어 이번 판에는 넣지 않았습니다.
- 모드 봇은 모드가 주는 약이 0~3개라(PMC는 13개 안팎) 다치면 치료를 거의 못 합니다. 모드 장비 설정이라 SAIN과 무관합니다.
- 이상하면 스크린샷과 `BepInEx/LogOutput.log`, 레이드 일지를 보내 주세요.

------------------------------------------------------------------------------------------------------------------------------------------------------

**<Changes (English)>**

**Previous release:** Remix6.1 (`remix6.1`, published 10/21, 12 commits since) → **this: Remix6.2** (modded bot support). Vanilla SAIN bot behaviour is the same as Remix6.1.

**Install / update:** overwrite the client (`SAIN.dll`, `SAIN.Preset.Shared.dll`) and the server mod. Preset values did not change; the server fills the new personality override into existing presets on start. If you used the zzap fork of Black Division, switch back to the original mod.

**1. Modded bots fought with the vanilla AI** — Black Division, RUAF and UNTAR (MoreBotsAPI) rebuild the brain on spawn, and three vanilla combat layers (Pmc, AssaultHaveEnemy, AdvAssaultTarget) were left active, so SAIN's combat layers never got their turn. SAIN now re-excludes them as the very last step of the spawn (the Black Division fork's approach, moved into SAIN), and checks at raid start that every modded bot type has its SAIN layers (no more duplicate layers per raid). In-game sim (Factory, 20 min): 3 leftover vanilla layers → 0 for every modded role; Black Division on SAIN layers 0 % → 67 %, RUAF / UNTAR 73 % (the rest is patrol with no fight). A few `[SAIN ModdedBots]` log lines per raid.

**2. Hit by an unknown enemy (modded bots)** — the shooter becomes a known enemy and the bot fights back (from the Black Division fork); added to the group's enemy list first if needed.

**3. F6 "Personality Override (Modded Bots)"** — per bot type and difficulty; Black Division (6 types incl. Wedge) default GigaChad, others Unassigned. Existing presets get the default only where the field is missing.

**4. Raid end cleanup** — the alive-player set and the bot sets are emptied at raid end (they held the last raid's data until the next raid). SAIN no longer shows in RAM Cleaner's after-raid holders.

**5. Sim lab** — "modded bot SAIN check" preset (half of the refill squads are installed modded bots, one mod after another); runs even with faction squads off; the analysis flags a check with zero modded bots and keeps other mods' DLL load warnings out of the error count; F8 marks keep a line when the bot state fails.

**6. Other bot mods** — ISB and Scourge keep their own AI by design; not affected.

**Known issues** — a dying Coward modded bot stood ~3 min flipping between hold and fall back in its cover; a fix made bots camp longer in the offline sim, so it is not in this release. Modded bots carry 0-3 meds from their mod, so they rarely heal.

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
