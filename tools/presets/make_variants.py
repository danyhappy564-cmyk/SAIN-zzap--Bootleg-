#!/usr/bin/env python3
"""
zzap fork: builds the difficulty / low-spec variants of the bundled "zzap" preset.

    python3 tools/presets/make_variants.py

Reads SAINServerMod/Presets/zzap.json (TwitchPlayers hardcore aim/shoot values + zzap tactics) and writes
  zzap 쉬움.json            - SAIN "easy" tuning on top of zzap (same tactics, much weaker aim/sight/hearing)
  zzap 보통.json            - SAIN "normal"-like tuning on top of zzap
  zzap 저사양 [테스트].json  - zzap 보통 + performance settings (performance mode, AI-vs-AI limits)
The tuning mirrors SAINServerMod/Extensions/PresetTunerExtensions.cs (ApplyEasy / ApplyNormal) but scales zzap's own
per-bot values (zzap fires 3x faster than SAIN defaults, aims in 0.3s), so easy/normal stay in the same order relative
to zzap. Re-run after changing zzap.json so the variants keep its tactics.
"""
import copy
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PRESETS = os.path.join(ROOT, "SAINServerMod", "Presets")

PMC_LIKE = {"pmcUSEC", "pmcBEAR", "exUsec", "pmcBot", "arenaFighter", "arenaFighterEvent"}

LEVELS = {
    "easy": dict(
        base="easy", recoil=3.0, scatter=2.0, precision=0.33, accuracy=2.0, visible=0.5, gain=0.33, hearing=0.4,
        modifier=0.5, angle=120.0, firerate=0.3, burst=0.3, aim_min=2.0, upgrade_min=0.4, strafe_max=0.6, mistakes=2.0,
    ),
    "normal": dict(
        base="lesshard", recoil=1.6, scatter=1.0, precision=0.75, accuracy=1.0, visible=1.0, gain=0.75, hearing=0.75,
        modifier=0.85, angle=150.0, firerate=0.6, burst=0.6, aim_min=1.0, upgrade_min=0.3, strafe_max=0.8, mistakes=1.5,
    ),
}


# zzap (2026-10-02, user: "fit each preset to the fixes"): the zzap tactics stay the same KIND of behavior in every preset,
# but how often the tricks fire scales with the difficulty. zzap itself keeps the code defaults (no section written).
TACTICS = {
    "easy": {
        "DoorTactics": dict(ChanceMultiplier=0.5, GigaChadFakeTrickChance=15.0, ChadFakeTrickChance=8.0,
                            GigaChadTrapFakeHealChance=10.0, RoomClearGrenadeChance=30.0, RunByChance=25.0),
        "SquadCombat": dict(SquadStorm=False, CrossfireBackDoorPush=False, TradeWindow=6.0),
        "CloseCombat": dict(ThreatTargetKeepTime=3.0, CornerChasePrefireChance=25.0, CornerChaseJumpChance=10.0,
                            CornerJiggleChance=20.0, LeanSpamChance=25.0, GrenadeMaxInfoAge=12.0,
                            GrenadeMaxThrowDistance=30.0, DiamondStepPlant=False),
        "Reposition": dict(BaitPeekChance=15.0, FakeReloadChance=10.0, RelocateChance=45.0),
    },
    "normal": {
        "DoorTactics": dict(ChanceMultiplier=0.75, GigaChadFakeTrickChance=30.0, ChadFakeTrickChance=15.0,
                            GigaChadTrapFakeHealChance=20.0, RoomClearGrenadeChance=45.0, RunByChance=35.0),
        "SquadCombat": dict(SquadStormWeakness=0.6, TradeWindow=8.0),
        "CloseCombat": dict(ThreatTargetKeepTime=2.5, CornerChasePrefireChance=45.0, CornerChaseJumpChance=20.0,
                            CornerJiggleChance=35.0, LeanSpamChance=45.0, GrenadeMaxInfoAge=16.0,
                            GrenadeMaxThrowDistance=38.0, DiamondStepPlantMinDistance=12.0),
        "Reposition": dict(BaitPeekChance=25.0, FakeReloadChance=20.0, RelocateChance=60.0),
    },
}


def r2(x):
    return round(float(x), 2)


def tune(src, level):
    t = LEVELS[level]
    d = copy.deepcopy(src)
    g = d["GlobalSettings"]
    g["Shoot"]["BOT_RECOIL_COEF"] = t["recoil"]
    diff = g["Difficulty"]
    diff["ScatteringCoef"] = t["scatter"]
    diff["PRECISION_SPEED_COEF"] = t["precision"]
    diff["ACCURACY_SPEED_COEF"] = t["accuracy"]
    diff["VisibleDistCoef"] = t["visible"]
    diff["GainSightCoef"] = t["gain"]
    diff["HearingDistanceCoef"] = t["hearing"]
    g["Aiming"]["FasterCQBReactionsGlobal"] = False
    cc = g["General"].setdefault("CloseCombat", {})
    cc["UtilityMistakeMultiplier"] = t["mistakes"]
    for section, values in TACTICS[level].items():
        g["General"].setdefault(section, {}).update(values)
    for name, bot in d["BotSettings"].items():
        bot["DifficultyModifier"] = r2(min(2.0, max(0.01, bot["DifficultyModifier"] * t["modifier"])))
        for s in bot["Settings"].values():
            s["Core"]["VisibleAngle"] = min(s["Core"].get("VisibleAngle", 180.0), t["angle"])
            s["Shoot"]["FireratMulti"] = r2(s["Shoot"]["FireratMulti"] * t["firerate"])
            s["Shoot"]["BurstMulti"] = r2(s["Shoot"]["BurstMulti"] * t["burst"])
            aim = s["Aiming"]
            aim["MAX_AIM_TIME"] = max(aim.get("MAX_AIM_TIME", 0.0), t["aim_min"])
            aim["MAX_AIMING_UPGRADE_BY_TIME"] = max(aim.get("MAX_AIMING_UPGRADE_BY_TIME", 0.0), t["upgrade_min"])
            if "STRAFE_SPEED" in s["Move"]:
                s["Move"]["STRAFE_SPEED"] = min(s["Move"]["STRAFE_SPEED"], t["strafe_max"])
    d["Info"]["BaseSAINDifficulty"] = t["base"]
    return d


def lowspec(src_normal):
    d = copy.deepcopy(src_normal)
    gen = d["GlobalSettings"]["General"]
    gen["Performance"]["PerformanceMode"] = True
    ai = gen["AILimit"]
    ai["LimitAIvsAIGlobal"] = True
    ai["LimitAIvsAIVision"] = True
    ai["LimitAIvsAIHearing"] = True
    # Fewer raycasts per "step to where the angle opens" search (holding bots check every 1.5s).
    gen.setdefault("CloseCombat", {})["HoldStepToOpenAngleMax"] = 1.0
    gen.setdefault("FreezeAmbush", {})["PeekSpotMaxStep"] = 1.5
    return d


def write(d, name, description):
    d["Info"]["Name"] = name
    d["Info"]["Description"] = description
    path = os.path.join(PRESETS, name + ".json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(d, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print("wrote", path)


def main():
    with open(os.path.join(PRESETS, "zzap.json"), encoding="utf-8") as f:
        zzap = json.load(f)
    tactics = "전술 종류(문 피킹·페이크, 다이아몬드 스텝, 코너 지키기 등)와 ORBIT 권장 설정은 zzap과 같고, 트릭이 나오는 빈도만 난이도에 맞게 낮춤."
    easy = tune(zzap, "easy")
    write(easy, "zzap 쉬움",
          "zzap 난이도 1/3 (쉬움). " + tactics + " 원본 SAIN '쉬움' 보정: 반동 크고 탄이 많이 퍼짐, 시야 거리 절반, 늦게 발견, "
          "청각 약함, 시야각 120°, 연사 느림, 조준 최소 2초, 사람 같은 실수 2배. 문 전술 ×0.5, 페이크·미끼·코너 흔들기 크게 줄임, 분대 일제 돌격 없음, 수류탄 30m까지, 스텝 중 쏠 때 멈추기 없음.")
    normal = tune(zzap, "normal")
    write(normal, "zzap 보통",
          "zzap 난이도 2/3 (보통). " + tactics + " 원본 SAIN '보통' 보정: 반동·탄 퍼짐 중간, 시야 거리 보통, 발견·청각 약간 약함, "
          "시야각 150°, 연사 중간, 조준 최소 1초, 사람 같은 실수 1.5배. 문 전술 ×0.75, 페이크·미끼·코너 흔들기 조금 줄임, 수류탄 38m까지, 스텝 중 쏠 때 멈추기는 12m 이상에서만.")
    write(lowspec(normal), "zzap 저사양 [테스트]",
          "[테스트] 저사양 PC용. zzap 보통 + 성능 설정(성능 모드 켬, 멀리 있는 봇끼리의 업데이트·시야·청각 제한 켬, 조준선 자리 찾기 범위 축소). "
          "프레임은 조금 나아질 수 있지만, 멀리서 벌어지는 봇끼리의 싸움은 덜 정교해질 수 있음. 의도한 전투 경험과 다를 수 있는 시험용.")


if __name__ == "__main__":
    main()
