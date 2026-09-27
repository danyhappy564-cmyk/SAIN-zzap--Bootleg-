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

- 📜 **변경 이력:** [`changelog/CHANGELOG.md`](changelog/CHANGELOG.md)
- 🔧 **문 끼임 수정 상세 기록(1~15번):** [`changelog/FIXES.md`](changelog/FIXES.md)

## 최근 변경 (2026-09-28 06:49)

문 앞 수류탄·문 닫고 후퇴·폭발 후속·분대 브리칭·폭음 측면·한 탄창 이탈·고스트 우회 **제거**, 페이크 수류탄/치료 **개선**(바로 집어넣기, 적이 가까울 때만,
뛰는 소리 들리면 취소), 문 전술 중 **제3자 감지**, **다이아몬드 스텝**, 피격 음성 끔, **zzap 프리셋** 동봉. 자세한 건 변경 이력 참고.

## 현재 들어 있는 기능

| 분류 | 기능 | F6 위치 |
|---|---|---|
| 문 버그 | 문 감지/스턱/대기모드 낑김/문 상태 워치독 등 (FIXES.md 1~13번) | — |
| 문 버그 | 이동 중 열린 문 자동 닫기 끔 (열기만 함) | `General > Door Tactics (zzap) > Bots Close Open Doors In Their Way` |
| 문 전술 | GigaChad/Chad **점프 피킹 / 런바이 / 스텝 피킹**, 문틀 옆 대기 | `General > Door Tactics (zzap)` |
| 문 전술 | **페이크 수류탄 / 페이크 치료·스팀** (문틀에서, 적이 가까울 때만) | 〃 `Fake Grenade`, `Fake Heal / Stim` |
| 문 전술 | GigaChad/SnappingTurtle **방 가두기**(문 옆 대기), Rat **문 옆 매복** | 〃 `Room Trap` |
| 문 전술 | 분대 역할(오버워치 / 후방 경계), 제3자 감지 시 즉시 포기 | 〃 `Squad Roles` |
| 재배치 | 선제 피격 시 수류탄 후 재배치, 미끼 피킹, 가짜 재장전(탄창 확인), 수류탄 신관 선택 | `General > Reposition (zzap)` |
| 근접 교전 | **다이아몬드 스텝**(사격 중 좌우·앞뒤), 가까운 적 앞에서 등 안 돌림, 제압사격 절제 | `General > Close Combat (zzap)` |
| 분대 | 크로스파이어·엄호·트레이드 | `General > Squad Combat (zzap)` |
| 매복 | Rat/Turtle/Coward 정지 매복 (피격 시 해제) | `General > Freeze Ambush (zzap)` |
| 음성 | 피격 신음/"맞았다" 끔 | `Talk > Bot Pain Voice On Hit (zzap)` |

전술 행동은 전부 SAIN 전투 레이어(`SAIN : Combat Layer`) 안의 결정으로 들어가 있어서, 레이어 이름만 보는 **ORBIT과 수정 없이 호환**됩니다.

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
