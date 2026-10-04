# SAIN-zzap--Bootleg- Remix4.1 릴리즈 노트 (remake4 대비)

> remake4 이후 버그 수정 릴리즈입니다. remake2·remake4의 안내 사항(램 사용량, 스터터링, 렉·버그 제보 방법 등)은 그대로 유효합니다.

**<설치>**

- **remake4에서 올라오는 경우:** 그냥 덮어쓰면 됩니다. 바뀐 건 클라이언트 `SAIN.dll`뿐이고, 프리셋·서버 모드·F6 설정은 바뀌지 않았습니다.
- **remake3 이하 / 원본 SAIN에서 올라오는 경우:** remake4 노트의 설치 순서대로 해 주세요.

---

**<변경점>**

- **문**

1. 플레이어가 문을 발로 차면 **애니메이션 없이 바로 열리던** 문제를 고쳤습니다. 이제 발이 닿는 순간 문이 젖혀지며 열립니다.
   (원인: zzap 포크에 넣은 봇용 "멈춘 문 강제 마무리" 장치가 발차기 동작 도중에 끼어들었습니다. 원본 SAIN에는 없던 버그입니다.)
2. **잠긴 문이 발차기로 무조건 열리던** 문제를 고쳤습니다. 이제 원래 게임 규칙대로입니다.
   문이 내 반대쪽으로 열리는 쪽(**미는 쪽**)에서 차면 열리고, 당기는 쪽에서 차거나 발로 못 여는 문은 몇 번을 차도 안 열립니다.
3. 열쇠를 돌리는 도중이나 카드키를 쓰는 도중에 문이 확 열리던 현상도 같은 원인이라 함께 고쳐졌을 것으로 보입니다.
4. 봇이 여는 문(문 전술 포함)은 바뀐 것이 없습니다.

- **후퇴**

1. 등을 보이고 도망칠 때 **고개를 숙였다 들었다 까딱이던** 문제를 고쳤습니다(지그재그 박자에 맞춰 반복됨).
   이제 쏘는 적에게 등을 보이는 동안에는 계속 고개를 숙이고, 길이 꺾여서 적이 옆이나 앞으로 오면 고개를 듭니다. 멈추거나 반격하려고 할 때도 바로 듭니다.
2. 그 덕분에 후퇴 지그재그도 중간에 덜 끊깁니다.

- **알려진 문제**

1. remake4 노트의 "레이드 중 갑자기 게임이 멈춘 1건"은 아직 원인을 찾지 못했습니다. 같은 증상이 생기면 BepInEx `LogOutput.log`와 SPT 폴더 안 `Logs\`의 최신 폴더 로그를 보내 주세요.
2. 짧은 버그 수정 릴리즈라 긴 테스트 레이드는 거치지 않았습니다. 문이나 후퇴 동작이 이상하면 `LogOutput.log`와 함께 알려 주세요.

^^7

------------------------------------------------------------------------------------------------------------------------------------------------------

# SAIN-zzap--Bootleg- Remix4.1 release notes (vs remake4)

> A bug-fix release after remake4. The notes from remake2 and remake4 (RAM usage, stuttering, how to report lag/bugs, ...) still apply.

**Install**

- **Coming from remake4:** just overwrite. Only the client `SAIN.dll` changed — presets, the server mod and F6 settings are the same.
- **Coming from remake3 or older / upstream SAIN:** follow the install steps in the remake4 notes.

---

**Changes**

- **Doors**

1. Fixed doors you kick **flying open instantly with no animation**. The door now swings open the moment your foot lands.
   (Cause: a "finish stuck doors" helper this fork added for bots stepped in while the kick was still playing. Upstream SAIN never had this bug.)
2. Fixed **locked doors always opening to a kick**. It's the game's own rule again: kick from the side the door swings away from you (**the push side**)
   and it opens; kick from the pull side, or a door that can't be breached, and it stays shut no matter how many times you kick.
3. Doors snapping open halfway through turning a key or using a keycard had the same cause and should be fixed as well.
4. Doors opened by bots (door tactics included) are unchanged.

- **Retreating**

1. Fixed the head **bobbing down and up** (in time with the zig-zag) while bots run away with their back to you.
   Now the head stays down as long as the back is turned to the shooter, and comes up when the path turns so the enemy is beside or ahead — or when the bot stops or turns to fight back.
2. As a side effect, the retreat zig-zag gets cut off less often.

- **Known issues**

1. The one sudden game freeze mentioned in the remake4 notes is still unexplained. If it happens, please send BepInEx `LogOutput.log` plus the logs in the newest folder under your SPT `Logs\` folder.
2. This is a short bug-fix release and did not go through a long test raid. If doors or retreats look wrong, please report with `LogOutput.log`.

^^7
