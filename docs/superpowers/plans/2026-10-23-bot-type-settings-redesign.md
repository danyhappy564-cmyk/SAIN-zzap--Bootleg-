# 봇 종류별 설정 화면 개편 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** F6·웹 '봇 종류별' 화면을 봇 하나 + 쉬운 손잡이 9개 + 실제 스폰 난이도 표시 + 접힌 고급 숫자로 바꾼다.

**Architecture:** 손잡이 정의·'전부' 배율 계산·스폰 기록 형식은 `SAIN.Preset.Shared`에 한 번만 두고(F6·웹 공용, 빠른 설정 `QuickSettings.cs`와 같은 방식), 클라는 스폰 난이도를 세어 레이드 끝에 기존 `/sain/data/save`로 저장, F6·웹은 그 기록과 공용 정의로 화면만 그린다. 프리셋 저장 형식은 그대로.

**Tech Stack:** C# (.NET, Unity IMGUI for F6, Blazor + MudBlazor for web), SPT 4.1 server mod, 오프라인 시뮬 selftest(`F:\코딩\레포\Cluade_For_spt\tools\sain-offline-sim\host`).

**Spec:** `docs/superpowers/specs/2026-10-23-bot-type-settings-redesign-design.md`

## Global Constraints

- 브랜치 `feature/1023-bot-settings-ui`, 커밋 작성자 R_F(`-c user.name=R_F -c user.email=danyhappy564@gmail.com`), AI 트레일러 없음, 푸시 금지.
- 프리셋 JSON에 새 필드 없음 — 손잡이는 기존 필드를 읽고 쓰기만.
- 매 프레임 할당 금지(F6 IMGUI): 목록·문구는 캐시, `string.Format`은 값이 바뀔 때만.
- 주변 코드 관례: zzap 수정 주석은 `// zzap (2026-10-23 ...)`, 한/영 문구는 `T(ko, en)` 또는 `EditorText`/`KoreanSettingText`.
- 빌드: `dotnet build SAIN -c Release -nologo -v q` 경고 3, `dotnet build SAINServerMod -c Release -nologo -v q` 경고 16 — 늘리지 않음.
- selftest: `cd F:\코딩\레포\Cluade_For_spt\tools\sain-offline-sim\host && dotnet run -c Release -- --selftest` 전부 통과(지금 86개 + 새 것).
- 기준 = bundled zzap 프리셋(F6 `PresetSync.TryGetCustomBundle("zzap", ...)`, 웹 `PresetEditorBody`의 `Zzap`).
- 오른쪽 = 항상 강하게(`HigherIsStronger`).

## Review Focus

- 기준 프리셋에 그 봇 종류가 없음(모드 봇은 서버 등록이라 bundled zzap에 없을 수 있음) → 기준 = 지금 저장된 값, 막대·문구는 '기준 없음', 예외 없음. (Task 2 테스트 `Knob_reference_missing_bot`)
- 봇 그룹에 난이도 칸이 4개 다 없음 → 있는 칸만 바꾸고 없는 칸은 건너뜀. (Task 2 `Knob_all_scope_skips_missing_difficulty`)
- 표시 값이 0인데 '전부'로 움직임 → 4칸 모두 새 값(배율 계산 0 나누기 없음). (Task 2 `Knob_all_scope_zero_shown`)
- 스폰 기록 JSON이 비었거나 깨졌거나 옛 형식 → '기록 없음'으로 동작, 예외 없음. (Task 4 Step 2b 게임 확인 + Task 6 역직렬화 try; 형식 자체는 Task 1 `SpawnRecord_json_roundtrip`)
- 손잡이를 여러 번 움직인 뒤 되돌리기 → 부동소수 누적 없이 기준 값과 정확히 같음. (Task 2 `Knob_reset_exact_after_moves`)

---

### Task 1: 스폰 기록 형식 (공용)

**Files:**
- Create: `SAIN.Preset.Shared/Preset/SpawnDifficultyRecord.cs`
- Test: `F:\코딩\레포\Cluade_For_spt\tools\sain-offline-sim\host\SelfTest\BotSettingsSelfTests.cs` (새, `SelfTests.cs`의 `AllTests()`에 `foreach (var t in BotSettingsTests()) yield return t;` 추가)

**Interfaces:**
- Produces:
  - `public sealed class SpawnRaidCounts { public string Time; public string Map; public Dictionary<string, Dictionary<string, int>> Counts = new(); }` (role 이름 = `ESainWildSpawnType.ToString()`, difficulty 이름 = `ESainBotDifficulty.ToString()`)
  - `public static class SpawnDifficultyRecord`
    - `public const string DataKey = "spawnDifficulties";`
    - `public const int KeepRaids = 5;`
    - `public static List<SpawnRaidCounts> Append(List<SpawnRaidCounts> existing, SpawnRaidCounts raid)` — existing null이면 빈 목록으로, 끝에 붙이고 최근 `KeepRaids`판만 남긴 새 목록
    - `public static Dictionary<ESainBotDifficulty, int> Totals(List<SpawnRaidCounts> raids, ESainWildSpawnType role)` — null·빈 목록·모르는 이름은 건너뜀
    - `public static ESainBotDifficulty ShownDifficulty(Dictionary<ESainBotDifficulty, int> totals)` — 가장 많은 것, 동률은 낮은 번호, 비었으면 `normal`
- JSON은 공용에서 다루지 않는다(netstandard2.1, `SimLabModels.cs`처럼 양쪽이 같은 필드 이름으로 직렬화): 클라 `SPT.Common.Utils.Json`, 서버 `System.Text.Json`(IncludeFields). 읽기 실패는 각 쪽에서 빈 목록(Task 4·6).

- [ ] **Step 1: 실패하는 selftest 작성** — `SpawnRecord_append_keeps_last_five`(6판 Append → 길이 5, 첫 판 Time이 두 번째로 넣은 것; Append(null, raid) → 1판), `SpawnRecord_totals_and_shown`(normal 12 + hard 3 → Totals[normal]=12, Shown=normal; 비었으면 normal; easy 2·hard 2 → easy; Counts에 모르는 역할·난이도 이름이 있어도 예외 없음), `SpawnRecord_json_roundtrip`(System.Text.Json IncludeFields로 직렬화 → 역직렬화 → Totals 같음 — 서버 쪽 형식 확인).
- [ ] **Step 2: selftest 실행 → 빌드 실패(타입 없음) 확인**
- [ ] **Step 3: `SpawnDifficultyRecord.cs` 구현**
- [ ] **Step 4: selftest 실행 → 전부 통과**
- [ ] **Step 5: 커밋** — SAIN 저장소 `SpawnDifficultyRecord.cs`, KB 저장소 selftest 파일(그 두 파일만 `git add`).

### Task 2: 손잡이 9개 정의와 계산 (공용)

**Files:**
- Create: `SAIN.Preset.Shared/Preset/BotQuickSettings.cs`
- Test: `BotSettingsSelfTests.cs` (Task 1 파일에 추가)

**Interfaces:**
- Consumes: `SAINSettingsGroupClass.Settings: Dictionary<ESainBotDifficulty, SAINSettingsClass>`, `SAINSettingsClass.Aiming/Core/Mind/Shoot`, `SAINMindSettings.DefaultPersonalityOverride`.
- Produces:
  - `public enum BotKnobKind { Number, Toggle, ToggleNumber, Personality }`
  - `public sealed class BotKnob` — `Id, Group("aim"|"sense"|"mind"), NameKo, NameEn, HelpKo, HelpEn, Kind, Min, Max, Step, HigherIsStronger, UnitKo, UnitEn`, `Func<SAINSettingsClass, float> Get` (표시 값), `Action<SAINSettingsClass, float> SetScaled`(표시 값 기준 배율 r로 묶인 값 전부 × r), `Action<SAINSettingsClass, float> SetDirect`, `Action<SAINSettingsClass, SAINSettingsClass> CopyFrom`(되돌리기: 묶인 필드 전부를 기준 칸에서 복사), Toggle용 `Func<SAINSettingsClass,bool> GetOn`/`Action<SAINSettingsClass,bool> SetOn`, `Personality` 용 `GetPersonality/SetPersonality`, `string DeltaText(float value, float reference, bool korean)`(빠른 설정 `QuickSetting.DeltaText`와 같은 문구 규칙 — 그 코드를 재사용하거나 같은 헬퍼로 뽑는다)
  - `public static class BotQuickSettings`
    - `public static readonly BotKnob[] All` — 순서·묶는 필드는 스펙 4절 표 그대로: aimSpeed(MAX_AIM_TIME, DistanceAimTimeMultiplier, AngleAimTimeMultiplier, FIRST_CONTACT_ADD_SEC; 표시 MAX_AIM_TIME; 작을수록 강함), scatter(AUTOMATIC_FIRE_SCATTERING_COEF, COEF_IF_MOVE; 표시 AUTOMATIC_FIRE_SCATTERING_COEF; 작을수록), headshot(AimForHead + AimForHeadChance), sight(VisibleDistance), spotting(GainSightCoef, 클수록), hearing(HearingDistanceMulti), aggression(Aggression), personality(PersonalityOverride), grenade(CanGrenade)
    - `public static void Apply(BotKnob knob, SAINSettingsGroupClass group, ESainBotDifficulty? scope, ESainBotDifficulty shown, float newValue)` — scope null = 전부: 표시 칸 값 v0, v0 ≠ 0 이면 각 칸 SetScaled(r = newValue / v0), 아니면 각 칸 SetDirect(newValue); 칸이 없으면 건너뜀. scope 있으면 그 칸 SetDirect.
    - `public static void ApplyToggle(...)`, `ApplyPersonality(...)` — 같은 scope 규칙, 값 그대로
    - `public static void Reset(BotKnob knob, SAINSettingsGroupClass group, SAINSettingsGroupClass reference, ESainBotDifficulty? scope)` — reference null 또는 칸 없음 → 그 칸 그대로
    - `public static bool IsChanged(BotKnob knob, SAINSettingsGroupClass group, SAINSettingsGroupClass reference)` — 4칸 중 하나라도 묶인 필드가 기준과 다르면(float은 1e-4) true; reference null → false
    - `public static bool AnyChanged(SAINSettingsGroupClass group, SAINSettingsGroupClass reference)` — 손잡이 + 고급 49개 전부(왼쪽 노란 점)
- Min/Max/Step은 그 필드의 `[MinMax]` 속성 값을 쓴다(새 스타일 값 금지).

- [ ] **Step 1: 실패하는 selftest 작성** — 4칸 그룹을 만들어(easy 2.4 / normal 2.0 / hard 1.6 / impossible 1.2 MAX_AIM_TIME, 나머지 묶인 필드도 칸마다 다르게):
  - `Knob_all_scope_keeps_ratio`: shown=normal, Apply(aimSpeed, null, normal, 1.6) → normal 1.6, easy 1.92, hard 1.28, impossible 0.96, DistanceAimTimeMultiplier도 모두 ×0.8
  - `Knob_single_scope_only_that`: scope=hard → hard만 바뀜
  - `Knob_all_scope_zero_shown`: normal 값 0 → Apply 0.5 → 4칸 모두 0.5
  - `Knob_all_scope_skips_missing_difficulty`: impossible 칸 없는 그룹 → 예외 없음, 나머지 3칸 바뀜
  - `Knob_reset_exact_after_moves`: Apply 3번(1.7, 1.3, 2.9) → Reset → 4칸 모든 묶인 필드가 기준과 `==`
  - `Knob_reference_missing_bot`: Reset(reference null) 예외 없음·값 그대로, IsChanged false
  - `Knob_toggle_and_personality_all`: ApplyToggle(grenade, null, false) → 4칸 false; ApplyPersonality(GigaChad) → 4칸 GigaChad
  - `Knob_definitions_match_spec`: All.Length 9, Id 순서 `aimSpeed, scatter, headshot, sight, spotting, hearing, aggression, personality, grenade`, aimSpeed/scatter `HigherIsStronger == false`, 나머지 숫자형 true
- [ ] **Step 2: selftest 실행 → 실패 확인**
- [ ] **Step 3: `BotQuickSettings.cs` 구현** — 되돌리기는 배율 역산이 아니라 기준 칸 필드 복사(`CopyFrom`)라서 정확히 같아진다.
- [ ] **Step 4: selftest 통과**
- [ ] **Step 5: 커밋**

### Task 3: 설정 이름·설명 정리와 고급 목록 묶음 (공용)

**Files:**
- Modify: `SAIN.Preset.Shared/BotSettings/SAINSettings/Categories/SAINMindSettings.cs` (PersonalityOverride `[Name]`/`[Description]`), 설명 없는 14개 필드가 있는 Categories 파일들
- Modify: `SAIN.Preset.Shared/Localization/KoreanSettingText.cs`
- Create: `SAIN.Preset.Shared/Preset/BotAdvancedFields.cs`
- Test: `BotSettingsSelfTests.cs`

**Interfaces:**
- Produces: `public static class BotAdvancedFields { public static IReadOnlyList<(string Group, FieldInfo Category, FieldInfo Field)> List(); }` — `SAINSettingsClass`의 카테고리 필드 순서대로, `[Hidden]` 제외, `[Name]`이 `VANILLA EFT CONFIG VALUE :`로 시작하면 Group `"vanilla"`로 맨 끝. F6·웹이 이 목록으로 고급을 그린다.
- 이름: `"Personality Override"` / `"성격 지정"`, 설명에서 '모드 봇용' 문구 제거(블디 기본 GigaChad 문장은 유지). 옛 키 `"Personality Override (Modded Bots)"` 번역 줄은 지운다.

- [ ] **Step 1: 실패하는 selftest** — `Advanced_all_fields_described`(List()의 모든 Field에 `[Description]` 비어 있지 않음, 한국어 이름·설명이 `KoreanSettingText`에 있음), `Advanced_vanilla_last`(vanilla 묶음이 모두 끝, 개수 = 이름이 VANILLA로 시작하는 필드 수), `Advanced_personality_name`(PersonalityOverride 이름 "Personality Override").
- [ ] **Step 2: 실패 확인**
- [ ] **Step 3: 14개 설명(영/한) 작성, 이름 변경, `BotAdvancedFields` 구현** — 설명은 '언제 무엇이 달라지나'를 한 문장으로(스펙 6절).
- [ ] **Step 4: selftest 통과, `dotnet build SAIN.Preset.Shared -c Release` 경고 그대로**
- [ ] **Step 5: 커밋**

### Task 4: 스폰 난이도 수집·저장 (클라)

**Files:**
- Create: `SAIN/Classes/BotManager/SpawnDifficultyRecorder.cs`
- Modify: `SAIN/Components/BotComponent.cs` (`InitializeBot`, `Info = new SAINBotInfoClass(this)` 뒤), `SAIN/Components/BotManagerComponent.cs` (`Dispose`의 `ClearRaidLists` try 옆에 별도 try)

**Interfaces:**
- Consumes: Task 1 `SpawnRaidCounts`, `SpawnDifficultyRecord.Append/DataKey`; `ServerDataClient.Get/Save`; `BotProfile.WildSpawnType`, `BotProfile.BotDifficulty`; `SainEnumMirrorExtensions`(WildSpawnType → ESainWildSpawnType), 난이도 `ToESain()`.
- Produces: `public static class SpawnDifficultyRecorder { public static void Count(ESainWildSpawnType role, ESainBotDifficulty difficulty); public static void FlushRaid(string map); public static List<SpawnRaidCounts> Load(); }` — Count는 사전 +1만(할당은 처음 보는 역할만). FlushRaid: 센 게 없으면 아무것도 안 함, 있으면 사본을 만들고 비운 뒤 `Task.Run`에서 Get → `Json.Deserialize<List<SpawnRaidCounts>>`(실패·null → 빈 목록) → Append → `Json.Serialize` → Save, 실패는 `Logger.LogWarning` 한 번. Load: Get → 같은 역직렬화, 실패 시 빈 목록.

- [ ] **Step 1: `SpawnDifficultyRecorder` 구현과 두 훅 연결** (`map` = `Singleton<GameWorld>.Instance?.LocationId`).
- [ ] **Step 2: `dotnet build SAIN -c Release -nologo -v q` → 오류 0, 경고 3**
- [ ] **Step 2b: 깨진 기록 확인** — 게임 폴더 `SPT_Runtime/user/mods/Solarint-SAIN-ServerMod/ClientData/spawnDifficulties.json`에 `{` 한 글자를 넣은 상태를 Task 7 게임 확인 목록에 넣는다(F6이 '기록 없음'으로 열리고, 레이드 끝에 정상 목록으로 덮어씀).
- [ ] **Step 3: 오프라인 시뮬 스모크(`--map factory --suite smoke`, 보이는 창, CRLF .cmd)로 링크된 코드 영향 없음 확인** — 새 파일은 1차 미링크라 카운터가 수정 전과 같아야 함.
- [ ] **Step 4: 커밋**

### Task 5: F6 봇 종류별 화면 새로 그리기 (클라)

**Files:**
- Modify (사실상 새로 씀): `SAIN/Preset/Editor/BotSelectionClass.cs`
- Modify: `SAIN/Preset/Editor/Localization/KoreanUIText.cs` (새 UI 문구)

**Interfaces:**
- Consumes: Task 1 `Totals/ShownDifficulty`, Task 2 `BotQuickSettings.*`, Task 3 `BotAdvancedFields.List()`, Task 4 `SpawnDifficultyRecorder.Load()`; 기존 `ZzapUI`(ListItem, Pill, Panel1, Search, PanelHeading, Separator), `AttributesGUI.EditFloatBoolInt`, `ConfigEditingTracker.Add`, `PresetSync.TryGetCustomBundle("zzap", out SAINPresetBundle)`, `SAINPlugin.LoadedPreset.BotSettings.SAINSettings[WildSpawnType]`.
- Produces: `BotSelectionClass.Menu()` (같은 진입점 — 탭 쪽 코드 변경 없음).

- [ ] **Step 1: 왼쪽 열** — 검색 칸, `BotType.Section` 묶음, 줄마다 배지(Totals 0이면 `기록 없음`, 아니면 나온 난이도 이름 `·` 연결)와 `AnyChanged` 노란 점, 단일 선택. 배지·점 문구는 기록을 다시 읽을 때와 저장 때만 다시 만든다(캐시).
- [ ] **Step 2: 오른쪽 위** — 이름, `최근 레이드 N판: 보통 12명`/`아직 기록 없음 — 레이드를 한 판 하면 나옵니다`, 적용할 곳 Pill 5개(기본 전부, 안 나온 칸 `(안 나옴)`), `기준: zzap 프리셋`, '다른 봇에 복사' 버튼.
- [ ] **Step 3: 손잡이 9줄** — `QuickSettingsGUI`의 줄 그리기(막대·기준 눈금·DeltaText·되돌리기)를 재사용하거나 같은 모양으로; 값 변경 시 `BotQuickSettings.Apply…` + `ConfigEditingTracker.Add`.
- [ ] **Step 4: 고급 접기** — `BotAdvancedFields.List()` 묶음별, 줄마다 이름·설명·기준값·노란 점·되돌리기, 적용할 곳 규칙(전부면 배율), vanilla 묶음 끝.
- [ ] **Step 5: 복사 화면** — 체크 칸 목록(섹션별 전부 선택), `복사` → 고른 봇의 4칸을 지금 봇 4칸 값으로(`Mind.PersonalityOverride` 포함), `ConfigEditingTracker`에 표시.
- [ ] **Step 6: 빌드 오류 0, 경고 3** → 커밋. (게임 확인은 Task 7 뒤 사용자)

### Task 6: 웹 봇 종류별 화면 새로 그리기 (서버, Codex)

**Files:**
- Modify (사실상 새로 씀): `SAINServerMod/Web/Components/BotSettingsEditor.razor`
- Modify: `SAINServerMod/Web/Components/PresetEditorBody.razor` (`<BotSettingsEditor ... Zzap="Zzap" />`)
- CSS: 빠른 설정이 쓰는 기존 클래스(`sain-refchip`, `sain-q*`)를 재사용, 필요하면 같은 css 파일에 추가.

**Interfaces:**
- Consumes: Task 1~3 공용 API, `ClientDataStorageService.GetAsync(SpawnDifficultyRecord.DataKey)` → `System.Text.Json` IncludeFields 역직렬화(실패·null → 빈 목록), 기존 `SainFieldEditor`, `Bundle: SAINPresetBundle`, `Zzap: SAINPresetBundle?`, `OnChanged`.
- Produces: 같은 컴포넌트 이름·파라미터(+`Zzap`).

- [ ] **Step 1: Codex 브리프 작성**(`F:\코딩\Index\코덱스방\sain-1023-botui-web\BRIEF.md`: 스펙 3·5·6·7절, 이 Task의 Interfaces, F6과 같은 문구, 금지: 공용 코드 변경·빌드 외 실행·커밋) → `codex exec -s workspace-write` 를 worktree `F:\코딩\진행중\sain-wt-codex`(브랜치 `codex/1023-botui-web`, `feature/1023-bot-settings-ui`에서)에서 실행.
- [ ] **Step 2: Claude 검토** — 웹 '모든 난이도/모든 봇 복사' 두 버튼이 복사 화면으로 바뀌었는지, 문구가 F6과 같은지, 기준 없는 봇에서 예외 없는지.
- [ ] **Step 3: 병합, `dotnet build SAINServerMod -c Release -nologo -v q` 오류 0·경고 16** → 커밋.
- [ ] **Step 4: 사용자가 서버를 켜면 브라우저 창에서 블디 선택 → 손잡이 이동 → 저장 → 다시 열어 값 유지, 스크린샷.**

### Task 7: 마무리 — 변경 이력·빌드·설치

**Files:**
- Modify: `README.md` (`## 변경 이력` 최신 1줄, 개수), `changelog/CHANGELOG-024.md` (또는 10개 차면 025 새 파일)

- [ ] **Step 1: 변경 이력 한 줄(KST, 쉬운 말, '게임 미확인')**
- [ ] **Step 2: Release 빌드 두 개(경고 3·16) + selftest 전부 통과**
- [ ] **Step 3: 게임 폴더 설치**(게임·서버 꺼짐 확인, `-LiteralPath`, 백업 `F:\코딩\진행중\sain-mapdata\install\<stamp>`, 해시 일치)
- [ ] **Step 4: 커밋, NOTES·방명록 기록(그 두 파일만 add)**
- [ ] **Step 5: 사용자 확인 요청** — F6에서 블디 선택 → 배지 '보통' 보이는지, 조준 속도 손잡이 이동 → 저장 → 인게임 시뮬 1판 뒤 일지/F8에서 반영, 스폰 기록이 생기는지(`ClientData/spawnDifficulties.json`).
