<p align="center">
  <a href="README.md"><img alt="한국어" src="https://img.shields.io/badge/%ED%95%9C%EA%B5%AD%EC%96%B4-%ED%95%9C%EA%B5%AD%EC%96%B4%EB%A1%9C%20%EB%B3%B4%EA%B8%B0-6e7681?style=for-the-badge"></a>
  <a href="README.en.md"><img alt="English (this page)" src="https://img.shields.io/badge/English-This%20page-1f6feb?style=for-the-badge"></a>
</p>

### ⚠️ IMPORTANT NOTICE / DISCLAIMER

- **Original Author:** Solarint
- **Original Repository:** SAIN - Solarint's AI Modifications
- **Original Link:** https://github.com/Solarint/SAIN
- **License:** See upstream repository
- **This Port By:** R_F (danyhappy564-cmyk) — unofficial, AI-assisted port. Not affiliated with or endorsed by the original author.

1. **Reflection & Take-Downs:** I deeply reflect on the ECOT incident. As an AI-assisted "vibe coder," I will immediately delete files if the original authors ask.
2. **No Re-Distribution:** These ported builds are unverified, temporary fixes. Please do NOT re-upload or share them anywhere else.
3. **Do Not Pester Original Authors:** Never report bugs or pester original modders regarding issues from my unofficial ports.
4. **Full Credit & Respect:** I will always credit original creators on GitHub and prioritize their decisions above all else.
5. **Support Original Creators:** Instead of using my ports, please visit the original authors' Forge pages to leave kind words or tips.

---

**Project site (English / 한국어):** https://danyhappy564-cmyk.github.io/SAIN-zzap--Bootleg-/ — tactics, door fixes, settings screens and install steps on one page, with GIFs.

## Changelog

The full changelog (258 entries) is in the [`changelog/`](changelog/) folder, 10 per file (higher number = newer, latest: [`CHANGELOG-026.md`](changelog/CHANGELOG-026.md)). Per-release summaries: [Releases](../../releases).

# SAIN (zzap fork)

> **Original author · Original project**
> **Solarint** — https://github.com/Solarint/SAIN · SAIN(Solarint's AI Modifications)
> 4.x maintenance: **ArchangelWTF** — https://github.com/ArchangelWTF/SAIN
>
> Forked from upstream **v4.5.1** (SPT 4.1).
>
> **Ported code (bot door ramming)** — attribution
> 1. **bmpq (tarkin)** — doordash — https://github.com/bmpq/spt-doordash (no license file)
> 2. **maschine34675** — DualSideDoorBreach 2.0.2 — https://github.com/maschine34675/DualSideDoorBreach (MIT, Copyright (c) 2026 maschine34675)

SAIN is a major mod that replaces EFT's bot AI. This fork ① fixes **bots getting stuck at doors**,
② adds **tactical behavior** modeled on experienced players within SAIN's combat layer, and ③ includes ready-to-use **zzap presets**.

The changelog is in [`changelog/`](changelog/), release notes are on the [GitHub releases page](https://github.com/danyhappy564-cmyk/SAIN-zzap--Bootleg-/releases), and details of the door fixes are in [`changelog/FIXES.md`](changelog/FIXES.md).

**Latest changes (2026-11-04, Remix7.4 release):** Settings screen cleanup — new 'ZZAP (전술 등..) (tactics, etc.)' tab with plain-language switches for 14 tactics, and '시뮬레이션 (Simulation)' tab; bot settings split into five tabs (빠른 설정 (Quick settings), 성격 (Personality), ZZAP, 맵별 보정 (Per-map adjustments), 고급 (Advanced)); Global/On/Off overrides for each bot type's ZZAP tactics and per-map multipliers (hearing, vision, spotting speed, etc.); English preset names in the English interface; comparison baseline order; custom preset renaming. Bot behavior and zzap values are unchanged from Remix7.3. Details: [`Remix7.4 릴리즈 노트` (Remix7.4 release notes)](https://github.com/danyhappy564-cmyk/SAIN-zzap--Bootleg-/releases/tag/remix7.4).

**Previous changes (2026-11-03, Remix7.3 release):** Bot settings overhaul — select multiple bot types in F6 or on the web (circles or Ctrl+click) and adjust their difficulty, knobs, and advanced values together; save to custom slots (3–9 slots) and exchange share codes; show only new or changed settings and reset individual settings; a three-step guide for first-time users. A compatible-mods table was added to the README (including PBT and Twitch Players). Bot behavior and zzap values are unchanged from Remix7.2. Details: [`Remix7.3 릴리즈 노트` (Remix7.3 release notes)](https://github.com/danyhappy564-cmyk/SAIN-zzap--Bootleg-/releases/tag/remix7.3).



Per-release summaries: [`Remix7.4`](https://github.com/danyhappy564-cmyk/SAIN-zzap--Bootleg-/releases/tag/remix7.4) (since Remix7.3) · [`Remix7.3`](https://github.com/danyhappy564-cmyk/SAIN-zzap--Bootleg-/releases/tag/remix7.3) (since Remix7.2) · [`Remix7.2`](https://github.com/danyhappy564-cmyk/SAIN-zzap--Bootleg-/releases/tag/remix7.2) (since Remix7.1) · [`Remix7.1`](https://github.com/danyhappy564-cmyk/SAIN-zzap--Bootleg-/releases/tag/remix7.1) (since Remake7) · [`Remake7`](https://github.com/danyhappy564-cmyk/SAIN-zzap--Bootleg-/releases/tag/remake7) (since Remix6.1, includes the unpublished Remix6.2) · `Remix6.2` (draft only, unpublished) · [`Remix6.1`](https://github.com/danyhappy564-cmyk/SAIN-zzap--Bootleg-/releases/tag/remix6.1) (since remake6) · [`remake6`](https://github.com/danyhappy564-cmyk/SAIN-zzap--Bootleg-/releases/tag/remake6) (since Remix5.1) · [`Remix5.1`](https://github.com/danyhappy564-cmyk/SAIN-zzap--Bootleg-/releases/tag/remix5.1) (since remake5) · [`remake5`](https://github.com/danyhappy564-cmyk/SAIN-zzap--Bootleg-/releases/tag/remake5) (since Remix4.2) · [`Remix4.2`](https://github.com/danyhappy564-cmyk/SAIN-zzap--Bootleg-/releases/tag/remake4.2) (since Remix4.1) · [`Remix4.1`](https://github.com/danyhappy564-cmyk/SAIN-zzap--Bootleg-/releases/tag/Remake4.1) (since remake4) · [`remake4`](https://github.com/danyhappy564-cmyk/SAIN-zzap--Bootleg-/releases/tag/remake4) (since remake3) · [`remake3`](https://github.com/danyhappy564-cmyk/SAIN-zzap--Bootleg-/releases/tag/remake3) (since remake2) · [`remake2`](https://github.com/danyhappy564-cmyk/SAIN-zzap--Bootleg-/releases/tag/remake2) (since remake1) · [`remake1`](https://github.com/danyhappy564-cmyk/SAIN-zzap--Bootleg-/releases/tag/remake1) (since fix7).

## Using bot settings (Remix7.4)

1. **Choose bots:** Click a name to open one bot. Use the ○ on the left or **Ctrl+click a name** to add or remove it from the selection; ● means selected and ◐ means part of a faction is selected. Clicking just a name shows that bot while keeping the selection. Return with **묶음 편집 (Edit selection)** on the yellow bar.
2. **Difficulty and knobs:** Pick Easy/Normal/Hard/Impossible and fine-tune the nine knobs. Moving a knob in a selection applies the same displayed value to every selected editable bot. The advanced list includes all existing settings; use **새 항목만 보기 (New only) · 바뀐 항목만 보기 (Changed only)**, NEW, the yellow ●, and **원래대로 (Reset)** to find settings and restore each bot's own baseline. Global and fixed-vanilla entries are skipped during batch editing.
3. **Save, slots, and sharing:** Use **저장 (Save)** at the top to save changes for the game. **＋ 지금 값 저장 (Save current)** stores the displayed bot's values in a custom slot (3 initially, up to 9). The slot's ⋯ menu offers rename, overwrite, delete, and copy share code. On another screen, use **공유 코드 붙여넣기 (Paste share code)** to add a slot, then click the slot name to apply it to the current bot or selection. AI changes apply after restarting the game. The **?** at the top right reopens the three-step guide.
4. **Tabs (Remix7.4):** After choosing bots, use the tabs below — **빠른 설정 (Quick settings)** (knobs), **성격 (Personality)** (assign a personality directly to this bot type), **ZZAP (전술 등..) (tactics, etc.)** (Global/On/Off for each tactic), **맵별 보정 (Per-map adjustments)** (choose a map and multipliers such as hearing and vision; 1.0 = unchanged, applies from the next raid), **고급 (Advanced)** (numbers by group). Tactics shared by all bots are under '한눈에 켜고 끄기 (At a glance)' on the top 'ZZAP (전술 등..) (tactics, etc.)' tab.

- **Why are edits to default presets saved as `[Modified]`?** Modified values are saved as a `[Modified]` copy to preserve the bundled zzap 쉬움 (Easy), zzap 보통 (Normal), zzap, and TEST originals.
- **What does ‘섞임 (Mixed)’ mean?** The selected bots have different displayed values. Moving a knob gives them the same displayed value. The baseline for **원래대로 (Reset)** differs by bot.
- **Where are slots saved?** In the server mod's `ClientData/botSettingsSlots.json`, separately from presets. They survive preset changes, and F6 and the web use the same file. Saving slots and saving presets are separate actions. Share codes contain only the name, difficulty, AI choice, and changed fields.

### English — Bot settings (Remix7.4)

1. **Choose bot types:** Click a name to open one bot. Click ○ or **Ctrl+click a name** to add it to (or remove it from) the selection: ● means selected, ◐ means only part of a faction is selected. A faction's ○ selects the whole faction. Opening one bot keeps your selection; **Edit selection** on the yellow bar takes you back to it.
2. **Level and knobs:** Choose Easy, Normal, Hard or Impossible, then fine-tune the nine knobs. Moving a knob gives every selected bot the same value. Advanced still lists every setting; **New only** and **Changed only** narrow it down, NEW marks settings added in this release, a yellow ● marks changed ones, and **Reset** puts one setting back to that bot's own original. Bots locked to EFT's own AI (vanilla) are skipped.
3. **Save, slots and sharing:** Press **Save** to apply changes in game. **＋ Save current** keeps the shown bot's values in a custom slot (3 to start, up to 9). Use ⋯ to **Rename**, **Overwrite**, **Delete** or **Copy share code**. **Paste share code** adds a friend's slot; click a slot to apply it to the open bot or the whole selection. AI changes apply after a game restart. The **?** button at the top right opens the 3-step guide again.
4. **Tabs (Remix7.4):** after picking bots, use the tabs below - **Quick** (knobs), **Personality** (fix the personality for this bot type), **ZZAP (tactics etc.)** (Global / On / Off per tactic), **Per map** (pick a map, then multipliers such as hearing and vision; 1.0 = unchanged, from the next raid), **Advanced** (numbers by group). Tactics for all bots are on the top 'ZZAP (tactics etc.)' tab, 'At a glance'.

- **Why are my edits saved as a `[Modified]` copy?** So the bundled zzap presets (zzap 쉬움 (Easy), zzap 보통 (Normal), zzap, TEST) stay untouched.
- **What does "mixed" mean?** The selected bots have different values for that setting. Moving the knob gives all of them the same value; **Reset** returns each bot to its own original.
- **Where are custom slots stored?** In the server mod's `ClientData/botSettingsSlots.json`, separate from presets, so they survive preset changes. Custom slots are shared by F6 and the web page. Saving a slot does not save the preset (use **Save** for that). A share code holds only the slot name, Level, AI choice and changed values.

## Installation / Updating

**We recommend removing your previous SAIN installation (upstream or an older version of this fork) before installing.** This clears out problems caused by leftover DLLs and presets with old names.

1. **(If you have custom presets)** Make a separate backup of **only your custom preset files** from `SPT_Runtime/user/mods/Solarint-SAIN-ServerMod/Presets/`. The four defaults (`zzap 쉬움` (Easy) · `zzap 보통` (Normal) · `zzap` · `zzap TEST [시뮬 전용]` (Simulation only)) are supplied again, but **files with `[Modified]` in their names are your edited copies, so be sure to keep them.**
2. **Delete both folders entirely:**
   - `BepInEx/plugins/SAIN/` (client mod — also contains the F6 editor settings, `EditorDefaults.json`, so these are reset too)
   - `SPT_Runtime/user/mods/Solarint-SAIN-ServerMod/` (server mod — includes presets and server settings, `ServerConfig.json`)
3. **Do not delete `BepInEx/config/SAIN-zzap/`** — it holds player adaptation and learning data (your playstyle and outcome learning). Delete it only if you want bots to learn from scratch.
4. Extract the new archive into `SPT_Runtime/user/mods/` (server mod) and `BepInEx/plugins/` (client mod). If you backed up presets in step 1, put them back in `Presets/`.
5. On first launch, **the `zzap` preset is selected automatically once** (an existing custom-preset selection is preserved). After that, your F6 preset selection is kept.
   `[시뮬 전용]` (Simulation only) is for watching bots fight with a free camera, not for regular play.

## What are player adaptation and learning? (On by default)

This lets bots **learn your habits as a player across raids**. It works only against human players and has no effect on bot-versus-bot fights.

- **① Style recording** — Saves numbers describing your play in each raid: time spent moving, proportion of shots fired while moving, jump count, Q/E lean peeks,
  grenade use, time camping in one spot, kill/death distances, etc. Saved to: `BepInEx/config/SAIN-zzap/PlayerStyle/<프로필ID>.jsonl`
- **② Style adaptation** — At the start of a raid, reads your history, identifies your tendencies, and adjusts bot responses.
  - Aggressive running and pushing → waits at corners and fights up close
  - Frequent bunny hops → holds the corner you are approaching
  - Frequent lean peeks → responds with lean peeks
  - Heavy grenade use → squad members spread out
  - Response strength varies by personality (Rat and SnappingTurtle hold positions, Chad fights back, Coward barely changes).
  - **Activates after at least 10 minutes of recorded data**, reaching full strength at around 60 minutes. Your first few raids behave like normal SAIN.
- **③ Learning from outcomes** — Scores each response to you (push, hold, grenade, flank…): hitting you is a success; dying to you is a failure.
  Results accumulate across raids (`<프로필ID>.learned.json`), so bots **choose responses that worked against you more often and failed responses less often**.
- Your **gear is not used to judge you** (it may be developer-tool or cheat gear). Raids where invincibility, infinite ammo, or developer-tool mods are detected are marked as test raids.
- Disable via F6 → `General > Player Style Recorder (zzap)` → `Adapt Bots To Player Style` (adaptation) / `Learn From Outcomes` (learning). To start recording from scratch, delete the files in the folder above.
- This feature **uses statistics from completed raids, rather than letting bots see you**. It does not secretly reveal your position during a raid.
The table below uses the original English names for F6 settings locations. If you cannot find one, switch with `한 / [EN]` (Korean / English), or search in either English or Korean (both work).

## Added features

**Behavior (tactics)** — All decisions run inside SAIN's combat layer (`SAIN : Combat Layer`), so they are **compatible with ORBIT without changes**.

| Feature | What it does | Who uses it | F6 settings location |
|---|---|---|---|
| Jump peeking | Jumps from a corridor across a doorway to look inside, then immediately jumps back. Opens closed doors from beside the frame first | GigaChad / Chad (PMC only — bosses and followers use run-by and step peeks) | `General > Door Tactics (zzap)` |
| Run-by peeking | Where jumping is risky (low ceilings or stairs), runs past the door to look in, turns around, **walks back past with the gun raised**, then holds the door from **an angle 2~3m away**, rather than right beside the frame. Immediately stops and aims at the door if sounds suggest the enemy is approaching it (running, doors, footsteps at the doorway) | GigaChad / Chad | 〃 `Run-By Peek Chance` |
| Step peeking | After a peek, briefly moves in and out of the doorway 2~3 times (standing/crouching, alternating the room's left/right corners) | GigaChad / Chad | 〃 `Step Peeks After Jump Peek` |
| Fake grenade | Makes the sound of pulling out a grenade at the doorway, then immediately puts it away. Only if an enemy in the room was heard or identified within 15 seconds, is within 14m of the door, and the bot is behind real cover (out of every enemy's sight and indoors/enclosed); cancels on hearing running | GigaChad / Chad | 〃 `Fake Grenade` |
| Fake heal / stim | Makes a healing/injection sound and cancels as soon as it is in hand. Not used while bleeding or badly injured (real treatment takes priority) | GigaChad / Chad | 〃 `Fake Heal / Stim` |
| Room entry | Does not slowly walk into an enemy's room gun-first: hugs the frame, opens the door from the side → mixes in 0~2 fake grenades (cancels if footsteps rush out) → throws a real grenade and **confirms the actual explosion** before rushing in, or dashes in hard without a grenade. Does not throw if its own blast could hit it. Corner chasing does not interrupt an active door tactic (switches to rushing only for an enemy reload, a near-dead enemy, or a squad storm) | GigaChad / Chad / Wreckless / Normal | 〃 `Room Clear` |
| Room trapping | Hugs the side of the door and holds it (leaves the door as it is) | GigaChad / SnappingTurtle | 〃 `Room Trap` |
| Hold means hold the door | If the hidden-enemy decision chooses hold and the enemy is in a room beyond a nearby door (within 8m), **holds beside that door** instead of stopping anywhere (Rat stays low and quiet; SnappingTurtle holds longer). If push is chosen but door tactics are unavailable (cooldown, failed chance roll, etc.), holds the door for 8~15 seconds and reassesses instead of running straight in (Wreckless or a weak enemy still triggers a rush) | All | 〃 `Hold Stance Holds The Door` |
| Door ambush | Waits quietly in a low stance beside a door | Rat | 〃 `Room Trap` |
| Squad roles | While one bot uses door tactics, teammates take overwatch (cross angles) or rear security. If a wall blocks a door holder's aim, moves up to 1.5m to a clear firing angle. Assigned teammates start their role before other decisions (roles previously often expired before starting) | Squads | 〃 `Squad Roles` / `Close Combat > Door/Squad Hold: Step To See The Angle` |
| Third-party detection | Immediately abandons a door tactic to respond if another enemy appears or makes noise within 25m | All | Automatic |
| Diamond stepping | Short WASD-like taps during firing and close-range DogFight combat (0.13-second taps, 60% sideways · 40% forward/back). If a wall blocks the intended direction, chooses an open opposite, side, or diagonal direction (fixes standing still in narrow corridors) | GigaChad / Chad / Wreckless / Normal | `General > Close Combat (zzap)` |
| Brief close-range occlusion tolerance | Keeps firing/rushing decisions and aims at the last visible position when an enemy fought within 8m is hidden by a frame or pillar for under 0.8 seconds (fixes three decision changes within 0.4 seconds interrupting fire). Reassesses immediately if another enemy appears or shoots | All | Automatic |
| Corner jiggle peeking | During corner/door fights, has a chance (default 50%) to rapidly alternate A↔D, **hiding behind a wall for 0.18~0.32 seconds and exposing itself for 0.25~0.45 seconds** while firing. Checks 0.8m to either side at enemy eye level and only uses it where one side actually conceals the bot | Diamond-step users | 〃 `Corner Jiggle Peek` / `Corner Jiggle Peek Chance` |
| Leaning | While firing during diamond stepping, holds a lean toward the enemy's movement for 0.35~1 seconds, changing sides if the enemy reverses. Occasionally mixes in 2~4 rapid Q/E taps — 45% within 7m, 15% farther away (60% per engagement) | Diamond-step users | `General > Close Combat (zzap)` |
| Hidden-enemy decisions (utility AI) | When an enemy's position is known but hidden, scores push, grenade, hold, flank, search, and retreat by expected benefit: information age, whether the enemy is holding an angle/approaching, health/ammo/grenades, numerical advantage, enemy gear/reloading/healing, how long the enemy has been quiet (time favors campers), distance, and personality. Makes a score-weighted roll (higher scores are more likely; Wreckless is loose, Rat/Coward almost always choose the highest). Hold loses score after 10 seconds without activity; other failed actions do not fall back to hold. `[Utility]` logs scores and reasons. **Skips hold (standing still) while taking fire or just after being hit**, preventing stationary deaths. If the top choice is temporarily unavailable (e.g. path recalculation), keeps the previous decision for 1.2 seconds instead of briefly falling back to the upstream cover decision | All | `General > Close Combat (zzap)` `Utility Decision For Hidden Enemy` |
| Visible-enemy decisions (utility AI) | Chooses continued fire, moving to cover, or pushing by expected benefit during visible contact: whether the enemy sees the bot, exposure time, recent hits, health/magazine, nearest cover distance (does not run if too far), enemy gear/reloading/healing, weapon suitability for the range, teammates, and personality. Replaces SAIN's fixed rule of waiting N seconds before cover. `[UtilityV]` log | All | `General > Close Combat (zzap)` `Utility Decision For Enemy In Sight` |
| Reload timing (utility AI) | Below 80% magazine during combat, weighs reload, keep firing, or pistol swap: keeps firing if watched by the enemy; reloads if the enemy is hidden or the bot is in cover; shoots during enemy reloads; uses a pistol at 10% ammo or less with an enemy within 30m. Drops the magazine for a fast reload only when urgent; retains it when safe with over 30% left. `[Reload]` log | PMC | `General > Close Combat (zzap)` `Utility Reload Timing` |
| Healing timing (utility AI) | With enemies present, weighs first aid, stim, surgery, or waiting: waits if an enemy is visible/approaching or the bot was just hit; heals in cover when the nearest threat is old or distant; uses a stim even in combat for serious injuries; operates only when truly safe. Considers all enemies with known positions (closer and more recent favors waiting); immediately stops healing if an enemy appears within 40m or the bot is hit; cancels leaning while healing; delays treatment in positions visible from an enemy's last location. `[Heal]` log | All | `General > Close Combat (zzap)` `Utility Heal Timing` |
| Gear-based combat style | Calculates preferred range and close/long-range strengths from the held weapon (sniper, bolt-action, DMR, rifle, SMG, shotgun, scope/red dot, automatic fire/fire rate), plus grenade and medical supplies. Snipers keep distance (back away from approaching enemies, do not push); SMGs/shotguns close in and avoid long-range fights; more grenades encourage throws, more meds encourage treatment | All | Automatic (included in utility decisions) |
| Fear | Personality baseline (Coward 0.35 ~ GigaChad/Wreckless 0), plus nearby teammate deaths, player kill streaks, injuries, being outnumbered, and suppression, scaled by bravery (little for Wreckless, much for Coward). More fear favors retreat/hold; over 0.75 causes panic | All | `General > Close Combat (zzap)` `Fear` |
| Outcome learning (responses to players) | Records every decision against a player (hit = success, death = failure), accumulating across raids to favor responses that worked against that player. Ignores player gear to account for developer tools/overpowered equipment | Against human players | `General > Player Style Recorder (zzap)` `Learn From Outcomes` / `Ignore Player Gear` |
| Raid journal (**off by default**, on in the `[시뮬 전용]` (Simulation only) preset) | One timestamped file per raid (`BepInEx/config/SAIN-zzap/Journal/`): all bots' decision changes/reasons, all tactical logs, player position/hits/kills (every 2 seconds), `[Battle]` bot-versus-bot totals every 5 minutes and at the end (kills/deaths by personality, action, and decision reason), final totals, and `[Weapon]`/`[Stuck]`/`[DeadBug]` diagnostic lines — for post-raid analysis and bug reports | All | 〃 `Raid Journal` |
| Human-like mistakes | Occasionally picks the second/third choice instead of the best in all utility decisions — Timmy 12%, Wreckless 8%, Coward 6%, Normal 5%, Chad 4%, Rat/Turtle 3%, GigaChad 2% (off in TEST). Logged as `(mistake)` | All | 〃 `Utility Mistakes` |
| Close combat priority | Keeps fighting instead of walking to cover if an enemy is visible within 8m (except while healing/reloading) | PMC (except Coward) | `General > Close Combat (zzap)` |
| Fight instead of sprinting across the open | If taking fire from a visible enemy within 22m with no cover within 6m, fights in place with diamond steps instead of running far and getting shot in the back (Normal only when cover is over 12m away) | GigaChad / Chad / Wreckless / Normal | 〃 `Fight Instead Of Running Across The Open` |
| Corner chasing | If an enemy retreats around a corner in close combat, follows using one of four methods: enter leaning (E for a right-hand corner), take a wide slow slice, jump-shot if the ceiling allows, or prefire only when confident the enemy is there and the magazine has enough ammo (stops at 30% to avoid reloading inside). Waits if its own grenade near the corner has not exploded | GigaChad 90% / Wreckless 85% / Chad 75% / Normal 40% | 〃 `Corner Chase` |
| Head down + weaving during retreat | When sprinting away with its back to an enemy while visible or just hit, looks 55° down (held at least 0.8 seconds), zigzags 30° left/right every 0.45~0.8 seconds, and occasionally jumps | All | 〃 `Retreat Head Down` / `Retreat Weave` |
| Light discipline | Turns off flashlights/lasers while holding an angle on an unseen enemy (stationary/slow movement, known enemy within 60m and 60 seconds), turning them on to dazzle only when the enemy appears | All | 〃 `Light Discipline` |
| Corner pre-aim | While approaching an unseen enemy, stops sprinting within 6m of a corner/door the enemy could emerge from, pre-aims and leans toward it (avoids looking only after half the body is exposed). Aims **beyond the corner where the enemy could appear**, not at the wall edge (up to 1m toward the last position + 30% of distance into the open, 0.6~1.5m), keeping height within ±10° of horizontal (avoids sky/wall aim on stairs or near corners). Door-tactic aim is also limited to ±12° | All | 〃 `Corner Pre-Aim` / `Corner Hold Aim Offset` (aim width 0~60%) |
| Threat-based target selection | Switches from the current target to a greater threat hitting/shooting the bot (even unseen or behind it). Threat considers visibility, hits, shots, enemy looking at the bot, and distance; current targets get a stability bonus and near-dead enemies a finishing bonus; recent exchanges within 10 seconds add weight; PMCs rank above Scavs (prevents both fighting PMCs from effectively teaming up on a newly appearing Scav). While fighting a visible enemy, switches to an unseen enemy only on an actual hit. Upstream SAIN ignored unseen shooters whenever any enemy was visible. `[Threat]` log. Keeps a threat-selected target while it continues firing and for at least F6 `Threat Target Keep Time` seconds (default 2), fixing 0.1-second oscillation between unseen shooters and visible enemies | All | `General > Close Combat (zzap)` `Threat Targeting` |
| Grenade judgment | Only when the enemy is hidden, position information is no older than 20 seconds, and the bot is safe (not taking fire, path to enemy at least 10m, not badly injured, no other visible enemies). **First checks for a real throwing trajectory** (enemy position → path corner → roll 1.2~2.6m inside an open door to the enemy's room; also tries high arcs indoors beyond 6m); rests 1.5 seconds if none exists. Only then applies situational weights: enemy stationary over 6 seconds +30%, indoors +10%, healing/reloading +15%, information over 10 seconds old −20% (base 50%). `[Nade]` logs reasons. No attempts beyond default 45m (F6 `Grenade Max Throw Distance`; Normal 38m, Easy 30m). Indoor/outdoor checks also use the game's under-roof flag (fixes all of Factory being treated as outdoors and only high arcs being tried) | All | 〃 `Grenade Judgment` |
| Own-grenade tracking | Tracks grenades it threw, confirms actual explosions, dodges if one rolls back, and prevents diamond stepping/corner chasing toward its own live grenade | All | Automatic |
| Pistol swap | If the magazine is empty during combat and an enemy is visible within 30m, swaps to a pistol instead of reloading | PMC | 〃 `Pistol Swap When Empty` |
| Quick reload | Drops the magazine for a fast reload in combat, like double-tapping R | PMC | 〃 `Quick Reload In Combat` |
| Long-range weapon swap | Uses a DMR/sniper in slot 2 against enemies beyond 80m; returns to the primary at close range | PMC | 〃 `Long-Range Weapon Swap` |
| Avoid turning its back | Walks toward cover with its gun aimed rather than exposing its back to an enemy within 12m (dashes to cover within 8m; simply runs against more distant enemies) | PMC | 〃 `No Back Turning Near Enemy` |
| Suppression discipline | Prevents emptying the magazine at unseen enemies (short bursts, retains at least 60% ammo) | PMC | 〃 `Suppression Discipline` |
| Repositioning | After being hit first: cover → heal → grenade → move to an angle the enemy cannot see. Outdoors only (excludes roofed areas such as Factory) | PMC | `General > Reposition (zzap)` |
| Bait peek | Jumps forward from cover to gather information, returns, then alternates short firing peeks left/right. Exit positions are 0.9~2m sideways or slightly forward around a cover edge (only directly walkable positions) | Aggressive personalities | 〃 `Bait Peek` |
| Fake reload | Uses the magazine-check animation/sound to fake reloading, then aims at the enemy's likely approach | PMC | 〃 `Fake Reload` |
| Grenade fuse selection | Short fuses indoors/up close; long fuses outdoors at range | PMC | 〃 `Fuse Selection` |
| Squad combat | Crossfire angles, covering fire, and trading a teammate's death — tracks **the enemy who actually killed the teammate** (last attacker) and prioritizes retaliation when the teammate falls (aggressive bots push that position; cautious bots hold an angle or, if no angle is found, aim at the position from where they stand). Indoors, crossfire first covers **another entrance (back door)** to the enemy's room (after 15 seconds without an exit, enters through the back for a pincer — always for Chad/GigaChad/Wreckless, 60% for Normal; bots that do not push hold another 15 seconds only with enemy activity within 8 seconds). Otherwise finds a short 4~14m angle (including crouch-height angles and corners clear after one step). Outdoors uses the existing method plus crouch-height checks | Squads | `General > Squad Combat (zzap)` |
| Squad storm on weak enemies | Squads (at least 2 members within 35m) assess enemies every 20 seconds: no armor/class 1~2, no helmet, pistol/no gun in hand, injuries, healing/reloading, being alone. If weak, rushes together (Chad/GigaChad/Wreckless always, Normal 80%, Timmy 60%, Rat/Turtle 35%, Coward 10%) | Squads | 〃 `Squad Storm Weak Enemy` |
| Post-combat tidy-up (fills the ORBIT handoff gap) | After combat lasting over 1.5 seconds (including close DogFight and flashbang encounters), stays **only while there is work to do** (up to 60 seconds for remaining treatment, using ORBIT's criteria — removes the gap where BSG vanilla AI healed while walking), normally up to 14 seconds and hands over as soon as finished. **Nearby gunfire (70m, 6 seconds) or approaching footsteps/doors (25m, 3 seconds) keeps SAIN active up to 60 seconds**: aims toward sounds, cancels advancing, avoids healing near footsteps/stops healing within 15m (ignores squadmates/dead characters). Keeps the gun raised for 25 seconds after combat: **in the open, first moves quickly to cover within 12m** (found by surrounding walls + blocked sight from the combat direction, reloading while moving); searches up to 25m and runs there if needed; if none exists, **goes prone** to heal/watch (stands to move). Reloads below 70%; crouches to heal injuries in place (first aid → surgery, up to 60 seconds until finished); rejoins a leader over 25m away, otherwise watches the last threat direction (including the killed enemy's position) in a half-crouch for 3~6 seconds, then may advance to check the kill by personality (GigaChad 80% · Chad 65% · Normal 40% · Rat 10% · Coward 0%, ×0.2 if badly injured). Runs in SAIN's squad layer without delaying ORBIT's handoff (15 seconds after the combat layer ends); previously those 15 seconds fell back to vanilla behavior | All | `General > Squad Combat (zzap)` `Post-Combat Tidy-Up` |
| Friendly fire-lane avoidance | Does not walk into a teammate's firing/aiming line: stops outside or sidesteps if already inside (passes through after being blocked over 1.5 seconds). The shooter also checks every tick during bursts and predicts teammate positions 0.3 seconds ahead to stop firing | Squads | 〃 `Fire Lane Guard` |
| Freeze ambush | Stops and aims at a corner when an enemy is close (immediately cancels on being hit). If a wall blocks the aim line, checks toward the corner edge → diagonal → sideways and moves to the first clear angle (up to 2.5m). If only low cover blocks a crouched bot, stands up in place | Rat / SnappingTurtle / Coward (zzap preset) | `General > Freeze Ambush (zzap)` |
| Explosion response | A grenade explosion within 25m makes even previously unaware bots recognize the thrower as an enemy | All | Automatic |
| Player-style response (**on by default**, see above) | Adjusts corner holding, close combat, leaning, and spacing to recorded aggression, bunny hopping, leaning, and grenade use. Strength varies by personality and grows with more data; excludes TEST | Against human players | `General > Player Style Recorder (zzap)` |
| Playstyle recording (**on by default**) | Automatically marks test raids (detects invincibility, infinite ammo, developer-tool mods), while still applying style adaptation. Records movement, peeking, shooting, reloads, and kills/deaths per raid for adaptation. Key-input recording (keys/timestamps) and key timeline files are **off by default** (`Record Keys` / `Key Timeline File`); adaptation still uses jump/lean counts from style records when these are off | Player | `General > Player Style Recorder (zzap)` |
| Disable hit voice lines | Removes the grunts / hit callouts on every hit | All | `Talk > Bot Pain Voice On Hit (zzap)` |
| Footstep discipline (remake5) | Approaching a known but unseen enemy: walks instead of sprinting within 30m, crouch-walks within 12m. Personality-based chance (Rat/SnappingTurtle/Coward always ~ Wreckless 15%); always quiet against looting enemies. Exceptions for taking fire, squad support, and chasing snipers; normal movement resumes after 20 seconds per enemy | All | Automatic |
| Sound-specific vulnerability response (remake5) | On hearing reload (~2.5 seconds), healing (~5 seconds), surgery (~10 seconds), or looting, **rushes only if it can arrive within that window** (5m/s sprint, personality-based courage; not while taking fire or with another enemy visible). Previously required a path under 10m, rarely triggering against bots. Backs away from grenade-pull sounds instead of rushing | Rush-capable personalities | Automatic |
| Three tiers of distant gunfire (remake5) | Within 100m = searches that location; 100~250m = looks toward it only (does not add an enemy, preserves ORBIT movement); beyond 250m = ignores. Always responds to **someone shooting at it**, regardless of distance (a passing bullet or hit within 5 seconds). Personalities that chase distant shots do so at most once per minute when not already fighting | All | Automatic |
| Sounds across floors (remake5) | Indoors, footsteps and similar sounds from unseen enemies one floor (2.5m+) above/below are halved; gunshots are slightly quieter (more reduction across two or more floors). Door tactics' third-party detection ignores footsteps from other floors | All | Automatic |
| Bots ignore other bots' bait (low-end PCs) | Bots do not treat other bots' reload, heal, grenade-pin, or looting sounds as rush openings (human-player sounds still work), saving computation on bot-versus-bot baiting | Low-end preset | `Hearing > Bots Take Other Bots' Bait` |

**Bot door ramming — sprinting through doors (built into SAIN, no other mod needed)** — added 2026-10-13, **not verified in game (build only)**

Code from [doordash](https://github.com/bmpq/spt-doordash) (bmpq) and [DualSideDoorBreach](https://github.com/maschine34675/DualSideDoorBreach) (maschine34675, MIT) was **ported into SAIN for bots only** (see 'Ported code' for attribution).
Bots can therefore ram doors **without installing either mod**. Having them installed is also fine:
- **doordash**: Player ramming still uses doordash's own settings (`com.tarkin.doordash.cfg`, 4.5m/s speed threshold, etc.). Bots ignore those settings and use only the SAIN settings below.
- **DualSideDoorBreach**: Player kicks use that mod's settings. **SAIN determines the final direction of bot rams** (10/14 — the mod's formula could misread slightly rotated doors or bots close to a hinge, opening the leaf toward the bot). SAIN's patch runs after that mod, avoiding a double reversal.
- **Doors bots will not ram**: Doors marked unbreachable by the game; sliding doors (Labs glass doors); doors with someone in the leaf's opening space (within 1.5m beyond the door); doors opening toward the bot without space for the reversed leaf to rest (narrow corridors — checked before running up since 10/14).
- **Kick if the ram fails at the door (10/14)**: If an angled or slow approach fails to trigger a ram and the bot is already at the door, kicks it open in place instead of using its hands. Kicked doors always open away from the bot.
- **Push even when opening by hand (10/14)**: F6 `Always Push Open Doors` (on by default) works again. A door that normally opens toward the bot is pushed away instead, preventing the leaf from shoving the bot into a wall gap. If a wall/object blocks that swing, it opens normally.

Difference from doordash: Bots often sprint while aiming at corners, so doors are detected along their actual movement direction rather than their facing direction. The flying-door effect (doordash 1%) uses player-based calculations and was not added for bots.
This caused failed rams in simulation: a bot initially moving at 4.6m/s slowed to 1.2m/s near the door while aiming at a corner, falling below doordash's 4.5m/s threshold.

| F6 `General > Door Tactics (zzap)` | Default | Meaning |
|---|---|---|
| `Bot Sprint Ram` | On | Master switch for bot ramming |
| `Bot Sprint Ram: Speed Needed (m/s)` | 1.6 | Minimum speed at door contact to open it (doordash player threshold 4.5; changed 2.8 → 1.6 on 10/14) |
| `Bot Sprint Ram: Arm Damage` | 5 | Damage to the ramming bot's left arm (double for metal doors, can fracture). Also drains stamina |
| `Bot Sprint Ram: Both Sides Of A Door` | On | Rams doors opening toward the bot too — opens them the opposite way, away from the bot (DualSideDoorBreach method) |

| When | What happens | F6 setting |
|---|---|---|
| Room entry (door tactic) | For entry without a grenade, runs up from 1.8~4.5m before the door, rams at full sprint, and continues inside | `Sprint Ram Into The Room` / `… Chance` |
| Escaping | While retreating/running to cover under pursuit (hit within a few seconds or enemy visible), rams through an unlocked closed door 1.5~7.5m ahead instead of stopping to open it | `Sprint Ram While Running Through Doors` + `Ram Chance - Running Away` |
| Rushing | Rams closed doors on the approach to an enemy | `Ram Chance - Rushing` |
| Searching | Sprints the last few meters and rams through while searching for an enemy that may be behind the door (breaches when uncertain) | `Ram Chance - Searching` |
| Other combat movement (added 10/13) | Closed doors along other combat routes: running to cover without pursuit, flanking, squad movement, repositioning, etc. Not used during sneaking, freeze ambush, or door tactics | `Ram Chance - Other Combat Moves` |

Shared conditions: an unlocked, closed door; a path straight toward it (no turn over 50°); no ally directly in front. Rolls once per door every 10 seconds. If a ram fails (insufficient speed), opens it normally.
Frequency varies by preset difficulty (see the zzap preset table). In the 10/13 simulation it appeared only 1~2 times per 15 minutes, mainly because too few situations considered ramming rather than low chances (13 rolls at closed doors over 60 minutes across 4 maps). Other combat movement was added and chances raised. To check in simulation, use the **`문 전술·박치기 점검` (Door tactics / ramming check) simulation preset** on the simulation page (100% ramming and GigaChad door tactics for all PMCs only in simulation raids; SAIN presets remain unchanged).
Raid journals include `[DoorMod]` (settings/installed door mods) and `[DoorRam]` (rams while running); SUMMARY includes `doorram.breach*` (actual breaches), `doormod.ram.*` (room entry), and `doormod.move.*` (escape/rush/search/other movement) counters.
(Door Overhaul integration added that same day — shooting through doors, grenade breaching, pushing partly open doors — was removed because that mod used around 600MB of memory per minute.)

**Bug fixes**

| Fix | Details |
|---|---|
| Stuck at doors | Door detection range, stuck checks, stopping door interactions when entering standby, a door-state watchdog (1.2 seconds), etc. Details: [`changelog/FIXES.md`](changelog/FIXES.md) |
| Shooting walls/ceilings at upstairs enemies (10/13) | Suppression, blind fire, and prefiring at enemies upstairs (height difference at least 2.3m) known only by sound used to hit walls/ceilings. Now, for unseen targets with an obstructed muzzle-to-target line, does not fire if the target is on another floor or a wall is within 2.5m of the muzzle (blind fire exempts the close-wall check because it raises the gun over cover). SUMMARY `shoot.blocked.*` (not verified in game) |
| Walking into open door leaves (10/31, Remix7.1) | Open leaves were not excluded from bot paths, so bots walked into them and stopped. Their space is now excluded (enables an EFT mechanism present but disabled per door; disables it when closed). `Door Tactics (zzap) > Bots Walk Around Open Door Leaves` (on by default). Door frames/leaves are also excluded as cover. Image: [`remix7.1-open-door-leaf.png`](release-notes/images/remix7.1-open-door-leaf.png) |
| Stuck behind open doors (10/13) | Aiming while moving (DogFight backpedaling, diamond-step sidestepping) could enter the gap between an open leaf and a wall. Both movements now avoid open leaves like walls (not verified in game) |
| DMR/sniper long-range accuracy (10/13) | Residual recoil aim sway recovers slowly over about 2 seconds; high-recoil DMRs/snipers fired again before recovery at range, missing more than assault rifles. Their aim now recovers 3 times faster (not verified in game) |
| Error from a despawned shooter (10/13) | Damage from an already removed player body caused EFT to read its position and throw an NRE. Skips only the hit-by-someone reaction in that case |
| Disable automatic door closing | Removes the close/reopen loop on open doors along a route (`Door Tactics (zzap) > Bots Close Open Doors In Their Way`, off by default) |
| Waypoints door links | Handles swapped fields in door links created by DrakiaXYZ-Waypoints |
| Repeated tactical-device clicking | Guns with no usable light retried every second. After 2 failures, waits 60 seconds |
| Flashlight dazzling | Switches on within 0.1~0.35 seconds of seeing an enemy |
| Constant weapon swapping (ping-pong) | Primary↔secondary↔pistol switching every 1~2 seconds. BSG toggled primary/secondary without checking ammo and returned to an empty primary after 30 seconds without seeing an enemy. Now keeps a loaded held gun; if empty, chooses **only a loaded gun**. Two remaining paths fixed 10/13: ① after swapping to a distant-target sniper, the game switched back to primary on reload because the enemy was far away → a sniper deliberately selected by SAIN is allowed to reload; ② a primary with an empty magazine but spare magazines swapped to pistol, then the game returned to primary for reload, repeating → waits for reload if spares exist (swaps only after 6 seconds of failure), and does not draw the pistol again for 5 seconds after the game holsters it. Third path fixed 10/14: alternating targets between a visible enemy at 81m and one unseen for 33 seconds made the sniper repeatedly holster/draw → keeps the sniper if any distant enemy was seen within 20 seconds, even if not the current target |
| Cannot reload/swap looted guns | PMCs looting guns from Scavs or containers could fail to reload despite spare magazines, stand idle with empty guns instead of swapping, or draw knives despite a usable gun in slot 2. EFT created per-slot gun/reload information only on first use and never refreshed it, so it searched for magazines for the old gun; never-used slots were absent from loaded-gun searches. Now synchronizes with actual slots every second and when the weapon list changes (right after ORBIT looting) |
| Lost magazines from quick reloads | Quick reloads dropped magazines bots never retrieved; after several fights they had loose rounds but no magazines, causing weapon cycling. Now drops magazines only with at least 2 spares |
| Holding an empty pistol | In close combat, immediately returns from an empty pistol to a loaded primary, otherwise chooses a reloadable weapon |
| Repeatedly walking into walls | Upstream SAIN's unstuck feature was **entirely disabled**. New stuck monitoring in any layer: after 3 seconds without moving recalculate path → 5 seconds vault/jump → 8 seconds short relocation to the next path point (not through walls/closed doors) → 14 seconds reset movement commands |
| Walking dead bodies | An error anywhere in death processing prevented ragdolls, leaving upright bodies sliding. Invokes subscribers separately for both the health-controller death event and events inside `Player.OnDead`; completes corpse creation even after errors; directly handles the body/disables the brain if still no corpse after 1.5 seconds; ignores dead bodies' movement/jump input |
| Doors nobody can open | Forced door recovery did not synchronize BSG's internal interaction record, leaving doors unusable by bots and players. Now synchronizes that record and tries recovery first when blocked at a door |
| Accumulating corpse blood/bullet decal textures (EFT) | EFT creates blood/bullet decal textures per hit body part (about 4MB each) and retains **even corpse textures until raid end** — about 790 textures (roughly 3GB+) in a 20-minute simulation. Clears decals on characters dead for 30 seconds and returns textures to EFT's pool for reuse on later hits. Bodies/gear remain (only blood marks disappear). F6 `General > Performance` `Free Corpse Blood Decal Textures` / delay |
| Mixed presets (upstream SAIN bug) | Loading the preset list applied **the global settings of the last preset read**. Now locks them to the selected preset |
| Peeking while healing | Cancels leaning during treatment and avoids starting treatment in plain enemy sight |
| Throwing smoke like fragmentation grenades | EFT picked fragmentation → flash → **anything**, so smoke-only bots threw smoke at enemies. Vanilla smoke (RDG-2B/M18) is excluded; modded grenades such as CS gas (블디 (Black Division)) are still thrown |
| Zryachiy (Lighthouse) could receive SAIN | If the server's SAIN exclusion list was unavailable at startup, SAIN could attach to Zryachiy, followers, BTR, marksmen, and infected (reducing long-range awareness/accuracy and making Lighthouse easier). These are now always forced to vanilla even without the list; exclusions are logged |
| Post-combat tidy-up missed gunfire | The rule to retain SAIN for nearby gunfire (up to 60 seconds) never received gunshots because they used another delivery path. Connected that path |
| Simulation raids saved as player style | Invincible, stationary spectator raids from simulation presets were entering player-adaptation history. No longer saves when a simulation preset is active |

## zzap presets

Four presets are included in the server mod folder `SAINServerMod/Presets/` and appear directly in the SAIN preset list (F6 or `https://127.0.0.1:6969/sain/presets`).

**Default presets are read-only.** Editing and saving creates an `이름 [Modified]` (Name [Modified]) copy, which the game uses. Originals cannot be overwritten/deleted in F6 or on the web; copies can be freely edited/deleted. The old `zzap 저사양 [테스트]` (Low-end [Test]) was removed — enable the same effect on any preset via '빠른 설정 › 저사양 PC (Quick settings › Low-end PC)' (the server deletes old files at startup and preserves custom copies).

| Preset (F6 list order) | Purpose |
|---|---|
| `zzap 쉬움` (Easy) | Difficulty 1/3. Same tactics as zzap, **much lower frequency** (door tactics ×0.5; ramming: room entry 25% · escape 40% · rush 35% · search 25% · other combat movement 15%; fakes 15/8%; run-by 25%; reduced bait peeks, fake reloads, corner jiggles, rapid lean taps, and prefire; no squad storm/back-door entry; retaliation 6 seconds; threat-target retention 3 seconds). Upstream SAIN easy modifiers (high recoil/spread, half vision distance, slower spotting, weaker hearing, 120° field of view, slower fire, minimum 2-second aim, double human-like mistakes) |
| `zzap 보통` (Normal) | Difficulty 2/3. Same tactics as zzap, slightly lower frequency (door tactics ×0.75; ramming 35% · 55% · 50% · 35% · 25%; fakes 30/15%; run-by 35%; reduced bait peeks, fake reloads, corner jiggles; retaliation 8 seconds). A true midpoint between Easy and zzap since 2026-10-18: recoil 0.8 (zzap 0.1 · Easy 3.0), vision distance ×1.4, 160° field of view, 75% of zzap fire rate, PMC aim minimum 0.6 seconds, mistakes ×1.25 |
| `zzap` | Difficulty 3/3, default. Shooting/aim/reaction values from upstream TwitchPlayers (hardcore), behavior from this fork's tactics (ramming: room entry 50% · escape 75% · rush 65% · search 50% · other combat movement 35%) |
| `zzap TEST [시뮬 전용]` (Simulation only) | **Not for regular play.** Simulation preset for watching bot fights with a free camera. All PMCs use GigaChad door tactics (100%), always reposition, all diamond-step, 100% run-by, no human-like mistakes, no player adaptation, detailed logs, **raid journal enabled**, **corpse cleanup** (hides oldest bodies above 40 — for simulation frame measurements) |

**ORBIT compatibility settings (all four presets):**
- Personality distribution — Rat 10 / Wreckless 5 / SnappingTurtle 5 / Coward 5 / Chad 5 / Timmy 3 / GigaChad 3 (recommended by ORBIT's README)
- `General > Extract > SAIN Extract Behavior` **off** — ORBIT handles extraction
- `General > Looting Bots > Bot Extraction From Loot` **off** — ORBIT also handles extraction after looting

**Works without BotCallsigns / TwitchPlayers.** Both can also be installed — TwitchPlayers installs its own presets separately and
only **adds** TTV-name-to-personality mappings to SAIN without changing the selected preset. Keeping `zzap` selected preserves its settings while also applying the TTV personality mappings.

⚠️ Updating the mod overwrites `Presets/zzap.json` too. Corpse hiding works only for presets with `TEST` in their names. When you edit and save a zzap preset (Easy, Normal, zzap, Low-end, Simulation only) in F6 or on the web, the original remains and an **`이름 [Modified]` (Name [Modified]) copy** is saved (`[Modified 2]`… if one exists). Updates preserve copies. However, if server settings force that preset, saves still go to the original.

## Simulation lab (`zzap TEST [시뮬 전용]` (Simulation only) preset only)

For extended bot-behavior testing across multiple maps. **Completely inactive with other presets** (tells the server it is off, and client code returns at its first line — no overhead for regular users).
Works only for presets with F6 `General > 시뮬레이션 랩 (zzap)` (Simulation lab) → `시뮬레이션 모드` (Simulation mode) enabled; only `zzap TEST [시뮬 전용]` (Simulation only) has this.

| Feature | How it works |
|---|---|
| Map rotation | Starts only after pressing **"시뮬 시작 (Start simulation)"** on web `/sain/sim` (launching the game alone does not start it; press again after restarting). Starts the next map after a main-menu countdown (default 15 seconds, or the post-raid inter-map wait of 120 seconds, counted from when the full menu appears; cleans memory during the wait). Never starts while leaving a raid. After each map's configured duration (default 20 minutes), ends the raid as Survived, skips results, returns to menu, and starts the next map. Custom maps such as Icebreaker are excluded (edit exclusion words on the web). Labs, The Labyrinth, night Factory, and Ground Zero 21+ are off by default (entry requirements unverified). |
| Spectator mode (default) | Player is invincible and bots cannot add the player as an enemy (blocked in `BotsGroup.AddEnemy`, the bot-group enemy-add method). Teleports to a spawn in side A's zone at raid start because distant bots use simplified AI. |
| Per-map combat tests | Different locations/goals per map: Factory close combat; Customs Dorms attack/defense (Dorms defenders vs approaches from Gas Station/Crossroads); Interchange Goshan vs IDEA; Reserve pawn buildings; Shoreline Resort East vs West Wing; Woods Sawmill open ground; Lighthouse Village vs Chalets; Streets of Tarkov Concordia vs Construction; Ground Zero city blocks, etc. Side A USEC, side B BEAR. |
| Bot spawning | Each raid, the server clears other spawns (ABPS, default PMCs, Scavs, bosses) and inserts reserve squads. The client watches living bot counts (default max 12), calling 3~4-member squads to the smaller side at each reinforcement interval (default 45 seconds). Creates profiles only when calling squads, avoiding mass pre-generation. APBS gear tiers remain unchanged. |
| Web settings `/sain/sim` | (All SAIN web pages default to Korean; switch to English with the top button.) Map order (arrows), on/off, per-map minutes, scenarios (name, goal, A/B zones, factions, highlighted counters), click to add from actual map zones, pause/resume rotation, designate next map, start a new run. |
| Simulation presets | Separate from SAIN presets: bundles **what to test, map order, spawns, and personality distribution**. Nine defaults (standard rotation / pre-release check: Factory 30 minutes + Labs 30 minutes / quick check: Factory 10 minutes / close-range and indoor tactics / **문 전술·박치기 점검 (Door tactics / ramming check)** (10/13 — Factory, Customs, Reserve, Labs 15 minutes each, mostly aggressive personalities, door-check switch on) / personality comparison (even) / defensive personalities / aggressive personalities / outdoor and long-range), plus save current settings with a name/purpose. Applying sets rotation, spawn pace, personality distribution, and mod-item exclusion; next start begins at the first map. Analysis shows the preset used (or modified if changed). |
| Personality distribution | Picks simulation PMC personalities by ratios such as `Chad:2,Rat:1` or `even` (all 8 equally). Empty uses SAIN defaults (level/gear rules). Forced personalities in the SAIN preset take priority. |
| Disable Blackout outages | Prevents the Labs Blackout event mod from rolling outages only during simulation raids (on by default), keeping bright/dark data separate. Blackout settings remain unchanged. |
| Exclude modded items | Bots avoid modded weapons, attachments, gear, and clothing only in simulations (on by default). Clears APBS's per-raid mod-item list only for simulation raids, preserving APBS settings/files; APBS fills slots containing only modded items normally to avoid empty hands. No effect without APBS. |
| **zzap cross-check (remake5, on by default)** | Uses **zzap preset values** for behavior settings (close combat, doors, repositioning, player response, freeze/ambush, squads) during simulation raids, preventing TEST switches (diamond-step test mode, all GigaChad door tactics, 100% run-by, etc.) from hiding zzap-only bugs. Only logging switches use simulation preset values; restores settings after the raid. The server compares TEST/zzap preset files and shows differences the cross-check cannot override in analysis. |
| **Door check (10/13)** | In simulation raids only, always (100%) rams when conditions allow, and all PMCs plan GigaChad-style door tactics (100% start). Overrides zzap cross-check values and restores after the raid; SAIN presets stay unchanged. Enabled by the `문 전술·박치기 점검` (Door tactics / ramming check) simulation preset. Replaces the old F6 `zzap TEST 문 모드 [시뮬 전용]` (Door mod [Simulation only]) preset (leftover files are deleted at server startup). |
| **Mixed-faction squads (remake5, on by default)** | Makes 30% of reinforcement squads Rogues (`exUsec`) or Raiders (`pmcBot`) via the game's boss spawner. Modded faction waves such as Black Division are preserved when map spawns are cleared. All simulation roles are enemies to each other (only bosses and their own followers are allies). |
| **Random boss each raid (remake5, on by default)** | Spawns one of Reshala, Killa, Shturman, Glukhar, Sanitar, Tagilla, Knight (Goons), Kaban, Kollontay, or Partisan (different from the previous raid), with followers, in the simulation area 60 seconds after the first squad. Uses templates from server map data (added by ABPS), falling back to built-in templates. Analysis warns if no spawn within 90 seconds. |
| remake5 analysis section | Faction tables (spawns, kills/deaths, SAIN/ORBIT/vanilla brain shares, diamond state at death, frequent decisions), boss/faction call results, first hits by jump type, sound reactions (distance tiers, floor attenuation, bait), vulnerability-rush funnel, footstep discipline (approach movement), and whether the side hearing first won. Adds `ignoredSniper` anomalies (not targeting a distant attacker while being hit). Every warning includes fix guidance for Claude. |
| Analysis `/sain/sim/analysis` | Per run/map: status (completed/interrupted), elapsed time, bot deaths, minimum fps, errors, **automatically detected problems** (stutters, error types, missing spawns, non-simulation bots, no engagements, frame drops/freezes, memory growth, deaths just after decision changes, deaths during freeze/ambush, door-tactic interruption rate, etc.), counters (map-specific highlights), kills/deaths by personality/decision reason, and timeline. **"Claude용 요약 만들기 (Create summary for Claude)"** exports one run as one md file (also saved in server `SimLab/runs/`). |
| Crash recovery | Records locally every 10 seconds (`BepInEx/config/SAIN-zzap/SimLab/*.jsonl`) and sends full records to the server every minute. Alt+F4 sends one final report; after process death, the next launch sends the last local record and marks the run interrupted. Errors from BepInEx logs (all mods + Unity) are grouped/count by matching first lines. |

Note: These are raids on a real profile, so XP and time advance — a **dedicated simulation profile** is recommended. Automatic menu starts, skipping results, and ignoring the spectator **have not yet been verified in game** (build only). If automatic start fails, you can launch maps manually; simulation spawns, time limits, and recording still apply.

## Version / Compatibility

**Based on upstream v4.5.1 (SPT 4.1).** The previous version was based on 4.4.3 (SPT 4.0);
the four door fixes were carried over unchanged onto upstream v4.5.1.

Checks made during synchronization:

- Upstream v4.4.3 → v4.5.1 spans 30 commits / 403 files, but **contains no door/stuck fixes**.
  The four bugs above remain upstream and are fixed only in this fork.
- There were no rebase conflicts. Since that alone does not prove correctness, every symbol touched
  by the fixes (`CanInteract`, `_nextDoorUpdateTime`, `DoorDataStruct.LastCloseTime` /
  `.CurrentSqrMagnitude`, `OnPathComplete`, `BotComponent.DoorOpener`) was **checked individually**
  against v4.5.1. All remain present with the same signatures.
- `CheckObjectInWay` has a changed signature (added `out RaycastHit`), but v4.5.1 also has
  **only that one call site**, so nothing breaks.
- `OnPathComplete` was an event upstream **declared but never raised**. In this fork,
  `SAINMoverClass.PathComplete` actually raises it.

### Other bot mods (2026-10-23)

| Mod | With SAIN zzap |
|---|---|
| **Black Division, RUAF Come Home, UNTAR Go Home** (registered with SAIN through MoreBotsAPI) | SAIN attaches its combat layers directly, rechecking at every raid start to fill missing entries, and recognizes unknown enemies when hit by them. **Use the original Black Division mod** (the old zzap fork is no longer needed). Black Division defaults to GigaChad and is immune to CS gas grenades |
| **ISB Special Forces** | Behaves as designed by its original mod. ISB handles combat decisions with its own AI; SAIN supplies only aim, vision, and hearing values. Unaffected by SAIN zzap's combat changes |
| **Scourge** | Excludes itself from SAIN and uses its own AI as originally designed. Unaffected by SAIN zzap |

These details have no effect if those mods are not installed. SAIN class/method names looked up at runtime by ISB/Scourge are preserved.

### Compatible mods (verified only, 2026-11-03)

There are two kinds of checks:
- **In game**: Installed alongside SAIN zzap and used in raids without errors.
- **Code**: Compared both mods' code and found no overlapping or breaking behavior. Not yet run together in game.

A mod missing from this list is not necessarily incompatible; it simply has not been checked.

| Mod (version checked) | Checked | Notes |
|---|---|---|
| ORBIT 3.1.0 | In game, code | All four zzap presets include ORBIT's recommended settings (ORBIT handles extraction) |
| Black Division 1.3.1 · RUAF Come Home 1.2.1 (MoreBotsAPI 2.1.1) | In game | See the Other bot mods table above |
| Bot Placement System (ABPS) 2.1.0 · Progressive Bot System (APBS) 2.4.1 | In game | zzap handles spawning only in simulation raids; regular raids stay unchanged |
| BigBrain 1.5.0 · Waypoints 1.9.0 | In game | Required by SAIN |
| Bot Callsigns 2.0.5 | In game, code | Only adds PMC names; does not touch SAIN |
| Twitch Players 3.1.2 | Code | Requires Bot Callsigns. Streamer-named bots still receive Wreckless/SnappingTurtle personalities. Its bundled `Twitch Players` preset also opens unchanged in zzap (new zzap settings use defaults). A PMC personality assigned directly in F6 via 성격 지정 (Assign personality) takes priority |
| Harmony Profiles (PBT) 1.0.0 | Code | Handles only PMC names, levels, gear, and stats, not bot AI. PBT personalities are for stats/chat and are separate from SAIN combat personalities |
| Blackout 4.1.1 | In game | Prevents outages only during simulation raids |
| Manimal CS Gas 2.0.0 | In game | Black Division bots are immune to gas |
| MapVariants 1.0.3 (FactoryClassic 1.0.2) | In game | |
| TYR DeClutter 2.1.1 · CompoundingPerf 2.3.0 · RAM Cleaner Interval 2.15.0 | In game, code | No overlapping patches |

### Compatible mods (verified only, 2026-11-03)

**In game** = run together with SAIN zzap in raids with no errors. **Code** = both mods' code checked side by side with nothing overlapping or breaking; not yet run in game. A mod missing from this list isn't known to be broken; it just hasn't been checked yet.

| Mod (version checked) | Checked | Notes |
|---|---|---|
| ORBIT 3.1.0 | In game, code | All four zzap presets include ORBIT's recommended settings (ORBIT handles extraction) |
| Black Division 1.3.1 · RUAF Come Home 1.2.1 (MoreBotsAPI 2.1.1) | In game | SAIN adds its combat layers to these bots |
| Bot Placement System (ABPS) 2.1.0 · Progressive Bot System (APBS) 2.4.1 | In game | zzap takes over spawning only in sim raids; normal raids are unchanged |
| BigBrain 1.5.0 · Waypoints 1.9.0 | In game | Required by SAIN |
| Bot Callsigns 2.0.5 | In game, code | Only adds PMC names; doesn't touch SAIN |
| Twitch Players 3.1.2 | Code | Needs Bot Callsigns. Streamer-named bots still get Wreckless or SnappingTurtle. Its bundled `Twitch Players` preset opens in zzap as is (settings new in zzap use their defaults). A personality you set for PMCs in F6 takes priority |
| Harmony Profiles (PBT) 1.0.0 | Code | Handles PMC names, levels, gear and stats only, not bot AI. PBT's personalities are for stats and chat, separate from SAIN's combat personalities |
| Blackout 4.1.1 | In game | Blackouts are turned off in sim raids only |
| Manimal CS Gas 2.0.0 | In game | Black Division bots are immune to the gas |
| MapVariants 1.0.3 (FactoryClassic 1.0.2) | In game | |
| TYR DeClutter 2.1.1 · CompoundingPerf 2.3.0 · RAM Cleaner Interval 2.15.0 | In game, code | No overlapping patches |

### An upstream bug was also fixed

**The upstream v4.5.1 server mod does not build at all.** The foreach variable
in `SainSectionEditor.razor` was named `section`, so `@section.Name` was parsed
as a **`@section` directive** rather than member access:

```
RZ2005: The 'section' directive must appear at the start of the line
RZ9986: Component attributes do not support complex content
```

Renaming the variable fixed it. The same failure was confirmed after checking out the upstream tag unchanged,
so this was not introduced by the fork.

`SptVersion` was also raised from upstream's `4.1.3` to **`4.1.5`** (the server version actually used).
It builds as-is.

## Build

```
dotnet build SAIN.slnx -c Release
```

Two projects: client (`SAIN`, netstandard2.1) and server (`SAINServerMod`, net10.0).
Release builds also create a distributable zip in `release/` (disable with `-p:SkipPackage=true`).

References come from `References/` (updated upstream for SPT 4.1) and NuGet.
The client requires **BepInEx's dedicated feed** (`https://nuget.bepinex.dev/v3/index.json`)
for `BepInEx.Core 5.*` and `UnityEngine.Modules 2022.3.43`; it is already
registered in `nuget.config`.

## Reporting bugs / lag

For reports such as lag or unusual bots, **always include `BepInEx/LogOutput.log`**. To record SAIN behavior too, go to F6 → General:
- Enable `플레이어 스타일 기록 (zzap)` (Player style recorder) → `레이드 일지` (Raid Journal), and also send `BepInEx/config/SAIN-zzap/Journal/<날짜>_<맵>.log`.
- For more detail: enable `진단 로그` (Diagnostic Logs) under `문 전술` (Door tactics) / `분대 교전` (Squad combat) / `위치 변경` (Repositioning) / `정지 및 매복 (zzap)` (Freeze and ambush), which writes to LogOutput.log.
- **장면 표시 (Mark scene) (F8, added 10/14)**: Press F8 when you see unusual bot behavior. Records up to 4 bots near the center of the screen as `[Mark] #번호` (number) lines in the raid journal (screen notification: "장면 표시 #n (Scene marked #n)"). Records layer/decision/reason, speed/sprint/stance, angle between movement and facing, reload/fire/hits, leg health, target enemy, nearby doors (type/state/leaf), and distance to the wall ahead. Send a screenshot too to locate the exact scene. Change the key in F6 → `SAIN Editor` → `Mark Scene Shortcut`. Simulation raids also show it in the analysis md's 장면 표시 (Marked scenes) section.
- Turn logging off again after reporting (files grow and RAM usage rises slightly while enabled). All are off normally, and no log strings are constructed while disabled.

**Do not report problems with this fork to the original authors (Solarint / ArchangelWTF).**

## License

Follows original author Solarint's license.
Bot door ramming (`SAIN/Classes/Bot/Doors/BotDoorBreacher.cs`) includes code from doordash (bmpq) and DualSideDoorBreach (maschine34675, MIT — Copyright (c) 2026 maschine34675; full permission notice in [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)).

