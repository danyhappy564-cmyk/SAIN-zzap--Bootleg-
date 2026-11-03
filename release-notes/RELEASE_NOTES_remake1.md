# SAIN-zzap--Bootleg- remake1 릴리즈 노트 (fix7 대비)

> fix7(2026-09-25 01:59 KST) 이후 변경분입니다. 아래 "꼭 읽어주세요" 공지 글은 fix7과 동일하게 붙이면 됩니다.

**규모:** 커밋 116개 · C# 파일 110개 변경(새 파일 55개) · 코드 약 +16,600줄.
fix7까지가 "문 끼임·CS 가스 버그 수정" 위주였다면, fix8은 **봇이 싸우는 방식 자체를 새로 만든 판**입니다.
봇끼리 붙이는 팩토리 시뮬(최대 수백 명 스폰, 프리캠 관전)을 10번 돌리며 로그로 확인·수정했습니다.

---

**<변경점>**

- **봇이 "계산해서" 싸웁니다 (유틸리티 AI)**

1. 적이 안 보일 때: 밀기 / 수류탄 / 지키기 / 우회 / 수색 / 후퇴를 "기대 이득" 점수로 비교해서 고릅니다 — 정보가 얼마나 최신인지, 내 체력·탄·수류탄, 수적 우위, 적이 장전·치료 중인지, 성격 등.
2. 적이 보일 때: 계속 쏘기 / 엄폐로 빠지기 / 밀어붙이기를 같은 방식으로 — 적이 나를 보고 있는지, 방금 맞았는지, 가장 가까운 엄폐물 거리(멀면 개활지를 뛰지 않고 그 자리에서 싸움).
3. 재장전·치료 타이밍도 계산: 적이 나를 보는 중이면 참고 쏘고, 안 보일 때 장전. 적 눈앞에서는 치료를 시작하지 않고, 치료 중엔 피킹 안 함.
4. 든 총에 맞게 싸움: 저격총은 거리 유지, SMG·샷건은 붙어서, 수류탄·치료템이 많으면 더 적극적으로.
5. 두려움(동료 사망·부상·수적 열세)과 "사람 같은 실수"(가끔 2·3순위 선택, 티미 12% ~ 기가채드 2%)가 성격마다 다르게 들어갑니다.
6. 위협 기준 목표 선택: 뒤에서 나를 쏘는 적이 있으면 그쪽으로 돌아섭니다. PMC끼리 싸우다 스캐브가 끼면 둘 다 스캐브만 쏘던 "티밍"도 사라졌습니다.

- **고인물 전술 추가**

1. 문 전술: 점프 피킹, 런바이 피킹, 스텝 피킹, 페이크 수류탄·치료, 방 진입(옆에서 문 열기 → 페이크 섞기 → 수류탄 폭발 확인 후 돌입), 방 가두기, 문 옆 매복.
2. 다이아몬드 스텝(와다다 무빙) + 기울이기 섞기, **코너 흔들기 피킹**(A↔D로 벽 뒤에 숨었다 드러났다 하며 사격, 확률 50%).
3. 코너 추격(적이 코너 뒤로 빠지면 기울여 진입 / 파이 / 점프샷 / 프리파이어), 코너 선조준(적이 나올 자리를 미리 조준, 조준 폭은 F6에서 조절).
4. 재배치: 먼저 맞았으면 엄폐 → 치료 → 수류탄 → 다른 각으로 이동, 미끼 피킹, 가짜 재장전.
5. 분대 교전: 크로스파이어(실내면 뒷문 막고 협공), 엄호, 트레이드, 약한 적 일제 돌격, 아군 사선 안 들어가기(팀킬 방지).
6. 후퇴할 때 고개 숙이고 지그재그, 적이 안 보이는 동안 플래시·레이저 끄기.

- **전투 후 정리 (ORBIT 인계 빈틈 메우기)**

1. 싸움이 끝나면 엄폐로 이동 → 장전 → 치료 → 경계 → 성격에 따라 킬 자리 확인. 원래는 ORBIT이 받기 전 15초 동안 바닐라 행동(걸으며 치료 등)으로 떨어졌습니다.
2. 근처 총소리·발소리가 들리면 계속 SAIN이 붙잡고 경계합니다. **ORBIT 코드는 건드리지 않았습니다.**

- **플레이어 적응 · 학습 (기본 켜짐)**

1. 레이드마다 **내 플레이 스타일**(뛰는 비율, 점프, 기울이기 피킹, 수류탄, 존버 시간 등)을 통계로 저장합니다.
2. 기록이 10분 이상 쌓이면 봇이 거기에 대응합니다: 뛰어다니는 사람 → 코너에서 기다렸다 붙어서 싸움, 버니합 → 다가올 코너 지키기, 기울이기 피킹 → 기울이기로 맞받아침, 수류탄 → 간격 벌리기.
3. 봇이 나에게 한 대응마다 결과(맞히면 성공, 죽으면 실패)를 판을 넘어 누적 → **나한테 잘 통한 대응을 더 자주** 씁니다.
4. 레이드 중에 내 위치를 몰래 아는 식이 아니라, **끝난 판의 통계만** 씁니다. 내 장비로는 판단하지 않습니다. F6에서 끌 수 있습니다.

- **오래된 버그 수정**

1. 무기 계속 바꾸기(주무기↔보조↔권총 핑퐁): BSG의 탄 확인 없는 무기 토글이 원인 → 이제 탄 있는 총으로만 바꿉니다.
2. 빠른 재장전이 탄창을 다 버려서 탄창이 바닥나던 것 → 여분이 2개 이상일 때만 버립니다.
3. 벽에 계속 박다가 적이 오면 풀리던 것: 원본 SAIN의 끼임 해제 기능이 꺼져 있었음 → 새 끼임 감시 기능(시뮬 10차에서 끼임 전부 자동 해제 확인).
4. 죽은 채로 서서 걷거나 점프하는 시체 → 사망 처리 중 오류가 나도 시체가 제대로 쓰러지게 수정(시뮬 10차에서 0건).
5. 봇·플레이어 모두 못 여는 문 → 문 내부 상태까지 맞춰서 복구.
6. **원본 SAIN 버그:** 선택한 프리셋이 아니라 마지막으로 읽힌 프리셋의 설정이 적용되던 문제 수정.

- **기타**

1. F6 편집기 한국어화(설정 이름·설명·버튼·선택지), 상단 [한]/EN 전환, 한국어·영어 둘 다 검색.
2. PMC 분대 무전 대사, 맞을 때 "윽" 음성 기본 끔.
3. **처음 설치(또는 이 포크로 처음 덮어쓸 때) 한 번만 `zzap` 프리셋이 자동 선택됩니다.** 직접 만든 프리셋을 쓰던 분은 그대로 유지되고, 이후엔 F6에서 고른 게 유지됩니다.
   원본 SAIN이 저장된 프리셋 이름을 "포함" 여부로 찾아서 `zzap`이 `zzap TEST ...`로 잡히던 버그도 고쳤습니다.
   테스트 프리셋 이름이 `zzap TEST [시뮬 전용]`으로 바뀌었습니다. **업데이트할 때 서버 모드 폴더의 `Presets/zzap TEST.json`을 지워 주세요.** 실제 플레이는 `zzap` 프리셋을 쓰세요.
4. 키 입력 기록·레이드 일지·진단 로그는 기본으로 꺼져 있고, 꺼져 있으면 로그 문장 자체를 만들지 않습니다(분석용).
5. **램 누수 수정:** 지난 레이드 전체가 다음 레이드 내내 램에 남던 문제(두 번째 판부터 램이 크게 오름).
6. **시체 피·총알 자국 텍스처 비우기:** EFT가 맞은 부위마다 만드는 자국 텍스처(개당 약 4MB)를 시체 것까지 레이드 끝까지 들고 있어 사망이 많을수록 램·VRAM이 수 GB 늘던 것 — 죽은 지 30초 뒤 자국을 지우고 재사용. 시체는 그대로. 버그를 보면 F6에서 `레이드 일지`를 켜고 한 판 돌리면 원인 파악에 도움이 됩니다.

^^7

------------------------------------------------------------------------------------------------------------------------------------------------------

**Changes** (since fix7 — 116 commits, 110 C# files, ~16,600 lines added)

fix7 was mostly door/CS gas bug fixes. fix8 rebuilds **how bots fight**, tuned over 10 bot-vs-bot Factory simulations watched in free cam.

- **Bots now fight by the numbers (utility AI)**

1. Enemy out of sight: push / grenade / hold / flank / search / fall back are scored by expected gain (info age, own health/ammo/grenades, numbers, enemy reloading or healing, personality).
2. Enemy in sight: keep shooting / take cover / push, scored the same way (is he looking at me, did I just get hit, how far is the nearest cover - no more sprinting across the open).
3. Reload and heal timing are scored too: no healing in the enemy's face, no peeking while healing.
4. Fights to its loadout: snipers keep range, SMGs/shotguns close in, more grenades/meds = used more.
5. Fear (dead mates, wounds, outnumbered) and "human mistakes" (sometimes picks the 2nd/3rd best option; Timmy 12% ... GigaChad 2%) per personality.
6. Threat-based targeting: bots turn on whoever is shooting them from behind; PMCs no longer "team up" on a scav mid-fight.

- **Veteran tactics**

1. Door tactics: jump peek, run-by peek, step peeks, fake grenade/heal, room clear (open from the side, fakes, dash in after the real blast), room trap, door ambush.
2. Diamond stepping with lean mixing, and a **corner jiggle peek** (quick A/D steps in and out of cover while shooting, 50% chance).
3. Corner chase (lean in / wide pie / jump shot / prefire), corner pre-aim at where the enemy will come out (aim width adjustable in F6).
4. Reposition after being hit first, bait peeks, fake reloads.
5. Squad combat: crossfire (hold the back door indoors, then pincer), cover, trade, storm a weak enemy, never walk into a teammate's line of fire.
6. Head down + weaving when retreating; light/laser off while holding an angle.

- **Post-combat tidy-up (fills the gap before ORBIT takes over)**

1. After a fight: move to cover, reload, heal, watch, then (by personality) check the kill spot - instead of dropping to vanilla behaviour for 15s.
2. Nearby gunfire/footsteps keep the bot on guard in SAIN. **ORBIT itself is not modified.**

- **Player adaptation & learning (on by default)**

1. Records your play style per raid (movement, jumps, lean peeks, grenades, camping...).
2. After 10+ minutes of records, bots counter it: runners get held corners and close fights, bunny hoppers get the approach corner watched, lean peekers get lean-rocked back, grenade users make squads spread out.
3. Every response a bot makes against you is scored (hit you = win, killed by you = loss) across raids; what worked against **you** gets picked more.
4. Only finished-raid statistics are used - no live wallhack of your position. Your gear is ignored. Can be turned off in F6.

- **Long-standing bug fixes**

1. Endless weapon swapping (primary/secondary/pistol ping-pong) - BSG's blind weapon toggle; bots now only swap to a gun that has ammo.
2. Quick reloads dropping every magazine until none were left - now only with 2+ spares.
3. Bots running into walls until an enemy showed up - upstream SAIN's unstuck logic was switched off; new stuck watchdog (all stuck events freed in sim 10).
4. Dead bodies walking/jumping around - death handling now finishes the ragdoll even if some mod's death handler throws (0 in sim 10).
5. Doors nobody (bots or player) could open - the door's internal interaction state is now repaired too.
6. **Upstream SAIN bug:** the last-read preset's global settings were applied instead of the selected one.

- **Misc**

1. F6 editor in Korean ([한]/EN toggle, search in both languages).
2. PMC squad voice callouts and hit-pain voice off by default.
3. **On first install (or the first time this fork is dropped over an older build) the bundled `zzap` preset is selected once.** Players on a preset of their own keep it, and later choices in F6 stick.
   Also fixed upstream SAIN matching the saved preset name with "contains", which loaded `zzap TEST ...` for a saved `zzap`.
   The test preset is now `zzap TEST [시뮬 전용]` (simulation only). **When updating, delete `Presets/zzap TEST.json` in the server mod folder.** Play with the `zzap` preset.
4. Key recording, the raid journal and diagnostic logs are off by default, and no log text is built while they are off (analysis tools).
5. **RAM leak fix:** the whole previous raid stayed in memory through the next raid (RAM jumped from the second raid on).
6. **Corpse blood decal textures freed:** EFT gives every hit body mesh its own blood/bullet-hole texture (~4 MB) and keeps them, corpses included, until the raid ends - several GB with many deaths. 30s after a death the marks are wiped and the texture is reused. Corpses stay. If you see a bug, turn on `Raid Journal` in F6 for a raid.

^^7
