# SAIN-zzap--Bootleg- remake5 릴리즈 노트 (Remix4.2 대비)

> Remix4.2(10/6) 이후 변경분입니다. remake2·remake4의 안내 사항(램 사용량, 스터터링, 렉·버그 제보 방법 등)은 그대로 유효합니다.
> 이번 판의 수정은 대부분 **봇끼리 싸우는 시뮬레이션**으로 확인했습니다. 사람 플레이어 상대 체감은 아직 충분히 확인하지 못했으니, 이상하면 제보 부탁드립니다.

**<설치>**

- **Remix4.2 / remake4에서 올라오는 경우:** 클라이언트 `SAIN.dll`과 **서버 모드 둘 다** 덮어써 주세요. 이번엔 서버 모드도 바뀌었습니다(웹 페이지·프리셋).
  - 서버 모드 폴더 `Presets/`의 zzap 프리셋도 덮어써 주세요(보통·쉬움·저사양·TEST 값이 바뀌었습니다). 기본 `zzap` 프리셋은 그대로입니다.
  - 프리셋을 따로 복사해 고쳐 쓰는 분은 아래 "설정" 항목의 새 값을 직접 넣어 주세요.
- **remake3 이하 / 원본 SAIN에서 올라오는 경우:** remake4 노트의 설치 순서대로 해 주세요.

---

**<변경점>**

- **새로 생긴 봇 행동**

1. **교전 중 피킹 3종** — 적이 보이는 상태에서 코너·문틀에 붙어 싸울 때, 숨었다 나왔다를 반복하며 쏩니다. 교전마다 한 번 종류를 정합니다.
   - 스텝 피킹(60%): A/D로 짧게 나갔다 들어오며 사격 (예전 "코너 흔들기")
   - 점프 피킹(25%): 옆으로 점프해 나가 확인 → 착지해서 사격 → 다시 숨기. 공중에선 안 쏘고, 한 교전에 최대 3번
   - 런바이 피킹(15%): 트인 곳을 반대편 엄폐까지 뛰어 건넌 뒤 그쪽에서 피킹
   - 점프·런바이는 공격적인 봇(Chad·GigaChad, PMC Normal의 절반)만 합니다. 나머지는 스텝만.
2. **분대 협동 (제압 + 측면 + 후방)** — 같은 분대 2명 이상이 숨어 있는 같은 적(15~80m)을 상대하면 역할을 나눕니다.
   - 1명은 다른 각도로 돌아가고(측면), 나머지는 그 자리에 제압 사격, 3명 이상이면 1명은 뒤를 봅니다.
   - 측면조가 사격 선 근처에 오면 제압 사격을 멈춥니다(아군 사격 방지). 최대 25초, 끝나면 그 적에게 25초 동안 다시 안 함.
   - 확률: zzap 60% / 보통 45% / 쉬움 30%
3. **봇끼리 발소리를 듣습니다** — 원래 SAIN은 봇의 발소리를 다른 봇에게 안 들려줘서, 봇끼리는 보이거나 쏴야만 알아챘습니다.
4. **빈틈 노리기** — 적이 장전(약 2.5초)·치료(5초)·수술(10초)·루팅(10초) 하는 소리를 들으면, 그 시간 안에 뛰어서 닿을 수 있을 때만 달려듭니다.
   수류탄 핀 뽑는 소리를 들으면 반대로 물러납니다.
5. **조용히 다가가기** — 안 보이는 적에게 다가갈 때 30m 안에선 걷고, 12m 안에선 숙여 걷습니다(겁 많은 성격일수록 자주).
6. **먼 총소리는 덜 반응** — 100m 안 총소리는 수색, 100~250m는 고개만 돌림, 250m 밖은 무시. 나를 쏜 사람은 거리와 상관없이 반응합니다.
7. **층이 다르면 소리가 작게** — 실내에서 위·아래층 발소리는 작게 들립니다(총소리는 거의 그대로).
8. **벽 보고 서 있지 않기** — 숨은 적 쪽이 벽으로 막혀 있으면 벽 대신 적이 나올 코너를 봅니다.

- **바뀐 행동**

1. **"얼음 매복" → "정지 및 매복"** 으로 이름을 바꿨고, 조건을 손봤습니다.
   - 멀리 있는 적(45m 이상)에게는 덜 매복합니다.
   - 다른 적이 나를 볼 수 있는 자리면 매복하지 않습니다.
   - 매복 직후 바로 한 발짝 내딛어 엄폐 밖으로 나가던 것을 고쳤습니다.
2. **이동할 때 조준 해제** — 엄폐·후퇴·돌격으로 움직이며 쏘지 않을 땐 조준을 풀고 제 속도로 움직입니다. 예전엔 조준한 채 느리게 걸어 맞아 죽는 일이 많았습니다.
   맞는 중이면 엄폐로 바로 뜁니다.
3. **문 근처 느린 걸음 줄임** — 싸움 중 급하게 움직일 땐 열린 문은 무시하고, 닫힌 문 바로 앞에서만 조심합니다.
4. **후퇴 점프** — 뛰기 시작하자마자 짧게 뛰던 점프를 없애고, 속도가 붙은 뒤에 길게 뜁니다. 엄폐 3~5m 앞에선 착지 점프로 엄폐에 꽂힙니다.
5. **판단이 왔다 갔다 하던 것 줄임** — 맞는 중에 매 순간 다시 고르던 것, 적이 잠깐 가려질 때마다 행동이 바뀌던 것을 고쳤습니다.
6. **총구가 좌우로 흔들리던 것** — 적이 1.5초 안으로 잠깐 가려지면 마지막으로 본 자리를 계속 조준합니다.
7. **돌격하면서 치료하지 않음** (빈사 상태 스팀만 예외).
8. **하늘 보던 것** — 적이 안 보일 때 위아래 시선을 ±25°로 제한했습니다.
9. **문 전술** — 평범한 성격(Normal) PMC도 문 피킹을 하고, 위·아래층 문을 잘못 고르던 것을 고쳤습니다.

- **버그 수정**

1. **총을 계속 바꾸던 문제(핑퐁)** — 빈 주무기 ↔ 권총을 몇 초마다 바꾸던 것. 원인이 여러 개였고 전부 막았습니다(시뮬 기준 70번 → 2번).
2. **주운 총으로 장전·무기 교체를 못 하던 문제** (Remix4.2 제보) — 이제 주운 총도 장전하고, 탄이 떨어지면 탄 있는 총으로 바꿉니다. 칼 들고 걸어오던 것도 없어졌습니다.
3. **로그·군즈·보스·레이더·블랙 디비전이 zzap 프리셋에서 바닐라처럼 싸우던 문제** — "PMC만" 설정이 PMC가 아닌 봇을 전부 빼고 있었습니다. 이제 스캐브만 뺍니다(설정 이름도 "스캐브 제외"로 바뀜).
4. **연막만 든 봇이 연막을 수류탄처럼 던지던 것** 을 막았습니다.
5. **멈춰 서 있던 봇** — 갈 곳 없는 후퇴, 엄폐 바로 앞에서 멈춤, 잘못된 경로를 "도착"으로 착각, 빈 총으로 후퇴만 하던 것 등을 고쳤습니다.
6. **게임 오류 여러 개 차단** — 총구 연기 오류(맵당 수백 번), 무기 바꾸는 중 죽은 봇, 엎드리기 소리 오류 등.
7. 자리키·부하·BTR·마크스맨·감염자는 항상 바닐라로 고정했습니다(원본 SAIN에선 자리키가 SAIN을 받아 등대가 쉬워지기도 했음).

- **시뮬레이션 랩 (새 테스트 도구, 일반 플레이와 무관)**

1. 봇끼리 맵을 돌며 싸우게 하고 결과를 자동으로 분석하는 도구입니다. F6에서 `zzap TEST [시뮬 전용]` 프리셋을 고르고 SAIN 웹(`/sain/sim`)의 "시뮬 시작"을 누르면 됩니다.
2. 분석 페이지에서 싸움 요약, 기능별 발동 횟수, 이상 행동(판단 뒤집힘·제자리 등)을 볼 수 있습니다. **TEST 프리셋에서만 작동합니다.**
3. SAIN 웹 페이지가 전부 한글이 되었습니다(English 전환 가능).

- **설정**

1. 새 F6 설정: 봇이 다른 봇의 발소리를 들음, 벽 보고 서 있지 않기, 코너 피킹 점프·런바이 비율, 분대 협동 / 분대 협동 확률, 점프 중 사격 확률, 후퇴 착지 점프 확률
2. 이름만 바뀐 설정: "PMC만" → **"스캐브 제외"**, "얼음 매복" → **"정지 및 매복"** (기존 값은 그대로 유지)

- **테스트 결과 (공장 30분 시뮬, 최종 빌드)**

1. 봇 94명 등장·87명 사망, **게임 오류 0, 멈춤 0, 튕김 0**, 최저 61fps
2. 교전 중 피킹(스텝 47·점프 12·런바이 5회 선택), 분대 협동 18회, 빈틈 노리기·조용히 다가가기 등 새 기능 모두 실제로 작동
3. 무기 핑퐁 2회(이전 70), 아군 사격 사망 0

- **알려진 문제**

1. 아직 남은 작은 문제: 무기 핑퐁 2회, 정지 및 매복 중 사망 1회, 판단이 몇 번 뒤집히는 장면 — 다음 Remix에서 계속 고칩니다.
2. 런바이 피킹은 공장처럼 좁은 맵에선 반대편 엄폐가 없어 잘 안 나옵니다.
3. 분대 협동은 측면으로 돌 자리가 없으면 바로 취소됩니다(공장 실내에서 자주).
4. **사람 플레이어 상대로는 충분히 확인하지 못했습니다.** 봇이 너무 세거나 이상하면 BepInEx `LogOutput.log`와 함께 제보 부탁드립니다.

^^7

------------------------------------------------------------------------------------------------------------------------------------------------------

# SAIN-zzap--Bootleg- remake5 release notes (vs Remix4.2)

> Changes since Remix4.2 (Oct 6). The notes from remake2 and remake4 (RAM usage, stuttering, how to report lag/bugs, ...) still apply.
> Most of this release was checked with **bot-vs-bot simulations**. How it feels against a human player is not fully tested yet — please report anything odd.

**Install**

- **Coming from Remix4.2 / remake4:** overwrite the client `SAIN.dll` **and the server mod** — the server mod changed this time (web pages, presets).
  - Also overwrite the zzap presets in the server mod's `Presets/` folder (normal, easy, low-spec and TEST values changed). The default `zzap` preset is unchanged.
  - If you keep your own copy of a preset, add the new values from "Settings" below by hand.
- **Coming from remake3 or older / upstream SAIN:** follow the install steps in the remake4 notes.

---

**Changes**

- **New bot behavior**

1. **Three fighting peeks** — with the enemy in sight at a corner or door frame, bots keep showing and hiding while they shoot. One style per engagement:
   - step peek (60%): quick A/D steps out and back while shooting (the old "corner jiggle")
   - jump peek (25%): hop out sideways to look, land and shoot, hide again. No shots in the air, at most 3 per fight
   - run-by peek (15%): sprint across the opening to cover on the far side, then peek from there
   - Jump and run-by only for aggressive bots (Chad, GigaChad, half of the PMC Normals); everyone else step-peeks.
2. **Squad maneuver (suppress + flank + rear)** — 2+ squadmates against the same hidden enemy (15-80 m) split up:
   - one flanks to another angle, the rest suppress from where they are, with 3+ one watches the rear.
   - Suppressors stop firing when the flanker gets near their line. Up to 25 s, then 25 s before trying again on that enemy.
   - Chance: zzap 60% / normal 45% / easy 30%
3. **Bots hear each other's footsteps** — upstream SAIN never let bots hear other bots walking, so bots only noticed each other by sight or gunfire.
4. **Punishing openings** — hearing an enemy reload (~2.5 s), heal (5 s), do surgery (10 s) or loot (10 s), a bot rushes him only if it can get there in time.
   Hearing a grenade pin pulled makes it back off instead.
5. **Moving quietly** — closing in on a hidden enemy, bots walk within 30 m and crouch-walk within 12 m (cautious personalities more often).
6. **Distant gunfire matters less** — within 100 m they search, 100-250 m they only look, beyond 250 m they ignore it. Someone shooting at them always gets a reaction.
7. **Sound through floors** — indoors, footsteps from the floor above/below sound quieter (gunshots mostly don't).
8. **No staring at walls** — if the hidden enemy is behind a wall, they watch the corner he'd come around instead.

- **Changed behavior**

1. **"Freeze ambush" renamed "Hold & ambush"**, with new rules: less against far enemies (45 m+), never where another enemy can see them, and no step out of cover right after starting.
2. **No aiming while moving** — moving to cover / retreating / rushing without shooting, bots drop ADS and move at full speed (before, they crawled along aimed and got shot). Under fire they run to cover at once.
3. **Less slow-walking near doors** — in a hurry they ignore open doors and only slow down right at a closed one.
4. **Retreat jumps** — no more short hop on the first sprint frame; they jump once at speed, and land into cover 3-5 m ahead.
5. **Less flip-flopping** — no re-deciding every moment while under fire, and no switching every time an enemy blinks out of sight.
6. **No muzzle wobble** — when the enemy disappears for under 1.5 s, they keep aiming where he was last seen.
7. **No healing while rushing** (except a stim when dying).
8. **No more staring at the sky** — up/down look is limited to ±25° while the enemy is out of sight.
9. **Door tactics** — Normal PMCs peek doors too, and doors on another floor are no longer picked by mistake.

- **Bug fixes**

1. **Weapon ping-pong** — swapping an empty main gun and the pistol every few seconds. Several causes, all blocked (sim: 70 → 2).
2. **Picked-up guns not reloaded / no weapon swap** (Remix4.2 report) — bots now reload picked-up guns and switch to a gun with ammo when one runs dry. No more walking in with a knife.
3. **Rogues, Goons, bosses, raiders and Black Division fighting like vanilla on the zzap preset** — the "PMC only" setting excluded every non-PMC. It now only excludes Scavs (renamed "Exclude Scavs").
4. **Bots throwing a smoke grenade as if it were a frag** — fixed.
5. **Bots standing still** — retreating with nowhere to go, stopping right before cover, taking a failed path as "arrived", only retreating with an empty gun — fixed.
6. **Several game errors contained** — muzzle smoke error (hundreds per map), bots dying mid weapon swap, prone sound errors.
7. Zryachiy and his followers, event Zryachiy, BTR, marksmen and infected always stay vanilla (upstream SAIN could make Zryachiy easy).

- **Simulation lab (new testing tool, no effect on normal play)**

1. Bots fight each other across a map rotation and the results are analyzed automatically. Pick the `zzap TEST [시뮬 전용]` preset in F6 and press "시뮬 시작" (start) in the SAIN web page (`/sain/sim`).
2. The analysis page shows fight summaries, how often each feature fired and odd behavior (flip-flops, standing still, ...). **Works with the TEST preset only.**
3. The SAIN web pages are now in Korean, with an English switch.

- **Settings**

1. New F6 settings: bots hear other bots' footsteps, don't stare at walls, corner peek jump / run-by shares, squad maneuver + its chance, jump air-shot chance, retreat landing jump chance
2. Renamed only: "PMC only" → **"Exclude Scavs"**, "Freeze ambush" → **"Hold & ambush"** (your values stay)

- **Test results (Factory 30 min simulation, final build)**

1. 94 bots spawned, 87 died — **0 game errors, 0 freezes, 0 crashes**, lowest 61 fps
2. Fighting peeks (picked step 47 / jump 12 / run-by 5), squad maneuver 18 times, openings and quiet approach — all new behavior actually ran
3. Weapon ping-pong 2 (was 70), friendly-fire deaths 0

- **Known issues**

1. Small leftovers: weapon ping-pong 2, one death during hold & ambush, a few decision flip-flops — to be fixed in the next Remix.
2. Run-by peeks are rare on tight maps like Factory (no cover across the opening).
3. The squad maneuver cancels when there is no flank spot (common indoors on Factory).
4. **Not fully tested against human players.** If bots feel too strong or odd, please report with BepInEx `LogOutput.log`.

^^7
