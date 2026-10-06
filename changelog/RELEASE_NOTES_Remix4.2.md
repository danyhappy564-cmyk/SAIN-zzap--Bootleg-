# SAIN-zzap--Bootleg- Remix4.2 릴리즈 노트 (Remix4.1 대비)

> Remix4.1 이후 작은 수정 릴리즈입니다. remake2·remake4의 안내 사항(램 사용량, 스터터링, 렉·버그 제보 방법 등)은 그대로 유효합니다.

**<설치>**

- **Remix4.1 / remake4에서 올라오는 경우:** 그냥 덮어쓰면 됩니다. 바뀐 건 클라이언트 `SAIN.dll`뿐이고, 프리셋·서버 모드·F6 설정은 바뀌지 않았습니다.
- **remake3 이하 / 원본 SAIN에서 올라오는 경우:** remake4 노트의 설치 순서대로 해 주세요.

---

**<변경점>**

- **봇**

1. 탄도, 예비 탄창도, 권총도 없는 봇이 장전에 실패할 때마다 "권총으로 바꾸기"를 시도한 것으로 잘못 처리되던 문제를 고쳤습니다.
   봇 행동은 원래도 정상이었습니다(빈총이면 후퇴). 내부 기록과 재시도 대기 시간만 틀렸던 것이라 플레이에서 체감되는 변화는 거의 없습니다.

- **테스트용 기록 (레이드 일지를 켠 경우만)**

1. 무적으로 테스트할 때 "이만큼 맞았으면 몇 번 죽었을지" 세는 **가상 사망**이 한 번의 연사(예: 2초에 머리 4발)를 여러 번으로 세던 것을 고쳤습니다.
   이제 가상 사망 뒤 3초 안에 맞은 건 같은 죽음으로 봅니다. 일반 플레이에는 영향이 없습니다.

- **테스트 결과**

1. Remix4.1 빌드로 공장 57.6분 레이드를 돌려 확인했습니다. 레이드 도중 멈춤 없음, 봇이 출구로 빠지는 일 없음, 서 있는 시체 없음.
2. Remix4.1의 "후퇴할 때 고개 숙임 유지"가 정상 작동하는 것을 확인했습니다.
3. 엎드려 버티는 플레이어에게 봇이 문 함정·문 지키기·페이크 수류탄·들어가기 전 방 정리(진짜 수류탄 포함)를 실제로 쓰는 것을 확인했습니다.
4. 문 발차기 수정(Remix4.1)은 이번 테스트에서 차 볼 일이 없어 따로 확인하지 못했습니다.

- **알려진 문제**

1. remake4 노트의 "레이드 중 갑자기 게임이 멈춘 1건"은 그 뒤로 다시 생기지 않았지만 원인은 아직 모릅니다.
   같은 증상이 생기면 BepInEx `LogOutput.log`와 SPT 폴더 안 `Logs\`의 최신 폴더 로그를 보내 주세요.

^^7

------------------------------------------------------------------------------------------------------------------------------------------------------

# SAIN-zzap--Bootleg- Remix4.2 release notes (vs Remix4.1)

> A small fix release after Remix4.1. The notes from remake2 and remake4 (RAM usage, stuttering, how to report lag/bugs, ...) still apply.

**Install**

- **Coming from Remix4.1 / remake4:** just overwrite. Only the client `SAIN.dll` changed — presets, the server mod and F6 settings are the same.
- **Coming from remake3 or older / upstream SAIN:** follow the install steps in the remake4 notes.

---

**Changes**

- **Bots**

1. A bot with no ammo, no spare magazines and no pistol was treated as trying to "switch to the pistol" every time its reload failed.
   Its behavior was already fine (it falls back with an empty gun) — only its internal bookkeeping and retry timer were wrong, so you will hardly notice anything in play.

- **Testing records (only with the raid journal on)**

1. The **virtual deaths** count ("how many times would I have died", for invincible test raids) counted one burst (say 4 headshots in 2s) as several deaths.
   Hits within 3s after a virtual death now belong to the same death. No effect on normal play.

- **Test results**

1. Checked with a 57.6-minute Factory raid on the Remix4.1 build: no freeze, no bots walking out to an exit, no standing corpses.
2. Remix4.1's "head stays down while retreating" works as intended.
3. Against a player camping prone, bots did use door traps, door holds, fake grenades and clearing the room before entering (real grenades included).
4. The door-kick fix from Remix4.1 wasn't exercised in this test (no doors were kicked), so it is still unconfirmed in game.

- **Known issue**

1. The one sudden game freeze from the remake4 notes hasn't happened again, but its cause is still unknown.
   If it happens, please send BepInEx `LogOutput.log` plus the logs in the newest folder under your SPT `Logs\` folder.

^^7
