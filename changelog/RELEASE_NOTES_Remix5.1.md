# SAIN-zzap--Bootleg- Remix5.1

**태그:** `Remix5.1` (master — 코드 기준 커밋 `ce292ba`, 태그는 이 노트가 들어간 master 최신 커밋에)

**원작:**

1. **Solarint** — SAIN (Solarint's AI Modifications)
   - Repository: [Solarint/SAIN](https://github.com/Solarint/SAIN)
   - License: 원본 저장소 참고
2. **ArchangelWTF** — SAIN 4.x 유지보수판 (이 포크의 기준 v4.5.1, SPT 4.1)
   - Repository: [ArchangelWTF/SAIN](https://github.com/ArchangelWTF/SAIN)
   - License: 원본 저장소 참고
3. **bmpq (tarkin)** — doordash (봇 몸통박치기 코드를 봇 전용으로 이식)
   - Repository: [bmpq/spt-doordash](https://github.com/bmpq/spt-doordash)
   - License: 라이선스 파일 없음
4. **maschine34675** — DualSideDoorBreach 2.0.2 (문 양쪽 박치기 방식 이식)
   - Repository: [maschine34675/DualSideDoorBreach](https://github.com/maschine34675/DualSideDoorBreach)
   - License: MIT, Copyright (c) 2026 maschine34675

**이전 릴리즈:** remake5 (GitHub 릴리즈 `remake5`, 10/12 게시, 그 뒤 커밋 22개) → **이번: Remix5.1** (버그 수정 위주 + 봇 몸통박치기 추가)

> remake5 이후 이틀 동안 바뀐 걸 모았습니다. remake2·remake4·remake5의 안내 사항(램 사용량, 스터터링, 렉·버그 제보 방법 등)은 그대로 유효합니다.
> 이번 판의 수정은 대부분 **봇끼리 싸우게 돌려 보는 시뮬레이션**과 사용자 스크린샷으로 찾았습니다. 시뮬에서 동작을 확인한 것은 따로 적었고, 나머지는 빌드만 확인했습니다(게임 미검증). 사람 상대 체감은 아직 충분히 확인하지 못했으니 이상하면 제보 부탁드립니다.
> SAIN 내부 버전 숫자는 프리셋 호환 때문에 원본 기준 4.5.1 그대로입니다(remake5와 같음).

---

**<설치 / 업데이트>**

- **remake5에서 올라오는 경우:** 클라이언트 `SAIN.dll`과 **서버 모드 둘 다** 덮어써 주세요(서버 모드의 시뮬 페이지·프리셋이 바뀌었습니다).
  - 서버 모드 `Presets/`의 zzap 프리셋도 덮어써 주세요. 보통·쉬움·저사양 프리셋에 봇 박치기 확률이 들어갔습니다. 기본 `zzap` 프리셋 값은 그대로입니다.
  - doordash·DualSideDoorBreach는 **안 깔아도 됩니다.** 깔려 있어도 충돌하지 않습니다(플레이어 박치기는 그 모드들이 그대로 처리).
- **remake4 이하 / 원본 SAIN에서 올라오는 경우:** remake4 노트의 설치 순서대로 한 뒤 위처럼 덮어써 주세요.

---

**<변경점>**

**1. 새 기능 — 봇 몸통박치기 (달려서 문 박차 열기, SAIN 자체 기능)**
- doordash·DualSideDoorBreach 코드를 **봇 전용으로 SAIN 안에 옮겨 왔습니다.** 두 모드가 없어도 봇이 닫힌 문을 달려서 박차 엽니다.
- 쓰는 상황: 방 진입(수류탄 없이 돌입할 때 도움닫기 1.8~4.5m), 도망칠 때, 돌격할 때, 수색할 때(긴가민가할 때 돌파), 그 밖의 전투 이동(쫓기지 않는 엄폐 이동·측면·분대 이동 등). 길 1.5~7.5m 앞의 닫힌 문, 길이 50° 넘게 꺾이지 않을 때, 문마다 10초에 한 번만 굴림.
- 확률(zzap 기본): 방 진입 50% · 도망 75% · 돌격 65% · 수색 50% · 그 밖 35%. 보통 35/55/50/35/25%, 쉬움 25/40/35/25/15%.
- 문에 닿을 때 1.6m/s 이상이면 문이 열림(doordash 플레이어 기준은 4.5m/s — 봇은 코너를 조준하며 달려 느려지기 때문). 박은 봇은 왼팔 피해 5(금속 문 2배)와 스태미나 감소.
- 봇 쪽으로 열리는 문도 박아서 **반대로**(봇에게서 멀어지게) 엽니다. 방향은 SAIN이 문짝 모양으로 직접 판정합니다 — DualSideDoorBreach가 깔려 있어도 봇 박치기 방향은 SAIN이 최종 결정(그 모드의 판정은 살짝 덜 닫힌 문이나 경첩 옆에 붙은 봇이면 방향을 틀리게 읽어 문짝이 봇 쪽으로 열렸음). 플레이어 발차기는 그 모드 그대로.
- 박치기가 안 터졌는데 이미 문 앞이면 손으로 여는 대신 그 자리에서 걷어찹니다.
- 박지 않는 문: 게임이 "부술 수 없는 문"으로 정한 문, 미닫이문(연구소 유리문), 문짝이 열릴 자리(문 너머 1.5m 안)에 누가 있는 문, 반대로 돌린 문짝이 내려앉을 자리가 없는 문(좁은 복도). 아군이 문 바로 앞에 있으면 안 함.
- 반대로 열린 문짝(다른 모드·박치기 때문)에 1.5초 막히면 봇이 그 문짝을 원래 열림 방향으로 밀어 돌립니다(공장에서 문짝이 통로를 막아 봇 7명 넘게 제자리 달리기 하던 장면).
- F6 `General > Door Tactics (zzap)`: `Bot Sprint Ram`(봇 전력질주 문 박치기), `Speed Needed`, `Arm Damage`, `Both Sides Of A Door`, `Sprint Ram Into The Room` + 확률, `Sprint Ram While Running Through Doors`, `Ram Chance - Running Away / Rushing / Searching / Other Combat Moves`.
- 확인 상태: **시뮬에서 동작 확인** — 10/13 문 점검 판(4맵 60분) 박치기 시작 32·문 부숨 17, 10/14 공장 판 박치기 3번 모두 문짝이 봇 반대쪽으로 열림. 세관 기숙사 복도에서 7번 시작해 0번 성공하던 것은 이번에 고침(아래 9번, 미검증).
- 같은 기간에 넣었던 Door Overhaul 연동(문 너머 사격·수류탄 문 폭파)은 그 모드의 메모리 사용량(분당 600MB 안팎) 때문에 뺐습니다 — 이번 릴리즈엔 없습니다.

**2. 문 관련 끼임 수정**
- **손으로 여는 문도 밀어서:** F6 `Always Push Open Doors`(문 항상 밀어서 열기, 원래 SAIN 설정인데 이 포크에서 아무 코드도 안 읽던 것)를 되살렸습니다. 봇이 손으로 여는 문이 자기 쪽으로 열리면 반대로 밀어 엽니다 — 문짝에 밀려 벽 틈에 끼던 문제. 문짝 자리에 벽·물건이 있으면 원래대로.
- **연구소 미닫이 유리문:** 문을 "반대로" 여는 동작을 하면 유리문은 닫힌 그대로인데 게임은 열렸다고 여겨 봇이 유리에 계속 달려들었습니다. 이제 미닫이문은 박지도·걷어차지도·반대로 밀지도 않고, 앞에서 열리는 중이면 다 열릴 때까지(최대 2.5초) 기다립니다.
- **열린 문짝 뒤 틈에 낑김:** 적을 조준하며 움직일 때(근접 난전 뒷걸음, 다이아몬드 스텝 옆걸음) 열린 문짝과 벽 사이 틈으로 들어가던 것 → 열린 문짝을 벽처럼 피합니다.
- 확인 상태: 빌드만 확인, 게임 미검증.

**3. 무기 핑퐁 (총을 계속 바꾸던 것) 세 경로**
- 멀리 있는 적 때문에 꺼낸 저격총을 장전하려 하면 게임이 주무기로 되돌리던 것 → 그 총을 그대로 장전.
- 탄창만 빈 주무기를 권총으로 바꾸고 게임이 되돌리면 또 권총이던 것 → 여분 탄창이 있으면 장전을 기다림(6초 넘게 못 하면 교체), 게임이 권총을 넣은 뒤 5초는 다시 안 꺼냄.
- 노리는 적이 "보이는 먼 적"과 "오래 못 본 적" 사이를 오갈 때마다 저격총을 넣었다 뺐다 하던 것(리저브 20초에 4번) → 20초 안에 본 먼 적이 하나라도 있으면 저격총을 그대로 듦.
- 확인 상태: 빌드만 확인, 게임 미검증.

**4. 위층의 안 보이는 적에게 벽·천장 사격**
- 위층(높이 차 2.3m 이상)에 있어 보이지도 않는 적을 소리만 듣고 제압 사격·블라인드 파이어·프리파이어로 벽·천장에 쏘던 것 → 안 보이는 적에게 쏠 때 사선이 막혀 있고 다른 층이거나, 총구 앞 2.5m가 벽이면 안 쏩니다.
- 확인 상태: 시뮬에서 동작 확인(쏘기를 거부한 횟수가 기록됨).

**5. 가까운 적 반응**
- 문 옆으로 2~3m 앞을 적이 지나가도 늦게 반응하던 것: "다른 적과 싸우는 중 나타난 제3자"(×1.5)와 "아직 모르는 적"(×1.5) 발견 지연이 거리 제한 없이 겹쳐 2m 앞 적도 2.25배 늦게 봤습니다. 이제 6m 안이면 두 지연을 빼고, 3m 안이면 어떤 조건도 기본보다 늦게 보지 않습니다.
- 벽 없이 3m 안에 있는 적 봇은 등 뒤라도 발소리를 들은 것처럼 알아챕니다(봇끼리만 — 플레이어가 몰래 다가가는 건 그대로).
- 확인 상태: 빌드만 확인, 게임 미검증.

**6. 뒷걸음질 (적도 안 보이는데 뒤로 걷기)**
- 전투 후 정리 중 멀리 있는 분대장에게 모이러 가거나 먼 엄폐로 갈 때 → 가는 방향을 봄. 무슨 소리를 들었으면 모이러 가지 않고 그 자리에서 소리 쪽을 지킴.
- 엄폐로 걸어갈 때(다리 부상으로 못 뛰거나 장전·치료 중) 적이 안 보이면 → 가는 방향을 봄. 가까운 적(근접 대치 설정 거리 안, 4초 안에 봄)이나 3초 안에 맞은 경우는 예전처럼 적을 본 채 걸음(remake4의 "가까운 대치에서 등 안 보이기"는 그대로).
- 확인 상태: 빌드만 확인, 게임 미검증. (10/14 시뮬 뒷걸음 감지 24건 중 22건이 이 경우였음)

**7. 그 밖의 봇 행동 수정 (사용자 스크린샷·시뮬 장면)**
- **벽에 얼굴 박고 조준:** 적이 나올 코너를 미리 조준했는데 그 코너가 바로 옆 문틀이라 1m 앞 벽을 보던 것 → 조준선이 0.8m 안에서 벽에 막히면 열린 방향을 봄. 서 있을 땐 4m 안 벽도 벽 응시로 봄.
- **자기 수류탄에 죽음:** 실내에서 벽에 튕겨 돌아온 자기 수류탄을 안 피했던 것 → 던진 지 0.7초 넘은 자기 수류탄이 6m 안 트인 곳에 있으면 피하고, 한 번 피하기 시작하면 10m 안이면 터질 때까지 계속 피함(피하다 원래 하던 후퇴로 수류탄 쪽으로 되돌아가던 것).
- **보이는 곳에서 치료:** 엄폐로 "가는 중"을 "엄폐 안"으로 세서 뛰다 말고 구급상자를 꺼냈고, 적이 보이면 주사기 점수가 오히려 올랐음 → 엄폐 도착 전엔 치료 감점, 트인 곳에서 적이 보이면 주사기 감점.
- **턱에 걸려 늦게 점프:** 무릎 높이 턱이 바로 앞이면 1초 만에 넘기·점프(전엔 5초).
- **레이저 켜고 존버:** 전투가 끝나고 ORBIT로 돌아가 2초 넘게 서 있으면 라이트·레이저를 끔.
- **총구가 하늘로:** 수류탄 던지려다 판단이 바뀌어 수류탄을 든 채 남은 봇을 2초 뒤 총으로 되돌림(원인 확정은 아직 — 다음 확인 거리).
- **DMR·저격총 장거리 명중률:** 쏜 뒤 반동 흔들림이 다 풀리기 전에 다음 발을 쏴 돌격소총보다 빗나가던 것 → 장거리에서 흔들림이 3배 빨리 회복.
- **이미 지워진 사수에게 맞은 오류:** 그 피해를 받으면 게임이 위치를 읽다 오류를 내던 것 → "누구한테 맞았다" 반응만 건너뜀.
- 확인 상태: 빌드만 확인, 게임 미검증.

**8. 레이드 일지 — 스크린샷 장면 찾기 (F8)**
- `레이드 일지`를 켜 둔 상태에서 이상한 장면에 **F8**(BepInEx 설정 `Mark Scene Shortcut`)을 누르면, 화면 가운데 봇들의 상태 전체(판단·위치·속도·가는 방향/보는 방향·손에 든 것·총구 높이·옆 문·눈앞 벽·엄폐 도착 여부)와 60초 안에 그 자리에서 죽은 봇이 일지에 남습니다. 제보할 때 스크린샷과 그 줄을 같이 주시면 정확히 찾을 수 있습니다.
- 일지 [Death] 줄에 닉네임·피해 종류·자기 피해(SELFKILL) 표시.

**9. 10/14 시뮬 반영 (이번 릴리즈 직전 수정)**
- 세관 기숙사 복도처럼 문짝이 넘어갈 자리가 없는 문은 달려가기 전에 박치기를 포기하고 평소처럼 엶(전엔 달려가기만 하고 문 앞에서 손으로 엶).
- 뒷걸음질 수정(6번), 자기 수류탄 회피 유지(7번)도 이 판에서 나온 것입니다.
- 확인 상태: 빌드만 확인, 게임 미검증.

**10. 시뮬레이션 랩 (서버 웹 `/sain/sim`, 시뮬 판에만 적용)**
- 문 확인용 F6 프리셋 `zzap TEST 문 모드` 삭제 → 시뮬 페이지의 `문 전술·박치기 점검` 시뮬 프리셋(시뮬 판에서만 박치기 100%·전원 기가채드 문 전술, SAIN 프리셋은 그대로). 옛 문 모드 프리셋 파일은 서버가 자동으로 지웁니다.
- 시뮬 판 ORBIT: 전리품 감지 거리 80 → 35m, 싸움 구역이 끌어당기는 힘 1.5~2 → 3~4(ORBIT 시간의 42~46%가 싸움 구역 밖이었음). 일반 레이드와 ORBIT 파일은 그대로.
- 세관 시뮬 싸움 중심을 기숙사로.
- 시뮬 시체 지우기 때 손에 들던 총도 같이 정리(몸만 사라지고 총이 남던 것).
- 이상 행동 자동 감지 6종 추가(뒷걸음질·맞는 중 천천히 걸음·문 앞 끼임·벽 보고 서 있음·3m 안 적 무반응·전투 밖 레이어의 총격 무시), 분석 md의 경고 정리(매복 1.5초 안 사망 제외, 1초 넘는 멈춤에 램 클리너 스냅샷 안내, SAIN이 이미 막은 다른 모드 오류는 오류 목록에서 제외).

**11. 기타**
- README 원작자 표기를 원작별로 나눠 정리, 변경 이력을 `changelog/` 폴더에 10개씩 나눔.
- 램 클리너 누수 검사가 짚은 SAIN 쪽 작은 참조(문짝 검사 버퍼)를 레이드 뒤 비움.

**알려진 점**
- 위 항목 대부분은 게임에서 사람이 직접 확인하지 못했습니다(시뮬 확인 표시가 있는 것만 시뮬에서 동작 확인).
- 총구가 하늘로 향한 채 굳는 장면은 원인이 확정되지 않았습니다. 보시면 F8 + 스크린샷 부탁드립니다.
- 봇 박치기가 마음에 안 들면 F6 `General > Door Tactics (zzap) > Bot Sprint Ram`을 끄면 전부 꺼집니다(문 전술·다른 기능은 그대로).

**배포 파일**
- 클라이언트 `SAIN.dll`은 사용자 PC 빌드(`dotnet build`, SPT 4.1 참조)로 배포합니다. 서버 모드 zip은 `SAINServerMod/release/SAINServerMod-4.5.1.zip`(Release 빌드가 자동 생성).

------------------------------------------------------------------------------------------------------------------------------------------------------

**<Changes (English)>**

> Everything since remake5 (2 days, 22 commits). Most fixes were found with bot-vs-bot simulation raids and user screenshots. Items marked "seen working in the sim" were checked there; everything else is build-verified only, not tested in game.
> SAIN's internal version number stays 4.5.1 for preset compatibility (same as remake5).

**Install / update**
- From remake5: overwrite **both** the client `SAIN.dll` and the server mod (sim page and presets changed). Also overwrite the zzap presets in the server mod's `Presets/` (Normal/Easy/Low-spec got bot ram chances; the default `zzap` values are unchanged).
- doordash and DualSideDoorBreach are **not required**; if installed they don't conflict (they keep handling the player's own rams/kicks).
- From remake4 or older / original SAIN: follow the remake4 install steps first.

**1. New — bot sprint ram (burst a shut door open at a run, built into SAIN)**
- Code from doordash and DualSideDoorBreach ported into SAIN for bots only. Used on room clears without a grenade (1.8-4.5 m run-up), when fleeing, rushing, searching, and on other combat moves; shut doors 1.5-7.5 m ahead on a straight path, rolled once per door per 10 s.
- Chances (zzap default): room clear 50% / flee 75% / rush 65% / search 50% / other 35% (Normal 35/55/50/35/25, Easy 25/40/35/25/15).
- Bursts at 1.6 m/s+ (doordash's player value is 4.5). Rammer takes 5 left-arm damage (x2 on metal doors) and loses stamina.
- Doors that swing toward the bot are rammed open the other way. SAIN decides the direction from the leaf's own geometry, also when DualSideDoorBreach is installed (its test misread doors left slightly off shut and bots standing beside the hinge, so the leaf came at the bot); player kicks stay with that mod.
- No burst but already at the door: kicks it open instead of opening it by hand.
- Never rammed: unbreachable doors, sliding doors (Labs glass), doors with someone in the leaf's swing, doors whose turned leaf has no room to land (narrow corridors), a teammate right at the door.
- A bot blocked 1.5 s by a reversed leaf pushes it back to its normal open side.
- Settings: F6 `General > Door Tactics (zzap)` - `Bot Sprint Ram` and the related speed / damage / chance entries.
- Status: seen working in the sim (door test runs: 32 starts / 17 doors burst; 10/14 Factory: all 3 rams swung away from the bot). The Customs dorm corridor failures (7 starts, 0 bursts) are fixed in this release (item 9, not tested).
- The Door Overhaul bridge added the same week was removed (that mod used ~600 MB per minute); it is not in this release.

**2. Door jams**
- `Always Push Open Doors` works again: a door a bot opens by hand that would swing into it is pushed the other way (unless the leaf has no room).
- Labs sliding glass doors: never rammed, kicked or pushed the other way (that left the glass shut while the game thought it open), and bots wait up to 2.5 s for one that is still sliding open.
- Aim-walking (close-combat backpedal, diamond step) no longer slips into the gap behind an open leaf.
- Status: build only.

**3. Weapon ping-pong, three paths**
- The long-range gun SAIN pulled out is reloaded instead of the game swapping back to the main gun; an empty-mag main gun waits for its reload (swaps after 6 s) instead of flipping to the pistol and back, and the pistol isn't drawn again for 5 s after the game puts it away; the long-range gun stays out while any far enemy was seen in the last 20 s.
- Status: build only.

**4. No more shooting walls/ceilings at unseen enemies upstairs**
- Fire at an unseen enemy is refused when the line is blocked and the enemy is on another floor, or a wall is within 2.5 m of the muzzle. Status: seen working in the sim.

**5. Close-range reactions**
- Within 6 m the third-party and unknown-enemy spotting slowdowns no longer apply; within 3 m nothing is slower than base. Hostile bots within 3 m with no wall between are noticed even from behind (bots only - sneaking up as a player is unchanged). Status: build only.

**6. Walking backwards with no enemy in sight**
- Post-combat regroups with a far leader / runs to far cover face the walk (and hold if something was heard). Walking to cover (legs broken, reloading, healing) with the enemy out of sight faces the walk; a close enemy seen in the last 4 s or fresh fire still keeps the eyes on the enemy (remake4's no-back-turning stays). Status: build only.

**7. Other behaviour fixes**
- Staring into a wall while pre-aiming a corner right beside it (and standing in cover with a wall within 4 m); dodging its own grenade (0.7 s after the throw, within 6 m, kept until it blows within 10 m); no first aid before reaching cover and no stim in the open with an enemy in sight; knee-high ledge vaulted after 1 s instead of 5; light/laser off when idle in ORBIT after a fight; a grenade left in hand goes back to the gun after 2 s (muzzle-in-the-sky cause not confirmed yet); DMR/sniper recoil sway recovers 3x faster at long range; hit by an already-removed shooter no longer throws. Status: build only.

**8. Raid journal - find a screenshot's scene (F8)**
- With the raid journal on, **F8** (`Mark Scene Shortcut`) writes the full state of the bots on screen and of bots that died there in the last 60 s. [Death] lines show the nickname, damage type and SELFKILL.

**9. 10/14 sim fixes (right before this release)**
- Rams skip doors whose turned leaf has no room before running at them; backwards-walk and own-grenade fixes above. Status: build only.

**10. Simulation lab (server web `/sain/sim`, sim raids only)**
- Door test moved from an F6 preset to the `Door tactics / ram check` sim preset (old preset file removed by the server). Sim ORBIT: loot detect 80 -> 35 m, stronger arena pull. Customs fight centre moved to the dorms. Corpse cleanup also releases the gun in hands. Six new oddity detectors; analysis warnings cleaned up (handled errors from other mods no longer listed as errors).

**11. Other**
- README credits split per original work; changelog split into `changelog/` files of 10. A small SAIN-side reference flagged by RamCleaner's leak check is cleared after the raid.

**Known issues**
- Most items are not tested in game by a person. The muzzle stuck pointing at the sky is not root-caused yet - F8 + screenshot please. To turn the bot ram off: F6 `General > Door Tactics (zzap) > Bot Sprint Ram`.

**Files**
- The client `SAIN.dll` is built on the user's PC (SPT 4.1 references). The server mod zip is `SAINServerMod/release/SAINServerMod-4.5.1.zip` from the Release build.

------------------------------------------------------------------------------------------------------------------------------------------------------

**!!!!!!!!!!!PLEASE READ THIS!!!!!!!!!!!**

Hi everyone,

I'd like to share a few thoughts and important warnings regarding the process of porting mods from version 4.0 to 4.1.

Following the recent "ECOT" mod incident, I've had a lot of time to reflect on my actions.
I’m not here to lecture anyone—I’m writing this simply in the hope that you’ll keep these points in mind.


**1. I am not a professional programmer; I am mostly a "vibe coder" who relies heavily on AI.**

By that, I mean I’m not someone who spends days designing and painstakingly crafting mods by hand from scratch.
At the same time, having tinkered with simple mod edits back in the TS/JS (SPT 2.3.1) days, I know firsthand how much time, effort, and care goes into this process.
Therefore, even if a mod is covered under an MIT License, if the original author requests that I delete the source code and stop distribution, I will willingly comply.


**2. Please DO NOT re-distribute or re-upload these modified mods to 3rd/4th party places.**

Even if my ported version has some fixes or improvements, please do not treat it as a long-term solution. Unlike official releases on "THE FORGE," I do not perform thorough debugging or full security scans.
Furthermore, imagine how disruptive it would be if someone mistook my ported build for an "official release" and pestered the original author about bugs in my version. It would be terrible.
Moving forward, I intend to approach any porting work with the utmost respect for original modders.
If I had spent days pouring my heart into making a mod, only to have someone take my code and modify it however they pleased, I wouldn't feel good about it either.
Therefore, when writing README.md files on GitHub, I will always credit the original creators, and if it's not a mod I built from scratch, I will fully respect the original modder's decisions and opinions.


**3. Please respect the original creators.**

If people ignore warning 2. and act as if my port is the "real deal," I would feel immense guilt.
Please do not do that. As the saying goes, "When in Rome, do as the Romans do"—and showing proper respect and courtesy is essential.
Instead, please visit the original creators' comment sections on The Forge to leave warm words of appreciation, encouragement, or even buy them a coffee/leave a tip if you can


**Thank you for reading this long post.**
**Stay safe and stay happy!**

**^^7**

------------------------------------------------------------------------------------------------------------------------------------------------------

**!!!!!!!!!!!꼭 읽어주세요!!!!!!!!!!**

안녕하세요 여러분

4.0 -> 4.1로 포팅하는 과정에 대해 드릴 말씀과 주의사항을 전달하고자 합니다.

이번 "ECOT" 모드 사건 이후로, 많은 반성의 계기를 가지게 되었습니다

뭔가, 설교를 드리는게 아니라 참고 해주셨으면 하는 마음에 글을 작성합니다.


**1. 저는 전문 프로그래머가 아니고, AI를 주로 이용하는 바이브코더입니다.**

해당말의 뜻은, 저는 매일 구상하고 힘들게 한땀한땀 손으로 모드를 만드는 사람이 아니고

또한, TS/JS (SPT 2.3.1) 시절에 간단하게 모드 수정을 거치며 이 과정이 얼마나 노력을 들이고, 시간이 필요한지 잘 알고 있습니다.

그렇기에, 해당 모드를 수정하면서 아무리 MIT 라이선스를 받았더라도 원 모드 제작자가 소스코드 삭제 및 배포를 중단해달라고 하면 중단할 의향이있습니다.


**2. 무단으로 수정한 모드를 "3차 4차 배포하지 마십쇼"**

제가 만든 버전이 개선점이 있어도, 오래 사용하지 마세요 저는 "FORGE" 에 올린 것 처럼 바이러스 검사나 디버깅을 완벽하게 수행하지 않습니다.

또한, 제가 포팅한 버전을 "진짜 릴리즈된 버전" 이라고 생각하고, 제 포팅 모드를 들고 원작자에게 가면 얼마나 민폐일까요? 끔찍합니다

저는 모더들을 최대한 존중하는 방향으로 포팅할 계획입니다.

아마, 제가 모드를 제작할 때 하루를 갈아넣어 만들었는데 누군가가 멋대로 코드를 뺏겨 지 멋대로 작성했다면 기분을 썩 좋지않을 것 같습니다.

고로 GITHUB에 README.MD 작성 시, 원작자 표기 및 제가 실제로 제작하지 않은 모드라면 최대한 해당 모드를 제작한 모더의 의견을 존중하는 방향으로 나아갈 것입니다.

**3. 원작자를 존중해주세요**

만일 2번 주의사항을 어겼을 시, 제가 포팅한 모드가 "진짜 모드다!" 이런 느낌이면 진짜 죄책감에 휩싸일 것 같습니다.

그런 짓은 하지말아주고, 로마에 왔으면 로마법을 지키듯이 한국에서는 예의를 갖추는 것이 미덕입니다.

해당 모드 제작자의 FORGE COMMENT 창에 들어가 따뜻한 위로와 감사하다는 격려 또는 그들에게 TIP을 건네주세요


**긴 글 읽어주셔서 감사합니다.** 

**행복하세요! ^^7**

^^7
