#!/usr/bin/env python3
"""
zzap fork: builds the difficulty variants of the bundled "zzap" preset.

    python3 tools/presets/make_variants.py

Reads SAINServerMod/Presets/zzap.json (TwitchPlayers hardcore aim/shoot values + zzap tactics) and writes
  zzap 쉬움.json            - SAIN "easy" tuning on top of zzap (same tactics, much weaker aim/sight/hearing)
  zzap 보통.json            - halfway between easy and zzap
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
    # normal (2026-10-18, user picked "B"): a true middle between easy and zzap. It used to be SAIN's own "normal"
    # (recoil 1.6 = 16x zzap, aim 1.0s, sight 1.0), which made the gap to zzap much bigger than the gap to easy.
    "normal": dict(
        base="lesshard", recoil=0.8, scatter=1.0, precision=0.85, accuracy=1.0, visible=1.4, gain=0.85, hearing=0.85,
        modifier=0.9, angle=160.0, firerate=0.75, burst=0.75, aim_min=0.6, upgrade_min=0.3, strafe_max=0.9, mistakes=1.25,
    ),
}


# zzap (2026-10-02, user: "fit each preset to the fixes"): the zzap tactics stay the same KIND of behavior in every preset,
# but how often the tricks fire scales with the difficulty. zzap itself keeps the code defaults (no section written).
TACTICS = {
    "easy": {
        "DoorTactics": dict(ChanceMultiplier=0.5, GigaChadFakeTrickChance=15.0, ChadFakeTrickChance=8.0,
                            GigaChadTrapFakeHealChance=10.0, RoomClearGrenadeChance=30.0, RunByChance=25.0,
                            # bot sprint rams (SAIN's own): clear / flee / rush / search / other moves, zzap 50/75/65/50/35
                            DoorModRamChance=25.0, DoorModRamFleeChance=40.0, DoorModRamRushChance=35.0,
                            DoorModRamSearchChance=25.0, DoorModRamMoveChance=15.0),
        "SquadCombat": dict(SquadStorm=False, CrossfireBackDoorPush=False, TradeWindow=6.0, ManeuverChance=30.0),
        "CloseCombat": dict(ThreatTargetKeepTime=3.0, CornerChasePrefireChance=25.0, CornerChaseJumpChance=10.0,
                            CornerJiggleChance=20.0, LeanSpamChance=25.0, GrenadeMaxInfoAge=12.0,
                            GrenadeMaxThrowDistance=30.0, DiamondStepPlant=False),
        "Reposition": dict(BaitPeekChance=15.0, FakeReloadChance=10.0, RelocateChance=45.0),
    },
    "normal": {
        "DoorTactics": dict(ChanceMultiplier=0.75, GigaChadFakeTrickChance=30.0, ChadFakeTrickChance=15.0,
                            GigaChadTrapFakeHealChance=20.0, RoomClearGrenadeChance=45.0, RunByChance=35.0,
                            DoorModRamChance=35.0, DoorModRamFleeChance=55.0, DoorModRamRushChance=50.0,
                            DoorModRamSearchChance=35.0, DoorModRamMoveChance=25.0),
        "SquadCombat": dict(SquadStormWeakness=0.6, TradeWindow=8.0, ManeuverChance=45.0),
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
    # Bot-vs-bot footstep hearing: every moving bot's steps go through every bot's hearing - off for low-spec.
    d["GlobalSettings"].setdefault("Hearing", {})["BotsHearBotFootsteps"] = False
    # Bots don't bait each other with reload / heal / grenade-pin sounds (user 2026-10-11: "low-spec users are better off
    # without bots fishing each other - fewer frame drops"); a human player's sounds still count.
    d["GlobalSettings"]["Hearing"]["BotsTakeBotBait"] = False
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
          "zzap 난이도 1/3 (쉬움). " + tactics + " 원본 SAIN '쉬움' 보정: 반동 크고 탄이 많이 퍼짐, 시야 거리 SAIN 기본의 절반(zzap의 1/4), 늦게 발견, "
          "청각 약함, 시야각 120°, 연사 느림, 조준 최소 2초, 사람 같은 실수 2배. 문 전술 ×0.5(봇 문 박치기: 방 진입 25%·도망 40%·돌격 35%·수색 25%·그 밖의 전투 이동 15%), 페이크·미끼·코너 흔들기 크게 줄임, 분대 일제 돌격 없음, 수류탄 30m까지, 스텝 중 쏠 때 멈추기 없음.")
    normal = tune(zzap, "normal")
    write(normal, "zzap 보통",
          "zzap 난이도 2/3 (보통). " + tactics + " 쉬움과 zzap의 중간: 반동 zzap의 8배(쉬움 30배), 시야 거리 SAIN 기본의 1.4배(zzap의 70%), 발견·청각 약간 약함, "
          "시야각 160°, 연사 zzap의 75%, PMC 조준 최소 0.6초(zzap 0.3초), 사람 같은 실수 1.25배. 문 전술 ×0.75(봇 문 박치기: 방 진입 35%·도망 55%·돌격 50%·수색 35%·그 밖의 전투 이동 25%), 페이크·미끼·코너 흔들기 조금 줄임, 수류탄 38m까지, 스텝 중 쏠 때 멈추기는 12m 이상에서만.")
    # "zzap 저사양 [테스트]" is no longer shipped (user 2026-10-24): the Quick settings low-spec PC switch does it on any
    # preset, and the server removes the old file (PresetService.RetiredBundled). lowspec() stays as that switch's reference.

    # The door check is no longer an F6 preset (user 2026-10-13: "I meant a preset at the sim stage"): sim preset
    # "문 전술·박치기 점검" + the sim page's door check switch (SimLabDefaults / SimLabZzapCheck.ApplyDoorTest).


if __name__ == "__main__":
    main()
