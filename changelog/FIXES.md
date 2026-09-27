# 문 끼임 수정 · 전술 추가 상세 기록

> README에서 분리한 상세 기록입니다(2026-09-28). 14번 문 전술 표의 일부(문 앞 수류탄, 문 닫기, 폭발 후속, 분대 브리칭 등)는
> 이후 제거됐습니다 — 현재 상태는 README와 `CHANGELOG.md` 최신 항목 기준.

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

### 14. 문 전술 — 고인물·존버 플레이 방식 추가 (2026-09-27)

사용자가 설명한 실제 고인물/존버 플레이 방식을 봇 행동으로 옮겼습니다. SAIN 원작에는 없던
**새 행동**이라 SAIN 전투 레이어(`SAIN : Combat Layer`) 안에 새 결정(`DoorTactic`)과
새 동작(`DoorTacticAction`)으로 넣었습니다. ORBIT은 이 레이어 이름만 보고 "SAIN이 전투 중"으로
판단하므로 ORBIT 쪽 수정 없이 호환됩니다(ORBIT이 SAIN에서 읽는 `BotManagerComponent`,
`EPersonality`, 레이어 이름은 하나도 바꾸지 않음).

**언제 켜지나:** 적이 보이지 않고, 마지막으로 파악한 적 위치가 25초 이내이며, 봇에서 8m 안에 있는
문 **건너편**(문을 사이에 두고 반대쪽) 12m 안에 적이 있을 때. 성격별 확률로 한 번 굴리고,
실패하거나 끝나면 그 문은 잠시(25~60초) 다시 안 씁니다. **적이 보이는 순간 원래 SAIN 판단
(사격)이 바로 이어받고 전술은 끝납니다.**

| 성격 | 확률 | 하는 일 |
|---|---|---|
| GigaChad (고인물) | 60% | 문이 열려 있으면 **점프 피킹** 또는 **방 가두기** 중 하나. 피킹 뒤 40% 확률로 **페이크 수류탄**(안 하면 다쳤을 때 40% 확률로 **페이크 치료**). 가두기 때 50% 확률로 **문 앞 수류탄**, 다쳤으면 30% 확률로 페이크 치료 |
| Chad (공격형) | 35% | 문이 열려 있으면 **점프 피킹**, 20% 확률로 페이크 수류탄(안 하면 다쳤을 때 20% 확률로 페이크 치료) |
| SnappingTurtle (존버) | 60% | **방 가두기**: 문 닫고(열려 있으면) 문 옆에 앉아서 45~90초 존버. 도발 안 함(프리셋 설정) |
| Rat (쥐) | 50% | **문 옆 매복**: 문 안 닫고 문 옆에 거의 엎드린 자세로 60~120초 조용히 대기 |

각 동작:
- **점프 피킹**: 복도 쪽 문틀 옆(방 안에서 안 보이는 자리)에 붙었다가, **복도에서 문 앞으로 앞버니 → 고개만 방 안으로
  돌려 0.15초 확인 → 바로 뒷버니**로 원위치. 방 안으로는 들어가지 않음. 문이 닫혀 있으면 먼저 그 자리(문틀 옆)에서
  문을 연 뒤 피킹. 피킹 동안만 "문 근처 달리기 금지"(11번)를 잠깐 풉니다.
- **페이크 수류탄**: 수류탄을 손에 꺼내서(꺼내는 소리가 남 — 플레이어·봇 둘 다 들음) 1.3초 들고 있다가
  핀을 뽑지 않고 다시 총으로 바꿈. 그 뒤 문을 겨누고 3~6초 대기. **총이 아직 안 올라왔는데 적이 10m 안으로 튀어나오면
  긴급 후퇴**(SAIN 후퇴로 엄폐까지 빠진 뒤 다시 대치), 총이 올라와 있으면 바로 사격. 페이크 치료 뒤에도 같음.
- **페이크 스팀**: 안 다쳤으면 스팀 주사 동작(소리)을 시작했다가 0.7초 만에 총으로 바꿔 취소(주사가 들어가기 전).
- **소리 듣고 취소**: 위 속임수들과 문 앞 수류탄 준비 중에 적이 문 근처(7m)에서 뛰는 소리·점프/착지·문 소리를 내면 즉시 취소하고
  총을 들어 문을 다시 겨눔(발소리·살금걸음은 무시, 소리만으로는 도망 안 감 — 상대의 페이크 대비).
- **페이크 치료**: 다친 봇이 문 옆에서 게임 원래 방식으로 치료를 시작(치료 소리)했다가 1.5초 뒤 게임 원래의 치료 취소
  (`StopUse`, 총으로 복귀)를 합니다. SAIN 봇은 적의 치료 소리를 "무방비"로 보고 돌진하는 성격이 있어서, 봇끼리도 먹힙니다.
- **방 가두기**: 문 앞으로 가서 문을 닫고, 성격이 도발하는 성격이면 도발한 뒤, 문 옆에서 문을 겨누고 대기.
  문이 열리면(적이 나오면) 로그에 남고, 보이는 순간 사격으로 넘어갑니다.
- **문 앞 수류탄**: 문을 닫은 뒤 문틀 옆에 **쪼그려 앉아 0.7초 소리를 듣고**, 문 바로 앞(봇 쪽 0.4m)에 **낮은 각도로 살짝**
  던진 다음(튕겨 돌아오지 않게) 4.5m 이상 물러나 문을 겨눔. **장신관 수류탄(신관 4.5초 이상, M67류)만** 쓰고, 착발 수류탄은 안 씀.
  적이 놀라서 문을 열고 나오면 폭발이나 사격에 걸리는 구조입니다.

**설정 위치: F6 (SAIN 에디터) → Global Settings → General → `Door Tactics (zzap)`** (2026-09-27부터, 전에는 F12였음):
전체 On/Off, 점프 피킹 / 페이크 수류탄 / 페이크 치료 / 방 가두기 / 문 앞 수류탄 개별 On/Off, 문 앞 수류탄 최소 신관(3~8초, 기본 4.5),
긴급 후퇴 거리(3~20m, 기본 10), 확률 배수(0~3, 기본 1), 진단 로그(기본 켜짐), 상세 로그(기본 꺼짐 — "왜 안 썼는지"까지, 10초당 1줄).
SAIN 프리셋 값이라 **프리셋마다 따로 저장**되고, 이 기능이 생기기 전에 만든 프리셋(예: TwitchPlayers 프리셋)은 기본값으로 채워집니다.
바꾼 뒤 에디터에서 저장하면 레이드 중에도 바로 적용됩니다.

**진단 로그 읽는 법** (`BepInEx/LogOutput.log`에서 `[DoorTactic]` 검색):
```
[DoorTactic] [봇이름] [GigaChad] START plan=Peek door=123 doorState=Open botDist=4.2m enemyDepth=3.1m ...
[DoorTactic] [봇이름] [GigaChad] step None -> MoveToStack (start) t=0.0s
[DoorTactic] [봇이름] [GigaChad] jump peek at door 123
[DoorTactic] [봇이름] [GigaChad] step PeekOut -> PeekBack (peekedInside) t=2.1s
[DoorTactic] [봇이름] [GigaChad] fake grenade drawn: in hands
[DoorTactic] [봇이름] [GigaChad] END plan=Peek lastStep=Hold result=enemySpotted(StandAndShoot) duration=6.3s jumped=True ...
```
- `result=`가 핵심입니다: `enemySpotted(StandAndShoot)` = 전술 도중 적을 발견해 사격으로 넘어감,
  `holdTimeout` = 끝까지 기다림, `noPath:…`/`moveTimeout:…` = 이동 실패(맵 구조 문제 가능성),
  `interrupted(…)` = 제압/엄폐 등 다른 판단이 끼어듦, `emergencyRetreat` = 속임수 뒤 총이 안 올라온 상태에서 적이 튀어나와 후퇴,
  `doorOpenFailed`/`doorDidNotOpen` = 피킹 전에 닫힌 문을 못 엶(잠긴 문 등).
- `EMERGENCY RETREAT:` 줄에는 적과의 거리와 당시 손에 든 것(`hands=`)이 찍힙니다. `door grenade picked … fuse=` 줄로
  어떤 수류탄(신관 몇 초)을 골랐는지 확인할 수 있습니다.
- `close door …: interact=FAILED`, `door grenade …: no valid arc`, `WARNING could not put the fake grenade away`
  는 문제 신호입니다. 이 줄이 보이면 로그를 주세요.

**성격별 확률 (같은 화면, 퍼센트):** GigaChad Chance 60, Chad Chance 35, SnappingTurtle Chance 60, Rat Chance 50,
GigaChad Peek vs Trap 55(둘 다 가능할 때 피킹을 고를 확률), GigaChad/Chad Fake Trick Chance 40/20, GigaChad Trap Fake Heal
Chance 30, GigaChad Door Grenade Chance 50. 최종 확률 = 성격 확률 × Chance Multiplier.

**존버 매복 (F6 → Global Settings → General → `Freeze Ambush (zzap)`):** SAIN의 "Freeze"는 Heard From Peace Behavior가
Freeze인 성격(이 포크 프리셋 기준 Rat, SnappingTurtle, Chad, GigaChad)이 **교전 전에 가까운 적 소리를 들으면 그 자리에서
엄폐 자세로 멈춰 기다리는** 동작입니다. 조건과 원작 값:

| 설정 | 기본값 | SAIN 원작 |
|---|---|---|
| Max Enemy Distance (적이 이 거리 안일 때만) | 45m | 70m |
| Freeze Outdoors (야외에서도 매복) | 꺼짐 | 꺼짐(실내만) |
| Min / Max Freeze Time (대기 시간, ÷ 성격 공격성) | 10 / 120초 | 10 / 120초 |
| Not Seen For (이 시간 동안 적을 못 봤을 때만) | 240초 | 240초 |
| Heard Within (이 시간 안에 소리를 들었을 때만) | 80초 | 80초 |
| Watch Approach Corner (적이 돌아 나올 코너 겨누기) | 켜짐 | 없음 |

야외 매복을 켜면 봇이 서 있던 자리에서 그대로 멈추기 때문에 트인 곳에서 멈출 수 있습니다.
로그: `[Freeze] … START ambush 45s enemyDist=23m indoors=True heard=2.1s ago corner=yes` / `END ambush: time up`.

**다인큐(분대) 상황:** 문 하나에 한 봇만(`door.skipClaimedBySomeoneElse`), 팀원이 방 안이거나 문을 밀고 들어가는 중이면 중단
(`door.abort.teammateInsideRoom` / `teammatePushingDoor`), 팀원 근처엔 문 앞 수류탄 안 던짐(`door.nadeCancelledTeammateNear`),
문을 보는 동안 다른 적이 봇 쪽 15m 안에 나타나면 측면 공격으로 보고 중단(`door.abort.otherEnemyOnOurSide`).

**분대 역할 분담:** 리더가 전술을 시작하면 `squad roles for door …: overwatch=<봇> rearGuard=<봇>` 로그가 남고, 팀원 쪽은
`START plan=Overwatch for leader <리더>` / `START plan=RearGuard …`. 집계 키: `door.role.overwatchAssigned`, `door.role.rearGuardAssigned`,
`door.role.start.Overwatch|RearGuard`(팀원이 실제로 시작), `door.role.expiredBeforeStart`(배정됐는데 팀원이 3초 안에 못 받음 — 그 팀원이
사격 등 다른 판단 중이었다는 뜻), `door.role.noMateAvailable`, `door.role.solo`(혼자), `door.end.Overwatch.leaderDone|leaderGone|enemySpotted`.

**제3자 개입 시:** ① 제3자가 **보이면** → SAIN 사격이 먼저라 즉시 전술 종료(`enemySpotted`/`interrupted`), ② 봇 쪽 15m 안에서
**들리거나 확인되면** → 측면으로 보고 중단(`otherEnemyOnOurSide`), ③ **총에 맞거나 총알이 스치면**(먼 사수 포함) → 중단(`underFire`),
④ SAIN이 목표 적을 제3자로 **바꾸면** → 중단(`goalEnemyChanged`), ⑤ 심한 제압 → 공격 행동 자체가 막혀 중단(`interrupted`).
⑥ 방 안 적이 아닌 누군가가 **12m 안에서 총을 쏘면** → 중단(`closeGunfire`). 리더가 멈추면 역할 팀원도 2초 뒤 해제. 그보다 먼 총소리만으로는
계속 문을 봄(실제 플레이어처럼). **①~⑥ 중 제3자 때문에 멈춘 경우**(`underFire`, `otherEnemyOnOurSide`, `closeGunfire`, `goalEnemyChanged`)는
90초 안에 방 안 적 정보가 남아 있으면 같은 문으로 돌아와 재개(`door.resumeArmed` → `door.resume`, 로그 `RESUME door …`).

**분대 브리칭:** 로그 `BREACH roles for door …: overwatch=<봇> assault=[<봇>, …]`, `BREACH grenade thrown, blast in ~4.3s`,
팀원 `assault grenade thrown`. 집계: `door.start.<성격>.Breach`, `door.role.assaultAssigned`, `door.role.start.Assault`,
`door.breach.breach.thrown|noArc|noGrenade|teammateInRoom`, `door.breach.assault.thrown|…`, `door.breach.rush.Breach|Assault`(실제 돌입 수),
`door.end.Breach.breachEntered`(진입 완료) / `breachBlastTimeout`(폭발 대기 8초 초과 — 문제 신호).

**로그로 확인하는 법 (체크리스트)** — `BepInEx/LogOutput.log`에서 검색:
1. `[Tactics] SETTINGS` — 레이드마다 2줄. F6에서 바꾼 값(확률, 거리 등)이 여기 그대로 찍혀 있으면 설정이 적용된 것. 이 줄이
   아예 없으면 새 DLL이 안 깔렸거나 두 Diagnostic Logs가 모두 꺼져 있는 것.
2. `[Tactics] SUMMARY` — 5분마다, 그리고 다음 레이드 시작 때 이전 레이드 최종 집계. 항목 뜻:
   - `door.start.<성격>.<Peek|Trap|Ambush>` 문 전술 시작 수 / `door.rollFailed.<성격>` 확률에서 떨어진 수
   - `door.end.<계획>.<결과>` 끝난 방식 (`enemySpotted` 적 발견, `holdTimeout` 끝까지 대기, `noPath`/`moveTimeout` 이동 실패,
     `emergencyRetreat` 긴급 후퇴, `doorDidNotOpen` 문 못 엶, `interrupted` 다른 판단)
   - `door.jumpOut` / `door.jumpBack` 앞버니·뒷버니 실제 점프 수 (0이면 점프가 안 되는 것)
   - `door.closeOk` / `door.closeFailed`, `door.stateAfterClose.<상태>` (Shut이어야 정상)
   - `door.nadeThrown` / `door.nadeNoArc`, `fakeNade.drawn` / `fakeNade.putAway` / `fakeNade.putAwayRefused`,
     `fakeHeal.started` / `fakeHeal.failedToStart`, `fakeStim.started` / `fakeStim.cancelled` / `fakeStim.cancelRefused`,
     `trick.cancelledOnSound`(속임수 중 나오는 소리 듣고 취소), `door.nadeAbortedOnSound`(수류탄 준비 중 소리 듣고 포기),
     `emergencyRetreat.start`
   - `freeze.start.<성격>.<indoors|outdoors>`, `freeze.end.<enemySpotted|timeUp|interrupted>`,
     `freeze.cornerWatch`(코너 겨눔) / `freeze.defaultLook`(코너 못 찾음)
3. 개별 사건: `[DoorTactic]`(문 전술 한 건씩), `[Freeze]`(매복 한 건씩: START → watching corner → END).
4. SAIN 디버그 모드 오버레이: 문 전술 중인 봇에 `Door Tactic: plan=… step=… look=…` 표시.

**문제 신호:** `closeFailed`, `stateAfterClose`가 Shut이 아님, `nadeNoArc`만 있고 `nadeThrown`이 없음, `putAwayRefused`,
`jumpOut=0`인데 `door.start.*.Peek`는 있음, `door.end.*.noPath`/`moveTimeout`가 많음. 이런 게 보이면 로그를 주세요.

**못 한 것 / 한계:**
- **복도 T자 교차로에서 뛰어서 옆을 보고 수류탄** 은 이번에 넣지 않았습니다(문이 있는 경우만). SAIN 원래 기능으로
  "놓친 적의 마지막 위치·사각 코너에 수류탄"은 이미 합니다.
- 손전등 기도비닉, 미세 피킹·앉기 높이, 듣고 멈췄다 진입은 새 코드가 아니라 **프리셋 설정**으로 처리했습니다
  (twitchplayers-zzap--Bootleg-- 프리셋 v3.5.0).
- 봇은 사람처럼 "스쳐 보고 판단"하지 않고, 점프 중 시야에 들어오면 SAIN의 일반 시야 판정으로 적을 봅니다.
- **게임 안 검증 전입니다.** 컨테이너에서는 Unity 2022 전용 API 때문에 원본 SAIN도 26개 에러가 나는
  환경이라(환경 문제), "원본과 같은 26개 외에 새 에러 0개"로 컴파일만 확인했습니다. 실제 빌드는 PC에서
  `dotnet build SAIN.slnx -c Release`.


### 15. 분대 교전 틀 — 다인큐가 각자 따로 싸우던 문제 (2026-09-27)

**문제:** 원래 SAIN의 분대 레이어는 봇이 적을 10초 넘게 못 봤을 때만 동작합니다. 그래서 교전이 한창일 때는 분대원이
각자 혼자 싸우는 것처럼 보였습니다(같은 자리에 뭉치거나, 한 명이 쏘는 동안 나머지는 뒤에 줄 서 있음).

**해결:** SAIN 전투 레이어(`SAIN : Combat Layer`) 안에 새 판단 `SquadTactic`을 추가했습니다. 레이어 이름과 구조를 그대로
두었기 때문에 ORBIT 호환성에는 영향이 없습니다. 판단 순서는 사격 → 원거리 사격 → 러시 → 문 전술 → **분대 교전** →
수류탄 → 교전 → 수색 → 매복 → 엄폐입니다. 적이 **보이거나 총에 맞고 있으면 동작하지 않습니다**(SAIN 원래 사격이 우선).
적은 알지만 지금 안 보이는 분대원에게만 동작합니다.

| 모드 | 조건 | 행동 |
|---|---|---|
| **Crossfire** (크로스파이어) | 같은 적에게 팀원이 지금 쏘고 있음 | 팀원과 **35도 이상 다른 각도**, 팀원과 **4m 이상 떨어진** 지점 중 적 위치가 보이는 곳으로 이동해서 그 방향을 겨누고 대기(최대 20초). 같은 지점은 한 명만 차지 |
| **CoverMate** (팀원 엄호) | 20m 안 팀원이 재장전·치료·수술 중 | 그 팀원 옆으로 가서 그 팀원의 적 방향을 홀드. 한 팀원당 엄호자 1명, 팀원이 끝나면 해제 |
| **TradePush** (트레이드 돌격) | 10초 안에 팀원이 쓰러짐 + 공격형 성격(적 재장전·치료 때 러시하는 성격) | 그 팀원을 죽인 적의 위치로 밀고 들어감 |
| **TradeAngle** (트레이드 각 잡기) | 같은 조건 + 신중한 성격 | 그 적 위치에 다른 각도를 잡고 홀드 |

**F6 설정** (General → `Squad Combat (zzap)`): `Squad Combat Enabled`(전체 끄기/켜기), `Crossfire`, `Crossfire Min Angle`(35),
`Min Teammate Spacing`(4m), `Cover Reloading/Healing Teammate`, `Cover Max Distance`(20m), `Trade Downed Teammate`,
`Trade Window`(10초), `Max Engage Distance`(80m — 이보다 먼 적에는 동작 안 함), `Diagnostic Logs`.
예전 프리셋 파일에는 이 항목이 없어도 기본값으로 로드됩니다.

**로그 확인:**
- `[Tactics] SETTINGS SquadCombat: enabled=… crossfire=… minAngle=…` — 레이드마다 1줄. F6 값이 적용됐는지 확인.
- `[SquadCombat]` — 한 건씩: 시작(`crossfire`, `trade for <팀원>: pushing the killer's position` 등), 도착, 종료 이유.
- `[Tactics] SUMMARY`의 `squad.*` 집계:
  - `squad.start.<Crossfire|CoverMate|TradePush|TradeAngle>` 시작 수, `squad.arrived.<모드>` 목표 지점 도착 수
  - `squad.end.<모드>.<이유>` — `underFire`, `goalEnemyChanged`, `mateGone`/`mateDone`(엄호 대상 사망/행동 끝), `coverTimeout`,
    `holdTimeout`, `enemyInfoStale`, `pushTimeout`, `reachedKillerPosition`, `moveTimeout`, `noPath`
  - `squad.teammateDown` 팀원 사망 감지 수
  - `squad.crossfire.noPoint.<noNavmesh|bunched|noLineOfSight|noPath>` — 크로스파이어 자리를 못 찾은 이유
- SAIN 디버그 오버레이: `Squad Combat: mode=…`

**문제 신호:** `squad.start.*`는 많은데 `squad.arrived.*`가 거의 없음(이동 실패), `squad.end.*.noPath`/`moveTimeout`가 많음,
`squad.crossfire.noPoint.bunched`가 대부분(간격 값이 맵에 비해 너무 큼), 분대가 있는데 `squad.start.*`가 0.

**첫 실전 로그 결과 (2026-09-27, 커스텀 15분):** 크로스파이어 6 / 엄호 12 / 트레이드 1 시작, 팀원 사망 감지 4, SAIN 예외 0.
위 변경 이력의 3가지를 고쳤습니다. 새 집계 키: `squad.actionRestartKept` / `door.actionRestartKept`(행동 재시작에도 세션 유지 —
이 숫자가 올라가면 버그가 고쳐진 것). `interrupted(SeekCover)`는 봇이 심하게 제압당했을 때(SAIN 원래 동작)라 그대로 둡니다.

**한계:** 위 3가지 수정은 아직 게임에서 다시 확인하지 않았습니다(컴파일만 확인 — 원본과 같은 26개 외 새 에러 0개).
SAIN 분대 레이어(10초 이상 적을 못 봤을 때의 수색/집결)는 그대로이고, 이 틀은 그 전 단계인 교전 중에만 동작합니다.

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

