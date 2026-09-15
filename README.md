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

# SAIN (fork)

> **원작자 · 원본**
> **Solarint** — https://github.com/Solarint/SAIN · SAIN(Solarint's AI Modifications)
> 4.x 유지보수: **ArchangelWTF** — https://github.com/ArchangelWTF/SAIN
>
> 이 포크는 상류 **v4.5.1** 기준입니다. 기능은 원작 그대로고,
> **봇이 문 앞에서 낑기는 문제**만 고쳤습니다.

SAIN은 EFT 봇 AI를 통째로 갈아끼우는 대형 모드입니다 — 성격, 엄폐, 조준, 소리 반응, 스쿼드
행동. 이 저장소는 그중 **문 처리(`DoorOpener`, `BotPathData`)** 쪽 버그를 잡은 포크입니다.

## 왜 포크했나

레이드에서 봇이 문에 대가리를 박고 10~15초씩 서 있는 게 반복적으로 목격됐습니다. 파보니
원인이 하나가 아니라 **네 가지가 겹친 것**이었습니다.

## 고친 것

### 1. 문 감지 범위가 너무 좁았음 (`461f6e34`)

전투 중 도망치는 봇은 스프린트 조향 스무딩 때문에 **바라보는 방향과 실제로 밀착한 문이
안 맞습니다.** `RaycastToDoors`의 좁은 방향성 캐스트는 매 틱 그 문을 놓치는데, ORBIT의 문
충돌 패치는 (정상적으로) 통과를 막고 있으니 봇이 그냥 거기 끼어버립니다.

- `SPHERECAST_RADIUS` **0.15m → 0.3m**
- 그래도 방향성 캐스트가 전부 빗나가면, **이미 상호작용 사거리 안에 있는 가장 가까운 문**을
  그냥 고릅니다 (`doors` 후보는 `DoorDataStruct.InRangeToInteract`로 이미 걸러져 있음).
  방향이 우연히 맞아떨어질 때까지 기다리지 않습니다.

### 2. 스턱 체크가 봇을 문에서 **떼어내고** 있었음 (`aa25357d`)

봇이 아직 안 열린 문으로 전력 질주하면, `DoorOpener`의 스캔은 0.5초 폴링이라 스프린트 속도에
경주를 집니다 — 부딪히는 바로 그 순간 문이 등록되어 있지 않습니다. 그러면 `CheckStuck`이
그걸 그냥 "앞에 물체 있음"(문은 벽과 같은 충돌 레이어)으로 보고 **경로 재계산**을 요청하는데,
재계산된 경로가 다시 같은 문을 지나가서 무한 반복 — 겉보기엔 봇이 문에 계속 비비는 모습입니다.

`CheckObjectInWay`가 이제 **부딪힌 콜라이더를 같이 반환**합니다. 그게 `Door`면 `CheckStuck`은
벽처럼 우회하지 않고, `DoorOpener`가 폴링을 건너뛰고 **바로 다음 틱에 재스캔**하도록
강제합니다. 경로를 포기하는 것보다 문 상호작용에 우선순위를 줍니다.

### 3. …근데 그러면 못 여는 문에서 영원히 대기 (`56bb4f7d`)

2번 수정이 무조건적이라, **진짜로 못 여는 문**(잠김, `Operatable` 아님, 발로 안 차는 문)이면
탈출구 없이 계속 재확인만 하게 됐습니다. 대기를 **4초로 제한**했습니다 — 그 안에 안 풀리면
일반 장애물 처리로 떨어져서 봇이 우회합니다.

### 4. 봇이 자기가 연 문을 자기한테 닫음 (`cb0acfaf`)

필드 리포트: 도망/교전하며 문을 통과하던 봇이 **방금 자기가 연 문에 정면으로 처박혀서**
~15초간 멈췄다가 "포기"하고 다시 여는 현상.

`RaycastToDoors`는 열려 있는 문을 발견하면 무조건 "닫을 대상"으로 봅니다 — 그 문을 방금
자기가 열었고 아직 다 지나가지도 않았다는 개념이 없습니다. 전투 조향(백페달, 피격 반응,
그룹 대기) 중에는 봇이 문턱에서 어물거리는 시간이 깔끔한 통과보다 길어지기 쉬워서, 다음 문
재평가 때 **자기가 서 있는 문을 닫기로 결정**합니다.

문을 연 직후 **짧은 유예 시간** 동안은 그 문을 닫을지 재검토하지 않게 했습니다
(`LastCloseTime`이 그 시점에 이미 찍히고 있어서 그걸 그대로 씁니다). 봇이 문턱에서 얼마나
꾸물거리든 상관없습니다.

### 6. …그런데 4번은 실제로 한 번도 작동한 적이 없었습니다 (2026-09-15)

재보고: **"문 낑김 아직도 일어남. 문 열리고 닫히는 모션은 보임 ← 이게 선행 동작임."**
4번에서 넣은 유예 시간이 안 먹고 있었습니다. 코드를 다시 보니 **먹을 수가 없는
구조**였습니다.

`DoorDataStruct` 는 **구조체(struct)** 이고 리스트 두 개에 값으로 들어 있습니다.

1. `InteractWithDoor(ref data, ...)` 가 `LastOpenTime` / `LastCloseTime` /
   `LastInteractTime` 을 찍습니다
2. `TryInteractWithDoor` 가 그걸 `_interactionDoors[i] = data` 로 **`_interactionDoors`
   에만** 되돌려 씁니다. `_allDoors` 에는 안 씁니다
3. `SearchForDoors` 가 0.5초(`DOOR_UPDATE_INTERVAL`)마다 `_interactionDoors.Clear()`
   하고 **`_allDoors` 로부터 통째로 다시 만듭니다** → 찍어둔 시간이 전부 0으로 리셋
4. `_allDoors` 자체도 봇의 복셀이 바뀌면 새로 만듭니다. 그게 하필 **문을 지나갈 때**입니다

그래서 `RecentlySelfOpened` 는 거의 항상 `LastCloseTime == 0` 을 읽고 **"내가 연 문
아님"** 이라고 답합니다. 봇은 1초 전에 자기가 연 문을 다시 닫으러 갑니다. 그게 리포트에
적힌 **열림→닫힘→열림 루프**입니다. 같은 이유로 `DoorDataStruct.CanInteractByTime` 의
쿨다운 3개(`DOOR_INTERACTION_INTERVAL` / `DOOR_OPEN_INTERVAL` / `DOOR_CLOSE_INTERVAL`)도
전부 무효였습니다.

고친 방식: 시간 3개를 구조체에 의존하지 말고 **`DoorOpener` 안의 작은 딕셔너리**에
문 링크 id로 보관합니다. 리스트를 몇 번을 다시 만들든 살아남고, 봇이 죽으면 같이
사라집니다. 복원할 때는 **앞으로만** 갱신해서 이번 틱 값이 옛날 값으로 되돌아가지
않게 했습니다.

### 5. 빌드 경고 5개 (`94fbb4e6`)

억제(suppress)가 아니라 **원인을 고쳤습니다**: 안 쓰는 nullable 필드, 캡처된 `ref` 매개변수,
안 쓰는 생성자 매개변수 2건, 발화되지 않던 이벤트.

건드린 파일: `BotPathCorner.cs`, `SAINMoverClass.cs`, `CoverAnalyzer.cs`,
`PersonActiveClass.cs`, `PlayerSoundController.cs`, `CoverFinderComponent.cs`,
`PlayerComponent.cs`.

### 7. 리로드 판정이 매 틱 예외를 던지고 있었음 (2026-09-15)

한 라이드(7분)에 **완전히 같은 예외가 901번**, 초당 두 번꼴로 나왔습니다:

```
IndexOutOfRangeException
  InventoryController.GetAcceptableItemsNonAlloc<T>(EquipmentSlot[], ...)
  InventoryController.GetReachableItemsOfTypeNonAlloc<T>
  BotReload.GetMagazineForReload(Weapon)
  BotReloadMagazine.CanReload(...)
  SelfActionDecisionClass.TryReload            ← 여기서 부름
  ... BotDecisionManager.ManualUpdate → GameWorldUnityTickListener.Update
```

터지는 곳은 **BSG 자기 인벤토리 순회 안쪽**입니다. 어떤 봇의 장비가 열거에 쓰이는 슬롯
배열과 안 맞는 건데, 그 라이드에 로드된 어떤 모드도 저 메서드들을 패치하지 않습니다
(Harmony 로그 전수 확인). 그 인벤토리는 우리가 고칠 수 없습니다.

**우리가 통제하는 건 "그런데도 다음 틱에 또 물어본다"는 쪽입니다.** 예외를 던지는 건
공짜가 아닙니다 — 관리 스택을 통째로 캡처하고, 그게 **월드 틱 안, 메인 스레드**에서
초당 두 번씩 일어납니다. 라이드 프레임타임이 나오는 바로 그 자리입니다.

그래서 판정이 터지면 **그 봇만 10초간 리로드 검사를 건너뜁니다.** 결정은 그냥 "지금
리로드 못 함"으로 읽히는데, 이건 꺼낼 탄창이 없는 봇이 어차피 받았을 답입니다. 로그는
901줄 대신 세션당 한 줄만 남습니다. 봇이 회복하면(탄창을 줍든, 문제 아이템을 버리든)
몇 초 안에 다시 리로드합니다.

## 상태

- 1번은 필드 리포트 기반으로 고쳤고 **재현이 사라진 것까지 확인**했습니다.
- **4번은 확인이 틀렸습니다.** 코드는 들어갔지만 위 6번 때문에 실제로는 작동한 적이
  없습니다. 그때 증상이 줄어 보인 건 다른 이유였거나 우연입니다.
- 2·3·6·7번은 논리적으로는 맞지만 **재테스트 대기** 상태입니다.

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
