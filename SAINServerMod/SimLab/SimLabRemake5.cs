using System.Text;
using SAIN.Preset.Shared.SimLab;
using static SAINServerMod.Web.Services.WebText;

namespace SAINServerMod.SimLab;

/// <summary>
/// zzap remake5 analysis (user 2026-10-11: "I can't watch the hour-long sim - log so that you can check and fix it yourself").
/// Everything here reads counters the game writes in sim raids, grouped by prefix:
///   faction.{group}.spawned / .layer.{sain|orbit|vanillaLayer|vanilla} / .decision.{decision} / .death.diamond.{state}[.{why}]
///   sim.boss.{called|seen|notSeen|waveMissing|callFailed}, sim.faction.called.{role}
///   jump.{kind}.made / jump.{kind}.hit.{air|landed}.{fast|slow}
///   sound.*  (phase 2), vuln.*  (phase 3), quiet.* / firstContact.* (phase 4)
/// Each section is plain numbers + a one-line reading; findings carry a "how to fix" line for Claude.
/// </summary>
public static class SimLabRemake5
{
    private static int Get(SimBeat b, string key)
    {
        return b.Counters != null && b.Counters.TryGetValue(key, out int v) ? v : 0;
    }

    private static int Sum(SimBeat b, string prefix)
    {
        return b.Counters?.Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal)).Sum(kv => kv.Value) ?? 0;
    }

    private static IEnumerable<KeyValuePair<string, int>> With(SimBeat b, string prefix)
    {
        return b.Counters?.Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal)) ?? [];
    }

    private static string Pct(int part, int total)
    {
        return total > 0 ? $"{100f * part / total:0}%" : "-";
    }

    public static List<string> Factions(SimBeat b)
    {
        return (b.Counters?.Keys ?? Enumerable.Empty<string>())
            .Where(k => k.StartsWith("faction.", StringComparison.Ordinal))
            .Select(k => k.Split('.')[1])
            .Distinct()
            .OrderBy(f => f == "PMC" ? 0 : 1)
            .ThenBy(f => f)
            .ToList();
    }

    // ------------------------------------------------------------------ markdown

    public static void Append(StringBuilder sb, SimMapRecord m)
    {
        var b = m.Last;
        if (b == null)
        {
            return;
        }
        var raid = m.Raid;

        sb.AppendLine();
        sb.AppendLine("### remake5 교차 검증 — zzap 값으로 돌았나");
        if (raid == null || !raid.ZzapBehaviour)
        {
            sb.AppendLine("- **꺼짐**: 이 판은 시뮬 프리셋의 테스트 스위치(다이아몬드 테스트 모드·전원 GigaChad 문 전술 등) 그대로. zzap 사용자와 다를 수 있음.");
        }
        else
        {
            sb.AppendLine($"- 켜짐. 클라가 바꾼 값 {b.ZzapOverrides?.Count ?? 0}개: {string.Join(", ", b.ZzapOverrides ?? [])}");
            sb.AppendLine($"- 시뮬 프리셋이 zzap과 다른데 교차 검증이 안 덮는 곳: {(string.IsNullOrEmpty(raid.PresetDiffUncovered) ? "없음" : raid.PresetDiffUncovered)}");
        }

        var factions = Factions(b);
        if (factions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### 팩션별 (역할 그룹) — 레이어는 2초 표본, 판단은 SAIN 전투 레이어 표본");
            sb.AppendLine("| 팩션 | 나옴 | 킬/사망 | SAIN 레이어 | ORBIT | SAIN봇인데 바닐라 레이어 | SAIN 아님(바닐라 봇) | 사망 시 다이아몬드 상태 | 많이 한 전투 판단 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (string f in factions)
            {
                int samples = Sum(b, $"faction.{f}.layer.");
                string kd = b.ByFaction != null && b.ByFaction.TryGetValue(f, out var row) ? $"{row.Kills}/{row.Deaths}" : "-";
                var diamond = With(b, $"faction.{f}.death.diamond.").OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key.Substring($"faction.{f}.death.diamond.".Length)} {kv.Value}");
                int decisions = Sum(b, $"faction.{f}.decision.");
                var top = With(b, $"faction.{f}.decision.").OrderByDescending(kv => kv.Value).Take(4)
                    .Select(kv => $"{kv.Key.Substring($"faction.{f}.decision.".Length)} {Pct(kv.Value, decisions)}");
                sb.AppendLine($"| {f} | {Get(b, $"faction.{f}.spawned")} | {kd} | {Pct(Get(b, $"faction.{f}.layer.sain"), samples)} | {Pct(Get(b, $"faction.{f}.layer.orbit"), samples)} | "
                    + $"{Pct(Get(b, $"faction.{f}.layer.vanillaLayer"), samples)} | {Pct(Get(b, $"faction.{f}.layer.vanilla"), samples)} | {string.Join(", ", diamond)} | {string.Join(", ", top)} |");
            }
            var vanillaKills = b.ByFaction?.Where(kv => kv.Key.EndsWith("(vanilla)", StringComparison.Ordinal)).Select(kv => $"{kv.Key} 킬 {kv.Value.Kills}") ?? [];
            if (vanillaKills.Any())
            {
                sb.AppendLine("- SAIN 아닌 봇의 킬: " + string.Join(", ", vanillaKills));
            }
            sb.AppendLine("- 읽는 법: 'SAIN 아님'이 높으면 그 팩션은 SAIN이 안 붙음(제외 설정·다른 모드). 다이아몬드 `off.notPmc`가 보이면 10/10 'PMC만' 버그 재발. 보스·Goons 판단 분포가 그 보스의 패턴.");
        }

        if (raid != null && (!string.IsNullOrEmpty(raid.Boss) || Sum(b, "sim.boss.") > 0 || !string.IsNullOrEmpty(raid.FactionRoles)))
        {
            sb.AppendLine();
            sb.AppendLine("### 보스·팩션 분대 호출");
            sb.AppendLine($"- 보스: {(string.IsNullOrEmpty(raid.Boss) ? "없음" : $"{raid.Boss} + [{raid.BossEscorts}] (템플릿 {raid.BossSource})")} — 호출 {Get(b, "sim.boss.called")}, 등장 {Get(b, "sim.boss.seen")}, 90초 안 미등장 {Get(b, "sim.boss.notSeen")}, 웨이브 못 찾음 {Get(b, "sim.boss.waveMissing")}, 호출 실패 {Get(b, "sim.boss.callFailed")}");
            sb.AppendLine($"- 팩션 분대: [{raid.FactionRoles}] 비율 {raid.FactionShare:0.00}, 호출 " + string.Join(", ", With(b, "sim.faction.called.").Select(kv => $"{kv.Key.Substring(19)} {kv.Value}")) + $", 실패 {Get(b, "sim.faction.callFailed")}");
            if (!string.IsNullOrEmpty(raid.KeptModdedWaves))
            {
                sb.AppendLine($"- 남겨 둔 모드 팩션 웨이브: {raid.KeptModdedWaves}");
            }
        }

        var jumpKinds = (b.Counters?.Keys ?? Enumerable.Empty<string>()).Where(k => k.StartsWith("jump.", StringComparison.Ordinal) && k.EndsWith(".made", StringComparison.Ordinal))
            .Select(k => k.Split('.')[1]).Distinct().ToList();
        if (jumpKinds.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### 점프 후 2.5초 안 첫 명중 (점프 1회당 첫 명중만, fast = 1.2초 안)");
            foreach (string k in jumpKinds)
            {
                int made = Get(b, $"jump.{k}.made");
                int hit = Sum(b, $"jump.{k}.hit.");
                sb.AppendLine($"- {k}: 점프 {made}, 명중 {hit} ({Pct(hit, made)}) — 공중 {Sum(b, $"jump.{k}.hit.air.")}, 착지 후 {Sum(b, $"jump.{k}.hit.landed.")}, 빠름 {Get(b, $"jump.{k}.hit.air.fast") + Get(b, $"jump.{k}.hit.landed.fast")}");
            }
        }

        AppendGroup(sb, b, "sound.", "### 소리 반응 (2단계) — 들은 소리 → 반응",
            "gun.near=100m 안 수색, gun.mid=100~250m 고개만, gun.far=250m 밖 무시, gun.shotAtMe=나를 쏜 사람이라 거리 무시하고 반응, gun.chase=추격 성격, floor.*=층 차이로 줄어든 소리, bait.ignoredBotFake=저사양: 봇 미끼 무시, watch.gun=전투 후 정리가 총소리를 들음");
        AppendGroup(sb, b, "vuln.", "### 취약 순간 반응 (3단계) — 장전·치료·수술·수류탄·루팅 소리",
            "heard.{행동}=그 소리를 들음, reach.{행동}.yes/no=남은 시간 안에 도착 가능?, rush.{행동}.start/arrivedBusy/arrivedLate/died/killed=돌격 결과, grenade.backOff=수류탄 핀 소리에 물러남, loot.sneak=루팅 소리에 조용히 접근");
        AppendGroup(sb, b, "quiet.", "### 발소리 관리 (4단계) — 아는 적에게 다가갈 때 (2초 표본)",
            "move.walk/crouch/sprint=접근 중 이동 방식, timeout=20초 넘어 평소 이동으로 복귀, holdOnHear=다가오는 소리에 멈춤, skip.{이유}=안 한 이유");
        AppendGroup(sb, b, "firstContact.", "### 먼저 들은 쪽이 이겼나 (사망 때 기준)",
            "killer.heardFirst/sawFirst = 죽인 쪽이 상대를 소리로 먼저 알았나 / 먼저 봤나, victim.* = 죽은 쪽 기준. killer.heardFirst 비율이 victim보다 높으면 소리로 먼저 아는 게 이득");
    }

    private static void AppendGroup(StringBuilder sb, SimBeat b, string prefix, string title, string legend)
    {
        var rows = With(b, prefix).OrderBy(kv => kv.Key).ToList();
        if (rows.Count == 0)
        {
            return;
        }
        sb.AppendLine();
        sb.AppendLine(title);
        sb.AppendLine("- " + string.Join(", ", rows.Select(kv => $"{kv.Key.Substring(prefix.Length)}={kv.Value}")));
        sb.AppendLine("- 뜻: " + legend);
    }

    // ------------------------------------------------------------------ findings

    public static void AddFindings(SimMapRecord m, List<SimFinding> list)
    {
        var b = m.Last;
        var raid = m.Raid;
        if (b == null)
        {
            return;
        }
        float played = SimLabAnalyzer.PlayedMinutes(m);

        if (raid?.ZzapBehaviour == true && (b.ZzapOverrides == null || b.ZzapOverrides.Count == 0))
        {
            list.Add(new(SimLabAnalyzer.WARN, T("zzap 교차 검증이 적용 안 됨", "zzap cross-check not applied"),
                T("서버는 켜라고 했는데 클라 보고에 바꾼 값 목록이 없음. 구버전 SAIN.dll이거나 BeginRaid 전에 설정이 다시 로드됨.",
                  "The server asked for it but the client report lists no changed values. Old SAIN.dll, or settings reloaded after BeginRaid."))
            {
                Fix = "SimLabRunner.BeginRaid의 SimLabZzapCheck.Apply 호출·로그 '[SimLab] zzap cross-check' 확인. 프리셋 재로드(SAINPresetClass) 시점이 레이드 시작 뒤면 Apply가 덮임 → 재로드 이벤트에서 다시 Apply.",
            });
        }
        if (raid?.ZzapBehaviour == true && !string.IsNullOrEmpty(raid.PresetDiffUncovered))
        {
            list.Add(new(SimLabAnalyzer.WARN, T("시뮬 프리셋이 zzap과 다른 곳 (교차 검증 범위 밖)", "Sim preset differs from zzap outside the cross-check"),
                raid.PresetDiffUncovered)
            {
                Fix = "SAINServerMod/Presets의 TEST json에서 그 키를 zzap.json과 맞추거나, 행동 값이 아니면(로그용) SimLabFactions.IgnoredWords에 추가. 행동 섹션이면 SimLabZzapCheck.Apply에 섹션 추가.",
            });
        }

        foreach (string f in Factions(b))
        {
            int samples = Sum(b, $"faction.{f}.layer.");
            if (samples < 30 || f is "Scav" or "Other")
            {
                continue;
            }
            int vanilla = Get(b, $"faction.{f}.layer.vanilla");
            if (vanilla * 2 > samples)
            {
                list.Add(new(SimLabAnalyzer.HIGH, T($"{f} 팩션이 SAIN을 안 씀", $"{f} faction is not on SAIN"),
                    T($"표본 {samples} 중 {vanilla}({Pct(vanilla, samples)})가 SAIN 컴포넌트 없는 바닐라 봇.", $"{vanilla} of {samples} samples ({Pct(vanilla, samples)}) were vanilla bots without SAIN."))
                {
                    Fix = $"SAINEnableClass.ShallExludeByWildSpawnType / 프리셋 VanillaBots 설정 / BotSpawnController 엄격 제외 목록에 {f} 역할이 걸리는지, 다른 모드(블디 SainBrainFix 등)가 레이어를 바꾸는지 확인.",
                });
            }
            int notPmc = With(b, $"faction.{f}.death.diamond.").Where(kv => kv.Key.Contains("notPmc", StringComparison.Ordinal)).Sum(kv => kv.Value);
            if (notPmc > 0 && f != "PMC")
            {
                list.Add(new(SimLabAnalyzer.HIGH, T($"{f}: 'PMC만' 근접전 버그 재발 (diamond off notPmc)", $"{f}: 'PMC only' close-combat bug is back (diamond off notPmc)"),
                    T($"사망 시 다이아몬드 상태 notPmc {notPmc}회.", $"diamond notPmc at death {notPmc} times."))
                {
                    Fix = "PmcOnly 검사가 IsPMC로 돌아간 곳 찾기: grep 'PmcOnly' → 전부 Bot.Info.Profile.SkippedByPmcOnly 사용해야 함(8a32f65).",
                });
            }
        }

        if (Get(b, "sim.boss.notSeen") > 0 || Get(b, "sim.boss.waveMissing") > 0 || Get(b, "sim.boss.callFailed") > 0)
        {
            list.Add(new(SimLabAnalyzer.WARN, T("보스가 안 나옴", "Boss did not spawn"),
                T($"보스 {raid?.Boss} (템플릿 {raid?.BossSource}, 구역 {raid?.ZonesA}/{raid?.ZonesB}): 미등장 {Get(b, "sim.boss.notSeen")}, 웨이브 없음 {Get(b, "sim.boss.waveMissing")}, 호출 실패 {Get(b, "sim.boss.callFailed")}.",
                  $"Boss {raid?.Boss} (template {raid?.BossSource}): not seen {Get(b, "sim.boss.notSeen")}, wave missing {Get(b, "sim.boss.waveMissing")}, call failed {Get(b, "sim.boss.callFailed")}."))
            {
                Fix = "LogOutput '[SimLab] boss called' 뒤 BotBossSpawn 경고·예외 확인. 구역 문제면 SimLabFactions.AddBoss의 zone, 생성기 지연(20초 재시도)이면 보스만 ABPS 없이 직접 BotSpawner.SpawnBotsInZone 검토. ABPS 클라 BossProgressive 패치가 확률을 0으로 만드는지도.",
            });
        }
        foreach (var kv in With(b, "sim.faction.called."))
        {
            string role = kv.Key.Substring("sim.faction.called.".Length);
            string group = role == "exUsec" ? "Rogue" : role == "pmcBot" ? "Raider" : role;
            if (kv.Value >= 2 && Get(b, $"faction.{group}.spawned") == 0)
            {
                list.Add(new(SimLabAnalyzer.WARN, T($"팩션 분대({role})를 불렀지만 안 나옴", $"Faction squad ({role}) called but never spawned"),
                    T($"호출 {kv.Value}회, 등장 0.", $"{kv.Value} calls, 0 spawned."))
                {
                    Fix = "LogOutput '[SimLab] faction squad' 뒤 BotBossSpawn 경고 확인. 맵에 그 역할 보스 스폰 지점(Categories Boss)이 시뮬 구역에 있는지, 다른 모드가 그 역할을 막는지.",
                });
            }
        }

        int jsMade = Get(b, "jump.jumpShot.made");
        int jsHit = Sum(b, "jump.jumpShot.hit.");
        if (jsMade >= 10 && jsHit * 10 < jsMade)
        {
            list.Add(new(SimLabAnalyzer.WARN, T("점프샷 명중률 낮음", "Low jump-shot hit rate"),
                T($"점프샷 {jsMade}회 중 2.5초 안 명중 {jsHit} ({Pct(jsHit, jsMade)}).", $"{jsHit} hits within 2.5 s out of {jsMade} jump shots."))
            {
                Fix = "CornerChase.TickJumpShot: 착지 plant 시간(0.3~0.45초)·이륙 거리, SAINMoverClass.AirShotAllowed 확률. 일지 chase.jumpShot.landedPlant 대비 명중 시각 확인.",
            });
        }

        // phase 2: shot at from far away and still nothing
        int ignoredSniper = b.OddityCounts != null && b.OddityCounts.TryGetValue("ignoredSniper", out int igs) ? igs : 0;
        if (ignoredSniper > 0)
        {
            list.Add(new(SimLabAnalyzer.WARN, T("멀리서 쏘는 적에게 반응 없음", "No reaction to a far shooter"),
                T($"{ignoredSniper}회 — 맞는 중인데 10초 동안 쏜 적을 목표로 안 잡음.", $"{ignoredSniper} times - shot at, shooter not targeted for 10 s."))
            {
                Fix = "HearingSensor.ReactToHeardSound의 거리 단계(gun.mid/far)가 '나를 쏜 사람' 예외(ShotAtMeRecently)를 놓친 경우. OnBulletFlyBy 경로·Memory.SetUnderFire 확인.",
            });
        }
        int floorCut = Get(b, "sound.floor.cut");
        int floorApplied = Get(b, "sound.floor.applied");
        if (floorApplied >= 50 && floorCut * 2 > floorApplied)
        {
            list.Add(new(SimLabAnalyzer.INFO, T("층 차이로 못 들은 소리가 많음", "Many sounds lost to floor difference"),
                T($"층 감쇠 {floorApplied}회 중 그 때문에 못 들음 {floorCut}.", $"{floorCut} of {floorApplied} floor-attenuated sounds were lost."))
            {
                Fix = "HearingAnalysis 층 감쇠 계수(FLOOR_*)를 낮추거나 높이 기준을 올림.",
            });
        }

        // phase 3: rush funnel
        int rushStart = With(b, "vuln.rush.").Where(kv => kv.Key.EndsWith(".start", StringComparison.Ordinal)).Sum(kv => kv.Value);
        int rushDied = With(b, "vuln.rush.").Where(kv => kv.Key.EndsWith(".died", StringComparison.Ordinal)).Sum(kv => kv.Value);
        if (rushStart >= 8 && rushDied * 2 > rushStart)
        {
            list.Add(new(SimLabAnalyzer.WARN, T("취약 순간 돌격 중 사망이 많음", "Many deaths while rushing a busy enemy"),
                T($"돌격 {rushStart}회 중 사망 {rushDied}.", $"{rushDied} deaths in {rushStart} rushes."))
            {
                Fix = "VulnerableRush(소리 종류별 남은 시간) 도착 가능 판정이 너무 후함 → 속도 계수·여유 시간·엄폐 경로 조건 강화. 일지 [Vuln] 줄의 거리·행동 확인.",
            });
        }

        // phase 4: footstep discipline making bots stall
        int quietTimeout = Get(b, "quiet.timeout");
        int quietStart = Get(b, "quiet.start");
        if (quietStart >= 10 && quietTimeout * 3 > quietStart)
        {
            list.Add(new(SimLabAnalyzer.WARN, T("조용한 접근이 자주 시간 초과", "Quiet approach often times out"),
                T($"조용히 접근 {quietStart}회 중 20초 초과 {quietTimeout}.", $"{quietTimeout} of {quietStart} quiet approaches timed out."))
            {
                Fix = "FootstepDiscipline: 걷기 시작 거리(30m)를 줄이거나 숙여 걷기 구간을 짧게. stalledMove 이상 행동과 같은 봇인지 일지 [Quiet] 줄로 확인.",
            });
        }
        _ = played;
    }

    /// <summary>Roles that belong to this sim raid (PMC sides, factions, boss + followers, kept modded) - the rest is "non-sim".</summary>
    public static HashSet<string> ExpectedRoles(SimMapRecord m)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pmcUSEC", "pmcBEAR", m.Raid?.SideA ?? "", m.Raid?.SideB ?? "" };
        foreach (string r in SimLabFactions.Split(m.Raid?.ExpectedRoles))
        {
            set.Add(r);
        }
        return set;
    }
}
