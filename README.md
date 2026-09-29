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

**최근 변경 (2026-09-29 19:33):** 무기 교체 추적 로그(`[Weapon]` 무엇→무엇·호출 경로·탄 상태, `PING-PONG` 표시, `[Death]`에 총 상태). 직전: 빠른 재장전 탄창 소진 수정, 프리셋 섞임 버그 수정.
아래 표의 "F6 설정 위치"는 영어 원문 이름이라, 찾기 어려우면 `한 / [EN]`으로 바꾸거나 검색창에 영어·한국어 아무거나 입력하세요(둘 다 검색됨).

## 추가한 기능

**행동(전술)** — 전부 SAIN 전투 레이어(`SAIN : Combat Layer`) 안의 결정이라 **ORBIT과 수정 없이 호환**됩니다.

| 기능 | 하는 일 | 누가 | F6 설정 위치 |
|---|---|---|---|
| 점프 피킹 | 복도에서 문 앞으로 점프해 방 안을 보고 바로 점프해서 복귀. 닫힌 문은 문틀 옆에서 먼저 엶 | GigaChad / Chad | `General > Door Tactics (zzap)` |
| 런바이 피킹 | 점프가 위험한 곳(낮은 천장·계단)에선 문 앞을 달려 지나가며 보고, 돌아서 한 번 더 지나감 | GigaChad / Chad | 〃 `Run-By Peek Chance` |
| 스텝 피킹 | 피킹 후 문틀에서 짧게 2~3번 들락날락 (서기/앉기, 방 좌/우 모서리 번갈아) | GigaChad / Chad | 〃 `Step Peeks After Jump Peek` |
| 페이크 수류탄 | 문틀에서 수류탄 꺼내는 소리만 내고 즉시 집어넣음. 적이 문 근처에 있고 봇이 진짜 엄폐 뒤(모든 적 시야 차단 + 실내/사방이 막힌 곳)일 때만, 뛰는 소리 들리면 취소 | GigaChad / Chad | 〃 `Fake Grenade` |
| 페이크 치료·스팀 | 치료/주사 소리만 내고 손에 드는 순간 취소. 출혈·중상이면 안 함(진짜 치료 우선) | GigaChad / Chad | 〃 `Fake Heal / Stim` |
| 방 진입 | 방 안의 적을 칠 때 총구부터 내밀며 천천히 걸어 들어가지 않음: 문틀 옆에 붙어 옆에서 문을 열고 → 페이크 수류탄 0~2번 섞기(뛰어나오는 소리 들리면 취소) → 진짜 수류탄을 던지고 **실제 폭발을 확인한 뒤** 바로 돌입, 또는 수류탄 없이 짧고 강하게 대시 진입. 자기 폭발에 닿을 수 있는 위치면 안 던짐 | GigaChad / Chad / Wreckless / Normal | 〃 `Room Clear` |
| 방 가두기 | 문 옆에 붙어 문을 지킴 (문은 그대로 둠) | GigaChad / SnappingTurtle | 〃 `Room Trap` |
| 문 옆 매복 | 문 옆에 낮은 자세로 조용히 대기 | Rat | 〃 `Room Trap` |
| 분대 역할 | 한 명이 문 전술을 하면 동료는 오버워치(교차 각) / 후방 경계 | 분대 | 〃 `Squad Roles` |
| 제3자 감지 | 문 전술 중 다른 적이 보이거나 25m 안에서 소리 나면 즉시 문을 버리고 대응 | 전원 | 자동 |
| 다이아몬드 스텝 | 사격 중·근접 난전(DogFight) 중 WASD를 와다다 누르듯 짧게 끊어 움직임 (탭 0.13초, 좌우 60% · 앞뒤 40%) | GigaChad / Chad / Wreckless / Normal |
| 기울이기 | 다이아몬드 스텝 중 사격할 때 적이 움직이는 쪽으로 기울이기를 0.35~1초 누르고 있다가, 반대로 틀면 따라 바꿈. 중간중간 Q/E 연타(2~4번)를 섞음 — 7m 안 45%, 그 밖 15% (교전마다 60%) | 다이아몬드 스텝 대상 | `General > Close Combat (zzap)` |
| 안 보이는 적 판단 (유틸리티 AI) | 적 위치는 아는데 안 보일 때, 밀기·수류탄·지키기·우회·수색·후퇴를 "기대 이득"으로 점수 매겨 가장 이득인 것부터 실행 — 정보 신선도, 적이 각 잡는 중/다가오는 중, 내 체력·탄·수류탄, 수적 우위, 적 장비·장전/치료 중, 적이 조용한 시간(시간은 존버하는 쪽 편), 거리, 성격. 점수로 가중치 준 주사위로 최종 선택(점수 높을수록 잘 뽑힘, 렉리스는 느슨·쥐/겁쟁이는 거의 최고점만). 지키기는 10초 넘게 아무것도 안 오면 점점 감점, 다른 선택이 실행 못 될 때 대신 지키기로 빠지지 않음. `[Utility]` 로그에 점수와 이유 | 전원 | `General > Close Combat (zzap)` `Utility Decision For Hidden Enemy` |
| 보이는 적 판단 (유틸리티 AI) | 적이 보이는 교전 중 계속 쏘기·엄폐로 빠지기·밀어붙이기를 기대 이득으로 선택 — 적이 나를 보는지, 노출 시간, 방금 맞았는지, 체력·탄창, 가장 가까운 엄폐물 거리(멀면 뛰지 않음), 적 장비·장전/치료 중, 내 총과 거리 궁합, 동료, 성격. SAIN의 "N초 버티고 엄폐" 고정 규칙 대체. `[UtilityV]` 로그 | 전원 | `General > Close Combat (zzap)` `Utility Decision For Enemy In Sight` |
| 재장전 타이밍 (유틸리티 AI) | 교전 중 탄창 80% 미만이면 지금 장전 / 계속 쏘기 / 권총 전환을 기대 이득으로 — 적이 나를 보는 중이면 참고 쏘기, 적이 안 보이거나 엄폐 중이면 장전, 적이 장전 중이면 그 틈에 쏘기, 탄 10% 이하+적 30m 안이면 권총. 탄창 버리는 빠른 장전은 급할 때만, 여유 있고 30% 넘게 남았으면 탄창 챙기는 장전. `[Reload]` 로그 | PMC | `General > Close Combat (zzap)` `Utility Reload Timing` |
| 치료 타이밍 (유틸리티 AI) | 적이 있을 때 응급 치료·스팀·수술·기다리기를 기대 이득으로 — 적이 보이거나 다가오거나 방금 맞았으면 기다림, 엄폐 중이고 가장 가까운 위협이 오래됐거나 멀면 치료, 중상이면 교전 중에도 스팀, 수술은 정말 안전할 때만. 위협은 위치를 아는 모든 적(가까울수록·최근일수록 기다림), 치료 중 40m 안에 적이 보이거나 맞으면 즉시 중단, 치료 중엔 기울이기(피킹) 해제, 적이 있던 곳에서 보이는 자리면 치료 미룸. `[Heal]` 로그 | 전원 | `General > Close Combat (zzap)` `Utility Heal Timing` |
| 장비 기반 교전 방식 | 든 총(저격·볼트액션·DMR·소총·SMG·샷건, 스코프/도트, 자동 사격·연사력)으로 선호 교전 거리와 근접/원거리 강점을 계산, 가진 수류탄·치료템 개수까지 이득 계산에 반영 — 저격총은 거리 유지(적이 다가오면 빠짐, 밀지 않음), SMG·샷건은 붙어서 싸우고 먼 교전은 피함, 수류탄 많으면 더 던지고, 치료템 많으면 더 쉽게 치료 | 전원 | 자동 (유틸리티 판단에 포함) |
| 두려움 | 성격 기본값(Coward 0.35 ~ GigaChad·Wreckless 0) + 근처 동료 사망, 플레이어 연속 킬, 부상, 수적 열세, 제압당함 — 용감함에 따라 반영(렉리스 조금, 겁쟁이 크게). 두려울수록 후퇴·지키기, 0.75 넘으면 패닉 | 전원 | `General > Close Combat (zzap)` `Fear` |
| 결과로 배우기 (플레이어 대응) | 플레이어 상대로 한 판단마다 결과 기록(맞힘=성공, 죽음=실패), 판을 넘어 누적 → 이 플레이어에게 잘 먹힌 대응을 더 자주. 플레이어 장비로는 판단 안 함(데브툴·사기 장비 대비) | 사람 플레이어 상대 | `General > Player Style Recorder (zzap)` `Learn From Outcomes` / `Ignore Player Gear` |
| 레이드 일지 | 판마다 시간 찍힌 파일 하나(`BepInEx/config/SAIN-zzap/Journal/`): 모든 봇의 판단 변경과 이유, 전술 로그 전부, 플레이어 위치·피격·킬(2초마다), 5분마다·끝에 `[Battle]` 봇 대 봇 집계(성격·행동·판단 이유별 킬/데스), 끝에 집계 — 레이드 뒤 분석용 | 전체 | 〃 `Raid Journal` |
| 사람 같은 실수 | 모든 이득 계산 판단에서 가끔 최선 대신 2·3순위 선택 — Timmy 12%, Wreckless 8%, Coward 6%, Normal 5%, Chad 4%, Rat·Turtle 3%, GigaChad 2% (TEST 프리셋은 끔). 로그에 `(mistake)` | 전원 | 〃 `Utility Mistakes` |
| 근접 교전 우선 | 적이 8m 안에서 보이면 엄폐로 걸어가지 않고 계속 싸움 (치료·재장전 중 제외) | PMC (Coward 제외) | `General > Close Combat (zzap)` |
| 개활지 질주 대신 교전 | 22m 안에서 보이는 적에게 맞고 있는데 6m 안에 엄폐물이 없으면 멀리 뛰어가다 등에 맞지 않고 그 자리에서 다이아몬드 스텝으로 교전 (Normal은 엄폐물이 12m 넘게 멀 때만) | GigaChad / Chad / Wreckless / Normal | 〃 `Fight Instead Of Running Across The Open` |
| 코너 추격 | 근접전 중 적이 코너 뒤로 빠지면 쫓아가서 넷 중 하나로 코너를 돔: 기울인 채 진입(오른쪽 코너면 E) / 넓게 돌며 천천히 파이 / 천장 높이가 되면 점프샷 / 코너에 있다고 확신하고 탄창에 충분히 남았을 때만 프리파이어(30% 남으면 멈춤 — 안에서 장전 못 하니까). 자기 수류탄이 코너 근처에서 안 터졌으면 기다림 | GigaChad 90% / Wreckless 85% / Chad 75% / Normal 40% | 〃 `Corner Chase` |
| 후퇴 시 고개 숙이기 + 지그재그 | 적에게 보이거나 방금 맞은 상태로 등을 보이며 뛸 때 시선을 55° 아래로(최소 0.8초 유지), 0.45~0.8초마다 좌우 30° 지그재그, 가끔 점프 | 전원 | 〃 `Retreat Head Down` / `Retreat Weave` |
| 빛 관리 | 적이 안 보이는 상태로 각을 잡고 있을 때(멈춤·천천히 이동, 60m·60초 안에 알려진 적) 플래시·레이저 끔, 적이 보이는 순간만 켜서 눈뽕 | 전원 | 〃 `Light Discipline` |
| 코너 선조준 | 안 보이는 적 쪽으로 이동 중 적이 나올 코너·문 6m 안이면 달리기 멈추고 그 코너를 먼저 조준 + 그쪽으로 기울이기 (몸 반쯤 나온 다음에 돌아보던 것 방지). 조준점은 코너 모서리(벽)가 아니라 **코너 너머 적이 나올 자리**(적 마지막 위치 쪽 최대 2m), 높이는 수평 ±10° 안(계단·가까운 코너에서 하늘/벽 보던 것 방지). 문 전술 조준점도 ±12° |
| 위협 기준 목표 선택 | 지금 나를 맞히거나 쏘는 적(안 보여도, 뒤에서라도)이 원래 조준하던 적보다 위협이 크면 그쪽으로 전환 — 위협 = 보임·맞힘·쏨·나를 봄·거리, 현재 목표는 가산점(자주 안 바뀜)·빈사면 마무리 가산, 10초 안에 나와 주고받은 적 +, PMC가 스캐브보다 +(PMC끼리 싸우다 스캐브 나오면 둘 다 스캐브만 쏘던 "티밍" 방지), 보이는 적과 교전 중이면 안 보이는 적은 실제로 맞혀야만 전환. SAIN 원래는 보이는 적이 하나라도 있으면 안 보이는 사수는 무시했음. `[Threat]` 로그 | 전원 | `General > Close Combat (zzap)` `Threat Targeting` | 전원 | 〃 `Corner Pre-Aim` |
| 수류탄 판단 | 적이 안 보이고, 위치 정보가 20초 이내이고, 내가 안전할 때만(안 맞는 중·적 경로 10m 이상·중상 아님·다른 적 안 보임). 그다음 상황 가중치: 적이 6초 넘게 한자리 +30%, 실내 +10%, 치료·장전 중 +15%, 정보 10초 넘음 −20% (기본 50%). `[Nade]` 로그에 이유 | 전원 | 〃 `Grenade Judgment` |
| 자기 수류탄 계산 | 자기가 던진 수류탄을 추적 — 터지는 순간을 실제로 확인, 굴러오면 먼저 피함, 다이아몬드 스텝·코너 추격이 살아 있는 자기 수류탄 쪽으로 안 감 | 전원 | 자동 |
| 권총 전환 | 교전 중 탄창이 비고 적이 30m 안에 보이면 장전 대신 권총 | PMC | 〃 `Pistol Swap When Empty` |
| 빠른 장전 | 교전 중엔 R 두 번처럼 탄창을 버리고 빠르게 장전 | PMC | 〃 `Quick Reload In Combat` |
| 장거리 무기 전환 | 2번 슬롯 DMR/저격총을 80m 넘는 적에게 사용, 가까워지면 주무기로 | PMC | 〃 `Long-Range Weapon Swap` |
| 등 안 돌리기 | 12m 안 적 앞에서 엄폐로 갈 때 등을 보이지 않고 조준한 채 걸어서 이동 (8m 안 엄폐물은 대시, 더 먼 적이면 그냥 뛰어감) | PMC | 〃 `No Back Turning Near Enemy` |
| 제압사격 절제 | 안 보이는 적에게 난사하다 탄창 비우는 것 방지 (짧은 점사, 탄 60% 이상 유지) | PMC | 〃 `Suppression Discipline` |
| 재배치 | 먼저 맞았으면 엄폐 → 치료 → 수류탄 → 적이 못 보는 각으로 이동 | PMC | `General > Reposition (zzap)` |
| 미끼 피킹 | 엄폐에서 앞 점프로 정보 확인 후 복귀, 이후 짧은 사격 피킹 좌우 교대 | 공격형 성격 | 〃 `Bait Peek` |
| 가짜 재장전 | 탄창 확인 동작(소리)으로 재장전하는 척 후 적이 올 각을 조준 | PMC | 〃 `Fake Reload` |
| 수류탄 신관 선택 | 실내·근거리는 짧은 신관, 실외 원거리는 긴 신관 | PMC | 〃 `Fuse Selection` |
| 분대 교전 | 크로스파이어 각, 엄호, 동료 사망 시 트레이드. 크로스파이어는 실내면 적 방의 **다른 입구(뒷문)**를 먼저 막고(15초 동안 안 나오면 뒷문으로 치고 들어가 앞뒤 협공 — Chad/GigaChad/Wreckless 항상, Normal 60%; 안 미는 봇은 8초 안에 적 기척이 있을 때만 15초 더 지킴), 없으면 4~14m 짧은 각(앉은 높이·한 발 나가면 보이는 모서리 포함), 실외는 기존 방식 + 앉은 높이 | 분대 | `General > Squad Combat (zzap)` |
| 약한 적 일제 돌격 | 분대(35m 안 2명 이상)가 20초마다 적을 평가 — 방탄 없음/1~2등급, 헬멧 없음, 권총·총 안 듦, 부상, 치료·장전 중, 혼자. 약하면 다 같이 뛰어가 진압 (Chad/GigaChad/Wreckless 항상, Normal 80%, Timmy 60%, Rat/Turtle 35%, Coward 10%) | 분대 | 〃 `Squad Storm Weak Enemy` |
| 전투 후 정리 (ORBIT 인계 빈틈 메우기) | 1.5초 넘게 싸운 전투(근접 난전·섬광 포함)가 끝나면 **할 일이 있는 동안만**(치료거리가 남았으면 ORBIT과 같은 기준으로 최대 60초 — 그 사이 BSG 바닐라가 걸으며 치료하던 빈틈 제거)(최대 14초, 할 일 다 하면 바로 넘김) + **주변 총소리(70m, 6초)·다가오는 발소리/문소리(25m, 3초)가 들리면 최대 60초까지 SAIN 유지** — 소리 방향 조준, 전진 취소, 발소리 가까우면 치료 안 함/15m 안이면 치료 취소(분대원·죽은 사람 소리 제외). 전투 후 25초는 총 안 내림: **개활지면 먼저 12m 안 엄폐 자리로 빠르게 이동**(벽 둘러싸임 + 전투 방향 시야 차단으로 직접 찾음, 이동하며 장전), 없으면 25m까지 찾아 뛰어감, 그래도 없으면 **엎드려서** 치료·경계(움직일 땐 일어남), 탄창 70% 미만이면 장전, 다쳤으면 그 자리에서 앉아 치료(응급 → 수술, 치료 끝날 때까지 최대 60초), 분대장이 25m 넘게 멀면 합류, 아니면 반쯤 앉아 마지막 위협 방향(죽인 적 위치 포함) 3~6초 경계 후, 성격 확률로(기가채드 80%·채드 65%·노멀 40%·쥐 10%·겁쟁이 0%, 중상이면 ×0.2) 킬 자리 확인하러 전진. SAIN 분대 레이어에서 돌아서 ORBIT 인계 타이밍(전투 레이어 종료 15초 후)을 늦추지 않음 — 원래는 이 15초 동안 바닐라 행동으로 떨어졌음 | 전원 | `General > Squad Combat (zzap)` `Post-Combat Tidy-Up` |
| 아군 사선 회피 (팀킬 방지) | 동료가 쏘고 있는(또는 적을 보고 있는) 사선 안으로 걸어 들어가지 않음 — 밖에서 멈추고, 이미 들어가 있으면 옆으로 빠짐(1.5초 넘게 막히면 통과). 쏘는 쪽도 연사 중 매 틱 확인 + 0.3초 뒤 동료 위치 예측해서 사격 멈춤 | 분대 | 〃 `Fire Lane Guard` |
| 정지 매복 | 적이 가까울 때 멈춰서 코너 조준 (맞으면 즉시 해제) | Rat / SnappingTurtle / Coward (zzap 프리셋 기준) | `General > Freeze Ambush (zzap)` |
| 폭발음 반응 | 25m 안 수류탄 폭발이면 모르던 봇도 던진 사람을 적으로 인식 | 전원 | 자동 |
| 플레이어 스타일 대응 | 기록된 내 스타일(공격성·버니합·기울이기·수류탄)에 맞춰 봇이 코너 대기·근접 교전·기울이기·간격을 조절, 성격마다 강도 다름 (데이터 쌓일수록 강해짐, TEST 프리셋 제외) | 사람 플레이어 상대 | `General > Player Style Recorder (zzap)` |
| 플레이 스타일 기록 | 테스트 판 자동 표시(무적·무한 탄약·데브툴류 모드 감지) — 스타일 반영은 그대로. 내 플레이 방식(이동·피킹·사격·재장전·킬/사망)과 키 입력(누른 키·시간, Control.ini 키 설정 반영)을 레이드마다 기록 — 나중에 봇이 대응하도록 (2단계 대응에 사용) | 플레이어 | `General > Player Style Recorder (zzap)` |
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
