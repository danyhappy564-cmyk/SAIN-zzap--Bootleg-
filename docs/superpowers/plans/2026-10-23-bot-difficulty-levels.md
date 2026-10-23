# 봇 종류별 v2 (봇마다 난이도) + 성격 설명 번역 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 봇마다 쉬움/보통/어려움/불가능을 고르면 zzap 프리셋 순서 수준의 SAIN 값이 바로 써 넣어지고 이름 옆에 그 난이도가 보이게 한다; 성격 설명이 언어에 맞게 나오게 한다.

**Architecture:** 단계 계산은 공용 `BotLevels`(순수 함수, selftest)로 두고 F6·웹은 버튼·배지만 그린다. 단계별 전역 값은 bundled 'zzap 쉬움'/'zzap 보통'/'zzap' 번들에서 읽어 지금 프리셋 전역 값으로 나눠 상쇄한다. 고른 단계는 그룹에 필드 하나로 저장.

**Tech Stack:** C# (Unity IMGUI F6, Blazor/MudBlazor 웹), SAIN.Preset.Shared(netstandard2.1), 오프라인 시뮬 selftest.

**Spec:** `docs/superpowers/specs/2026-10-23-bot-difficulty-levels-design.md` (v1: `…-bot-type-settings-redesign-design.md`)

## Global Constraints

- 브랜치 `feature/1023-bot-settings-ui`, 작성자 R_F, AI 트레일러 없음, 푸시 금지.
- 게임 실행 코드(봇 행동) 변경 없음 — 화면·프리셋 값만.
- 프리셋 형식: `SAINSettingsGroupClass.DifficultyLevel` 하나만 추가.
- F6: 화면 값은 Layout 때만 계산(View 캐시), 쓰기는 전부 `ZzapUI.Defer` 경유(v1 최종 검토 규칙).
- 빌드 경고 클라 3·서버 16 그대로. selftest: `$TEMP/selftest_botui.sh`(기존 무관 실패 2개 `Action_unlinked_holds`·`Doors_suite_runs_and_is_deterministic`만 허용).
- 단계 배율 상수는 스펙 4절 값 그대로(불가능: 조준 시간 ×0.6, 퍼짐 ×0.7, 발견 ×1.5, 시야 ×1.2, 청각 ×1.2, 머리 노리기 ×1.5, 공격성 ×1.2).

## Review Focus

- 단계 번들('zzap 쉬움' 등)이 없는 환경(사용자가 지웠거나 서버가 안 보냄) → 그 단계 버튼만 꺼지고 예외 없음. (Task 2 `Level_missing_level_bundle`)
- 지금 프리셋의 전역 값이 0 → 나누기 대신 비율 1. (Task 2 `Level_zero_current_global`)
- 기준 봇이 zzap에도 SAIN 기본에도 없음(모드 봇) → 지금 값을 기준으로, 반복 전환해도 값이 틀어지지 않음(같은 단계 두 번 = 같은 값). (Task 2 `Level_no_reference_stable`)
- 그룹에 칸이 4개 다 없음 → 있는 칸만. (Task 2 `Level_missing_slots`)
- 영어 모드에서 한글 원문, 한글 모드에서 번역 없는 영어 → 성격 탭 0건. (Task 1 `Text_no_hangul_in_source`, `Text_personality_korean_complete`)

---

### Task 1: 성격 설명 번역 + 번역 검사

**Files:**
- Modify: `SAIN.Preset.Shared/Localization/KoreanSettingText.cs` (성격 설명 8문장 한국어)
- Modify: F6 성격 설명 표시 위치(`SAIN/Preset/Editor/BotPersonalityEditor.cs` 또는 그 탭이 쓰는 곳 — `PersonalitySettingsClass.Description`를 그리는 줄), 웹 `SAINServerMod/Web/Components/PersonalityEditor.razor:17-19`
- Test: `F:\코딩\레포\Cluade_For_spt\tools\sain-offline-sim\host\SelfTest\BotSettingsSelfTests.cs`

**Interfaces:**
- Produces: `public static string PersonalitySettingsClass.DescriptionOf(EPersonality p)` = `PersonalityDescriptions[p]`(없으면 빈 문자열). 화면은 이것을 번역해 보여 주고 프리셋의 `Description` 글자는 쓰지 않는다.

- [ ] **Step 1: 실패하는 selftest** — `Text_no_hangul_in_source`(GlobalSettingsClass·SAINSettingsClass·PersonalitySettingsClass 카테고리·장비 은밀성 타입의 모든 `[Name]`/`[Description]`, `PersonalityDescriptions` 값에 한글(U+AC00–U+D7A3) 없음), `Text_personality_korean_complete`(성격 카테고리 필드의 이름·설명, `PersonalityDescriptions` 8개가 `KoreanSettingText.Map`에 있음), `Text_missing_counts`(전역·장비 은밀성의 한국어 누락 개수를 계산해 출력만 — 항상 통과, 5·4번 작업용 숫자).
- [ ] **Step 2: 실패 확인**
- [ ] **Step 3: `DescriptionOf` 추가, 8문장 한국어, F6·웹 표시를 `DescriptionOf` + 번역으로 교체; 검사가 찾은 한글 원문은 영어로 옮기고 한국어는 번역표로**
- [ ] **Step 4: selftest 통과, 두 빌드 경고 그대로** → 커밋(SAIN·KB 각각 그 파일만)

### Task 2: 단계 계산 (공용)

**Files:**
- Create: `SAIN.Preset.Shared/Preset/BotLevels.cs`
- Modify: `SAIN.Preset.Shared/BotSettings/SAINSettings/SAINSettingsGroupClass.cs` (`DifficultyLevel` 필드), `SAIN.Preset.Shared/Enums/` 아래 `EBotLevel.cs`
- Test: `BotSettingsSelfTests.cs`

**Interfaces:**
- Produces:
  - `public enum EBotLevel { None, Easy, Normal, Hard, Impossible }`
  - `SAINSettingsGroupClass.DifficultyLevel` (`[DataMember]`, 기본 `None`)
  - `public sealed class LevelGlobals { public float VisibleDist, GainSight, Scattering, Hearing, AccuracySpeed, Recoil; public bool FasterCqb; public float PmcAimTime; public static LevelGlobals From(SAINPresetBundle b); }` (PmcAimTime = 번들 pmcUSEC normal 칸 MAX_AIM_TIME, 없으면 0)
  - `public sealed class LevelSources { public SAINSettingsGroupClass Reference; public LevelGlobals Current; public LevelGlobals Easy, Normal, Hard; public SAINSettingsGroupClass EasyBot, NormalBot, HardBot; }` (단계 번들 속 같은 봇, 없으면 null)
  - `public static class BotLevels`
    - `public static bool Available(EBotLevel level, LevelSources src)` — Easy/Normal은 해당 LevelGlobals 있어야, Hard·Impossible은 Hard 있어야
    - `public static SAINSettingsClass Compute(EBotLevel level, SAINSettingsGroupClass group, LevelSources src)` — 스펙 4절 식, 기준 = `src.Reference ?? group`의 normal 칸(없으면 첫 칸) 복사본에 적용, 범위 자르기
    - `public static void Apply(EBotLevel level, SAINSettingsGroupClass group, LevelSources src)` — Compute 결과의 단계 필드·손잡이 필드를 4칸 모두에 쓰고 `group.DifficultyLevel = level`
    - `public static bool IsAdjusted(SAINSettingsGroupClass group, LevelSources src)` — DifficultyLevel None이면 false, 아니면 Compute와 비교
    - `public static int AdjustedCount(SAINSettingsGroupClass group, LevelSources src)` — 다른 손잡이 수(경고 문구용)
    - `public static (EBotLevel Level, bool Adjusted, bool Custom) Badge(SAINSettingsGroupClass group, LevelSources src)` — 스펙 3절 규칙
- 쓰는 필드 목록은 스펙 4절 표 + 손잡이 9개 필드(성격 지정·수류탄 제외).

- [ ] **Step 1: 실패하는 selftest** — 가짜 번들 3개(전역: 쉬움 0.5/0.33/2/0.4/2/3/false, 보통 1.4/0.85/1/0.85/1/0.8/false, zzap 2/1/1/1/1/0.1/true; pmcUSEC 조준 2.0/0.6/0.3)로:
  - `Level_hard_is_reference_under_zzap`: 지금=zzap 전역 → Hard = 기준 값 그대로
  - `Level_easy_scales_by_globals`: 지금=zzap, 기준 시야 200 → Easy 시야 50(×0.25), 발견 ×0.33, 퍼짐 ×2, 청각 ×0.4, 조준 시간 = 쉬움 번들 같은 봇 값
  - `Level_current_preset_cancels`: 지금='zzap 쉬움' 전역 → Hard 시야 = 기준 × (2/0.5)
  - `Level_impossible_on_top_of_hard`: 조준 ×0.6, 퍼짐 ×0.7, 발견 ×1.5, 시야 ×1.2, 청각 ×1.2, 머리 노리기 ×1.5, 공격성 ×1.2 (범위 안에서)
  - `Level_recoil_clamped`: Easy 반동 ≤ 3
  - `Level_applies_all_slots_and_badge`: Apply(Hard) → 4칸 같은 값, Badge = (Hard,false,false); 손잡이 하나 바꾸면 (Hard,true,false), AdjustedCount 1
  - `Level_badge_unset`: None + 값이 Hard 계산값과 같음 → (Hard,false,false); 다름 → Custom true
  - Review Focus 4개(`Level_missing_level_bundle`, `Level_zero_current_global`, `Level_no_reference_stable`, `Level_missing_slots`)
- [ ] **Step 2: 실패 확인**
- [ ] **Step 3: 구현**
- [ ] **Step 4: selftest 통과** → 커밋

### Task 3: F6 v2

**Files:** Modify `SAIN/Preset/Editor/BotSelectionClass.cs`

**Interfaces:** Consumes Task 2 전부. `LevelSources`는 Layout 시 View.Update에서: Reference = zzap 번들 같은 봇 ?? `PresetSync.TryGetServerDefault(현재 SAIN 난이도…)` 같은 봇; Current = `LevelGlobals.From(지금 로드된 프리셋)`(F6은 `SAINPlugin.LoadedPreset.GlobalSettings`에서 같은 값 — `From`의 GlobalSettings 오버로드 추가 허용); Easy/Normal/Hard = `PresetSync.TryGetCustomBundle("zzap 쉬움"/"zzap 보통"/"zzap")`.

- [ ] **Step 1: 배지** — 목록 배지를 `Badge()` 결과로(쉬움/보통/어려움/불가능 + ` (조정됨)`, `직접 설정`), 기록 배지 제거; 배지는 기존 View 캐시 규칙(전체는 페이지 재진입·복사 뒤, 선택 봇은 매 갱신)
- [ ] **Step 2: 단계 버튼 4개 + 경고 줄** — `Available` 아닌 단계는 흐리게·툴팁, 누르면 `Write(() => BotLevels.Apply(...))`
- [ ] **Step 3: 손잡이 기준 = 단계 계산값**(고른 적 없으면 Hard 계산값), 칸 고르기·'지금 보는 칸'·'4칸 같음' 줄을 기본 화면에서 제거하고 고급 머리로 이동(고급 안의 scope는 고급 행 편집에만 적용, 손잡이는 항상 4칸)
- [ ] **Step 4: 빌드 오류 0·경고 3** → 커밋

### Task 4: 웹 v2 (Codex)

**Files:** Modify `SAINServerMod/Web/Components/BotSettingsEditor.razor`, 필요하면 `PresetEditorBody.razor`(단계 번들 3개 전달)

- [ ] **Step 1: Codex 브리프**(`F:\코딩\Index\코덱스방\sain-1023-botlevels-web\BRIEF.md`: 스펙 3절, Task 2 Interfaces, F6과 같은 문구, `LevelSources` 구성 — Reference = Zzap 번들 ?? `PresetGeneration` SAIN 기본 번들, Current = 편집 중 Bundle의 전역, 단계 번들 = 서버가 아는 bundled zzap 3개) → worktree `sain-wt-codex` 새 브랜치에서 실행
- [ ] **Step 2: 검토·적용, 서버 빌드 오류 0·경고 16** → 커밋
- [ ] **Step 3: 서버 켜지면 브라우저 확인**

### Task 5: 마무리

- [ ] **Step 1:** README·CHANGELOG 한 줄(게임 미확인)
- [ ] **Step 2:** Release 빌드 2개 + selftest
- [ ] **Step 3:** 최종 검토(새 검토자, 가장 좋은 모델) → Important 이상 수정
- [ ] **Step 4:** 게임 폴더 설치(게임·서버 꺼짐 확인, 백업, 해시), NOTES·방명록
- [ ] **Step 5:** 사용자 게임 확인 목록(스펙 7절)
