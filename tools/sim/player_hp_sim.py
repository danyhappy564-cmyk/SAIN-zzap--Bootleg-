#!/usr/bin/env python3
"""zzap sim analysis (user 2026-10-02: player kills mean nothing in god mode).

    python3 tools/sim/player_hp_sim.py <journal.log>...

God-mode raid -> how often the player would have died. Replays [PlayerHit] (post-armor damage) on EFT PMC base HP.
Head/Thorax at 0 = death (then a fresh body). A destroyed limb passes damage on to the living parts (EFT: arms x0.7,
legs x1.0, stomach x1.5), split evenly. No healing (the tester in god mode doesn't heal) - so this is the upper bound."""
import re, sys
BASE = {"Head": 35, "Chest": 85, "Stomach": 70, "LeftArm": 60, "RightArm": 60, "LeftLeg": 65, "RightLeg": 65}
SPREAD = {"LeftArm": 0.7, "RightArm": 0.7, "LeftLeg": 1.0, "RightLeg": 1.0, "Stomach": 1.5}
# 10/6: one burst of 4 headshots in 2s counted as 4 deaths - a real player dies once. Hits within this window after a
# virtual death belong to the same death.
DEAD_WINDOW = 3.0
hit_re = re.compile(r"^t=([\d.]+) \[PlayerHit\] by (.+?) (\w+) (-?\d+) dmg(?: from (\d+)m)?")

def run(path):
    hp = dict(BASE); deaths = []; hits = 0; dmg = 0.0; zero = 0; attackers = {}; life_start = None; first_t = last_t = None
    dists = []
    dead_until = -1.0
    raid_end = None
    kills = 0
    for line in open(path, encoding="utf-8", errors="replace"):
        if "RAID SUMMARY" in line or "SUMMARY (raid end)" in line:
            m = re.match(r"t=([\d.]+)", line)
            if m: raid_end = float(m.group(1))
        if "[Kill] player killed" in line: kills += 1
        m = hit_re.match(line)
        if not m: continue
        t, who, part, d, dist = float(m.group(1)), m.group(2), m.group(3), float(m.group(4)), m.group(5)
        hits += 1
        if first_t is None: first_t = t
        last_t = t
        if life_start is None: life_start = t
        if d <= 0: zero += 1; continue
        dmg += d
        if dist: dists.append(int(dist))
        attackers[who] = attackers.get(who, 0) + d
        if t < dead_until: continue  # rest of the burst that already killed him
        if part not in hp: part = "Chest"
        if hp[part] > 0:
            hp[part] -= d
        else:
            alive = [p for p in hp if hp[p] > 0]
            for p in alive: hp[p] -= d * SPREAD.get(part, 1.0) / len(alive)
        if hp["Head"] <= 0 or hp["Chest"] <= 0:
            deaths.append((t, who, part))
            hp = dict(BASE)
            dead_until = t + DEAD_WINDOW
    return dict(hits=hits, zero=zero, dmg=dmg, deaths=deaths, attackers=len(attackers), first=first_t, last=last_t,
                end=raid_end, dists=dists, kills=kills)

for path in sys.argv[1:]:
    r = run(path)
    name = path.split("/")[-1][:36]
    if r["hits"] == 0:
        print(f"{name}: no player hits"); continue
    mins = (r["end"] or r["last"]) / 60
    near = sum(1 for x in r["dists"] if x < 10); mid = sum(1 for x in r["dists"] if 10 <= x < 30); far = len(r["dists"]) - near - mid
    print(f"{name}: raid {mins:.1f}min | hits {r['hits']} (no dmg/armor {r['zero']}, damaging {r['hits']-r['zero']}) | dmg {r['dmg']:.0f} = {r['dmg']/440:.1f}x base HP | "
          f"would-die {len(r['deaths'])}x | per 10min {len(r['deaths'])/mins*10:.1f} | dist <10m {near} / 10-30 {mid} / 30+ {far} | shooters {r['attackers']} | player kills {r['kills']} per would-die {r['kills']/max(1,len(r['deaths'])):.1f}")
    for t, who, part in r["deaths"]:
        print(f"    t={t:.0f} killed by {who} ({part})")
