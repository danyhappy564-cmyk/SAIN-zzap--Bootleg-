# SAIN-zzap--Bootleg- remake4 릴리즈 노트 (remake3 대비)

> remake3 이후 변경분입니다. 아래 "꼭 읽어주세요" 공지 글은 이전 릴리즈와 동일하게 붙이면 됩니다.
> remake2의 안내 사항(램 사용량, 스터터링, 렉·버그 제보 방법 등)은 그대로 유효합니다 — `changelog/RELEASE_NOTES_remake2.md` 참고.

**<설치>**

- **remake3에서 올라오는 경우:** 그냥 덮어쓰면 됩니다. 서버 모드 폴더 `Presets/`의 zzap 프리셋 5개도 덮어써 주세요(쉬움·보통·저사양 값이 바뀌었습니다).
- **remake2 이하 / 원본 SAIN에서 올라오는 경우:** remake2 노트의 "깔끔하게 지우고 설치" 순서대로 해 주세요.
- **설치 후 게임을 한 번 완전히 재시작해 주세요.** 새 설정 "게임 기본 봇 탈출 끄기"는 재시작해야 적용됩니다.

---

**<변경점>**

- **방 안에서 버티는 플레이어 대응**

1. 방 안에서 조용히 버티면 봇이 그냥 문을 열고 걸어 들어오던 문제를 고쳤습니다. 원래 있던 "교착 대응"(10~60초 조용하면 문 전술·수류탄으로 방 정리)이
   오래된 25초 조건에 막혀 사실상 꺼져 있었습니다. 이제 60초까지 작동합니다.
2. 수색하러 오는 봇도 문 너머 방이면 들어가기 전에 방을 정리합니다(페이크 → 수류탄 또는 돌진). 조용히 버틸수록 수류탄·페이크 쪽으로 기웁니다.
3. 문 앞 페이크 수류탄(꺼내는 소리로 유인)이 거리·시간 조건이 안 맞아 거의 취소되던 것을 맞췄습니다.

- **목표 선택**

1. 적 둘과 동시에 싸울 때 목표가 0.1초마다 바뀌며 사격이 끊기던 버그를 고쳤습니다.
2. 다른 적과 싸우는 중에도 **옆으로 가까이 다가오는 적**(보이거나, 10m 안에서 발소리가 들리면)으로 목표를 바꿉니다. 전에는 쏘기 전까지 무시했습니다.
   지금 나를 쏘고 있는 적에게서는 쉽게 돌아서지 않고, 목표가 오락가락하지 않게 막아 두었습니다.

- **교전 동작**

1. 적이 문틀 뒤로 막 숨었거나 근처에서 발소리가 났을 때 장전하지 않고 겨눈 채 기다립니다(장전하다 다시 나온 적에게 죽던 것).
2. 다이아몬드 스텝이 봇마다 다르게 섞입니다 — 스텝만, 스텝 + Q/E, 쏠 때 잠깐 멈추는 봇. **맞는 중에는 절대 멈추지 않습니다.**
3. 문/코너 흔들기 피킹(A↔D 들락날락)이 한 번 하고 끊기던 것을 고쳤고, 적을 처음 본 순간 숨지 않고 먼저 쏩니다.
4. 가까운 적을 보면 근접전으로 바로 들어갑니다(서서 쏘기 → 근접전 전환 때 조준이 다시 시작되던 지연 감소).
5. 조준선 피킹 자리를 머리 기준으로 고릅니다(총구만 문틀 밖, 머리는 벽 뒤에서 못 보던 것).
6. 맞는 중에 제자리에서 지키기 금지, 엄폐가 없는데 엄폐를 고르던 것, 엄폐로 빠졌다가 바로 다시 돌격하던 왕복을 막았습니다.
7. 후퇴할 때 버니 점프가 조금 더 자주 나옵니다.

- **문 전술·분대·수류탄**

1. 문 전술이 실제로 끝까지 실행되는 비율을 올렸습니다(시작 조건 정리, "지키기"를 고르면 문 옆을 지킴, 런바이 재설계).
2. 동료가 죽으면 **실제로 죽인 적**에게 보복합니다(전에는 엉뚱한 적을 노리는 경우가 많았음).
3. 수류탄: 공장이 실외로 판정되던 문제를 고쳤고, 실내에서도 포물선으로 던집니다(벽에 튕기던 낮은 투척 감소). 문 너머로 굴리기, 프리셋별 최대 거리.

- **탈출**

1. SAIN 탈출 행동을 꺼도 **게임 기본 봇 탈출**이 남아 있어서, 교전이 끝난 직후 틈에 봇이 출구로 빠지던 것을 막았습니다.
   F6 설정의 탈출 항목 → `게임 기본 봇 탈출 끄기`(기본 켬, **재시작 필요**). ORBIT의 자체 탈출 설정에는 영향이 없습니다.
   (55분 테스트 레이드에서 기본 탈출 0회, 사라진 봇 0명 확인)

- **프리셋**

1. `zzap 쉬움`·`zzap 보통`(저사양 포함)은 문 전술·페이크·분대 돌격·수류탄 거리 같은 **트릭이 나오는 빈도**도 난이도에 맞게 낮췄습니다. `zzap`은 기본값 그대로입니다.

- **알려진 문제**

1. 테스트 중 한 번, 레이드 13분쯤 게임이 갑자기 멈춘 적이 있습니다. 로그상 SAIN 오류나 부하는 없었고 원인은 확인되지 않았습니다(당시 VRAM 98%).
   같은 증상이 생기면 BepInEx `LogOutput.log`와 함께 SPT 폴더 안 `Logs\`의 최신 폴더 로그를 보내 주세요.

^^7

------------------------------------------------------------------------------------------------------------------------------------------------------

# SAIN-zzap--Bootleg- remake4 release notes (vs remake3)

> Changes since remake3. The notes from remake2 (RAM usage, stuttering, how to report lag/bugs, ...) still apply — see `changelog/RELEASE_NOTES_remake2.md`.

**Install**

- **Coming from remake3:** just overwrite. Please overwrite the five zzap presets in the server mod's `Presets/` folder too (easy / normal / low-spec values changed).
- **Coming from remake2 or older / upstream SAIN:** follow the clean install steps in the remake2 notes.
- **Restart the game completely once after installing.** The new setting "Disable Vanilla Bot Extract" only applies after a restart.

---

**Changes**

- **Players camping in a room**

1. Fixed bots simply opening the door and walking in on a player camping quietly in a room. The existing "stalemate" response (quiet for 10-60s ->
   door tactic / grenade room clear) was cut off by an old 25-second limit, so it practically never ran. It now works up to 60 seconds.
2. Bots coming to search a room behind a door clear it before entering (fakes -> grenade or dash). The quieter the camper, the more likely grenades and fakes.
3. The fake grenade at the door (the draw sound as bait) was almost always cancelled by mismatched distance/time limits — fixed.

- **Target selection**

1. Fixed the target flipping every 0.1s (and the shooting stopping) when fighting two enemies at once.
2. While fighting someone else, bots now turn to **an enemy closing in on their side** (in sight, or footsteps heard within 10m). Before, they ignored you until you fired.
   They don't easily turn away from someone who is shooting at them right now, and target flip-flopping is guarded against.

- **Combat**

1. When the enemy has just ducked behind a door frame, or footsteps were heard nearby, bots keep their gun up instead of reloading (they used to die mid-reload).
2. Diamond stepping is mixed per bot — steps only, steps + Q/E lean, or a short plant to fire. **Never planted while being hit.**
3. Door/corner jiggle peeks (A <-> D in and out) no longer stop after one peek, and a bot that just spotted you shoots first instead of ducking.
4. Bots go into close-quarters fighting right away when an enemy is close (less delay from restarting the aim when switching actions).
5. Peek spots are chosen so the head can see the angle (no more muzzle past the frame with the head still behind the wall).
6. No holding still while being shot, no picking "go to cover" with no cover around, no cover <-> push flip-flop.
7. A bit more bunny-hopping while retreating.

- **Door tactics, squads, grenades**

1. Door tactics now run to completion much more often (start rules, "hold" picks hold the door from the side, run-by rework).
2. When a teammate dies, bots retaliate against **the enemy who actually killed them**.
3. Grenades: fixed Factory counting as outdoors; indoor throws are lobs now (fewer flat throws bouncing off walls); rolls through doorways; max throw distance per preset.

- **Extraction**

1. With SAIN's own extract behavior off, the game's **vanilla bot extraction** stayed active, so right after a fight bots could walk out to an exit.
   F6, Extract settings -> `Disable Vanilla Bot Extract` (on by default, **restart required**). ORBIT's own extraction settings are not affected.
   (55-minute test raid: vanilla extraction 0 times, no bots vanished.)

- **Presets**

1. `zzap 쉬움` / `zzap 보통` (and low-spec) also lower **how often the tricks happen** (door tactics, fakes, squad storms, grenade range) to match the difficulty. `zzap` keeps the defaults.

- **Known issue**

1. Once during testing, the game froze suddenly about 13 minutes into a raid. The logs showed no SAIN error or load spike and the cause is unknown (VRAM was at 98%).
   If it happens to you, please send BepInEx `LogOutput.log` plus the logs in the newest folder under your SPT `Logs\` folder.

^^7
