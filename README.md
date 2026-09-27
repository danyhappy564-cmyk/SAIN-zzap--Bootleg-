### ⚠️ IMPORTANT NOTICE / DISCLAIMER

**Original Author:** Solarint
**Original Repository:** SAIN - Solarint's AI Modifications
**Original Link:** https://github.com/Solarint/SAIN
**License:** See upstream repository
**This Port By:** R_F (danyhappy564-cmyk) — unofficial, AI-assisted port. Not affiliated with or endorsed by the original author.

1. **Reflection & Take-Downs:** I deeply reflect on the ECOT incident. As an AI-assisted "vibe coder," I will immediately delete files if the original authors ask.
2. **No Re-Distribution:** These ported builds are unverified, temporary fixes. Please do NOT re-upload or share them anywhere else.
3. **Do Not Pester Original Authors:** Never report bugs or pester original modders regarding issues from my unofficial ports.
4. **Full Credit & Respect:** I will always credit original creators on GitHub and prioritize their decisions above all else.
5. **Support Original Creators:** Instead of using my ports, please visit the original authors' Forge pages to leave kind words or tips.

---

# SAIN (zzap fork)

> **원작자 · 원본**
> **Solarint** — https://github.com/Solarint/SAIN · SAIN(Solarint's AI Modifications)
> 4.x 유지보수: **ArchangelWTF** — https://github.com/ArchangelWTF/SAIN
>
> 상류 **v4.5.1**(SPT 4.1) 기준 포크입니다.

SAIN은 EFT 봇 AI를 통째로 갈아끼우는 대형 모드입니다. 이 포크는 ① **봇이 문 앞에서 낑기는 버그**를 고치고,
② 실제 고인물 플레이를 흉내 낸 **전술 행동**을 SAIN 전투 레이어 안에 추가하고, ③ 바로 쓸 수 있는 **zzap 프리셋**을 같이 넣었습니다.

변경 이력은 [`changelog/CHANGELOG.md`](changelog/CHANGELOG.md), 문 끼임 수정 상세는 [`changelog/FIXES.md`](changelog/FIXES.md)에 있습니다.

## 추가한 기능

**행동(전술)** — 전부 SAIN 전투 레이어(`SAIN : Combat Layer`) 안의 결정이라 **ORBIT과 수정 없이 호환**됩니다.

| 기능 | 하는 일 | 누가 | F6 설정 위치 |
|---|---|---|---|
| 점프 피킹 | 복도에서 문 앞으로 점프해 방 안을 보고 바로 점프해서 복귀. 닫힌 문은 문틀 옆에서 먼저 엶 | GigaChad / Chad | `General > Door Tactics (zzap)` |
| 런바이 피킹 | 점프가 위험한 곳(낮은 천장·계단)에선 문 앞을 달려 지나가며 보고, 돌아서 한 번 더 지나감 | GigaChad / Chad | 〃 `Run-By Peek Chance` |
| 스텝 피킹 | 피킹 후 문틀에서 짧게 2~3번 들락날락 (서기/앉기, 방 좌/우 모서리 번갈아) | GigaChad / Chad | 〃 `Step Peeks After Jump Peek` |
| 페이크 수류탄 | 문틀에서 수류탄 꺼내는 소리만 내고 즉시 집어넣음. 적이 문 근처에 있을 때만, 뛰는 소리 들리면 취소 | GigaChad / Chad | 〃 `Fake Grenade` |
| 페이크 치료·스팀 | 치료/주사 소리만 내고 손에 드는 순간 취소. 출혈·중상이면 안 함(진짜 치료 우선) | GigaChad / Chad | 〃 `Fake Heal / Stim` |
| 방 가두기 | 문 옆에 붙어 문을 지킴 (문은 그대로 둠) | GigaChad / SnappingTurtle | 〃 `Room Trap` |
| 문 옆 매복 | 문 옆에 낮은 자세로 조용히 대기 | Rat | 〃 `Room Trap` |
| 분대 역할 | 한 명이 문 전술을 하면 동료는 오버워치(교차 각) / 후방 경계 | 분대 | 〃 `Squad Roles` |
| 제3자 감지 | 문 전술 중 다른 적이 보이거나 25m 안에서 소리 나면 즉시 문을 버리고 대응 | 전원 | 자동 |
| 다이아몬드 스텝 | 사격 중·근접 난전(DogFight) 중 WASD를 와다다 누르듯 짧게 끊어 움직임 (탭 0.13초, 좌우 60% · 앞뒤 40%) | GigaChad / Chad / Wreckless | `General > Close Combat (zzap)` |
| 권총 전환 | 교전 중 탄창이 비고 적이 30m 안에 보이면 장전 대신 권총 | PMC | 〃 `Pistol Swap When Empty` |
| 빠른 장전 | 교전 중엔 R 두 번처럼 탄창을 버리고 빠르게 장전 | PMC | 〃 `Quick Reload In Combat` |
| 장거리 무기 전환 | 2번 슬롯 DMR/저격총을 80m 넘는 적에게 사용, 가까워지면 주무기로 | PMC | 〃 `Long-Range Weapon Swap` |
| 등 안 돌리기 | 가까운 적 앞에서 엄폐로 뛸 때 등을 보이지 않고 조준한 채 걸어서 이동 (3m 안 엄폐물은 대시) | PMC | 〃 `No Back Turning Near Enemy` |
| 제압사격 절제 | 안 보이는 적에게 난사하다 탄창 비우는 것 방지 (짧은 점사, 탄 60% 이상 유지) | PMC | 〃 `Suppression Discipline` |
| 재배치 | 먼저 맞았으면 엄폐 → 치료 → 수류탄 → 적이 못 보는 각으로 이동 | PMC | `General > Reposition (zzap)` |
| 미끼 피킹 | 엄폐에서 앞 점프로 정보 확인 후 복귀, 이후 짧은 사격 피킹 좌우 교대 | 공격형 성격 | 〃 `Bait Peek` |
| 가짜 재장전 | 탄창 확인 동작(소리)으로 재장전하는 척 후 적이 올 각을 조준 | PMC | 〃 `Fake Reload` |
| 수류탄 신관 선택 | 실내·근거리는 짧은 신관, 실외 원거리는 긴 신관 | PMC | 〃 `Fuse Selection` |
| 분대 교전 | 크로스파이어 각, 엄호, 동료 사망 시 트레이드 | 분대 | `General > Squad Combat (zzap)` |
| 정지 매복 | 적이 가까울 때 멈춰서 코너 조준 (맞으면 즉시 해제) | Rat / SnappingTurtle / Coward (zzap 프리셋 기준) | `General > Freeze Ambush (zzap)` |
| 폭발음 반응 | 25m 안 수류탄 폭발이면 모르던 봇도 던진 사람을 적으로 인식 | 전원 | 자동 |
| 피격 음성 끔 | 맞을 때마다 내던 "윽 / 맞았다" 음성 제거 | 전원 | `Talk > Bot Pain Voice On Hit (zzap)` |

**버그 수정**

| 수정 | 내용 |
|---|---|
| 문 낑김 | 문 감지 범위, 스턱 체크, 대기모드 진입 시 문 상호작용 멈춤, 문 상태 워치독(1.2초) 등 — 상세는 [`changelog/FIXES.md`](changelog/FIXES.md) |
| 문 자동 닫기 끔 | 이동 중 열린 문을 닫았다 여는 루프 제거 (`Door Tactics (zzap) > Bots Close Open Doors In Their Way`, 기본 꺼짐) |
| Waypoints 문 링크 | DrakiaXYZ-Waypoints가 만든 문 링크의 뒤바뀐 필드 대응 |
| 전술장비 딸깍 반복 | 켤 광원이 없는 총에서 1초마다 켜기 재시도하던 것 → 2번 실패 시 60초 쉼 |
| 손전등 눈뽕 | 적이 보이면 0.1~0.35초 안에 켬 |

## zzap 프리셋

서버 모드 폴더 `SAINServerMod/Presets/`에 두 개가 들어 있고, SAIN 프리셋 목록(F6 또는 `https://127.0.0.1:6969/sain/presets`)에 바로 뜹니다.

| 프리셋 | 용도 |
|---|---|
| `zzap` | 실제 플레이용. 사격·조준·반응은 원본 TwitchPlayers(하드코어) 값, 행동은 이 포크의 전술 |
| `zzap TEST` | 테스트용. `zzap`과 같고 모든 PMC가 기가채드 문 전술(100%), 재배치 항상, 다이아몬드 스텝 전원, 런바이 100%, 상세 로그 |

**ORBIT 호환 설정(두 프리셋 공통):**
- 성격 분포 — Rat 10 / Wreckless 5 / SnappingTurtle 5 / Coward 5 / Chad 5 / Timmy 3 / GigaChad 3 (ORBIT README 권장값)
- `General > Extract > SAIN Extract Behavior` **끔** — 탈출은 ORBIT이 담당
- `General > Looting Bots > Bot Extraction From Loot` **끔** — 루팅 후 탈출도 ORBIT 담당

**BotCallsigns / TwitchPlayers 없이 작동합니다.** 둘이 깔려 있어도 상관없습니다 — TwitchPlayers는 자기 프리셋을 따로 설치하고
TTV 닉네임→성격 매핑만 SAIN에 **추가**할 뿐 선택된 프리셋을 바꾸지 않으므로, `zzap`을 선택해 두면 그대로 적용되고 TTV 닉네임 성격 매핑도 같이 먹힙니다.

⚠️ 모드를 새 버전으로 덮어쓰면 `Presets/zzap.json`도 덮어써집니다. 직접 값을 고쳐 쓰려면 SAIN 편집기에서 **다른 이름으로 저장**해서 쓰세요.

## 버전 / 호환

**상류 v4.5.1 (SPT 4.1) 기준입니다.** 이전 판은 4.4.3(SPT 4.0) 기준이었는데, 상류 v4.5.1 위로
문 수정 4개를 그대로 옮겨왔습니다.

동기화하면서 확인한 것:

- 상류 v4.4.3 → v4.5.1 은 커밋 30개 / 403파일이지만 **문·끼임 관련 수정은 하나도 없습니다.**
  위 4개 버그는 상류에 그대로 남아 있고, 이 포크에만 고쳐져 있습니다.
- 리베이스 충돌은 0건이었습니다. 다만 충돌이 없다고 맞는 건 아니라서, 수정이 건드리는
  심볼(`CanInteract`, `_nextDoorUpdateTime`, `DoorDataStruct.LastCloseTime` /
  `.CurrentSqrMagnitude`, `OnPathComplete`, `BotComponent.DoorOpener`)을 v4.5.1 코드에서
  **하나씩 대조**했습니다. 전부 그대로 있고 시그니처도 같습니다.
- `CheckObjectInWay` 는 시그니처를 바꿨는데(`out RaycastHit` 추가), 호출부가 v4.5.1에도
  **그 한 곳뿐**이라 깨지는 곳이 없습니다.
- `OnPathComplete` 는 상류가 **선언만 해두고 호출하지 않던** 이벤트입니다. 이 포크에서
  `SAINMoverClass.PathComplete`가 실제로 발생시킵니다.

### 상류 버그 하나를 같이 고쳤습니다

**상류 v4.5.1 은 서버 모드가 아예 빌드되지 않습니다.** `SainSectionEditor.razor` 의 foreach
변수 이름이 `section` 이라 `@section.Name` 이 멤버 접근이 아니라 **`@section` 지시문**으로
파싱됩니다:

```
RZ2005: The 'section' directive must appear at the start of the line
RZ9986: Component attributes do not support complex content
```

변수명만 바꿔서 해결했습니다. 상류 태그를 그대로 체크아웃해도 똑같이 실패하는 걸 확인했으니
이 포크가 만든 문제가 아닙니다.

`SptVersion` 도 상류의 `4.1.3` 에서 **`4.1.5`** 로 올렸습니다 (실제로 돌리는 서버 버전).
그대로 빌드됩니다.

## 빌드

```
dotnet build SAIN.slnx -c Release
```

클라(`SAIN`, netstandard2.1) + 서버(`SAINServerMod`, net10.0) 두 프로젝트입니다.
Release 빌드하면 `release/` 에 배포용 zip 도 같이 나옵니다 (`-p:SkipPackage=true` 로 끕니다).

참조는 `References/` 폴더(상류가 SPT 4.1용으로 갱신함)와 NuGet에서 가져옵니다.
클라 쪽은 **BepInEx 전용 피드**(`https://nuget.bepinex.dev/v3/index.json`)에서
`BepInEx.Core 5.*` 와 `UnityEngine.Modules 2022.3.43` 를 받아야 합니다 — `nuget.config` 에
이미 등록돼 있습니다.

## 라이선스

원작 Solarint의 라이선스를 따릅니다.
