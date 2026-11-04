# Third-party notices

## DualSideDoorBreach (maschine34675) — https://github.com/maschine34675/DualSideDoorBreach

Used in `SAIN/Classes/Bot/Doors/BotDoorBreacher.cs` (dual-side breach swing direction).

```
MIT License

Copyright (c) 2026 maschine34675

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## doordash (bmpq / tarkin) — https://github.com/bmpq/spt-doordash

Used in `SAIN/Classes/Bot/Doors/BotDoorBreacher.cs` (sprint ram breach, door swing check, rammer damage). License: MIT, as stated on its Forge mod page https://sp-mod.com/mod/2214/doordash (the GitHub repository has no license file). Credited here and in README. Will be removed on the author's request.

## ORBIT (Chazut) — https://github.com/Chazut/ORBIT

Not copied verbatim: the approach was re-implemented on SAIN's own door classes on 2026-09-20 and adjusted since from in-game problems.
Current state (full history in Korean: `changelog/FIXES.md` section 16):

1. Door-state finalize watchdog (after ORBIT's `DoorWatch` / `DoorSystem`) — now in `SAIN/Classes/PlayerManager/Doors/DoorHandler.cs`.
   Every door on the map is watched through `OnDoorStateChanged` (whoever opened it, as ORBIT's `DoorSystem` does). A door stuck in
   `Interacting` is finalized after 1.2 s (was 3 s), waits while a player or bot is still in its own door animation (10 s / 4 s),
   only opens a locked door when a kick really landed, and re-syncs BSG's interaction record so the door keeps working.
2. Sprint off + 0.25x speed near doors (ORBIT `MovementSystem.HandleDoors`, same factor) — `SAIN/Classes/Bot/Mover/BotPathData.cs`.
   Normal moves: any door within 3 m, as in ORBIT. Urgent combat moves (cover, retreat, grenade, rush while under fire or with
   the enemy seen in the last 3 s): shut doors only, slow-down only in the last 1.5 m. Sprint-ram moves: no slow-down.
3. Horizontal-only stillness check (ORBIT `SoftStuckRemediation`) — `BotPathData.CheckStuck`. A fixed 0.05 m threshold instead
   of ORBIT's speed-scaled one (not verified for SAIN's speed units). Unchanged since 2026-09-20.

The bundled zzap presets also use ORBIT's recommended personality mix and extraction settings. ORBIT's own license also credits
Phobos (SleepingPills, MIT) for its dispatch foundation; that part is not used here.

```
MIT License

Copyright (c) 2026 Chazu

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## TwitchPlayers (harmony) — https://github.com/harmonyzt/TwitchPlayers

The bundled zzap presets (`SAINServerMod/Presets/`) use the shooting, aiming and reaction values of the TwitchPlayers (hardcore) preset.
License: MIT, as stated on its Forge mod page https://sp-mod.com/mod/1895/sain-twitch-players (the GitHub repository has no license file). Credited here and in README. Will be removed on the author's request.
