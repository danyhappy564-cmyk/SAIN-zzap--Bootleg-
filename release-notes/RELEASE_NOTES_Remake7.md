# SAIN-zzap--Bootleg- Remake7

**태그:** `remake7` (master — 태그는 이 노트가 들어간 master 최신 커밋에)

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

**이전 릴리즈:** Remix6.1 (GitHub 릴리즈 `remix6.1`, 10/21 게시, 그 뒤 커밋 44개) → **이번: Remake7**

> 이번 판은 **설정 화면을 크게 바꾼 판**입니다. 봇 종류별 설정은 봇마다 난이도를 고르고, AI(SAIN/게임 기본)를 정하고, 쉬운 손잡이 9개로 조절합니다. 전역·성격·장비 설정에도 빠른 설정 탭처럼 프리셋 비교 막대가 붙었습니다.
> 함께 들어간 것: **블랙 디비전·RUAF·UNTAR 봇도 SAIN으로 싸움**(게시하지 않은 Remix6.2 내용), **기본 zzap 프리셋 읽기 전용**(고치면 복사본으로 저장), 저사양 프리셋 삭제.
> 원래 SAIN 봇의 전투 행동과 zzap 프리셋 값은 Remix6.1과 같습니다.
> 모드 봇 수정(5번)은 게임 안 시뮬레이션에서 확인했습니다. 새 설정 화면은 자동 검사와 빌드만 통과했고, **게임 F6·웹에서 직접 눌러 보는 확인은 아직 못 했습니다.**
> SAIN 내부 버전 숫자는 프리셋 호환 때문에 원본 기준 4.5.1 그대로이고, F6 왼쪽 위·로그 첫 줄·웹 업데이트 페이지에 릴리즈 이름(Remake7)이 나옵니다.

---

**<설치 / 업데이트>**

- **Remix6.1에서 올라오는 경우:** 클라이언트 `SAIN.dll`·`SAIN.Preset.Shared.dll`과 **서버 모드 둘 다** 덮어써 주세요. 한쪽만 바꾸면 새 설정 화면이 저장을 못 하거나 값이 맞지 않습니다.
  - zzap 프리셋 값은 바뀌지 않았습니다. 새 설정 칸(봇별 AI, 성격 후보)은 서버가 시작할 때 기존 프리셋에 기본값으로 채워 넣습니다.
  - **기본 zzap 프리셋을 직접 고쳐 쓰고 있었다면:** 이제 기본 프리셋은 덮어써지지 않습니다. 원하는 값을 다시 고르고 저장하면 `[Modified]` 복사본이 생기고, 그 복사본이 선택됩니다.
  - 지우고 새로 설치할 때는 `Presets/` 안의 **이름에 `[Modified]`가 붙은 파일과 직접 만든 프리셋**을 먼저 따로 복사해 두세요.
- **블랙 디비전을 zzap 포크판으로 쓰고 있었다면:** 원본 블랙 디비전 모드로 바꿔 주세요. 포크판의 SAIN 연동 부분과 이번 SAIN이 같은 일을 두 번 하게 됩니다.
- **remake6 이하에서 올라오는 경우:** 그 판 노트의 설치 순서대로 한 뒤 위처럼 덮어써 주세요.

---

**<변경점>**

**1. 봇 종류별 설정 화면 개편 (F6·웹 같은 구성)**
- 전에는 봇 종류마다 숫자 수십 개가 난이도 4칸(쉬움·보통·어려움·불가능)별로 펼쳐져 있어서, 무엇을 바꿔야 봇이 약해지는지 알기 어려웠습니다.
- 이제 **봇 하나를 고르면** 그 봇의 설정만 나옵니다. 왼쪽 목록은 팩션별로 묶여 처음엔 접혀 있고, 화살표로 펼칩니다.
- **봇마다 난이도 고르기:** 쉬움 = `zzap 쉬움 프리셋 수준`, 보통 = `zzap 보통 프리셋 수준`, 어려움 = `zzap 프리셋 수준`, 불가능 = `zzap보다 더 세게`. 고르면 그 봇의 값만 그 수준으로 바뀝니다. 지금 쓰는 프리셋과 상관없이 같은 뜻이 되도록 전역 능력치 배율을 그 봇 값에 접어 넣습니다. 문 전술 같은 행동은 성격·전역 설정을 따릅니다.
- 목록의 봇 이름 옆에 그 봇의 난이도가 표시됩니다. 고른 뒤 더 만지면 `(조정됨)`, 고른 적이 없고 zzap과 다르면 `직접 설정`입니다.
- **쉬운 손잡이 9개:** 조준 속도·탄 퍼짐·머리 노리기·시야 거리·발견 속도·소리 듣기·공격성·성격·수류탄. 기본으로 난이도 4칸 전부에 같은 비율로 들어갑니다. 기준 대비 '강하게/약하게'와 실제 값이 같이 보입니다. 칸마다 값이 다르면 표시해 주고, 칸별 값을 모두 보여 줍니다.
- **봇마다 AI 고르기:** `SAIN` / `게임 기본 (바닐라)`. 게임 기본을 고른 봇은 SAIN이 손대지 않고 게임 원래 AI로 움직입니다(그 봇의 SAIN 칸은 잠기고, 저장한 값은 보관됨). 게임 재시작 후 적용됩니다. 전역 바닐라 묶음(예: 스캐브 전체 바닐라)이 봇별 선택보다 우선이고, 자리키와 저격수는 원래대로 항상 바닐라입니다.
- **팩션 이름을 누르면** 그 팩션 봇 전체의 난이도·AI·되돌리기·원본 사용을 한꺼번에 합니다. 바닐라로 잠긴 봇은 일괄 변경과 복사에서 건너뜁니다.
- **되돌리기**(마지막 저장값) / **원본 사용**(zzap 값) / **다른 봇에 복사**(여러 봇 동시 선택 대신).
- **고급(접혀 있음):** 숫자 전부와 난이도별 따로 바꾸기, 그 봇이 최근 레이드에서 **실제로 나온 난이도** 기록(최근 5판, 레이드가 끝날 때 서버에 저장). 설명이 없던 15개 항목에 설명을 붙였고, 게임 원래 값은 따로 묶었습니다.
- 봇 반동은 범위상 3배까지만 손잡이를 따라갑니다.
- 확인: 회귀 검사에서 실제 zzap 쉬움·보통·zzap 프리셋의 봇·프리셋 조합 135개로 난이도 표시·단계 왕복·원본 복원을 확인했습니다. 게임 F6·웹 클릭은 미확인입니다.

**2. 성격 지정 → 성격 후보 (모든 봇)**
- Remix6.2 초안의 '성격 지정 (모드 봇용)'을 **모든 봇 종류**로 넓히고, **여러 개를 고를 수 있게** 했습니다(`성격 후보`).
- 1개를 고르면 그 성격으로 고정, 여러 개면 봇이 나올 때 한 번 그 중에서 같은 확률로 고릅니다. 비워 두면(`빈칸=자동`) 평소처럼 자동 배정입니다. 꺼진 성격은 빠지고, 전역 강제 성격이 있으면 그게 먼저입니다.
- 기본값: 블랙 디비전은 Chad/GigaChad 중 무작위, 웨지는 GigaChad, 나머지는 자동. 이미 직접 정해 둔 값은 그대로 둡니다.
- 성격 설명이 화면 언어에 맞게 나옵니다(전에는 GigaChad 설명이 영어 모드에서 한글로, Wreckless 설명이 한글 모드에서 영어로 나왔음).

**3. 전역·성격·장비 설정 화면 정리**
- 빠른 설정 탭처럼 **비교할 프리셋 고르기 + 노란 기준선 + 늘어남/줄어듦 막대 + 숫자 직접 입력**이 붙었습니다. 비교 프리셋을 바꿔도 설정값은 바뀌지 않습니다.
- 숫자 옆 ±10% 버튼을 없애고, 머리 줄의 `기본값으로` 하나로 정리했습니다. 이건 지금 쓰는 프리셋 계열의 원본값으로 되돌립니다(마지막 저장값으로 되돌리기와 다름).
- 전역 설정 왼쪽 분류에 접기/펼치기 화살표(처음엔 접힘). 검색하면 결과가 펼쳐져 보입니다.
- **장비 은밀성:** 높을수록 먼 거리에서 덜 눈에 띈다(소리가 아니라 눈으로 찾는 것)고 설명을 바꿨습니다. 30m 안은 적용 안 되고 30~60m 사이에서 점점 적용됩니다. 장비 검색·여러 개 골라 한꺼번에 조절·저장값으로 되돌리기가 생겼습니다.
- F6 봇 목록에서 긴 한글/영어 이름과 상태 글자가 잘리던 것을 고쳤습니다(긴 이름은 줄바꿈, 이름과 상태는 두 줄).
- 설명 문장 말투를 정리했습니다(예: '있나?' → '있는지?').
- F6 옛 프리셋 관리와 웹에서 SAIN 원본 기본 프리셋 목록(내부 기준값)을 고를 수 없게 뺐습니다. 내부 계산에는 그대로 씁니다.

**4. 기본 zzap 프리셋은 읽기 전용 + 저장 오류 수정**
- 기본 프리셋 4개(zzap 쉬움·zzap 보통·zzap·zzap TEST [시뮬 전용])는 **고쳐도 덮어쓰지 않고**, 저장하면 `[Modified]` 복사본으로 저장되며 게임은 그 복사본을 씁니다. 복사본은 고치고 지울 수 있고, 원본은 F6·웹·서버 어디서도 지워지지 않습니다.
- 고친 오류:
  - F6에서 zzap을 고쳐 저장하면 복사본 이름이 '… Copy(0)'으로 바뀌어, 저장한 값이 적용 안 된 것처럼 원본이 다시 뜨던 것.
  - 저장이 거절됐는데(편집 금지·기본 프리셋 이름·서버 오류) 저장된 것으로 처리하거나 목록에 가짜 항목이 남던 것. 이제 고친 값은 '저장 안 됨'으로 남습니다.
  - F6 옛 프리셋 관리의 '정보 저장'(이름 바꾸기)이 새 이름을 저장하기 전에 옛 파일을 지워, 저장이 막히면 프리셋이 사라질 수 있던 것. zzap 원본 파일도 지울 수 있었습니다.
  - F6 옛 프리셋 관리의 '삭제'와 서버 삭제 요청으로 원본 파일이 지워질 수 있던 것.
  - 옛 폴더형 프리셋 가져오기에 기본 프리셋 이름이 섞여 있으면 서버 시작 때 가져오기가 통째로 멈추던 것.
  - 웹 '새 프리셋'에서 복사본 이름으로 바뀌어 저장돼도 실제 저장된 이름을 엽니다. 장비 은밀성 화면은 비교 프리셋에 장비 목록이 없어도 오류 없이 저장값으로 비교합니다.
- **`zzap 저사양 [테스트]` 프리셋 삭제:** 같은 효과는 어느 프리셋에서든 `빠른 설정 › 저사양 PC` 스위치로 켭니다. 설치된 옛 파일은 서버가 시작할 때 지우고, 직접 만든 복사본은 남깁니다. 그 프리셋을 쓰고 있었다면 다음 실행 때 zzap이 선택됩니다.

**5. 모드 봇(블랙 디비전·RUAF·UNTAR)이 SAIN으로 싸움**
- 이 봇들이 나오기는 하는데, 싸울 때 SAIN 행동(엄폐·문 전술·좌우 스텝 등)을 안 하고 게임 기본 레이더 AI처럼 움직였습니다.
- 원인: SAIN 전투 판단은 게임 기본 전투 판단을 빼 줘야 차례가 옵니다. 이 모드들은 MoreBotsAPI로 봇이 나올 때 두뇌를 새로 만드는데, 그 뒤에 기본 전투 판단 3개(Pmc·AssaultHaveEnemy·AdvAssaultTarget)가 켜진 채로 남아 있었습니다.
- 고친 것: 모드 봇의 두뇌가 정해진 **맨 마지막**에 기본 전투 판단을 다시 뺍니다(블랙 디비전 zzap 포크가 쓰던 방법을 SAIN으로 옮김). 레이드 시작 때 모드 봇 종류마다 SAIN 판단이 붙어 있는지 확인해 빠진 것만 채우고, 판마다 한 벌씩 더 쌓이던 것도 막았습니다.
- **모르는 적에게 맞으면** 그 적을 알아챈 것으로 치고 반격합니다(블랙 디비전 포크 기능을 옮김).
- 확인: **게임 시뮬 공장 20분**, 남은 기본 판단 3개 → 0개. 블랙 디비전 SAIN 판단 비율 0% → 67%, RUAF·UNTAR 73%(나머지는 싸움이 없을 때 순찰이라 정상).
- 이제 블랙 디비전은 원본 모드 그대로 쓰면 됩니다. ISB·Scourge는 원작 설계대로 자체 AI로 움직입니다(이번 판과 무관).
- 로그: 레이드마다 `[SAIN ModdedBots]` 줄이 봇 종류마다 한 번 남습니다. 문제가 있을 때 보내 주시면 원인을 바로 볼 수 있습니다.

**6. 레이드가 끝나도 지난 판의 플레이어·봇 목록이 남아 있던 것**
- 레이드가 끝나면 살아 있는 플레이어 목록과 봇 목록을 비웁니다. 전에는 일부가 다음 레이드까지 지난 판 데이터를 붙잡고 있었습니다.
- 확인: 레이드 뒤 메모리를 붙잡은 모드 목록(램 클리너 기록)에 SAIN이 더 이상 나오지 않습니다. 메모리가 얼마나 줄었는지는 재지 않았습니다.

**7. 시뮬레이션 랩**
- 시뮬 프리셋 **"모드 봇 SAIN 점검 (블디·RUAF·UNTAR)"** — 보충 분대의 절반을 설치된 모드 봇으로 채우고(보스 제외), 모드마다 번갈아 부릅니다. 같은 모드끼리는 아군입니다.
- "팩션 분대 섞기"를 꺼도 모드 봇 점검은 따로 돕니다. 분석 md는 모드 봇이 0명이면 [high]로 알리고, 다른 모드 DLL 경고는 SAIN 오류로 세지 않습니다. F8 장면 기록은 봇 상태를 못 읽어도 한 줄은 남깁니다.

**8. 기타**
- 봇별 바닐라 선택은 SAIN 부품 생성도 막고 게임 기본 탈출 판단을 되살립니다. 전역 바닐라 판정은 두뇌 이름이 아니라 실제 봇 역할로 합니다.
- 자동 검사 추가: 설정 회귀 검사 35개, 웹 화면 정적 검사 35개.

**알려진 점**
- 새 설정 화면(1~4번)은 게임 F6·웹 브라우저에서 직접 눌러 본 확인이 아직 없습니다. 화면이 깨지거나 저장이 안 되면 스크린샷과 `BepInEx/LogOutput.log`를 보내 주세요.
- 서버 설정으로 프리셋을 zzap으로 강제해 둔 경우, 고친 값은 복사본에 저장되지만 다음 실행 때 강제된 원본이 다시 뜹니다.
- 겁쟁이(Coward) 성격의 빈사 모드 봇이 엄폐 자리에서 지키기와 물러나기를 오가며 3분 가까이 서 있던 장면이 있었습니다. 고쳐 보니 봇들이 엄폐에서 더 오래 버티는 쪽으로 바뀌어 이번 판에는 넣지 않았습니다.
- 모드 봇은 모드가 주는 약이 0~3개라 다치면 치료를 거의 못 합니다. 모드 장비 설정이라 SAIN과 무관합니다.

------------------------------------------------------------------------------------------------------------------------------------------------------

**<Changes (English)>**

**Previous release:** Remix6.1 (`remix6.1`, published 10/21, 44 commits since) → **this: Remake7**. A settings overhaul, plus modded bot support (the unpublished Remix6.2), read-only bundled presets and the low-spec preset removed. Vanilla SAIN combat behaviour and zzap preset values are the same as Remix6.1. The modded bot fix was checked in an in-game sim; the new settings screens passed automated checks and builds only - **not yet clicked through in game F6 or the web UI.**

**Install / update:** overwrite the client (`SAIN.dll`, `SAIN.Preset.Shared.dll`) **and** the server mod. Preset values did not change; the server fills the new fields (per-bot AI, personality pool) into existing presets on start. Bundled zzap presets are no longer overwritten - save once and a `[Modified]` copy is created and selected. On a clean install, keep your `[Modified]` and own preset files. If you used the zzap fork of Black Division, switch back to the original mod.

**1. Bot type page redesign (F6 + web)** - one bot at a time, factions folded. Per bot level: Easy / Normal / Hard = the zzap easy / normal / zzap preset level for that bot only (global coefficients folded in, so it means the same on any preset), Impossible = past zzap; badge next to the name, `(adjusted)` after further changes. Nine plain knobs (aim speed, scatter, headshot, sight range, spotting, hearing, aggression, personality, grenade) on all four difficulty slots by ratio, with stronger / weaker against the reference and every slot's value when they differ. Per bot AI: SAIN / EFT's own (applies after a game restart; global vanilla toggles win; Zryachiy and snipers stay vanilla; locked bots keep their saved SAIN values and are skipped by faction-wide edits and copies). Faction header applies level / AI / revert / original to the whole faction. Revert (last save), Use original (zzap), Copy to other bots. Advanced (folded): all numbers, per-difficulty edits, the real spawn difficulty record (last 5 raids), descriptions for 15 fields that had none, EFT's own values grouped. Bot recoil follows the knob up to 3x. 135 real bot / preset combinations checked in the regression tests.

**2. Personality pool (every bot type)** - pick one (fixed), several (uniform pick once per spawn) or none (automatic). Disabled personalities are skipped, a global forced personality wins. Black Division default Chad / GigaChad, Wedge GigaChad; explicit user choices are kept. Personality descriptions follow the UI language.

**3. Global / personality / gear screens** - preset comparison picker, yellow reference line, increase / decrease bars and number input like the quick settings tab. The ±10 % buttons are gone; one `Defaults` button restores the current preset family's original values. Collapsible left categories (folded by default, search shows results). Gear stealth: higher = less visible from far (sight, not sound), no effect within 30 m, blended 30-60 m; gear search, multi-select, bulk adjust, revert to saved. F6 bot list no longer clips long names. SAIN's generated default presets are no longer offered for selection (still used internally).

**4. Read-only bundled zzap presets** - edits save as a `[Modified]` copy that the game then uses; originals can't be deleted from F6, the web or the server. Fixed: the copy renamed to `Copy(0)` so the original came back; a refused save treated as saved or leaving a ghost entry; F6 rename deleting the old file before the new one was saved; F6 old manager delete and server delete removing originals; old folder import stopping on a bundled name; web new preset opening the wrong name; gear stealth compare with no gear list. `zzap 저사양 [테스트]` removed - use `Quick Settings › Low-spec PC` on any preset; the old file is deleted on server start, user copies are kept, and users who had it selected fall back to zzap.

**5. Modded bots (Black Division, RUAF, UNTAR) fight with SAIN** - three vanilla combat layers (Pmc, AssaultHaveEnemy, AdvAssaultTarget) stayed active after MoreBotsAPI rebuilt the brain, so SAIN's layers never got their turn. SAIN now re-excludes them as the very last spawn step and checks every modded type's SAIN layers at raid start (no duplicates per raid). Hit by an unknown enemy: the shooter becomes known and the bot fights back. In-game sim (Factory, 20 min): leftover layers 3 → 0; Black Division on SAIN layers 0 % → 67 %, RUAF / UNTAR 73 %. Black Division works as the original mod now. ISB and Scourge keep their own AI by design.

**6. Raid end cleanup** - the alive-player set and the bot sets are emptied at raid end; SAIN no longer shows in RAM Cleaner's after-raid holders.

**7. Sim lab** - "modded bot SAIN check" preset (half of the refill squads are installed modded bots, one mod after another), runs with faction squads off; the analysis flags zero modded bots and keeps other mods' DLL warnings out of the error count; F8 marks keep a line when the bot state fails.

**8. Other** - per-bot vanilla also blocks SAIN's components and restores EFT's extract layer; the global vanilla lock uses the real role, not the brain name. 35 settings regression tests and 35 static web checks added.

**Known issues** - the new settings screens are not yet checked in game or in a browser; with the server forcing the zzap preset, edits go to a copy but the forced original loads next time; a dying Coward modded bot stood ~3 min flipping between hold and fall back (a fix made bots camp longer in the offline sim, so it is not in); modded bots carry 0-3 meds from their mod.

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
