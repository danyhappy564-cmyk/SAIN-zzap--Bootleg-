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

## 변경 이력

(2026-09-19 규칙 추가 이전 항목은 소급 기입하지 않습니다 — 그 이전 내용은
아래 "고친 것" 절의 번호별 섹션 참고.)

- 2026-09-21 — 가스 반경/지속시간 수치를 추측값 대신 CS 가스탄 모드
  (`Manimal-CSGas`) 자체 설정값으로 교체: 반경 6m → 8m(그 모드의
  `Plugin.GasMaxRadius` 기본값), 지속시간 20초 → 30초(CS 가스탄 아이템
  자체에 박혀있는 `EmitTime` 값). 그리고 블랙 디비전(가스마스크 필수
  장착이 컨셉인 별도 모드 팩션)은 CS 가스탄에 회피도, 능력치 저하도
  전혀 안 받도록 완전 면제 처리 — 다른 팩션은 그대로 영향받음.
  (CS 가스탄 자체를 아이템 ID로 정확히 특정하는 건 기술적으로 불가능함을
  확인함 — 그 아이템의 ID 정보는 SAIN이 추적하는 월드 오브젝트에서는
  아예 접근할 수 없는 별도 객체에만 있음. 대신 쓰는 "연막탄 계열 클래스
  + 연막 아님" 조합이 현재 설치된 모드 목록 기준으론 사실상 CS 가스탄
  전용 판별식임.)
- 2026-09-21 — CS 가스탄을 완전 무반응(연막탄 취급)으로 고쳤더니 봇이
  가스 안에서도 아무렇지 않게 정조준 사격하는 게 부자연스럽다는 피드백을
  받아 절충안으로 교체. 이제 (1) 던져진 직후 4초간은 기존처럼 흩어지고,
  (2) 그 이후엔 반응을 끄되, 가스 반경(6m) 안에 실제로 있는 동안은
  섬광탄 맞았을 때와 같은 방식(기존 `BotFlashedClass` 재사용)으로 명중률
  ·시야·청각이 일시적으로 깎임 — 이 효과는 계속 갱신해줘야 유지되고 안
  갱신하면 자동으로 꺼지는 구조라, 오브젝트가 안 사라져서 고착되던 예전
  버그 구조 자체가 재발할 수 없음. 구현 중 반응이 None으로 되돌아가는
  전환 자체를 막고 있던 별개의 조건문 버그도 같이 발견해 수정.
- 2026-09-21 — CS 가스탄(별도 모드 아이템)을 맞은 봉이 계속 사격을 안 하고
  도망/엎드리기만 반복하며 원상복귀 안 되던 문제 수정. 원인: SAIN이 "이게
  일반 수류탄인지 연막탄인지"를 아이템의 소리 속성으로만 판단했는데, CS
  가스탄은 소리 속성이 안 맞아서 일반 수류탄처럼 취급됐음 — 근데 실제
  객체는 일반 수류탄(몇 초면 사라짐)과 달리 훨씬 오래 남아있어서, 그동안
  계속 "아직도 위협임"으로 재판정되며 반응이 안 풀렸음(로그로 확인: 일반
  수류탄은 몇 초 안에 반응이 풀리는데 CS 가스탄은 500줄 넘게 안 풀림).
  아이템 종류 자체를 직접 확인하는 방식을 추가해서 CS 가스탄도 일반
  연막탄과 똑같이 무해한 걸로 인식하도록 수정.
- 2026-09-21 — (위 수정을 위한 사전 작업, 그 자체로는 동작 변경 없음)
  수류탄이 근처에 떨어지면
  봇이 흩어지거나 엎드리는데, 사용자 필드 리포트로는 그 상태에서 안
  돌아오고 계속 사격을 안 하는 것으로 보임. 코드상 정상 흐름은 수류탄이
  터지고 사라지면 그 반응도 같이 풀려야 하는데, 이 과정 전체가 로그에
  안 남고 있어서 실제로 어디서 막히는지 특정이 안 됐음 — 반응 시작/해제/
  추적 종료 세 지점에 로그를 남기도록만 추가. 다음 실전 로그로 원인
  특정 예정, 아직 버그 수정은 안 됨.
- 2026-09-20 19:22 — 봇이 대기 모드로 들어가는 순간 문을 여는 도중이었으면
  그 자리에서 몸이 문에 낑긴 채 얼어붙는 상황(아래 참고)이 실제로 얼마나
  자주 일어나는지 로그로 확인할 수 있게, 그 상황이 실제로 풀렸을 때 기록이
  남도록 추가.
- 2026-09-20 18:28 — 문이 3초 넘게 안 열리는 상태로 멈춰 있으면 강제로
  풀어주는 감시 기능이, 레이드 시작 직후엔 맵의 문을 하나도 못 찾고 있던
  버그 수정 — 맵이 다 로드되기 전에 문 목록을 스캔해서 매번 빈 목록만
  돌려주고 있었음(맵 로드가 끝날 때까지 기다렸다가 다시 스캔하도록 변경).
- 2026-09-20 17:35 — 위 감시 기능을, "SAIN이 직접 연 문"만이 아니라
  맵에 있는 모든 문을 대상으로 하도록 범위 확장(다른 봇이나 게임 기본
  AI가 연 문도 낑길 수 있는데 감시가 안 되고 있었음).
- 2026-09-20 17:28 — 문 근처에서 봇의 실제 달리기만 멈추고 "계속 달리고
  싶다"는 내부 의도 상태는 안 꺼서, 문 앞에서 플레이어를 만나도 총을 안
  쏘고 달리기만 하던 것처럼 보이던 문제 수정.
- 2026-09-20 08:08 — ORBIT이라는 별도 봇 AI 모드가 문 처리를 잘한다는
  참고를 받아 세 가지를 이식: ① 문이 3초 넘게 안 열리는 상태로 멈춰 있으면
  강제로 풀어주는 감시 기능 신설, ② 문 근처에서는 무조건 천천히 걷게 해서
  문이 열리며 몸이 튕기는 것 방지, ③ 봇이 멈췄는지 판단하는 기준에서
  높이(Y축) 흔들림 때문에 오탐 나던 것을 좌우(XZ) 위치만 보도록 수정.
- 2026-09-18 06:55 — 플레이어가 멀어지거나 전투가 끝나서 봇이 대기
  모드로 들어갈 때, 문을 여는 도중이었으면 몸이 문에 겹친 채 콜리전이
  꺼진 상태로 얼어붙던 문제 수정(낑김·문 통과 두 증상의 공통 원인).

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

> **⚠️ 원인 정정 (2026-09-15 저녁).** 위에서 "어느 봇의 장비가 열거 슬롯 배열과 안 맞는다"
> 고 썼는데, **봇 장비 문제가 아니었습니다.** 범인은 다른 모드입니다 — 아래 9절 참조.
> 이 절의 수정(백오프) 자체는 그대로 유효하지만, **원인 수정이 아니라 보험**입니다.

### 8. 예외 하나가 봇 전체를 세우고 있었음 (2026-09-15, 7번 후속)

7번을 넣기 전 로그에서 같은 예외가 **한 판에 2085번**으로 늘었습니다. 그런데 진짜
문제는 횟수가 아니라 **그게 어디까지 번지느냐**였습니다.

```
BotComponent.TickClassGroup          ← 클래스 루프, 가드 없음
BotComponent.ManualUpdate
BotManagerComponent.ManualUpdate     ← 봇 루프, 가드 없음
```

**둘 다 try/catch가 없었습니다.**

- `TickClassGroup` 은 클래스 목록을 그냥 돕니다. 하나가 던지면 그 뒤 클래스가 전부
  스킵되고, `ManualUpdate` 가 `TickClassGroup` 을 **네 번** 부르므로 그 봇의 나머지
  틱도 통째로 날아갑니다
- `BotManagerComponent.ManualUpdate` 는 `foreach` 로 봇을 돕니다. 한 봇이 던지면
  **그 뒤 봇들은 그 프레임에 SAIN 틱을 못 받습니다.** `HashSet` 순서는 사실상
  고정이라 매 프레임 같은 봇들이 굶습니다

보통 설치본이면 BSG 전투 레이어가 남아 있어서 버팁니다. 근데 같은 라이드 로그에
이게 있습니다:

```
[BlackDiv] [SainBrainFix] SAIN layers added and vanilla combat layers
           excluded for Black Division/Wedge (brain PMC only)
```

**그 봇들은 바닐라 전투 레이어가 제거돼 있어서 SAIN 말고 아무것도 안 돕니다.** 틱이
죽으면 그대로 정지합니다. 리포트가 정확히 그겁니다 — *"블디 애들 처음 탄창 비면
장전도 안 하고 안 쏘고 보고 있고."*

고친 방식: 두 루프 다 항목 단위로 격리합니다. 고장난 서브시스템은 **그 서브시스템만**
잃고, 고장난 봇은 **그 봇만** 잃습니다. 로그는 클래스당 1번 / 봇당 1번만 남깁니다.
7번의 리로드 로그에는 봇 `Role` 도 같이 찍게 했습니다 — 어느 봇의 인벤토리가 깨진
건지 다음 판 로그 한 줄로 특정됩니다.

### 9. 진짜 원인은 SAIN이 아니었습니다 — `Use Items Anywhere` 2.1.4

7·8번을 넣고 나서 판을 늘려가며 로그를 대조한 결과, **SAIN 문제가 아니었습니다.**

| 판 | UIA 버전 | `TryReload` IOOR |
|---|---|---|
| 17:29 쇄빙선 | 2.1.3 | **0** |
| 18:04 등대 | 2.1.3 | **0** |
| 20:49 / 21:11 / 21:34 쇄빙선 | **2.1.4** | 1802 / 4170 / 1918 |

같은 설치본, 같은 맵, 같은 모드 목록. **바뀐 변수는 UIA 버전 하나뿐**입니다. 그리고 그
라이드의 HarmonyX 로그 전수(패치 타겟 1184개)를 봐도 `GetAcceptableItemsNonAlloc` /
`GetReachableItemsOfTypeNonAlloc` 를 패치한 모드는 **0건**입니다 — 즉 코드가 아니라 **그
메서드가 읽는 데이터**가 바뀐 겁니다.

UIA 2.1.4 `Plugin.cs` 의 `ExtendFastAccessSlots()` 가 BSG의 **static** 필드
`Inventory.FastAccessSlots` 를 건드립니다.

```csharp
// 2.1.3 — 5개짜리로 "교체"
fastAccessSlots.SetValue(fastAccessSlots, ExtendedFastAccessSlots);

// 2.1.4 — 기존 것과 "합집합", 즉 무조건 길어짐. 게다가 Awake·Start 두 번 돈다
FastAccessSlotsField.SetValue(null, mergedSlots.ToArray());
```

static이라 **플레이어·봇 구분 없이 전부 같은 배열**을 씁니다. 그래서 SAIN이 붙은 봇 전체가
재장전을 못 하게 됩니다. `GetReachableItemsOfTypeNonAlloc` 이 IL 오프셋 **0**에서 터지는
것도 첫 명령이 `ldsfld Inventory::FastAccessSlots` 라는 뜻이라 들어맞습니다. 같은 라이드
trace 로그에 `FastAccess item <id> for index Item4 not found` 도 찍혀 있어서, `Item1`~`Item4`
같은 고정 폭 인덱스 공간을 배열이 넘어선 것으로 보입니다(마지막 한 줄은 추론, 나머지는 증거).

**조치: UIA를 2.1.3으로 내리면 됩니다.** 이 레포에서 고칠 건 없습니다.

**그럼 7·8번은 왜 남겨두나.** 8번이 이번 사고의 교훈 그 자체이기 때문입니다 — *남의 모드가
전역 static 하나를 바꿨는데 우리 봇이 전부 동상이 됐고*, 그게 가능했던 건 SAIN의 루프 두
개에 항목 단위 가드가 없어서였습니다. 원인이 밖에 있든 안에 있든, **한 예외가 봇 전체를
세우면 안 됩니다.** 7번 백오프도 같은 이유로 남깁니다(UIA를 고치면 한 번도 안 돕니다).

### 10. 문 상호작용이 봇 대기(standby) 진입 시 영원히 안 풀리고 있었음 — 낑김과 클리핑 둘 다 이게 원인 (2026-09-18)

재보고: **① 아직도 문에 낑김. 대신 플레이어가 가까이 오면 풀리고, 문을 뚫고 나오거나
정상으로 돌아옴. ② 문을 닫았는데 몸이 그냥 통과해버림.**

`TryInteractWithDoor`는 상호작용을 시작하며 문 콜라이더에
`IgnoreInteractionCollision(collider, true)`를 걸어 둡니다(안 그러면 문을 여는 애니메이션
중에 자기 몸이 문짝에 밀려납니다). 이걸 되돌리는 건 `DoorOpener.Clear()`인데, 이게 불리는
유일한 지점은 `SelectDoor()`가 `_doorInteractionEndTime`이 지난 걸 **다음 틱에** 발견하는
경우뿐입니다. `SelectDoor()`는 `BotPathData.InteractWithDoor()` 안에서만 불리고, 그건
`SAINMoverClass.CheckTickPath()` 안에서만 불리고, `CheckTickPath()`는
`SAINMoverClass.ManualUpdate()`에서 **`if (Bot.SAINLayersActive)` 블록 안에서만** 불립니다.

`SAINActivationClass`는 플레이어가 멀어지거나(스탠바이 판정), 게임이 끝나거나, 활성 레이어가
`None`이 되면 `SAINLayersActive`를 그 자리에서 `false`로 내리고 `Bot.Mover.Stop()`을
부릅니다. 그런데 `SAINMoverClass.Stop()`은 경로를 그 자리에서 안 끝냅니다 —
`_activePath.Cancel()`은 `CancelRequested` 플래그와 0.25초 뒤 시각만 세팅해 두고, 그 플래그를
실제로 처리하는 `CanProceedWithPath()`는 `TickPath()` 안에서만 불립니다. `SAINLayersActive`가
이미 꺼졌으니 `TickPath()`가 다시 안 불리고, **경로도 안 끝나고 `DoorOpener.Interacting`도
콜라이더 무시도 그대로 얼어붙습니다.**

봇이 문 상호작용 도중(`Interacting == true`) 이 타이밍에 걸리면:

- 봇은 문틀에 겹친 채로 멈추고 그 문과의 충돌은 계속 꺼져 있습니다 → **낑김**(밀려나지도,
  더 움직이지도 않음).
- 플레이어가 다시 가까워져서 봇이 재활성화되면, 다음 `SelectDoor` 틱이 (한참 지난)
  타임아웃을 보고 바로 `Clear()`를 불러 충돌을 되살립니다 — 봇이 문 메시에 겹쳐 있던
  채로 물리가 갑자기 돌아오니 **밀려서 문을 뚫고 나오거나**, 운 좋으면 제자리로
  밀려납니다.
- 재활성화가 다시 안 일어나거나 그사이 문이 닫히면, 콜라이더 무시가 그대로 남아서
  **몸이 닫힌 문을 그냥 통과**합니다.

고친 방식: `DoorOpener`에 `CancelInteraction()`을 새로 추가했습니다(공개 메서드,
`Interacting`일 때만 `Clear()` 호출). 이걸 `SAINActivationClass.SetActive(false)`와
`ManualUpdate()`의 `wasActive && !activeNow` 분기 — 즉 `Bot.Mover.Stop()`을 부르는 바로 그
자리 — 에서 같이 부릅니다. 봇이 대기 모드로 들어가는 바로 그 프레임에 문 충돌이 틱
시스템과 무관하게 즉시 복구되므로, 얼어붙은 채로 남는 시간이 없습니다.

건드린 파일: `SAIN/Classes/Bot/Doors/DoorOpener.cs`, `SAIN/Classes/Bot/SAINActivationClass.cs`.

### 11. ORBIT(SAIN 기반 별도 봇 AI 모드)의 문 낑김 대응책 3개 이식 (2026-09-20)

10번 이후에도 완화는 됐지만 낑김이 완전히는 안 없어진다는 재보고가 있었습니다. `ORBIT`(SAIN에
의존하는 별도의 봇 AI 모드, `Orbit/Systems/MovementSystem.cs` · `DoorSystem.cs`)을 참고용으로
클론해서 자체 문 처리 코드를 대조했습니다. ORBIT은 SAIN의 `DoorOpener`/`BotPathData`를 전혀
안 쓰고 독자적으로 재구현했지만, 마지막 단계는 SAIN과 **동일한 BSG API 호출 체인**
(`Door.Interact` → `Player.ExecuteInteraction`)입니다 — 그래서 ORBIT이 문서화한 문제 세 개가
SAIN에도 그대로 적용될 수 있습니다.

**① `Door.DoorState`가 `Interacting`에 영구 고착될 수 있음.** BSG의 문 상태 완료 콜백은
플레이어 쪽 애니메이션 이벤트에 묶여 있는데, 봇은 그 이벤트를 안 쏩니다. 그래서 봇이 연(또는
닫은) 문의 실제 `DoorState`가 `Open`/`Shut`으로 안 넘어가고 `Interacting`에 영원히 멈출 수
있습니다. `DoorDataStruct.InRangeToInteract`와 `RaycastToDoors`는 `Open`/`Shut`만 인식하므로,
이 상태에 걸린 문은 **그 봇에게도, 나중에 지나가는 다른 봇에게도** 문 시스템에서 영구
제외됩니다. ORBIT의 `DoorWatch`를 본떠 3초 워치독(`DoorOpener.StartDoorFinalizeWatch` /
`TickDoorFinalizeWatches`)을 추가했습니다 — 상호작용을 건 문이 3초 뒤에도 `Interacting`이면
목표 상태로 강제 완결시킵니다. 봇마다 따로가 아니라 **static/공유** 워치독이라, 문을 연 봇이
그 사이에 죽어도 다른 봇이 이어서 정리합니다.

**② 러시/이동 액션이 문 앞에서도 스프린트를 재요청함.** `RushEnemyAction`/`MoveToEngageAction`
같은 액션은 매 `Update()` 틱마다 속도를 풀스프린트로 되돌립니다 — 그리고 그 `Update()`는
`BotPathData.TickPath()`보다 항상 먼저 돕니다(`SAINMoverClass.ManualUpdate`:
`CurrentAction.UpdateMovement()` 다음에 `CheckTickPath()`). 문 앞 감속은 지금까지
`SelectDoor`가 실제로 문을 "잡았을 때"만 걸렸는데, 그건 3m 반경 + 0.5초 폴링이라 스프린트
속도로는 이미 문에 붙은 뒤에야 걸릴 수 있고, 무엇보다 **방금 자기가 연 문**은 상호작용
쿨다운 중이라 이 조건에서 아예 빠집니다 — 스프린트가 제일 꺼져 있어야 할 순간에 꺼지지
않는 거죠. `DoorOpener.DoorsNearby`(문 상태·상호작용 가능 여부와 무관하게 3m 안에 아무 문이나
있으면 true)를 추가하고, `BotPathData.TickPath`에서 액션의 `Update()` 이후에 이걸로 스프린트를
무조건 끄고 이동속도를 0.25배로 낮췄습니다(ORBIT `MovementSystem.HandleDoors`의 배율과
동일 — 대조해서 그대로 씀).

**③ 정지 감지가 3D 거리라 높이 흔들림에 오탐 가능.** `CheckStuck`은 "목표 코너까지 남은 거리"의
변화량으로 정지 여부를 판단했는데, 이건 3D라서 코너의 Y(내비메시 고정값)와 봇의 Y(계단·웅크리기·
문턱 단차로 흔들림)가 섞입니다. 봇의 XZ 위치가 진짜로 멈췄어도 Y 흔들림만으로 "움직이는 중"으로
잘못 읽을 수 있습니다. ORBIT의 `SoftStuckRemediation`(`moveVector.y = 0f`)을 본떠, 코너까지의
거리 대신 **봇 자신의 XZ 위치 변화**(`_lastCheckedBotPositionXZ`)를 직접 재도록 바꿨습니다.
ORBIT은 속도에 비례한 임계값을 쓰는데, 그 계수(`3.5f / 2f`)가 SAIN의 속도 단위에서도 맞는지
검증할 방법이 없어서 그대로 베끼지 않고, 기존 임계값(`0.01f`, 대략 0.1m)과 같은 자릿수인
고정값(0.05m)으로 보수적으로 잡았습니다.

**옮기지 않은 것**: ORBIT의 문 콜리전 전체 아키텍처(문 상태에 이벤트로 항상 동기화, 봇마다
아니라 문마다 관리)와, 스윙 방향을 고려한 접근 로직 — 전자는 SAIN의 문 콜리전 관리 자체를
다시 설계해야 해서 지금 남은 증상 크기에 비해 과합니다. 후자는 ORBIT에 대응하는 코드가 아예
없습니다(설계가 다름 — ORBIT은 아예 전진을 얼려버림). ②를 넣고도 "문 열리며 봇이 벽으로
튕기는" 증상이 남으면 그때 SAIN의 기존 백스텝 로직(문 열린 이전 코너로 물러나는 부분)을
스윙 방향 인지형으로 고치는 걸 다음 단계로 남겨둡니다.

건드린 파일: `SAIN/Classes/Bot/Doors/DoorOpener.cs`, `SAIN/Classes/Bot/Mover/BotPathData.cs`.

### 12. 11번 실전 로그 분석 — 워치독 범위가 너무 좁았고, 스프린트 차단이 사격까지 막고 있었음 (2026-09-20)

11번을 실제로 돌린 라이드 로그(`LogOutput.log`)를 받아서 대조한 결과 두 가지 결함을 찾았습니다.

**① 워치독이 대부분의 낑긴 문을 놓치고 있었음.** 로그에선 SAIN 워치독이 3번 정상 발동했지만(전부
예외 없이 깨끗함), 이 맵을 만든 다른 모드의 독립적인 문 상태 진단 로그를 대조해보니 최소 두 개
문이 각각 **12초, 24초 넘게** `Interacting`에 멈춰 있었는데도 SAIN 워치독은 침묵이었습니다.
원인: 워치독을 `DoorOpener.TryInteractWithDoor`(SAIN 자신이 문을 여는 호출) 성공 시점에만 걸어서,
**SAIN이 직접 연 문만** 감시했습니다. 근데 `SAINLayersActive`가 꺼지는 순간(10번 참고, `GoalEnemy`가
없어질 때마다 일어남) 봇은 게임 기본 AI로 잠깐 넘어가고, **기본 AI가 여는 문도 SAIN이 여는 문과
똑같이 `Interacting`에 영구 고착되는 문제를 겪습니다** — 근데 그 문들은 감시 목록에 아예 등록된
적이 없었던 겁니다.

고친 방식: 워치독을 `DoorOpener`(봇 하나당)에서 `DoorHandler`(레이드당 하나, 맵의 모든 문을
`Init()` 시점에 `FindObjectsOfType<Door>()`로 이미 수집해두는 기존 클래스)로 옮겼습니다. 각 문의
`WorldInteractiveObject.OnDoorStateChanged`(누가 상태를 바꾸든 엔진이 쏘는 이벤트)를 구독해서,
**누가 열었는지와 무관하게** 맵의 모든 문을 지켜봅니다. ORBIT의 `DoorSystem`이 원래 쓰던 방식과
같습니다 — 이번에 "우리가 연 문만 봐도 충분하다"고 범위를 좁혔던 게 실수였습니다.

**② 문 근처 스프린트 차단이 사격까지 막고 있었음.** 11번의 스프린트 차단 코드는 "몸이 실제로
뛰고 있는지"만 껐고, "이 경로가 뛰고 싶어함"이라는 별도의 의도 플래그는 안 껐습니다. 근데 매 틱
조준/사격 상태를 초기화할지 결정하는 기존 코드(`CheckSprintSteering`)는 그 의도 플래그만 보고
판단합니다 — 그래서 문 근처에서 실제로는 느리게 걷고 있어도 게임은 "얘 아직 전력질주 중"으로
착각해서 계속 사격을 초기화했습니다. 봇이 문 앞에서 플레이어와 마주쳐도 총을 안 쏘고 달리기만
하는 것처럼 보이는 증상으로 실전에서 확인됐습니다(사용자 필드 리포트). 물리적 스프린트뿐 아니라
경로의 스프린트 의도(`RequestEndSprint`)도 같이 끄도록 고쳤습니다.

건드린 파일: `SAIN/Classes/Bot/Doors/DoorOpener.cs`, `SAIN/Classes/PlayerManager/Doors/DoorHandler.cs`,
`SAIN/Classes/Bot/Mover/BotPathData.cs`.

### 13. 12번 실전 로그 분석 — 워치독이 문을 0개 감시하고 있었음 (2026-09-20)

12번을 반영한 빌드로 돌린 레이드 로그 두 개를 받아서 대조했습니다. 1번째 로그는 SAIN 자체
로그(Debug/Info/Warning/Error)가 전부 비어 있어서 판단 불가였고, 2번째 로그는 SAIN 로그가
정상적으로 찍혀서 결정적인 증거가 나왔습니다.

`[DoorHandler] Watching 0 doors for stuck EDoorState.Interacting.` — 워치독이 그 레이드에서
**문을 0개** 감시하고 있었습니다. 근데 같은 레이드에서 이 맵을 만든 다른 모드의 독립적인 문 진단은
문 40개 이상, 그 중 여러 개가 `Interacting` 상태로 넘어가는 것까지 잡아냈습니다. 즉 지난 두 로그
모두 워치독 강제 발동이 0번이었던 건 "낑김이 안 일어나서"가 아니라 **워치독이 애초에 아무 문도
감시하고 있지 않았기 때문**입니다.

원인: `DoorHandler.Init()`은 `GameWorldComponent.Init()` 안에서, 즉 `GameWorld` 객체가 막 생성된
시점에 호출됩니다. 근데 그 시점엔 맵의 씬 콘텐츠(문 오브젝트 포함)가 아직 다 로드되기 전이라
`FindObjectsOfType<Door>()`가 매번 빈 배열을 돌려줬던 겁니다. 사실 같은 파일에 있는
`GameWorldComponent.findSpawnPointMarkers()`가 이미 똑같은 문제를 겪었었고, 거기선 `Camera.main`이
생길 때까지 매 틱 재시도하는 방식으로 우회하고 있었습니다 — 12번에서 워치독을 새로 만들 때 이
선례를 놓쳤습니다.

고친 방식: 문 스캔을 `Init()`에서 떼어내서 `ManualUpdate()`로 옮기고, 문을 찾을 때까지(또는
15초 타임아웃까지) 매 틱 재시도하도록 바꿨습니다. 문을 찾으면 그 시점에 한 번만 이벤트 구독을
겁니다(라이드 중 문이 새로 생기거나 없어지지 않는다는 전제는 그대로 유지).

건드린 파일: `SAIN/Classes/PlayerManager/Doors/DoorHandler.cs`.

## 상태

- 1번은 필드 리포트 기반으로 고쳤고 **재현이 사라진 것까지 확인**했습니다.
- **4번은 확인이 틀렸습니다.** 코드는 들어갔지만 위 6번 때문에 실제로는 작동한 적이
  없습니다. 그때 증상이 줄어 보인 건 다른 이유였거나 우연입니다.
- 2·3·6·7·8번은 논리적으로는 맞지만 **재테스트 대기** 상태입니다.
- **9번은 이 레포의 수정이 아니라 원인 규명입니다.** 재장전 문제의 해결책은
  **UIA 2.1.3으로 내리는 것**이고, 7·8번은 그와 별개로 유지되는 안전망입니다.
- **10번도 재테스트 대기**입니다. 코드 흐름 추적으로 원인을 특정하고 고쳤지만, 아직
  실제 레이드에서 "봇이 대기 모드로 빠졌다가 재활성화되는" 상황을 재현해서 확인하진
  못했습니다. 프리캠으로 비슷한 증상(문 근처에서 밀려서 구석에 낑겼다가 스스로 풀림,
  전투 상황도 아닌데 문틈으로 순간이동하듯 지나감)이 목격됐지만, 정확히 이 수정이
  막으려는 케이스인지 로그로 대조는 안 됐습니다 (2026-09-20). `CancelInteraction()`이
  실제로 발동하는 순간을 `LogWarning`으로 남기도록 추가해서, 다음 로그부터는 이 상황이
  실제로 몇 번 걸리는지, 그리고 관찰된 증상과 실제로 같은 코드 경로인지 확인할 수
  있습니다.
- **11번은 실전 로그로 절반 확인, 절반 결함으로 드러남.** 워치독 메커니즘 자체(3초 지나도
  `Interacting`이면 강제 완결)는 로그에서 3번 정상 발동해 **작동은 확인**됐지만, 감시 범위가
  "SAIN이 직접 연 문"으로 너무 좁아서 대부분의 낑긴 문을 놓쳤고, 스프린트 차단은 사격까지
  같이 막는 부작용이 있었습니다. 둘 다 12번에서 고쳤습니다. XZ 정지 감지는 여전히 미검증.
- **12번은 실전 로그로 확인해보니 절반은 애초에 작동한 적이 없었습니다.** 스프린트/사격
  부분은 실플레이에서 정상 확인됐지만, 워치독은 13번에서 드러났듯 문을 0개 감시하고
  있어서 발동 여부 자체를 테스트할 수 없는 상태였습니다.
- **13번도 재테스트 대기**입니다. 로그 분석 기반으로 원인을 특정하고 고쳤지만, 다음 실전
  로그에서 `DoorHandler`가 실제로 문을 N개(0이 아닌) 찾아서 감시를 시작하는지, 그리고
  그 상태에서 낑긴 문에 워치독이 정상 발동하는지 확인이 필요합니다.
- **컴파일 검증 (2026-09-20 갱신):** 작성 환경에 .NET SDK 8.0/10.0을 새로 설치했습니다.
  서버 쪽(`SAINServerMod` + 공유 프로젝트)은 `dotnet build`가 **경고 13개, 에러
  0개로 성공**했습니다 — 다만 6·7·8·10·11·12번이 건드린 파일은 전부 클라이언트
  프로젝트(`SAIN.csproj`, netstandard2.1) 쪽이라 이 성공이 그 수정들을
  검증해주진 않습니다. 클라 프로젝트는 `BepInEx.Core`/`UnityEngine.Modules`
  패키지가 필요한데, 그 피드(`nuget.bepinex.dev`)가 이 환경 네트워크 정책상
  차단돼 있어(403, 우회 금지 대상) 여전히 전체 컴파일은 못 합니다.
  대신 11·12번이 건드린 파일 전부(`DoorOpener.cs`, `BotPathData.cs`,
  `DoorHandler.cs`) Roslyn으로 **순수 구문 검증**(참조·타입
  체크 없이 문법만)을 돌려 에러 없음을 확인했고, 새로 쓴 BSG API 멤버
  (`Door.DoorState`/`CurrentAngle`/`GetAngle`의 setter, `GlobalEventsController.
  CreateEvent`, `InteractiveObjectInteractionResultEvent.Invoke`,
  `WorldInteractiveObject.OnDoorStateChanged`의 add/remove)는 전부
  `dnfile`로 실제 게임 어셈블리에서 `public`임을 개별 확인했습니다. **타입
  체크/링크까지 끝난 완전한 컴파일 검증은 아직 아닙니다** — 로컬(실제 SPT
  설치 환경)에서 최종 빌드 한 번은 필요합니다.

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
