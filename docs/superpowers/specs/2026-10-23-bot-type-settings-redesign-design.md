# 봇 종류별 설정 화면 개편 — 설계 (1/4)

- 날짜: 2026-10-23, 브랜치 `feature/1023-bot-settings-ui` (Remix6.2 준비 커밋 `37a2eef5`에서 갈라짐)
- 범위: F6 '봇 종류별' 탭과 웹 '봇 종류별' 화면. 설정 4탭 개편(봇 종류별 → 성격 → 장비 은밀성 → 전역) 중 첫 번째.
- 목업: `F:\코딩\Index\클로드방\sain-ui-redesign\.superpowers\brainstorm\2017-1792757032\content\bot-layout.html` (사용자 승인 A)

## 1. 왜

사용자(2026-10-23): "블디·군즈 등 난이도 개별 변경이 쉬움/보통/어려움 이렇게 되어 있어서 설정이 된 건지 안 된 건지도 모르겠고, 뭐가 뭔지 모르겠음, 숫자가 너무 많고 뜻 모름."

지금 화면(`SAIN/Preset/Editor/BotSelectionClass.cs`, `SAINServerMod/Web/Components/BotSettingsEditor.razor`):
- 봇 종류(여러 개) × 난이도(4칸)를 골라야 값이 보이고, 값 줄이 봇 × 난이도만큼 늘어난다.
- 난이도는 게임이 스폰할 때 붙이는 값인데, 어떤 봇이 실제로 어느 난이도로 나오는지 화면이 알려 주지 않는다. 17:43 시뮬에서 블디는 전부 '보통' — '어려움' 칸을 바꿔도 효과가 없다.
- 값 49개가 날것의 숫자, 14개는 설명이 없다.

## 2. 성공 기준

1. 봇 하나를 고르면 그 봇이 최근 레이드에서 실제로 어떤 난이도로 나왔는지 보인다.
2. 쉬운 손잡이 9개로 그 봇의 주요 성질을 바꿀 수 있고, 기본은 난이도 4칸 전부에 적용된다.
3. 각 손잡이와 고급 숫자에 기준(zzap 프리셋) 대비 '강하게/약하게'와 실제 값, 되돌리기가 있다.
4. F6과 웹이 같은 구성·같은 계산을 쓴다.

## 3. 화면 (F6·웹 공통)

**왼쪽**: 검색 칸 + 봇 목록(기존 `BotType.Section` 묶음: PMC·스캐브·보스·추종자·모드 봇…). 줄마다
- 실제 스폰 난이도 배지(초록, 예: `보통`, `보통·어려움`), 기록 없으면 `기록 없음`(회색)
- 기준과 다른 값이 하나라도 있으면 노란 점
- 한 번에 하나만 고른다(여러 개 동시 선택 없앰).

**오른쪽 위**: 봇 이름, `최근 레이드 N판: 보통 12명` 한 줄, **적용할 곳** 버튼 줄 `[난이도 4칸 전부] [쉬움] [보통] [어려움] [불가능]` — 기본 '전부', 기록에서 한 번도 안 나온 난이도는 `(안 나옴)` 흐리게(고를 수는 있음). 오른쪽 끝 `기준: zzap 프리셋`, **'이 봇 설정을 다른 봇에 복사'** 버튼.

**손잡이 9개** (3묶음, 빠른 설정과 같은 줄 모양: 이름+짧은 설명 | 기준 눈금이 있는 막대 | `▲ 강하게 · 20% 빠름` + 실제 값 `2.0초 → 1.6초` | 되돌리기).

**맨 아래**: `▸ 고급 — 숫자 전부 보기 (49개 · 바꾼 값 N개)` 접힘.

## 4. 손잡이 정의

| id | 이름 | 묶는 값 (`SAINSettingsClass` 안) | 표시 값 | 강한 쪽 |
|---|---|---|---|---|
| aimSpeed | 조준 속도 | `Aiming.MAX_AIM_TIME`, `Aiming.DistanceAimTimeMultiplier`, `Aiming.AngleAimTimeMultiplier`, `Aiming.FIRST_CONTACT_ADD_SEC` — 같은 배율 | MAX_AIM_TIME 초 | 작을수록 |
| scatter | 탄 퍼짐 | `Shoot.AUTOMATIC_FIRE_SCATTERING_COEF`, `Aiming.COEF_IF_MOVE` — 같은 배율 | 연사 퍼짐 배 | 작을수록 |
| headshot | 머리 노리기 | `Aiming.AimForHead`(켜기) + `Aiming.AimForHeadChance`(%) | 켜짐 · 33% | 클수록 |
| sight | 시야 거리 | `Core.VisibleDistance` | m | 클수록 |
| spotting | 발견 속도 | `Core.GainSightCoef` | 배 | 클수록 — 전역 빠른 설정 'gainSight'와 같은 통로(`BotDifficultyClass` → `RuntimeVisionEffectK`), 거기서 클수록 빨리 알아챔(`QuickSettings.Spotting`) |
| hearing | 소리 듣기 거리 | `Core.HearingDistanceMulti` | 배 | 클수록 |
| aggression | 공격성 | `Mind.Aggression` | 배 | 클수록 |
| personality | 성격 지정 | `Mind.PersonalityOverride` | 성격 이름 / 지정 안 함 | — |
| grenade | 수류탄 쓰기 | `Core.CanGrenade` | 켜짐/꺼짐 | — |

- 성격 지정은 지금 이름이 '성격 지정 (모드 봇용)'이지만 코드상 모든 봇 종류에 이미 있다. 이름을 '성격 지정'으로, 설명에서 '모드 봇용'을 뺀다. 블디 6종 기본 GigaChad는 그대로(`SAINMindSettings.DefaultPersonalityOverride`).
- 손잡이 정의는 공용 코드 한 곳(`SAIN.Preset.Shared/Preset/BotQuickSettings.cs`, 새 파일)에 둔다. 기존 `QuickSettings.cs`의 `QuickSetting`(Get/Set/Describe/DeltaText/HigherIsStronger/UnitKo…) 모양을 따르되, 대상이 '봇 종류 하나 + 적용할 곳'이다.

### '전부' 동작

- 표시 값 = 스폰 기록에서 가장 많이 나온 난이도의 값, 기록 없으면 `normal`.
- 숫자 손잡이: 새 값 / 표시 값 = r, 4칸의 묶인 값 전부에 × r(난이도 간 차이 유지). 표시 값이 0이면 4칸 모두 새 값으로.
- 켜기/끄기·성격: 4칸 모두 같은 값.
- 한 칸만 고르면 그 칸만.
- 기준 값(막대 눈금)은 bundled zzap 프리셋의 같은 봇·같은 표시 난이도 값. 빠른 설정이 기준 프리셋을 읽는 방식(`QuickSettingsGUI._referenceNames`, 웹 `PresetQuickSettings`)을 그대로 쓴다.
- 되돌리기 = 그 손잡이가 묶은 값들을 기준 프리셋의 각 칸 값으로(적용할 곳 범위 안에서).

## 5. 실제 스폰 난이도 기록

- **클라 수집**: SAIN 봇이 만들어질 때(`BotComponent` 초기화 쪽, `Info.Profile.WildSpawnType` + `BotDifficulty`) 레이드 집계 사전에 +1. 레이드 끝(`BotManagerComponent.Dispose` 정리 자리 근처)에 그 판 집계를 서버로 보내고 비운다. 시뮬레이션 랩 판도 센다.
- **전송·보관**: 기존 `ServerDataClient`(`/sain/data/save`, `/sain/data/get`) 키 하나(`spawnDifficulties`)에 최근 5판 목록 `[{time, map, counts: {role: {difficulty: n}}}]`. 6판째가 오면 가장 오래된 판을 버린다(서버 쪽에서 합침 — 클라는 한 판만 보냄).
- **읽기**: F6은 탭을 열 때 한 번 `/sain/data/get`, 웹은 서버 서비스에서 직접. 5판 합계로 배지·한 줄 문구를 만든다.
- **기록 없음**: 배지 `기록 없음`, 오른쪽 위 `아직 기록 없음 — 레이드를 한 판 하면 나옵니다`, 표시 난이도 `normal`.
- 실패(서버 안 됨 등)는 경고 한 줄 남기고 화면은 '기록 없음'처럼 동작.

## 6. 고급 (숫자 전부)

- 기존 그룹(조준·시야·이동·성격·사격·산포…)별, 줄마다 한국어 이름·설명·기준값·지금 값, 바뀐 줄 노란 점 + 되돌리기.
- 설명 없는 14개는 새로 쓴다(`[Description]` + `KoreanSettingText`).
- 이름에 `VANILLA EFT CONFIG VALUE :`가 붙은 값은 맨 아래 '게임 기본값' 묶음으로.
- 적용할 곳은 위쪽 선택을 따른다. '전부'일 때 숫자 줄도 같은 비율 규칙.
- 기존 `AttributesGUI.EditFloatBoolInt`(F6)·`SainFieldEditor`(웹) 편집 부품을 재사용한다.

## 7. 다른 봇에 복사

- 버튼 → 봇 목록이 체크 칸으로(섹션별 '전부 선택'), `복사` 누르면 지금 봇의 4칸 값 전체(성격 지정 포함, 장비 은밀성·성격 설정 제외)를 고른 봇에 덮어씀. 웹의 '모든 난이도·모든 봇에 복사' 두 버튼을 대체.

## 8. 코드 나누기

| 위치 | 할 일 |
|---|---|
| `SAIN.Preset.Shared/Preset/BotQuickSettings.cs` (새) | 손잡이 9개 정의, '전부' 배율 계산, 표시 난이도 고르기, 되돌리기 |
| `SAIN.Preset.Shared/Preset/SpawnDifficultyRecord.cs` (새) | 기록 형식, 5판 합치기·합계 |
| `SAIN/…` 클라 | 수집·전송(`SpawnDifficultyRecorder`), F6 `BotSelectionClass` 새로 그리기 |
| `SAINServerMod/…` | 기록 보관(키 `spawnDifficulties` 합치기), 웹 `BotSettingsEditor.razor` 새로 그리기 |
| `SAIN.Preset.Shared/Localization/*` | 새 문구·설명 14개·성격 지정 이름 |

- 웹 화면은 Codex(`codex exec`, 자기 worktree)에 맡기고 Claude가 검토·병합한다.
- 프리셋 저장 형식은 바꾸지 않는다(새 필드 없음 — 손잡이는 기존 값을 읽고 쓰기만).

## 9. 확인

- 오프라인 시뮬 도구 selftest에 공용 계산 검사 추가: 4칸 같은 비율, 0 값, 되돌리기 정확히 원래 값, 표시 난이도 고르기, 5판 합치기.
- `dotnet build -c Release` 오류 0, 경고 수 그대로(클라 3, 서버 16).
- 웹: 사용자가 서버를 켜면 Claude가 브라우저 창에서 직접 눌러 보고 스크린샷.
- F6: 사용자 게임 확인 — 블디 선택 → 손잡이 이동 → 저장 → 다음 레이드 반영(일지·F8).
- 봇 행동 코드는 안 바뀌므로 행동 비교 시뮬 불필요. 스폰 기록 수집은 레이드 중 코드라 인게임 시뮬 1판으로 오류 없음과 기록 생성 확인.

## 10. 안 하는 것

- 성격·장비 은밀성·전역 탭(다음 설계 2~4).
- 손잡이 값 범위·기본값 같은 스타일 값 변경.
- 여러 봇 동시 편집(복사 버튼으로 대체).
