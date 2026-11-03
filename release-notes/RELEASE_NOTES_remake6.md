# SAIN-zzap--Bootleg- remake6

**태그:** `remake6` (master — 코드 기준 커밋 `877e449`, 태그는 이 노트가 들어간 master 최신 커밋에)

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

**이전 릴리즈:** Remix5.1 (GitHub 릴리즈 `remix5.1`, 10/14 게시, 그 뒤 커밋 9개) → **이번: remake6** (설정 화면 대개편 — 게임 안 F6 + 서버 웹)

> 이번 판은 **봇 행동은 Remix5.1과 같고, 설정하는 화면을 전부 새로 만든** 판입니다. 바뀐 봇 값은 프리셋 '보통' 하나뿐입니다.
> F6 화면은 보내 주신 게임 스크린샷을 보며 두 번 고쳤고, 웹 프리셋 화면도 스크린샷으로 맞췄습니다. 마지막에 고친 몇 가지(아래에 표시)는 아직 게임·웹에서 확인하지 못했습니다.
> 창 왼쪽 위와 로그 첫 줄에 이제 릴리즈 이름(remake6)이 나옵니다. SAIN 내부 버전 숫자는 프리셋 호환 때문에 원본 기준 4.5.1 그대로입니다.

---

**<설치 / 업데이트>**

- **Remix5.1에서 올라오는 경우:** 클라이언트 `SAIN.dll`과 **서버 모드 둘 다** 덮어써 주세요(웹 화면·프리셋이 바뀌었습니다).
  - 서버 모드 zip에 이제 `wwwroot/` 폴더(웹 화면 디자인 파일)가 들어 있습니다. **같이 풀어 주세요.** 예전 zip에는 빠져 있어서 웹 화면이 꾸밈 없이 보였을 수 있습니다.
  - 서버 모드 `Presets/`의 zzap 프리셋도 덮어써 주세요('보통'이 바뀌었습니다).
  - **예전에 zzap 프리셋(쉬움·보통·zzap 등)을 직접 고쳐 저장했다면**, 덮어쓰기 전에 그 프리셋 파일을 다른 이름으로 복사해 두세요. 예전 판은 zzap 프리셋에 바로 덮어썼기 때문에 업데이트하면 원래 값으로 돌아갑니다. 이번 판부터는 저장하면 따로 복사본이 생깁니다(아래 4번).
- **remake5 이하에서 올라오는 경우:** Remix5.1 노트의 설치 순서대로 한 뒤 위처럼 덮어써 주세요.

---

**<변경점>**

**1. 게임 안 F6 설정 창 새로 만듦**
- 전에는 빨간 막대가 줄줄이 이어진 긴 목록이라, 무엇을 어디서 바꾸는지 찾기 어려웠습니다.
- **첫 탭 '빠른 설정'**
  - 왼쪽: zzap 프리셋 카드(난이도를 막대 세 칸으로 표시, 누르면 바로 바뀜). 원본 SAIN 프리셋은 접혀 있고, '내 프리셋'에서 지금 프리셋을 복사하거나 지울 수 있습니다.
  - 오른쪽 위: **'저사양 PC' 스위치 하나**로 프레임을 잡아먹는 계산 5가지를 한 번에 켜고 끕니다. 난이도와 상관없이 아무 프리셋에나 켤 수 있습니다.
  - 그 아래: **자주 바꾸는 값 14개**(반동·조준 시간·연사·탄 퍼짐·시야 거리·발견 속도·청각·시야각·문 전술·문 박치기·코너 프리파이어·분대 돌격·레이드 일지·진단 로그).
- **숫자를 쉬운 말로**
  - 슬라이더는 모두 **오른쪽 = 봇이 강해짐**으로 맞췄습니다(반동·조준 시간·탄 퍼짐은 숫자가 작을수록 강해서 거꾸로 그림).
  - 각 줄 위에 `zzap 3배 → 2.25배 ▼ 약하게 · 25% 느림`처럼 **비교 기준과 지금 값**이 나옵니다. 슬라이더에는 기준(노란 선)과 지금 값 사이가 빨강(봇이 강해짐 = 어려워짐) 또는 초록(약해짐 = 쉬워짐)으로 칠해집니다.
  - 비교 기준은 zzap / 쉬움 / 보통 / 저장된 값 중에서 고릅니다. '저장된 값'을 고르면 방금 바꾼 것만 막대로 보입니다.
  - 아래 설명은 **SAIN 기본값을 기준으로** 씁니다(예: "SAIN 기본보다 2.25배 빨리 방아쇠를 당김 (zzap 3배)"). 'zzap과 같음'만 보고는 zzap 값을 따로 알아야 했다는 테스터분 의견을 반영했습니다.
- **나머지 탭**
  - 전역 설정: 왼쪽에 검색칸과 묶음 목록(문 전술·근접 교전·정지 및 매복 같은 zzap 묶음도 따로), 오른쪽에 고른 묶음만 보입니다.
  - 봇 종류별: 세 칸입니다(봇 종류·난이도 | 설정 묶음 | 값). 전에는 펼침 버튼을 여러 번 눌러야 값이 나왔습니다.
  - 성격·장비 은밀성: 왼쪽 목록, 오른쪽 값.
  - 공통: 켬/끔은 스위치로 나옵니다. 값이 기본값과 다를 때만 '초기화' 버튼이 나옵니다. 저장 버튼은 위쪽 노란 버튼 하나로 합쳤습니다(옆에 '저장 안 한 변경 N개' 표시). 한/EN 전환도 됩니다.
- **같이 고친 버그 (미확인):** 값을 바꾸고 저장하지 않은 채 다른 프리셋을 고르면, 바꾼 값이 메모리에 남아 다시 골랐을 때 저장된 값처럼 보였습니다. 이제 저장 안 한 변경이 있으면 '저장하고 바꾸기 / 버리고 바꾸기 / 취소'를 묻습니다. '버리기'를 고르면 실제로 저장된 값으로 돌아갑니다.
- 확인: 첫 모양과 차이 표시는 게임 스크린샷으로 확인했습니다. 마지막에 고친 '이름 두 줄로 잘림'과 위 버그는 아직 게임에서 확인 못 했습니다.

**2. 서버 웹 화면 개편 (`https://127.0.0.1:6969/sain/...`)**
- **프리셋 화면**
  - 왼쪽은 난이도 표시가 붙은 카드 목록이고, 원본 SAIN 프리셋은 접혀 있습니다.
  - 탭: '빠른 설정'(F6과 같은 14개 + 저사양 PC 스위치 + 비교 기준·색 막대), '프리셋 비교'(zzap 프리셋끼리 나란히), '바꾼 값'(저장 안 한 변경이나 zzap 대비 바뀐 값 목록, 줄마다 되돌리기), 그리고 예전의 전체 설정.
  - 새 프리셋을 만들 때 zzap 프리셋을 바탕으로 고를 수 있습니다.
  - 전에는 웹이 zzap 프리셋을 제대로 못 찾아서 비교와 눈금이 비어 있었는데, 그것도 고쳤습니다.
- **시뮬레이션 페이지:** 지금 상태 요약, 순서 안내, 시뮬 프리셋 카드, 기능 스위치 23개를 7묶음으로 정리했습니다. 맵 순서 표는 그 자리에서 고칠 수 있습니다.
- **시뮬 분석 페이지:** 요약 숫자 7개, 왼쪽 맵 표, 오른쪽 탭으로 자세히 볼 수 있습니다.
- **업데이트 페이지:** 원본 SAIN 릴리즈 대신 **이 포크의 릴리즈 기록**(remake·Remix)을 보여 주고, '사용 중' 표시와 새 버전 알림이 나옵니다.
- 영어 화면도 같이 손봤습니다(시뮬 프리셋·기능 이름, 게임 안 시뮬 알림도 한/영).
- 확인: 프리셋 화면은 웹 스크린샷으로 확인했습니다. 슬라이더 손잡이 크기 수정은 같은 디자인 파일을 넣은 시험 화면으로만 봤습니다. 시뮬·분석 페이지는 아직 확인 못 했습니다.

**3. 프리셋 '보통'을 쉬움과 zzap의 진짜 중간으로**
- 전에는 원본 SAIN '보통' 값을 따라서, zzap과의 차이가 쉬움과의 차이보다 훨씬 컸습니다(반동 zzap의 16배, 조준 1초, 시야 거리 SAIN 기본과 같음).
- 이제: 반동 zzap의 8배(1.6 → 0.8), 조준 시간 1초 → 0.6초, 시야 거리 SAIN 기본의 1배 → 1.4배, 연사 zzap의 60% → 75%, 발견·청각 75% → 85%, 시야각 150° → 160°, 실수 빈도 조금 줄임. 전술 빈도(문 전술 75% 등)는 그대로입니다.
- '저사양 [테스트]' 프리셋도 '보통 + 성능 설정'이라 같이 바뀌었습니다. 이 프리셋은 '예전 방식'으로 표시했습니다. 이제 빠른 설정의 '저사양 PC' 스위치로 아무 난이도에나 같은 효과를 낼 수 있습니다.
- 확인: 게임 체감은 아직 확인 못 했습니다. 예전 '보통'이 더 좋으면 F6 빠른 설정에서 값을 낮추거나 '쉬움'과 섞어 쓰시면 됩니다.

**4. zzap 프리셋을 고쳐 저장하면 복사본으로 저장**
- 전에는 웹이나 F6에서 zzap 프리셋을 고쳐 저장하면 그 파일을 바로 덮어썼습니다. 그래서 모드를 업데이트하면 고친 값이 날아갔습니다.
- 이제 `이름 [Modified]` 복사본으로 저장됩니다(이미 있으면 `[Modified 2]`…). F6은 저장하자마자 그 복사본으로 바뀌고, 웹에서 저장했으면 F6에서 복사본을 고르면 됩니다.
- 웹에서 zzap 프리셋 이름을 바꿔 저장해도 원본 파일을 지우지 않습니다.
- 예외: 서버 설정에서 그 프리셋을 강제로 고정해 둔 경우는 예전처럼 원본에 저장합니다(고정된 이름의 프리셋이 게임에 불러와지기 때문).
- 확인: 빌드만 확인했고, 게임·웹에서는 미확인입니다.

**5. 기타**
- 서버 모드 zip에 웹 디자인 파일(`wwwroot/css/sain.css`)이 빠져 있던 것을 고쳤습니다(위 설치 안내 참고).
- 도움말·README의 '얼음 매복' 표기를 '정지 및 매복'으로 통일했습니다.
- 디클러터·CompoundingPerf·램클리너와 함께 써도 서로 같은 곳을 고치지 않는 것을 코드로 확인했습니다(겹치는 패치 없음).
- 시뮬 비교가 시뮬 전용 프리셋의 복사본 대신 원본 파일을 읽도록 했습니다.

**알려진 점**
- 위에 '미확인'으로 적은 것은 아직 게임·웹에서 보지 못했습니다. 화면이 이상하면 스크린샷을 보내 주세요(F6은 창 크기와 해상도에 따라 다를 수 있습니다).
- 클라이언트 `SAIN.dll`은 사용자 PC 빌드(SPT 4.1 참조)로 배포합니다. 서버 모드 zip은 `SAINServerMod/release/SAINServerMod-4.5.1.zip`(Release 빌드가 자동 생성)입니다.

------------------------------------------------------------------------------------------------------------------------------------------------------

**<Changes (English)>**

> Everything since Remix5.1 (9 commits). **Bot behaviour is the same as Remix5.1; the settings screens were rebuilt.** The only bot values that changed are in the 'normal' preset.
> The F6 screens were adjusted twice from in-game screenshots and the web preset page from a web screenshot. The last few fixes (marked below) are not checked in game yet.
> The editor title and the first log line now show the release name (remake6). SAIN's internal version number stays 4.5.1 for preset compatibility.

**Install / update**
- From Remix5.1: overwrite both the client `SAIN.dll` and the server mod. The server zip now contains `wwwroot/` (the web stylesheet) - extract it too; older zips left it out, so the web pages could look unstyled.
- Overwrite the zzap presets in the server mod's `Presets/` ('normal' changed). **If you edited and saved a zzap preset before**, copy that file under another name first - older builds saved over the bundled file, so updating resets it.

**1. New F6 editor**
- First tab 'Quick settings': zzap preset cards (difficulty bars, click to switch), a single 'Low-spec PC' switch (five costly calculations, any difficulty) and the 14 most changed values.
- Every slider makes bots stronger to the right. Each row shows `reference -> now` with stronger/weaker and the change in its own unit; the slider fills red (stronger, harder) or green (weaker, easier) between the reference line and the value. Reference: zzap / easy / normal / saved values. Descriptions are measured against SAIN's default.
- Global settings: search box and group list on the left, one group on the right. Bot types: bots and difficulties | groups | values. Personalities and gear stealth: list and values. On/off switches, 'Reset' only when a value differs from its default, one Save button in the header with an unsaved-changes count, Korean/English switch.
- Bug fixed (not checked in game): values changed and not saved stayed in memory after switching presets and later looked saved. Switching with unsaved changes now asks: save, discard (really restores the saved state) or cancel.

**2. Server web pages (`https://127.0.0.1:6969/sain/...`)**
- Presets: card list, tabs 'Quick settings' (same as F6, with reference and coloured stretch), 'Compare presets', 'Changed values' (per-row revert) and the full settings. New presets can be based on a zzap preset. The web now finds the bundled zzap presets (comparison and ticks used to be empty).
- Simulation: status summary, steps, preset cards, 23 feature switches in 7 groups, inline rotation table. Analysis: 7 summary numbers, map table, tabbed details. Updates page: this fork's releases with 'in use' and new-version notice. English text completed.
- Checked: preset page from a web screenshot; slider thumb fix only on a test page; sim pages not checked.

**3. 'Normal' preset now halfway between easy and zzap**
- Recoil 1.6 -> 0.8 (8x zzap), aim time 1.0 s -> 0.6 s, sight 1.0x -> 1.4x SAIN default, fire rate 60% -> 75% of zzap, spotting/hearing 75% -> 85%, view angle 150 -> 160 deg, fewer mistakes. Tactic frequencies unchanged. The low-spec [test] preset follows it and is marked 'old way' (use the Low-spec PC switch instead). Not checked in game.

**4. Saving a zzap preset makes a copy**
- Web and F6 save `name [Modified]` (`[Modified 2]`...) and leave the bundled file alone, so updates no longer wipe your changes. F6 switches to the copy right away. Renaming on the web no longer deletes the bundled file. A preset forced by the server still saves in place. Build-verified only.

**5. Other**
- Server zip now includes `wwwroot/css/sain.css`. 'Freeze ambush' wording unified. No overlapping patches with DeClutterer / CompoundingPerf / RamCleaner (code check). Sim comparison reads the bundled sim preset, not a copy.

**Known issues**
- Items marked unchecked have not been seen in game/web yet - please send a screenshot if something looks wrong.
- The client `SAIN.dll` is built on the user's PC (SPT 4.1 references). The server mod zip is `SAINServerMod/release/SAINServerMod-4.5.1.zip` from the Release build.

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

^^7
