# SAIN-zzap--Bootleg- remake2 릴리즈 노트 (remake1 대비)

> remake1(2026-09-29 22:09 KST) 이후 변경분입니다. 아래 "꼭 읽어주세요" 공지 글은 이전 릴리즈와 동일하게 붙이면 됩니다.

**규모:** 커밋 11개 · C# 파일 47개 변경 · 코드 약 +1,000줄.
remake1이 "봇이 싸우는 방식"을 새로 만든 판이었다면, remake2는 **램·안정성 판**입니다. 제보받은 "램 90%"를 봇끼리 시뮬 4판으로 추적해서 고쳤고,
서서 죽는 시체의 진짜 원인을 찾았습니다.

---

**<변경점>**

- **램 (메모리)**

1. 지난 레이드가 램에 남던 누수 수정: 두 번째 판부터 **이전 판 전체(플레이어·장비·아이템)가 다음 판 내내 램에 남아** 있었습니다. (remake1에서 생긴 문제)
2. 로그를 꺼 두면 로그 문장을 아예 만들지 않습니다. EFT는 레이드 중에 메모리 정리를 꺼 두기 때문에, 버려진 로그 문장도 레이드 끝까지 쌓였습니다.
   진단 로그도 기본으로 꺼져서 LogOutput.log에 수천 줄씩 쌓이던 것도 없어졌습니다.
3. 시체 피·총알 자국 텍스처 재사용: EFT는 맞은 부위마다 자국 텍스처를 하나씩 만들고 **시체 것까지** 레이드 끝까지 들고 있습니다.
   이제 죽은 지 30초 지나면 자국을 지우고 다음 피격에 다시 씁니다. (시체와 장비는 그대로, 피 자국만 사라짐. F6 성능 메뉴에서 끌 수 있음)
4. 참고 — 사망이 많을수록 램이 늘어나는 건 대부분 **봇 장비 모델**입니다. 봇이 처음 보는 종류의 무기·부품·장비를 들고 나올 때마다 게임이 그 모델을 불러와서
   레이드 끝까지 들고 있습니다. 시뮬 측정: 모드 무기·부착물을 많이 허용한 APBS 설정에서 사망당 약 293MB → 모드 무기 끄면 약 115MB.
   **램이 부족하면 봇 장비 모드(APBS 등)의 모드 무기·부착물 허용 범위를 줄이는 것이 가장 효과가 큽니다.**

- **서서 죽는 시체 (근본 원인 수정)**

1. 봇이 죽을 때 EFT는 먼저 "시야 컬링 끄기"를 하고 그다음에 쓰러짐 처리를 하는데, **다른 모드가 고친 컬링 함수가 오류를 내면 쓰러짐 처리 전체가 건너뛰어졌습니다.**
   이제 컬링 단계 오류는 무시하고 쓰러짐 처리를 계속합니다.
2. 안전장치 2개 추가: 죽음 이벤트를 받는 코드를 하나씩 따로 실행(하나가 오류 나도 나머지 진행), 체력은 0인데 1.5초 넘게 시체가 없으면 쓰러짐 처리와 두뇌 끄기를 직접 실행.

- **코너·문을 지킬 때 벽 보는 문제**

1. 지키는 봇과 조준할 곳 사이가 벽에 막혀 있으면(벽 뒤 깊숙이 웅크리고 벽만 보던 것), 코너·문 쪽으로 **조준선이 처음 열리는 자리**까지 옮겨서 지킵니다.
   처음 열리는 자리라 몸은 최소로만 드러납니다. 지키기, 문 전술(가두기·매복·오버워치), 분대 각(크로스파이어·엄호)에 적용. 엄폐 중에는 적용 안 함.

- **프리셋**

1. 프리셋이 매번 테스트 프리셋으로 바뀌던 버그 수정 (원본 SAIN이 저장된 이름을 "포함" 여부로 찾아서 `zzap`이 `zzap TEST ...`로 잡힘).
2. 처음 설치하거나 이 포크로 처음 덮어쓸 때 **한 번만** `zzap` 프리셋이 자동 선택됩니다. 직접 만든 프리셋을 쓰던 분은 그대로 유지됩니다.
3. F6에서 "프리셋 버전이 다르다"는 경고가 계속 뜨던 것 수정.

- **기타**

1. 새 봇이 들어올 때 SAIN 준비가 중간에 끊기던 원본 SAIN 버그 수정 (이미 사라진 플레이어 정보를 읽다가 오류).
2. `[시뮬 전용]` 프리셋: 40구 넘는 오래된 시체를 숨기지 않고 실제로 삭제 (램 측정용 실험, 실제 플레이용 `zzap`에는 없음).

^^7

------------------------------------------------------------------------------------------------------------------------------------------------------

**Changes** (since remake1 — 11 commits, 47 C# files, ~1,000 lines)

remake1 rebuilt how bots fight; remake2 is the **RAM and stability** release. The "RAM at 90%" report was tracked down over 4 bot-vs-bot sims,
and the real cause of the standing corpses was found.

- **RAM**

1. Fixed a leak where **the whole previous raid (players, gear, items) stayed in memory through the next raid**, from the second raid on. (introduced in remake1)
2. No log text is built while logging is off. EFT turns memory cleanup off during raids, so thrown-away log strings piled up until the raid ended.
   Diagnostic logs are off by default too, so LogOutput.log no longer fills with thousands of lines.
3. Corpse blood/bullet-hole textures are reused: EFT makes one decal texture per hit body part and keeps them, **corpses included**, until the raid ends.
   30s after a death the marks are wiped and the texture is reused for the next hit. (Corpses and gear stay; only the blood marks go. Can be turned off in F6 Performance.)
4. Note — most of the RAM that grows with deaths is **bot gear models**: every new kind of weapon/part/gear a bot spawns with is loaded and kept until the raid ends.
   Sim measurement: ~293 MB per death with a wide APBS mod weapon/attachment allowlist, ~115 MB with mod weapons off.
   **If you're short on RAM, narrowing the mod weapons/attachments your bot gear mod (APBS etc.) allows helps the most.**

- **Standing corpses (root cause fixed)**

1. When a bot dies, EFT first turns its view culling off and only then handles the death (ragdoll etc.). **If another mod's patched culling function threw, the whole death handling was skipped.**
   Errors in that culling step are now ignored and the death continues.
2. Two safety nets: each death-event handler runs on its own (one failing no longer stops the rest), and a player at 0 HP with no corpse after 1.5s gets the death handling and brain shutdown run directly.

- **Holding a corner/door while staring at the wall**

1. If the wall is between a holding bot and what it aims at (crouched deep behind the wall), it steps toward the corner/door to **the first spot where the line opens** (least exposure).
   Applies to holds, door tactics (trap, ambush, overwatch) and squad angles (crossfire, covering a mate). Not while in cover.

- **Presets**

1. Fixed the preset switching to the test preset every launch (upstream SAIN matched the saved name with "contains", so `zzap` picked `zzap TEST ...`).
2. On first install (or the first time this fork is dropped over an older build) the `zzap` preset is selected **once**. Players on a preset of their own keep it.
3. Fixed the F6 "preset version differs" warning.

- **Misc**

1. Fixed an upstream SAIN bug where a new bot's setup stopped halfway (it read data of a player that no longer existed).
2. `[시뮬 전용]` (simulation) preset: corpses beyond 40 are deleted instead of hidden (RAM measurement experiment; not in the playing preset `zzap`).

^^7
