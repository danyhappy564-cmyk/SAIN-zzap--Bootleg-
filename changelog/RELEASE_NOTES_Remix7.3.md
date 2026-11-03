# SAIN-zzap--Bootleg- Remix7.3

**태그:** `remix7.3` (master — 태그는 이 노트가 들어간 master 최신 커밋에)

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

**이전 릴리즈:** Remix7.2 (GitHub 릴리즈 `remix7.2`, 11/02 게시, 그 뒤 커밋 13개) → **이번: Remix7.3** (봇 설정 화면 개편)

> F6와 웹의 **봇 종류별 설정 화면**을 손본 판입니다. 봇 여러 종을 묶어서 한꺼번에 조절하고, 마음에 드는 값을 **내 설정 슬롯**에 담아 두었다가 다시 쓰거나 **공유 코드**로 주고받을 수 있습니다.
> 봇 행동(전투 방식)과 zzap 프리셋 값은 Remix7.2와 같습니다. 저장 방식(기본 zzap 프리셋은 `[Modified]` 사본으로 저장)도 그대로입니다.

---

**<설치 / 업데이트>**

- **Remix7.2에서 올라오는 경우:** `SAIN.REMIX.7.3.7z`를 풀어서 SPT 폴더(`EscapeFromTarkov.exe`가 있는 곳)에 덮어써 주세요. 클라이언트(`BepInEx`)와 서버 모드(`SPT_Runtime`)가 같이 들어 있고, **이번에는 둘 다 바뀌었습니다**(웹 화면 파일 `wwwroot/js/bot-settings.js`가 새로 생김). 설정·프리셋은 그대로입니다.
- **Remix7.2보다 전 판에서 올라오는 경우:** 그 판 이후 노트의 설치 순서대로 한 뒤 위처럼 덮어써 주세요. 지우고 새로 설치할 때는 `Presets/` 안의 **이름에 `[Modified]`가 붙은 파일과 직접 만든 프리셋**을 먼저 따로 복사해 두세요. 이번 판부터는 `ClientData/botSettingsSlots.json`(내 설정 슬롯)도 같이 챙기면 슬롯이 남습니다.

---

**<변경점>**

**1. 봇 여러 종을 묶어서 한꺼번에 설정 (F6·웹)**
- 전에는 봇 종류를 하나씩 열어 같은 값을 여러 번 바꿔야 했습니다.
- 이제 왼쪽 봇 목록에서 이름 옆 **동그라미**를 누르거나 이름을 **Ctrl+클릭**하면 묶음에 들어갑니다. 팩션 줄의 동그라미는 그 팩션 전체를 고르고, 팩션 일부만 고르면 개수가 `1/2`처럼 나옵니다. 이름만 누르면 묶음은 그대로 두고 그 봇 하나를 봅니다.
- 묶음에서 난이도·손잡이 9개·고급 값을 바꾸면 **고른 봇 모두에 같은 값**이 들어갑니다. 고른 봇끼리 값이 다르면 '섞임' 표시가 붙습니다.
- 묶음 밖의 봇과 전역 설정은 바뀌지 않고, 게임 기본 AI(바닐라)로 고정된 봇은 건너뜁니다.

**2. 내 설정 슬롯과 공유 코드**
- **＋ 지금 값 저장**으로 지금 보는 봇의 값을 슬롯에 담습니다. 처음 3칸, 최대 9칸. 슬롯 이름을 누르면 지금 봇이나 묶음 전체에 적용됩니다.
- 슬롯 옆 `[...]` 메뉴에서 이름 바꾸기·덮어쓰기·삭제·**공유 코드 복사**를 합니다. 받은 코드는 **공유 코드 붙여넣기**로 새 슬롯이 됩니다(`SAIN1-`로 시작). 코드에는 슬롯 이름·난이도·AI·바꾼 값만 들어갑니다.
- 슬롯은 프리셋과 따로 서버 모드의 `ClientData/botSettingsSlots.json`에 저장돼서 프리셋을 바꿔도 남고, F6와 웹이 같은 슬롯을 씁니다.
- 슬롯에 담거나 적용하는 것과 프리셋 저장은 별개입니다. 게임에 쓰려면 위쪽 **저장**을 눌러 주세요.

**3. 새 항목만 보기 · 바뀐 항목만 보기 · 항목별 원래대로**
- 고급 설정과 전역 설정 탭에 **새 항목만 보기**·**바뀐 항목만 보기** 필터가 생겼습니다. Remake7 계열에서 새로 생긴 설정에는 `NEW` 표시가 붙습니다(지금은 '봇이 열린 문짝을 돌아서 지나감').
- 원래 값과 다른 줄에는 노란 점이 붙고, 줄마다 **원래대로** 버튼으로 그 봇의 원래 값으로 되돌립니다(전에는 '기본값' 버튼).

**4. 처음 여는 사람을 위한 3단계 안내**
- 봇 설정을 처음 열면 노란 안내 띠가 나오고, **3단계 안내**(봇 고르기 → 난이도·손잡이 → 저장·슬롯·공유)를 볼 수 있습니다. **알겠어요**를 누르면 다시 안 나오고, 오른쪽 위 **?**로 언제든 다시 엽니다.

**5. 화면 정리**
- F6와 웹 배치를 같게 맞췄습니다: 위 줄에 '저장 안 한 변경 N개 · 저장 · ?', 번호 머리(01 난이도 고르기, 02 손잡이, 03 고급), 한 줄짜리 AI 선택, 웹 팩션 순서도 F6와 같게(스캐브 → PMC → …).
- 탭 이름 '전역 설정 (전체)' → '전역 설정'. 웹 탭의 `NEW` 표시가 'N'만 보이던 것 고침.
- 영어 화면: 한 개일 때 '1 bot types' → '1 bot type'. 고급 설정 버튼은 'Advanced settings: Off'로 바꿨습니다 — 한국어 번역 모드를 같이 쓰면 '[Off]'가 영어 화면에서도 '[꺼짐]'으로 바뀌어 보였습니다.
- 업데이트 뒤 브라우저가 예전 웹 화면 모양(CSS)을 계속 쓰지 않도록 했습니다.

**6. README: 같이 써도 되는 모드 표**
- 게임이나 코드로 확인한 모드만 버전과 함께 적었습니다(ORBIT, Black Division, ABPS·APBS, Bot Callsigns 등).
- **Harmony Profiles (PBT) 1.0.0** — 코드로 확인. PMC 이름·레벨·장비·전적만 다루고 봇 AI는 건드리지 않습니다. PBT의 성격은 전적·채팅용이라 SAIN 전투 성격과 따로입니다.
- **Twitch Players 3.1.2** — 코드로 확인(Bot Callsigns 필요). 방송인 이름 봇에 Wreckless·SnappingTurtle 성격이 그대로 들어가고, 같이 오는 `Twitch Players` 프리셋도 zzap에서 열립니다. F6에서 PMC '성격 지정'을 직접 정하면 그 값이 먼저입니다.

**확인**
- 게임 F6와 크롬 웹 화면에서 한국어·영어로 직접 눌러 봤습니다: 묶음 고르기, 묶음 편집, 슬롯 저장(바뀐 값만 담김)·적용·이름 바꾸기·삭제, 공유 코드 복사·붙여넣기, NEW·바뀐 항목 필터, 3단계 안내. 그날 게임 로그에 SAIN 오류 없음.
- 실제 프리셋 저장(`[Modified]` 사본, 저장 순서, 묶음 밖·전역 설정 안 바뀜)은 자동 시험으로 확인했습니다. 레이드에서 봇이 슬롯 값대로 싸우는지는 아직 따로 보지 않았습니다.
- PBT·Twitch Players는 코드로만 확인했습니다(게임에서는 아직).

**알려진 점**
- **되돌리기**를 눌러도 위쪽 '저장 안 한 변경' 개수가 줄지 않습니다(Remix7.2부터 있던 동작). 저장하면 정상으로 돌아옵니다.
- AI(SAIN/게임 기본) 변경은 게임을 다시 켜야 적용됩니다.
- 문제가 있으면 스크린샷과 `BepInEx/LogOutput.log`를 보내 주세요. Remix7.2로 되돌리려면 SAIN.REMIX.7.2.7z를 다시 덮어쓰면 됩니다(슬롯 파일은 남아도 문제없음).

------------------------------------------------------------------------------------------------------------------------------------------------------

**<Changes (English)>**

**Previous release:** Remix7.2 (`remix7.2`, published 11/02, 13 commits since) → **this: Remix7.3**, a bot settings screen overhaul for F6 and the web page. Bot behaviour and zzap preset values are the same as Remix7.2, and saving works as before (the bundled zzap presets save to a `[Modified]` copy).

**Install / update:** extract `SAIN.REMIX.7.3.7z` into your SPT folder (where `EscapeFromTarkov.exe` is) and overwrite. It holds both the client (`BepInEx`) and the server mod (`SPT_Runtime`), and both changed this time (new web file `wwwroot/js/bot-settings.js`). Settings and presets are kept. On a clean install, keep your `[Modified]` and own preset files, and `ClientData/botSettingsSlots.json` if you want to keep your custom slots.

**1. Edit several bot types at once (F6 and web)** - click the circle next to a name, or Ctrl+click the name, to add it to the selection. A faction's circle selects the whole faction; a partly selected faction shows a count such as `1/2`. Clicking a name alone opens that bot and keeps the selection. Level, the nine knobs and advanced values then apply the same value to every selected bot, and a setting shows "mixed" when the selected bots differ. Bots outside the selection and global settings are not touched, and bots locked to EFT's own AI (vanilla) are skipped.

**2. Custom slots and share codes** - **＋ Save current** keeps the shown bot's values in a slot (3 to start, up to 9); click a slot to apply it to the open bot or the whole selection. The `[...]` menu renames, overwrites, deletes or copies a **share code** (`SAIN1-...`); **Paste share code** turns a friend's code into a new slot. A code holds only the slot name, Level, AI choice and changed values. Slots live in the server mod's `ClientData/botSettingsSlots.json`, separate from presets, so they survive preset changes and are shared by F6 and the web page. Saving or applying a slot does not save the preset - press **Save** at the top for that.

**3. New only / Changed only / per-setting Reset** - Advanced and the Global settings tab get **New only** and **Changed only** filters. Settings added in the Remake7 line show `NEW` (right now: "bots walk around open door leaves"). Changed rows get a yellow dot and a **Reset** button that puts that setting back to the bot's own original (it was "Default").

**4. 3-step guide** - the first time you open bot settings, a yellow bar offers the **3-step guide** (choose bots → Level and knobs → save, slots and sharing). **Got it** hides it; the **?** at the top right opens it again.

**5. Layout clean-up** - F6 and the web page now share one layout: "N unsaved changes · Save · ?" on the top line, numbered steps (01 Level, 02 knobs, 03 Advanced), a one-line AI choice, and the same faction order on the web as in F6 (Scav → PMC → ...). The tab "Global settings (all)" is now "Global settings", and the web tab's `NEW` badge no longer shows just "N". English: "1 bot type" instead of "1 bot types"; the Advanced button now reads "Advanced settings: Off", because with the Korean translation mod installed a bracketed "[Off]" was shown as "[꺼짐]" even in English. The browser no longer keeps an old copy of the web page's stylesheet after an update.

**6. README: compatible mods table** - only mods checked in game or by code, with versions. **Harmony Profiles (PBT) 1.0.0** (code): handles PMC names, levels, gear and stats only, not bot AI; its personalities are for stats and chat, separate from SAIN's combat personalities. **Twitch Players 3.1.2** (code, needs Bot Callsigns): streamer-named bots still get Wreckless or SnappingTurtle, and its bundled `Twitch Players` preset opens in zzap; a personality you set for PMCs in F6 takes priority.

**Checked** - clicked through in game (F6) and in Chrome (web), in Korean and English: selecting, group editing, slot save (changed values only) / apply / rename / delete, share code copy and paste, NEW and changed filters, the 3-step guide; no SAIN errors in that day's game logs. The real preset save (`[Modified]` copy, save order, nothing outside the selection or global changed) is covered by the automated tests. Not yet watched in a raid with slot values applied. PBT and Twitch Players are checked by code only.

**Known issues** - **Undo** does not lower the "unsaved changes" count (since Remix7.2); saving clears it. AI (SAIN / EFT default) changes apply after a game restart. Send a screenshot and `BepInEx/LogOutput.log` if anything is off. To go back, overwrite with SAIN.REMIX.7.2.7z (a leftover slot file is harmless).

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
