# SAIN-zzap--Bootleg- remake3 릴리즈 노트 (remake2 대비)

> remake2 이후 변경분입니다. 아래 "꼭 읽어주세요" 공지 글은 이전 릴리즈와 동일하게 붙이면 됩니다.
> remake2의 안내 사항(램 사용량, 스터터링, 렉·버그 제보 방법 등)은 그대로 유효합니다 — `changelog/RELEASE_NOTES_remake2.md` 참고.

**<설치>**

- **remake2에서 올라오는 경우:** 그냥 덮어쓰면 됩니다. 서버 모드 폴더 `Presets/`에 새 프리셋 3개가 추가됩니다.
- **remake1 이하 / 원본 SAIN에서 올라오는 경우:** remake2 노트의 "깔끔하게 지우고 설치" 순서대로 해 주세요.
  (`BepInEx/plugins/SAIN/`과 `SPT_Runtime/user/mods/Solarint-SAIN-ServerMod/` 삭제, 직접 만든 프리셋은 미리 복사, `BepInEx/config/SAIN-zzap/`은 지우지 않기)

---

**<변경점>**

- **난이도별 프리셋 추가 — 총 5종**

"zzap은 너무 어렵다"는 분들을 위해 난이도를 나눴습니다. **전술(문 피킹·페이크, 다이아몬드 스텝, 코너 지키기, 분대 교전 등)은 다섯 개 모두 같고,
사격·조준·감각 난이도만 다릅니다.** F6 프리셋 목록에도 이 순서로 표시됩니다.

| 프리셋 | 난이도 | 내용 |
|---|---|---|
| `zzap 쉬움` | ★☆☆ | 반동이 크고 탄이 많이 퍼짐, 시야 거리 절반, 늦게 발견, 청각 약함, 시야각 120°, 연사 느림, 조준 최소 2초, 사람 같은 실수 2배 |
| `zzap 보통` | ★★☆ | 반동·탄 퍼짐 중간, 시야 거리 보통, 발견·청각 약간 약함, 시야각 150°, 연사 중간, 조준 최소 1초, 실수 1.5배 |
| `zzap` | ★★★ | 기본(지금까지의 zzap). 사격·조준·반응은 TwitchPlayers(하드코어) 값 |
| `zzap 저사양 [테스트]` | ★★☆ | 저사양 PC 시험용. `zzap 보통` + 성능 모드 + 멀리 있는 봇끼리의 업데이트·시야·청각 제한 |
| `zzap TEST [시뮬 전용]` | — | **실제 플레이용 아님.** 봇끼리 싸우는 걸 프리캠으로 구경하는 테스트용 |

1. 쉬움·보통은 원본 SAIN의 "쉬움 / 보통" 난이도 보정 방식을 그대로 가져와서, zzap의 값 위에 적용했습니다. 그래서 항상 **쉬움 < 보통 < zzap** 순서가 유지됩니다.
2. `zzap 저사양 [테스트]`는 말 그대로 **시험용**입니다. 프레임이 조금 나아질 수 있지만, 멀리서 벌어지는 봇끼리의 싸움은 덜 정교해질 수 있고
   제가 의도한 전투 경험과 다를 수 있습니다. 써 보시고 체감(프레임·봇 행동)을 알려주시면 다음 버전에 반영하겠습니다.
3. 프리셋 바꾸는 법: 게임에서 **F6 → 프리셋 선택**에서 고르면 됩니다. 처음 설치할 때 자동으로 선택되는 건 여전히 `zzap`입니다.

- **기타**

1. F6 프리셋 목록이 난이도 순서(쉬움 → 보통 → zzap → 저사양 → 시뮬)로 정렬됩니다. (전에는 파일 이름순이라 `zzap`이 맨 위, 한글 이름이 맨 아래였음)

^^7

------------------------------------------------------------------------------------------------------------------------------------------------------

# SAIN-zzap--Bootleg- remake3 release notes (vs remake2)

> Changes since remake2. The notes from remake2 (RAM usage, stuttering, how to report lag/bugs, ...) still apply — see `changelog/RELEASE_NOTES_remake2.md`.

**Install**

- **Coming from remake2:** just overwrite. Three new presets are added to the server mod's `Presets/` folder.
- **Coming from remake1 or older / upstream SAIN:** follow the clean install steps in the remake2 notes
  (delete `BepInEx/plugins/SAIN/` and `SPT_Runtime/user/mods/Solarint-SAIN-ServerMod/`, back up your own presets first, keep `BepInEx/config/SAIN-zzap/`).

---

**Changes**

- **Difficulty presets — 5 in total**

For everyone who found zzap too hard. **The tactics (door peeks and fakes, diamond stepping, corner holds, squad combat...) are the same in all five;
only the shooting / aiming / senses difficulty differs.** The F6 preset list shows them in this order.

| Preset | Difficulty | What changes |
|---|---|---|
| `zzap 쉬움` (easy) | ★☆☆ | Heavy recoil and spread, half sight range, slow to notice, weak hearing, 120° view, slow fire rate, at least 2s to aim, 2x human mistakes |
| `zzap 보통` (normal) | ★★☆ | Medium recoil/spread, normal sight range, slightly weaker noticing/hearing, 150° view, medium fire rate, at least 1s to aim, 1.5x mistakes |
| `zzap` | ★★★ | The default (the zzap you know). Shooting/aiming/reactions from TwitchPlayers (hardcore) |
| `zzap 저사양 [테스트]` (low-spec, test) | ★★☆ | For low-spec PCs, experimental. `zzap 보통` + performance mode + limited updates/vision/hearing between far-away bots |
| `zzap TEST [시뮬 전용]` | — | **Not for playing.** Test preset for watching bot-vs-bot fights in free cam |

1. Easy and normal use upstream SAIN's own "easy / normal" difficulty tuning, applied on top of zzap's values, so the order is always **easy < normal < zzap**.
2. `zzap 저사양 [테스트]` really is **experimental**: frame rates may improve a little, but far-away bot-vs-bot fights can get less precise and
   the combat may not feel the way I intended. Please tell me how it feels (frames, bot behavior) and I'll adjust it in the next version.
3. To switch presets: in game, **F6 -> preset selection**. On a fresh install `zzap` is still the one selected automatically.

- **Misc**

1. The F6 preset list is sorted by difficulty (easy -> normal -> zzap -> low-spec -> sim). (It used to be alphabetical, with `zzap` on top and the Korean names at the bottom.)

^^7
