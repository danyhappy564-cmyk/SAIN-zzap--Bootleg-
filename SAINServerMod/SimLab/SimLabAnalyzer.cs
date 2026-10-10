using System.Text;
using SAIN.Preset.Shared.SimLab;
using static SAINServerMod.Web.Services.WebText;

namespace SAINServerMod.SimLab;

public sealed record SimFinding(string Severity, string Title, string Detail)
{
    /// <summary>For Claude: how to fix / follow up (code places, what to change). Filled for high and warn findings.</summary>
    public string? Fix { get; init; }
}

/// <summary>
/// zzap fork: turns one sim map's reports into "what looks wrong" lines for the analysis page, and a whole run into one
/// markdown file to hand over for fixing (so the full journal/LogOutput only needs reading where a finding points).
/// Rules are deliberately plain thresholds; the raw numbers are always shown next to them.
/// </summary>
public static class SimLabAnalyzer
{
    public const string HIGH = "high";
    public const string WARN = "warn";
    public const string INFO = "info";
    public const string OK = "ok";

    /// <summary>zzap (2026-10-10 review: Shoreline "other 8" were all the spectator's shots): kills by the spectator, which bots
    /// can't react to - shown inside "other" so they aren't read as the bots' own fights.</summary>
    public static int SpectatorKills(SimBeat b) => Sum(b, "death.bySpectator.");

    public static string PersonalityLabel(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
        {
            return "SAIN 기본";
        }
        return string.Equals(spec.Trim(), "even", StringComparison.OrdinalIgnoreCase) ? "8가지 고르게" : spec;
    }

    /// <summary>"금지(APBS 목록 비움)" only when the bridge really emptied APBS's list for this raid.</summary>
    public static string ModItemsLabel(SimRaidInfo raid)
    {
        if (!raid.NoModItems)
        {
            return "허용";
        }
        return raid.ModItemsBlocked ? "금지 (APBS 목록 비움)" : "금지 설정이지만 적용 안 됨 (APBS 없음/못 찾음)";
    }

    private static string SpectatorPart(SimBeat b)
    {
        int n = SpectatorKills(b);
        return n > 0 ? $" — 그중 관전자 {n}" : string.Empty;
    }

    public static float PlayedMinutes(SimMapRecord m)
    {
        return (m.Last?.RaidSeconds ?? 0f) / 60f;
    }

    public static int ErrorTotal(SimMapRecord m)
    {
        return m.Last?.Errors?.Sum(e => e.Count) ?? 0;
    }

    private static int Sum(SimBeat b, string prefix)
    {
        return b.Counters?.Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal)).Sum(kv => kv.Value) ?? 0;
    }

    private static int Get(SimBeat b, string key)
    {
        return b.Counters != null && b.Counters.TryGetValue(key, out int v) ? v : 0;
    }

    public static List<SimFinding> Findings(SimMapRecord m)
    {
        return FindingsRaw(m).Select(f => f.Severity is HIGH or WARN ? f with { Fix = FixFor(f, m) } : f).ToList();
    }

    // user 2026-10-09: "when high/warn come up, the Claude summary should already say how it should be fixed, so I hand over the md
    // and you fix right away". Written for Claude (code places, Korean). Matched on the finding's title in either language.
    private static readonly (string Ko, string En, string Fix)[] _fixes =
    {
        ("중간에 끊김", "Interrupted", "사용자가 Alt+F4로 끊은 판이면 할 일 없음(사용자 메모 확인). 아니면 LogOutput 마지막 [Error]/예외 스택 → 그 코드 수정. SimLab 기록 복구(SimLabFile.RecoverUnfinished) 내용으로 끊긴 시각 확인."),
        ("보고 없음", "No reports", "SimLabRunner.BeginRaid/Setup에 못 들어감: LogOutput [SimLab] 줄, SimLab.Active(프리셋 SimLab.Enabled), /sain/sim/raid 응답(Applied) 확인."),
        ("시뮬 스폰 미적용", "Sim spawns not applied", "SimLabService.ApplyToRaid가 안 불렸거나 Armed/Running=false. 서버 로그 [SAIN SimLab] 줄, 시작 전 hello(start) 순서 확인."),
        ("시나리오 설정 문제", "Scenario setup problem", "SimLabDefaults.Scenarios의 구역 이름을 그 맵(원본 버전) SpawnPointParams.BotZoneName과 대조해 고치고 DefaultsVersion을 올려 저장된 설정도 갱신."),
        ("오류 ", "kinds of errors", "오류 목록 첫 스택의 위치를 수정. EFT.* 안쪽이면 그걸 부른 SAIN 코드에 null/상태 방어. 레이드 종료 중 오류라면 SimLogListener.Ending 처리(종료 뒤 집계 제외)가 빠진 경로 확인."),
        ("봇이 생성되지 않음", "No bots spawned", "LogOutput [SimLab] squad / 'stuck waiting' 줄과 [SimLab] ABPS found 로그 확인. ABPS 경유면 SimLabAbpsSpawn(구역 점 15m 조건, sim.abpsZone.fallback), 게임 기본이면 BotBossSpawn 지연(20초 재시도) 문제."),
        ("시뮬 외 봇이 섞임", "Non-sim bots mixed in", "RemoveOtherSpawns가 못 지운 경로(ABPS 자체 스캐브/보스, 웨이브 밖 스폰)를 찾아 SimLabService.Apply 또는 클라 StopOtherScenarios에 추가."),
        ("교전이 거의 없음", "Hardly any fighting", "분대 호출 수·봇 수·구역 줄(leash) 횟수·ORBIT 목적지를 보고: 생성 부족이면 스폰 경로, 못 만나면 시나리오 구역/Arena 반지름 조정, 페이스(MaxAliveBots/RespawnSeconds) 상향."),
        ("프레임 저하", "Low frame rate", "일지 [Perf] fps와 RAM 클리너 [mods] 줄로 원인 모드·봇 수 확인. 종료 직전 값이면 SimLabRunner의 _endRequested 뒤 FPS 제외 경로 확인."),
        ("멈춤(1초 넘는 프레임)", "Stalls", "해당 시각 LogOutput의 예외·GC·로딩 확인."),
        ("메모리 증가", "Memory growth", "맵끼리 비교. 시체(SimCorpseLimit/Delete)·디칼 해제 수치 확인, 계속 오르면 누수 후보(정적 리스트·이벤트 구독) 추적."),
        ("판단을 바꾼 직후 사망이 많음", "Many deaths right after a decision change", "death.switchedAway.<판단>.<이유> 상위를 보고, 적이 보일 때 엄폐로 빠지는 판단(VisibleEnemyUtility Cover/utilityFallBack)이면 '등 돌리는 순간' 노출 → 가까운 엄폐만 허용·뒷걸음 사격(close.walkFacingEnemy)·전환 최소 유지 시간 검토. 일지 [Death] 줄로 거리·맞은 부위 확인."),
        ("얼음 매복 중 사망", "Died while freeze-ambushing", "FreezeAction 종료 조건(enemySpotted/underFire 반응 지연) 점검."),
        ("벽을 보고 있는 시간이 많음", "Much time facing a wall", "WallStareByDecision 상위 판단의 시선 목표 확인 → SAINSteeringClass.AvoidWallStare 조건(조준 중 제외 등)·거리 조정."),
        ("스폰 위치가 정한 구역과 다름", "Spawns outside the wanted zones", "sim.abpsZone.used/fallback 비율 확인. fallback이 많으면 SimLabAbpsSpawn MIN_DISTANCE 완화나 구역 추가."),
        ("보기 전에 소리로 알아챘는지", "Heard before seen?", "notHeard.close/mid가 많음 → PlayerSoundController(BotsHearBotFootsteps)·SAIN 청각 거리/가림 계산 확인."),
        ("4m 안 적을 모름", "Enemy within 4 m unnoticed", "예시의 레이어가 ORBIT이고 t가 같으면 동시 생성 겹침 → SimLabAbpsSpawn 거리 조건. 전투 중이면 근거리(등 뒤 3m) 감지 보강 검토."),
        ("ORBIT 고스트 모드가 봇을 재움", "ORBIT Ghost Mode put bots to sleep", "OrbitGhostOff 적용 확인: 서버 로그 'ORBIT config: ghost off', ORBIT 클라 /orbit/config 재요청 여부."),
        ("ORBIT 목적지가 싸움 구역 밖", "ORBIT objectives outside the arena", "OrbitArenaZone 적용(서버 로그 'zones: arena zone') 확인, 구역 줄(ArenaLeash) 횟수 확인. 반지름이 크면 FindArena 반지름 공식 조정."),
        ("ORBIT가 봇을 탈출시키려 함", "ORBIT sends bots to extract", "OrbitNoExtract가 덮지 못한 탈출 이유(ExtractReason) 찾아 SimLabOrbitBridge.EditConfig에 키 추가."),
        ("봇이 ORBIT(맵 이동)에 오래 있음", "Bots spend long in ORBIT", "ORBIT 목적지(OrbitObjectiveSeconds)·구역 줄 확인. 싸움 구역 밖이면 OrbitArenaZone/Leash 조정."),
        ("버그 수정 항목에 나쁜 신호", "Bad signal on a bug fix", "md '기능 점검' 표에서 ❌ 행의 나쁜 신호 카운터 → 그 버그 수정 코드 경로 재확인(일지 태그 줄 시각으로 장면 찾기)."),
        ("문 전술이 중간에 끊긴", "Door tactic interrupted", "door.end.*.interrupted / door.abort 상위 이유 → DoorTacticClass 끊김 조건 점검."),
    };

    private static string? FixFor(SimFinding f, SimMapRecord m)
    {
        if (f.Title.StartsWith(T("이상 행동 자동 감지", "Automatic oddity detector"), StringComparison.Ordinal))
        {
            var kinds = m.Last?.OddityCounts?.OrderByDescending(kv => kv.Value).Take(3).Select(kv => OddityFix(kv.Key)) ?? [];
            return string.Join(" / ", kinds);
        }
        foreach (var (ko, en, fix) in _fixes)
        {
            if (f.Title.StartsWith(ko, StringComparison.Ordinal) || f.Title.Contains(en, StringComparison.OrdinalIgnoreCase))
            {
                return fix;
            }
        }
        return "원인 추적: 같은 맵의 일지(병합 md)에서 해당 시각 전후를 본다.";
    }

    private static string OddityFix(string kind)
    {
        return kind switch
        {
            "noShoot" => "noShoot: 예시 판단/이유에서 사격이 막힌 곳(SAINShootClass·Aim·CanShoot·FireLaneGuard 보류) 확인",
            "backTurned" => "backTurned: 가까운 적보다 다른 시선 목표가 이긴 것 → SAINSteeringClass 시선 우선순위",
            "hitNoReact" => "hitNoReact: 맞은 뒤 판단 전환(엄폐/반격)이 안 된 판단·이유 → EnemyDecisionClass 피격 반응",
            "flipFlop" => "flipFlop: 예시 판단 쌍 사이 전환 조건에 최소 유지 시간/히스테리시스",
            "bunched" => "bunched: 분대 간격(SquadCombat·이동 목표 겹침) 벌리기",
            "stalledMove" => "stalledMove: 이동 판단인데 목표/경로 실패 → Mover·엄폐 찾기(utilityFallBack 등) 경로 실패 처리",
            _ => kind,
        };
    }

    private static List<SimFinding> FindingsRaw(SimMapRecord m)
    {
        var list = new List<SimFinding>();
        var b = m.Last;
        float played = PlayedMinutes(m);

        if (m.Status == "interrupted")
        {
            list.Add(new(HIGH, T("중간에 끊김", "Interrupted"),
                T($"t={played:0.0}분에서 마지막 보고. 게임 강제 종료(Alt+F4)·충돌·직접 나가기 중 하나. 그 시각 전후의 LogOutput.log와 시뮬 기록 파일(BepInEx/config/SAIN-zzap/SimLab/)을 같이 보면 원인이 나옵니다.",
                  $"Last report at t={played:0.0} min. The game was closed (Alt+F4), crashed or was left by hand. LogOutput.log around that time plus the sim file (BepInEx/config/SAIN-zzap/SimLab/) show why.")));
        }
        else if (m.Status == "ended")
        {
            list.Add(new(INFO, T("시뮬 타이머가 아닌 이유로 레이드 종료", "Raid ended by something other than the sim timer"),
                T($"종료 이유: {b?.EndReason ?? "?"} (t={played:0.0}분)", $"End reason: {b?.EndReason ?? "?"} (t={played:0.0} min)")));
        }
        if (b == null)
        {
            list.Add(new(HIGH, T("보고 없음", "No reports"), T("클라이언트가 이 맵에서 보고를 한 번도 보내지 못했습니다.", "The client never reported from this map.")));
            return list;
        }

        if (m.Raid == null || !m.Raid.Applied)
        {
            list.Add(new(WARN, T("시뮬 스폰 미적용", "Sim spawns not applied"),
                T("이 레이드는 서버가 시뮬 스폰을 넣지 않은 상태로 시작됐습니다(서버가 시뮬 프리셋 신호를 못 받았거나 서버 재시작 직후). ",
                  "This raid started without the sim spawns (the server had no sim-preset signal, or had just restarted). ") + (m.Raid?.Note ?? string.Empty)));
        }
        else if (!string.IsNullOrEmpty(m.Raid.Note))
        {
            list.Add(new(WARN, T("시나리오 설정 문제", "Scenario setup problem"), m.Raid.Note));
        }

        int errors = ErrorTotal(m);
        if (errors > 0)
        {
            var top = b.Errors.OrderByDescending(e => e.Count).Take(3).Select(e => $"{e.Key} ×{e.Count}");
            list.Add(new(HIGH, T($"오류 {b.Errors.Count}종, 총 {errors}회", $"{b.Errors.Count} kinds of errors, {errors} in total"), string.Join(" / ", top)));
        }

        if (b.SimWavesActivated > 0 && b.BotsSeen == 0)
        {
            list.Add(new(HIGH, T("봇이 생성되지 않음", "No bots spawned"),
                m.Raid?.AbpsOff == true
                    ? T($"예비 분대 {b.SimWavesActivated}개를 불렀지만 봇이 하나도 안 나왔습니다 (ABPS는 꺼 둔 상태). 구역 이름({m.Raid?.ZonesA} / {m.Raid?.ZonesB})이 이 맵 버전(MapVariants 원본/변형)에 있는지, LogOutput의 `[SimLab] squad` 줄을 확인.",
                        $"{b.SimWavesActivated} reserve squads were called but no bot appeared (ABPS was off). Check the zones ({m.Raid?.ZonesA} / {m.Raid?.ZonesB}) exist in this map version and the `[SimLab] squad` lines in LogOutput.")
                    : T($"예비 분대 {b.SimWavesActivated}개를 불렀지만 봇이 하나도 안 나왔습니다. ABPS가 켜져 있으면 PMC 분대를 가로채 정한 구역({m.Raid?.ZonesA} / {m.Raid?.ZonesB})을 무시하고, 관전자·다른 PMC와 충분히 먼 자리가 없으면 말없이 건너뜀 — 시뮬 설정의 'ABPS 끄기'를 켜세요.",
                        $"{b.SimWavesActivated} reserve squads were called but no bot appeared. With ABPS on, it takes over PMC squads, ignores the zones ({m.Raid?.ZonesA} / {m.Raid?.ZonesB}) and silently skips them when no spot is far enough from the spectator and other PMCs - turn on 'ABPS off' in the sim settings.")));
        }

        var sides = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pmcUSEC", "pmcBEAR", m.Raid?.SideA ?? "", m.Raid?.SideB ?? "" };
        var others = b.SpawnedRoles?.Where(kv => !sides.Contains(kv.Key)).ToList() ?? [];
        if (others.Count > 0)
        {
            string roles = string.Join(", ", others.Select(kv => $"{kv.Key}×{kv.Value}"));
            list.Add(new(WARN, T("시뮬 외 봇이 섞임", "Non-sim bots mixed in"),
                roles + T(" — 다른 스폰 모드(ABPS 등)나 보스 스폰이 남아 있습니다. 결과 해석에 주의.", " — another spawn mod (ABPS etc.) or boss spawns are still active. Read the results with care.")));
        }

        if (played >= 8f && b.BotDeaths < 3)
        {
            list.Add(new(WARN, T("교전이 거의 없음", "Hardly any fighting"),
                T($"{played:0}분 동안 봇 사망 {b.BotDeaths}. 두 구역이 너무 멀거나 서로 못 찾음(수색 판단/경로 문제). 일지 [Decide]의 수색·이동 이유 확인.",
                  $"{b.BotDeaths} bot deaths in {played:0} min. The zones are too far apart or the sides don't find each other (search decision / path problem). Check the search/move reasons in the journal's [Decide] lines.")));
        }

        if (b.FpsMin > 0f && b.FpsMin < 20f)
        {
            list.Add(new(WARN, T("프레임 저하", "Low frame rate"),
                T($"10초 평균 최저 {b.FpsMin:0} fps (마지막 {b.Fps:0}). 같은 시각 [Perf] 줄과 살아 있는 봇 수 비교.", $"Lowest 10 s average {b.FpsMin:0} fps (last {b.Fps:0}). Compare with the [Perf] lines and alive bots at that time.")));
        }
        if (b.Stalls > 0)
        {
            list.Add(new(WARN, T("멈춤(1초 넘는 프레임)", "Stalls (frames over 1 s)"),
                T($"{b.Stalls}회. 같은 시각 LogOutput.log에 예외나 GC가 있었는지 확인.", $"{b.Stalls} times. Check LogOutput.log at those times for exceptions or GC.")));
        }
        if (m.Samples.Count >= 2)
        {
            long growth = m.Samples[^1].Mono - m.Samples[0].Mono;
            if (growth > 1500)
            {
                list.Add(new(WARN, T("메모리 증가", "Memory growth"),
                    T($"관리 메모리 +{growth} MB ({m.Samples[0].Mono} → {m.Samples[^1].Mono} MB). 레이드 중 GC가 꺼져 있어 정상일 수도 있으나 맵마다 비교 필요.",
                      $"Managed memory +{growth} MB ({m.Samples[0].Mono} → {m.Samples[^1].Mono} MB). GC is off during raids so this can be normal; compare between maps.")));
            }
        }

        int deaths = b.ByPersonality?.Values.Sum(r => r.Deaths) ?? 0;
        int switching = b.ByPersonality?.Values.Sum(r => r.DiedSwitching) ?? 0;
        if (deaths >= 6 && switching * 10 >= deaths * 3)
        {
            list.Add(new(WARN, T("판단을 바꾼 직후 사망이 많음", "Many deaths right after a decision change"),
                T($"사망 {deaths} 중 {switching}이 싸우던 중 엄폐·후퇴·수색 같은 비사격 판단으로 바꾼 지 1.5초 안 (30% 이상, 근접전 사격 판단 재선택은 제외). 카운터 death.switchedAway.*·일지 [Death] why=·decisionAge= 확인.", $"{switching} of {deaths} deaths within 1.5 s of switching away from fighting (cover/retreat/search; re-picking 'shoot' in a dogfight is excluded). See death.switchedAway.* and the journal's [Death] lines.")));
        }

        int freezeDeaths = Get(b, "death.Freeze") - Get(b, "death.bySpectator.Freeze"); // the spectator can't be reacted to
        if (freezeDeaths > 0)
        {
            list.Add(new(WARN, T("얼음 매복 중 사망", "Died while freeze-ambushing"), T($"death.Freeze={freezeDeaths} (0이어야 정상).", $"death.Freeze={freezeDeaths} (should be 0).")));
        }
        // zzap (2026-10-10 review): stuck.* summed stage + layer + freed counters (one episode counted 3-5 times) - every
        // episode in the 3-map run was freed. An episode = stage1; it is a problem only when it was not freed.
        int stuckEpisodes = Get(b, "stuck.stage1");
        int stuckFreed = Sum(b, "stuck.freed.");
        if (stuckEpisodes > stuckFreed)
        {
            list.Add(new(INFO, T("못 풀린 막힘", "Stuck not freed"), T($"막힘 {stuckEpisodes}번 중 {stuckEpisodes - stuckFreed}번이 안 풀림(그 사이 죽은 봇 포함). 일지 [Stuck] 줄의 위치가 같으면 맵 지형 문제.", $"{stuckEpisodes - stuckFreed} of {stuckEpisodes} stuck episodes not freed (bots that died meanwhile included). Same spot in [Stuck] lines = map geometry.")));
        }
        // door.reject.* are per-check "not now" answers (thousands is normal) - only real aborts count against starts.
        int doorStart = Sum(b, "door.start.");
        int doorAbort = Sum(b, "door.abort") + b.Counters?.Where(kv => kv.Key.StartsWith("door.end.", StringComparison.Ordinal) && kv.Key.EndsWith(".interrupted", StringComparison.Ordinal)).Sum(kv => kv.Value) ?? 0;
        if (doorStart >= 5 && doorAbort > doorStart)
        {
            list.Add(new(WARN, T("문 전술이 중간에 끊긴 횟수가 시작보다 많음", "More door tactics interrupted than started"), T($"시작 {doorStart} / 중단 {doorAbort}.", $"Starts {doorStart} / interrupted {doorAbort}.")));
        }
        int thrown = Sum(b, "nade.thrown");
        int nadeFail = Get(b, "nade.noArc") + Get(b, "nade.notReady");
        // zzap (2026-10-10 review): noArc is a retry count (the bot asks again every 1.5 s while the arc is blocked - mostly
        // 25-45 m under a roof), so "many" next to the throws was always true (3 maps: 53-86 vs 5-20 thrown, every judged
        // throw thrown). Only worth a line when nothing got thrown at all.
        if (nadeFail >= 10 && thrown == 0)
        {
            list.Add(new(INFO, T("수류탄을 한 번도 못 던짐", "No grenade thrown"), T($"궤적 없음·준비 안 됨 {nadeFail} (1.5초마다 다시 세는 시도 횟수). 일지 [Nade] no clear arc 줄의 거리·실내 여부 확인.", $"No arc + not ready {nadeFail} (retries every 1.5 s). See [Nade] 'no clear arc' lines for distance / indoors.")));
        }
        // spawn check
        int spawned = b.SpawnInZone + b.SpawnOffZone;
        if (spawned >= 4 && b.SpawnOffZone * 5 > spawned)
        {
            var off = b.SpawnOff?.Take(3).Select(x => $"{x.Side}:{x.Zone} ({x.X:0},{x.Z:0}, {(x.Distance >= 0 ? $"{x.Distance:0}m" : "?")})") ?? [];
            list.Add(new(WARN, T("스폰 위치가 정한 구역과 다름", "Spawns outside the wanted zones"),
                T($"{spawned}명 중 {b.SpawnOffZone}명이 정한 구역 밖에서 태어남. 예: {string.Join(", ", off)}. 구역에 보스 스폰 지점이 적거나, 다른 모드(ABPS 등)가 PMC 스폰 위치를 바꾸는지 확인.",
                  $"{b.SpawnOffZone} of {spawned} spawned outside the wanted zones. e.g. {string.Join(", ", off)}. Few boss spawn points in the zone, or another mod (ABPS...) moving PMC spawns?")));
        }

        // wall stare (screenshot 2026-10-09: bot "peeking" with its face on a flat wall)
        if (b.CombatSamples >= 50)
        {
            float share = (float)b.WallStareSamples / b.CombatSamples;
            if (share >= 0.15f)
            {
                var top = b.WallStareByDecision?.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{kv.Key} {kv.Value}") ?? [];
                list.Add(new(WARN, T("벽을 보고 있는 시간이 많음", "Much time facing a wall"),
                    T($"전투 중 {share:P0}의 시간을 1m 안 벽만 보고 있음 (그중 적 방향으로 벽 너머를 보는 것 {b.WallStareTowardEnemy}). 많은 판단: {string.Join(", ", top)}.",
                      $"{share:P0} of combat time looking at a wall within 1 m ({b.WallStareTowardEnemy} of it toward the enemy through the wall). Top decisions: {string.Join(", ", top)}.")));
            }
        }

        // hearing before sight (user: "they react only when they see him")
        int heardMove = Sum(b, "hear.firstSeen.heard.move");
        int notHeardClose = Sum(b, "hear.firstSeen.notHeard.close") + Sum(b, "hear.firstSeen.notHeard.mid");
        int firstSeen = Sum(b, "hear.firstSeen.");
        if (firstSeen >= 10)
        {
            list.Add(new(notHeardClose * 2 > firstSeen ? WARN : INFO, T("보기 전에 소리로 알아챘는지", "Heard before seen?"),
                T($"적을 처음 볼 때 {firstSeen}회 중 발소리 등으로 먼저 들은 것 {heardMove}, 40m 안인데 못 들은 것 {notHeardClose}. 못 들은 게 많으면 가까운 적 소리를 놓치는 것.",
                  $"Of {firstSeen} first sightings, {heardMove} were heard moving first, {notHeardClose} within 40 m were not heard at all.")));
        }

        // ORBIT vs SAIN time
        float layerTotal = b.LayerSeconds?.Values.Sum() ?? 0f;
        if (layerTotal > 60f)
        {
            float orbit = LayerShare(b, "orbit");
            float sainCombat = LayerShare(b, "combat");
            if (orbit > 0.6f && b.BotDeaths < 5)
            {
                list.Add(new(WARN, T("봇이 ORBIT(맵 이동)에 오래 있음", "Bots spend long in ORBIT"),
                    T($"봇 시간의 {orbit:P0}가 ORBIT, SAIN 전투는 {sainCombat:P0}. 원하는 싸움이 안 나면 ORBIT 목적지/구역 설정이 스폰 구역 밖으로 끌고 가는지 확인.",
                      $"{orbit:P0} of bot time in ORBIT, {sainCombat:P0} in SAIN combat. If the wanted fight doesn't happen, check whether ORBIT pulls bots out of the arena.")));
            }
        }

        // feature coverage (user 2026-10-09: "precisely whether ALL the features we added work")
        if (played >= 5f)
        {
            var silent = new List<string>();
            var bad = new List<string>();
            foreach (var f in SimLabFeatures.All)
            {
                if (f.Fired.Length == 0)
                {
                    continue;
                }
                var (fired, _, badCount) = SimLabFeatures.Measure(f, b);
                // zzap (user 2026-10-10: "a few blue !"): features whose 0 is expected in a sim (off in the TEST preset,
                // replaced by the utility, a fix that had nothing to repair) are not listed as "never fired".
                if (fired == 0 && !f.ZeroOk)
                {
                    silent.Add(f.Name);
                }
                if (badCount > 0 && f.Group == "fix")
                {
                    bad.Add($"{f.Name} {badCount}");
                }
            }
            if (bad.Count > 0)
            {
                list.Add(new(WARN, T("버그 수정 항목에 나쁜 신호", "Bad signal on a bug fix"), T($"{string.Join(", ", bad)} — md '기능 점검' 표의 나쁜 신호 칸.", $"{string.Join(", ", bad)} - see the 'feature check' table.")));
            }
            if (silent.Count > 0)
            {
                list.Add(new(INFO, T($"이 판에서 한 번도 안 나온 기능 {silent.Count}개", $"{silent.Count} features never fired here"),
                    T($"{string.Join(", ", silent)}. 맵 특성상 안 나올 수 있음(예: 재배치는 실외 전용) — md '기능 점검' 표 참고.", $"{string.Join(", ", silent)}. Some can't fire on this map (e.g. Relocate is outdoor only) - see the 'feature check' table.")));
            }
        }

        // automatic oddity detector (user 2026-10-09: "can't you measure it yourself?")
        int odd = b.OddityCounts?.Values.Sum() ?? 0;
        if (odd > 0)
        {
            var parts = b.OddityCounts!.OrderByDescending(kv => kv.Value).Select(kv => $"{OddityName(kv.Key)} {kv.Value}");
            list.Add(new(b.OddityCounts.Values.Max() >= 10 ? WARN : INFO, T("이상 행동 자동 감지", "Automatic oddity detector"),
                T($"{played:0}분 동안 {odd}회: {string.Join(", ", parts)}. 장면별 봇·판단·적 거리·위치·레이드 시각은 md '이상 행동' 절과 일지 [Oddity] 줄.",
                  $"{odd} in {played:0} min: {string.Join(", ", parts)}. Who/decision/enemy distance/position/raid time per scene in the md 'oddities' section and journal [Oddity] lines.")));
        }

        // enemies shoulder to shoulder without knowing (user screenshot 2026-10-09)
        if (b.CloseUnawareSamples > 0)
        {
            var ex = b.CloseUnaware?.Take(3).Select(x => $"{x.Role}↔{x.OtherRole} {x.Distance:0.0}m t={x.RaidTime / 60f:0.0}분 [{x.Layer}]") ?? [];
            list.Add(new(b.CloseUnawareSamples >= 10 ? WARN : INFO, T("4m 안 적을 모름", "Enemy within 4 m unnoticed"),
                T($"사이에 벽·바닥 없이 적 봇이 4m 안에 있는데 모르는 표본 {b.CloseUnawareSamples}개 (2초마다·봇마다). 예: {string.Join(", ", ex)}. 같은 자리 동시 생성이나 등 뒤 감지 문제.",
                  $"{b.CloseUnawareSamples} samples of a hostile bot within 4 m with nothing in between that the bot didn't know about. e.g. {string.Join(", ", ex)}. Spawned on the same spot, or no sense of someone right behind.")));
        }

        // ORBIT telemetry (ORBIT 2.1.1: Ghost Mode sleeps bots > 250 m from the player, objectives/extracts walk squads away)
        float orbitTotal = b.LayerSeconds?.Values.Sum() ?? 0f;
        if (orbitTotal > 60f && b.OrbitGhostSeconds > orbitTotal * 0.05f)
        {
            list.Add(new(WARN, T("ORBIT 고스트 모드가 봇을 재움", "ORBIT Ghost Mode put bots to sleep"),
                T($"봇 시간의 {b.OrbitGhostSeconds / orbitTotal:P0}를 잠든 상태(고스트)로 보냄. 잠든 봇끼리 싸움은 실제 사격 없이 주사위로 처리돼 SAIN 측정이 안 됨. 시뮬 설정의 'ORBIT 고스트 모드 끄기'를 켜세요.",
                  $"{b.OrbitGhostSeconds / orbitTotal:P0} of bot time asleep (ghost). Ghost fights are rolled, not fought, so SAIN isn't measured. Turn on 'ORBIT ghost mode off' in the sim settings.")));
        }
        float away = b.OrbitObjectiveSeconds != null && b.OrbitObjectiveSeconds.TryGetValue(SimOrbitKeys.AwayFromArena, out float a) ? a : 0f;
        float objTotal = b.OrbitObjectiveSeconds?.Where(kv => kv.Key != SimOrbitKeys.AwayFromArena).Sum(kv => kv.Value) ?? 0f;
        if (objTotal > 120f && away > objTotal * 0.4f)
        {
            list.Add(new(WARN, T("ORBIT 목적지가 싸움 구역 밖", "ORBIT objectives outside the arena"),
                T($"ORBIT가 봇을 보낸 시간의 {away / objTotal:P0}가 싸움 구역(양쪽 스폰 사이 원) 밖 목적지였음. 'ORBIT 싸움 구역 고정'이 켜져 있는지, 존 에디터의 맵 설정이 덮였는지 확인.",
                  $"{away / objTotal:P0} of ORBIT objective time pointed outside the arena circle. Check 'ORBIT arena zone' is on and the map's zones were replaced.")));
        }
        float extractSeconds = b.OrbitObjectiveSeconds?.Where(kv => kv.Key.StartsWith("extract:", StringComparison.Ordinal)).Sum(kv => kv.Value) ?? 0f;
        if (extractSeconds > 60f)
        {
            var why = b.OrbitObjectiveSeconds!.Where(kv => kv.Key.StartsWith("extract:", StringComparison.Ordinal)).OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{kv.Key.Substring(8).Trim()} {kv.Value / 60f:0.0}분");
            list.Add(new(WARN, T("ORBIT가 봇을 탈출시키려 함", "ORBIT sends bots to extract"),
                T($"탈출하러 가는 봇 시간 {extractSeconds / 60f:0.0}분 ({string.Join(", ", why)}). 탈출하면 싸움 구역에서 빠짐 — 'ORBIT 탈출 끄기'를 켜세요.",
                  $"{extractSeconds / 60f:0.0} bot-minutes heading to extract ({string.Join(", ", why)}). Turn on 'ORBIT no extract'.")));
        }

        if (b.TeamKills > 0)
        {
            list.Add(new(INFO, T("아군 사격 사망", "Team kills"), T($"{b.TeamKills}회.", $"{b.TeamKills}.")));
        }

        if (list.All(f => f.Severity is INFO or OK))
        {
            list.Insert(0, new(OK, T("큰 문제 없음", "No major problems"), T($"{played:0}분, 봇 사망 {b.BotDeaths}, 오류 0.", $"{played:0} min, {b.BotDeaths} bot deaths, 0 errors.")));
        }
        return list;
    }

    /// <summary>Share of bot time in a layer group: "orbit", "combat" (SAIN combat), "sain" (any SAIN layer), "other".</summary>
    public static string DayNight(SimBeat? b)
    {
        if (b == null || b.RaidHour < 0f)
        {
            return T("알 수 없음", "unknown");
        }
        int h = (int)b.RaidHour;
        int min = (int)((b.RaidHour - h) * 60f);
        return $"{h:00}:{min:00} " + (b.Night ? T("(밤)", "(night)") : T("(낮)", "(day)"));
    }

    public static string OddityName(string kind)
    {
        return kind switch
        {
            "noShoot" => T("보이는 적에게 안 쏨", "visible enemy not shot"),
            "backTurned" => T("가까운 적에게 등 돌림", "back to a close enemy"),
            "hitNoReact" => T("맞고도 반응 없음", "hit, no reaction"),
            "flipFlop" => T("판단이 계속 뒤집힘", "decision flip-flop"),
            "bunched" => T("아군과 몸이 겹침", "bunched with a mate"),
            "stalledMove" => T("움직여야 하는데 제자리", "should move, standing"),
            _ => kind,
        };
    }

    public static float LayerShare(SimBeat b, string group)
    {
        float total = b.LayerSeconds?.Values.Sum() ?? 0f;
        if (total <= 0f)
        {
            return 0f;
        }
        return b.LayerSeconds!.Where(kv => LayerGroup(kv.Key) == group || (group == "sain" && LayerGroup(kv.Key) == "combat")).Sum(kv => kv.Value) / total;
    }

    public static string LayerGroup(string layer)
    {
        if (layer.IndexOf("orbit", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "orbit";
        }
        if (layer == "SAIN : Combat Layer")
        {
            return "combat";
        }
        return layer.StartsWith("SAIN", StringComparison.Ordinal) ? "sain" : "other";
    }

    public static string LayerGroupName(string group)
    {
        return group switch
        {
            "orbit" => T("ORBIT (맵 이동·목표 행동)", "ORBIT (map movement / objectives)"),
            "combat" => T("SAIN 전투", "SAIN combat"),
            "sain" => T("SAIN 기타 (수류탄 피하기·탈출 등)", "SAIN other (grenade avoid, extract...)"),
            _ => T("바닐라·기타 모드 (순찰 등)", "Vanilla / other mods (patrol...)"),
        };
    }

    private static float Share(SimBeat b, string prefix, string key)
    {
        int total = Sum(b, prefix);
        return total > 0 ? (float)Get(b, prefix + key) / total : 0f;
    }

    private static string TopShares(SimBeat b, string prefix, int take, Func<string, string> name)
    {
        int total = Sum(b, prefix);
        if (total == 0)
        {
            return string.Empty;
        }
        var top = b.Counters!
            .Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal) && kv.Key.IndexOf('.', prefix.Length) < 0)
            .OrderByDescending(kv => kv.Value)
            .Take(take)
            .Select(kv => $"{name(kv.Key.Substring(prefix.Length))} {(float)kv.Value / total:P0}");
        return string.Join(", ", top);
    }

    private static string ActionName(string action)
    {
        return action switch
        {
            "StandAndShoot" => T("서서 쏘기", "stand and shoot"),
            "RushEnemy" => T("돌격", "rush"),
            "SeekCover" => T("엄폐 찾기", "seek cover"),
            "Freeze" => T("얼음 매복", "freeze ambush"),
            "Search" => T("수색", "search"),
            "DoorTactic" => T("문 전술", "door tactic"),
            "ThrowGrenade" => T("수류탄", "grenade"),
            "SquadTactic" => T("분대 전술", "squad tactic"),
            "DogFight" => T("근접 난전", "dogfight"),
            "Retreat" => T("후퇴", "retreat"),
            "ShiftCover" => T("엄폐 옮기기", "shift cover"),
            _ => action,
        };
    }

    /// <summary>
    /// Plain-language "how did they fight on this map" (user 2026-10-09). Built only from the counters, so each sentence can be
    /// checked against the numbers below it on the page.
    /// </summary>
    public static List<string> StyleSummary(SimMapRecord m)
    {
        var lines = new List<string>();
        var b = m.Last;
        if (b == null || b.Counters == null)
        {
            return lines;
        }
        float minutes = Math.Max(1f, PlayedMinutes(m));
        lines.Add(T($"{minutes:0}분 동안 봇 {b.BotsSeen}명이 나와 {b.BotDeaths}명이 죽음 (10분당 {b.BotDeaths * 10f / minutes:0.0}명). " + (b.BotDeaths * 10f / minutes >= 15f ? "교전이 아주 잦은 판." : b.BotDeaths * 10f / minutes >= 5f ? "교전이 꾸준한 판." : "교전이 적은 판."),
                      $"{minutes:0} min: {b.BotsSeen} bots, {b.BotDeaths} deaths ({b.BotDeaths * 10f / minutes:0.0} per 10 min)."));
        string visible = TopShares(b, "utilityV.do.", 3, ActionName);
        if (visible.Length > 0)
        {
            lines.Add(T($"적이 보일 때는 주로 {visible}.", $"With the enemy in sight: {visible}."));
        }
        string hidden = TopShares(b, "utility.do.", 3, ActionName);
        if (hidden.Length > 0)
        {
            lines.Add(T($"적이 안 보일 때는 {hidden} 순으로 골랐음.", $"With the enemy out of sight: {hidden}."));
        }
        string deaths = TopShares(b, "death.", 3, ActionName);
        if (deaths.Length > 0)
        {
            lines.Add(T($"죽을 때 하던 행동: {deaths}.", $"Doing when dying: {deaths}."));
        }
        var used = new List<string>();
        void Use(string ko, string en, int n)
        {
            if (n > 0)
            {
                used.Add(T($"{ko} {n}", $"{en} {n}"));
            }
        }
        Use("문 전술", "door tactics", Sum(b, "door.start."));
        Use("분대 전술", "squad tactics", Sum(b, "squad.start."));
        Use("수류탄", "grenades", Sum(b, "nade.thrown."));
        Use("코너 추격", "corner chases", Sum(b, "chase.start."));
        Use("다이아몬드 스텝", "diamond steps", Get(b, "diamond.start"));
        Use("위치 바꾸기", "repositions", Sum(b, "repo.start."));
        Use("얼음 매복", "freeze ambushes", Sum(b, "freeze.start."));
        if (used.Count > 0)
        {
            lines.Add(T("쓴 전술: ", "Tactics used: ") + string.Join(", ", used) + ".");
        }
        int botFirst = Get(b, "repo.contact.BotFirst");
        int enemyFirst = Get(b, "repo.contact.EnemyFirst");
        if (botFirst + enemyFirst > 0)
        {
            lines.Add(T($"첫 접촉은 내가 먼저 본 경우 {(float)botFirst / (botFirst + enemyFirst):P0}, 적이 먼저 쏜 경우 {(float)enemyFirst / (botFirst + enemyFirst):P0}.",
                        $"First contact: saw first {(float)botFirst / (botFirst + enemyFirst):P0}, shot first by the enemy {(float)enemyFirst / (botFirst + enemyFirst):P0}."));
        }
        int firstSeen = Sum(b, "hear.firstSeen.");
        if (firstSeen > 0)
        {
            int heard = Sum(b, "hear.firstSeen.heard.");
            lines.Add(T($"적을 처음 볼 때 소리로 먼저 알고 있던 비율 {(float)heard / firstSeen:P0} ({firstSeen}회 중).", $"Heard the enemy before first sight {(float)heard / firstSeen:P0} of {firstSeen} times."));
        }
        if ((b.LayerSeconds?.Values.Sum() ?? 0f) > 60f)
        {
            lines.Add(T($"봇 시간: SAIN 전투 {LayerShare(b, "combat"):P0}, ORBIT {LayerShare(b, "orbit"):P0}, 바닐라·기타 {LayerShare(b, "other"):P0}.",
                        $"Bot time: SAIN combat {LayerShare(b, "combat"):P0}, ORBIT {LayerShare(b, "orbit"):P0}, vanilla/other {LayerShare(b, "other"):P0}."));
        }
        if (b.OrbitGhostSeconds > 0f || (b.OrbitObjectiveSeconds?.Count ?? 0) > 0)
        {
            var top = b.OrbitObjectiveSeconds?.Where(kv => kv.Key != SimOrbitKeys.AwayFromArena).OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{kv.Key} {kv.Value / 60f:0.0}") ?? [];
            lines.Add(T($"ORBIT: 잠든 시간 {b.OrbitGhostSeconds / 60f:0.0}분, 주요 목적지(분) {string.Join(", ", top)}.",
                        $"ORBIT: asleep {b.OrbitGhostSeconds / 60f:0.0} min, top objectives (min) {string.Join(", ", top)}."));
        }
        var best = b.ByPersonality?.Where(kv => kv.Value.Kills + kv.Value.Deaths >= 3).OrderByDescending(kv => kv.Value.Deaths > 0 ? (float)kv.Value.Kills / kv.Value.Deaths : kv.Value.Kills).FirstOrDefault();
        if (best != null && best.Value.Key != null)
        {
            lines.Add(T($"가장 잘 싸운 성격: {best.Value.Key} ({best.Value.Value.Kills}킬/{best.Value.Value.Deaths}사망).", $"Best personality: {best.Value.Key} ({best.Value.Value.Kills}/{best.Value.Value.Deaths})."));
        }
        if (b.CombatSamples >= 50)
        {
            lines.Add(T($"전투 중 벽을 1m 앞에 두고 보고 있던 시간 {(float)b.WallStareSamples / b.CombatSamples:P0}.", $"Facing a wall within 1 m: {(float)b.WallStareSamples / b.CombatSamples:P0} of combat time."));
        }
        return lines;
    }

    /// <summary>Counters of one map, the scenario's watch prefixes first.</summary>
    public static List<KeyValuePair<string, int>> OrderedCounters(SimMapRecord m)
    {
        var counters = m.Last?.Counters ?? new Dictionary<string, int>();
        var watch = (m.Watch ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return counters
            .OrderBy(kv => watch.Any(w => kv.Key.StartsWith(w, StringComparison.Ordinal)) ? 0 : 1)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .ToList();
    }

    public static bool IsWatched(SimMapRecord m, string key)
    {
        return (m.Watch ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(w => key.StartsWith(w, StringComparison.Ordinal));
    }

    public static string Markdown(SimRunRecord run)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# SAIN 시뮬 요약 — 실행 {run.RunId}");
        sb.AppendLine();
        sb.AppendLine($"- 시작 {run.Started:yyyy-MM-dd HH:mm}, 마지막 보고 {run.Updated:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"- 프리셋 `{run.Preset}`, SAIN.dll 빌드 {run.Build}");
        sb.AppendLine($"- 맵 {run.Maps.Count}개: " + string.Join(", ", run.Maps.Select(m => $"{m.MapName}({StatusText(m.Status)})")));
        sb.AppendLine();
        AppendTodo(sb, run);
        AppendCodeGlossary(sb, run);
        foreach (var m in run.Maps)
        {
            var b = m.Last;
            sb.AppendLine($"## {m.Index + 1}. {m.MapName} (`{m.Map}`) — {m.Scenario}");
            sb.AppendLine();
            sb.AppendLine($"- 상태 **{StatusText(m.Status)}**, 진행 {PlayedMinutes(m):0.0}/{m.PlannedMinutes:0}분, 시작 {m.Started:HH:mm:ss}, 게임 시각 {DayNight(m.Last)}, 시작 전 메뉴 관리 메모리(GC 뒤) {(m.Last?.MenuMonoMB > 0 ? $"{m.Last.MenuMonoMB}MB" : "?")}");
            sb.AppendLine($"- 목적: {m.Focus}");
            if (m.Raid != null)
            {
                sb.AppendLine($"- 시뮬 프리셋: {(string.IsNullOrEmpty(m.Raid.SimPreset) ? "없음(직접 설정)" : m.Raid.SimPreset)} · 성격: {PersonalityLabel(m.Raid.Personalities)} · 모드 아이템: {ModItemsLabel(m.Raid)}{(m.Raid.BlackoutOff ? (m.Raid.BlackoutWasDark ? " · Blackout 정전 굴림 → 끔" : " · Blackout 정전 아님") : "")}");
                sb.AppendLine($"- 스폰: A {m.Raid.SideA}@[{m.Raid.ZonesA}] vs B {m.Raid.SideB}@[{m.Raid.ZonesB}], 동시 최대 {m.Raid.MaxAliveBots}, 분대 {m.Raid.SquadSizeMin}~{m.Raid.SquadSizeMax}, 지운 웨이브 {m.Raid.RemovedBossWaves}+{m.Raid.RemovedWaves}, ABPS {(m.Raid.AbpsOff ? "끔(서버)" : "안 건드림")}, 분대끼리 {(m.Raid.AllSquadsEnemies ? "전부 적" : "SPT 기본(같은 진영 85% 적)")}");
            }
            if (b != null)
            {
                sb.AppendLine($"- 봇: 본 봇 {b.BotsSeen}, 사망 {b.BotDeaths} (봇에게 {b.BotKillsByBots}, 기타 {b.OtherKills}{SpectatorPart(b)}, 아군 {b.TeamKills}), 분대 호출 {b.SimWavesActivated}, 마지막 생존 {b.AliveBots}");
                sb.AppendLine($"- 성능: 마지막 {b.Fps:0} fps, 최저 {b.FpsMin:0} fps, 멈춤 {b.Stalls}, 관리 메모리 {b.MonoUsedMB} MB");
                sb.AppendLine($"- 일지: `{b.JournalFile}`");
                if (b.SpawnedRoles?.Count > 0)
                {
                    sb.AppendLine("- 등장 역할: " + string.Join(", ", b.SpawnedRoles.Select(kv => $"{kv.Key}×{kv.Value}")));
                }
            }
            sb.AppendLine();
            sb.AppendLine("### 싸움 방식 요약");
            foreach (string line in StyleSummary(m))
            {
                sb.AppendLine("- " + line);
            }
            if (b != null && (b.LayerSeconds?.Count ?? 0) > 0)
            {
                float total = b.LayerSeconds!.Values.Sum();
                sb.AppendLine();
                sb.AppendLine("### 레이어 시간 (봇 전체 합, 2초 표본)");
                sb.AppendLine(string.Join("; ", b.LayerSeconds.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value / 60f:0.0}분 ({kv.Value / total:P0}) [{LayerGroup(kv.Key)}]")));
            }
            if (m.Raid != null || (b != null && (b.OrbitGhostSeconds > 0f || (b.OrbitObjectiveSeconds?.Count ?? 0) > 0)))
            {
                sb.AppendLine();
                sb.AppendLine("### ORBIT");
                sb.AppendLine($"- 서버가 바꾼 ORBIT 설정: {(string.IsNullOrEmpty(m.Raid?.OrbitOverrides) ? "없음 (ORBIT 없음/끔/요청 전)" : m.Raid!.OrbitOverrides)}");
                if (m.Raid?.HasArena == true)
                {
                    sb.AppendLine($"- 싸움 구역 원: 중심 ({m.Raid.ArenaX:0},{m.Raid.ArenaZ:0}) 반지름 {m.Raid.ArenaRadius:0}m, 구역 줄 {(m.Raid.Leash ? "켬" : "끔")}, 끌고 온 횟수 {b?.LeashTeleports ?? 0} (일지 `[SimLab] leash`)");
                }
                if (b != null)
                {
                    sb.AppendLine($"- 고스트(잠듦) 봇초 {b.OrbitGhostSeconds:0}; 목적지 봇초: " + string.Join(", ", (b.OrbitObjectiveSeconds ?? new()).OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value:0}")));
                }
            }
            if (b != null && b.SpawnInZone + b.SpawnOffZone > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"### 스폰 위치 검사: 정한 구역 {b.SpawnInZone} / 밖 {b.SpawnOffZone}");
                sb.AppendLine("- 구역별: " + string.Join(", ", b.SpawnZones.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}×{kv.Value}")));
                foreach (var x in b.SpawnOff ?? [])
                {
                    sb.AppendLine($"- 밖: {x.Role}({x.Side}) zone={x.Zone} wanted={x.Wanted} pos=({x.X:0},{x.Y:0},{x.Z:0}) 가까운 지정 지점까지 {x.Distance:0}m t={x.RaidTime:0}s");
                }
            }
            if (b != null)
            {
                sb.AppendLine();
                sb.AppendLine("### 기능 점검 (README '추가한 기능' + '버그 수정', 카운터 기준)");
                sb.AppendLine("| 분류 | 기능 | 발동 | 거부·실패 | 나쁜 신호 | 일지 태그 | 메모 |");
                sb.AppendLine("|---|---|---|---|---|---|---|");
                foreach (var f in SimLabFeatures.All)
                {
                    var (fired, refused, badCount) = SimLabFeatures.Measure(f, b);
                    string state = f.Fired.Length == 0 ? "➖" : fired > 0 ? "✅" : f.ZeroOk ? "➖" : "⚠️";
                    if (badCount > 0 && f.Group == "fix")
                    {
                        state = "❌";
                    }
                    string journal = f.Group == "ai" && f.Name == "레이드 일지" ? (string.IsNullOrEmpty(b.JournalFile) ? "⚠️ 없음" : "✅ 있음") : f.Journal;
                    sb.AppendLine($"| {f.Group} | {state} {f.Name} | {(f.Fired.Length == 0 ? "-" : fired.ToString())} | {(f.Refused.Length == 0 ? "-" : refused.ToString())} | {(f.Bad.Length == 0 ? "-" : badCount.ToString())} | {journal} | {f.Note} |");
                }
            }
            if (b != null && (b.OddityCounts?.Count ?? 0) > 0)
            {
                sb.AppendLine();
                sb.AppendLine("### 이상 행동 자동 감지 (SimLabOddity, SAIN 전투 레이어 봇, 2초 표본, 상태가 풀릴 때까지 1회)");
                sb.AppendLine("- 종류별: " + string.Join(", ", b.OddityCounts!.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}({OddityName(kv.Key)})={kv.Value}")));
                foreach (var x in (b.Oddities ?? []).OrderBy(x => x.RaidTime))
                {
                    sb.AppendLine($"- t={x.RaidTime / 60f:0.0}분 {x.Kind} [{x.Name}] {x.Role}/{x.Personality} {x.Decision}/{x.Reason} layer={x.Layer} 적 {x.EnemyDistance:0.0}m 보임={x.EnemyVisible} 각도={x.EnemyAngle:0} 속도={x.Speed:0.0} 탄={x.Bullets} {x.Seconds:0}초 pos=({x.X:0},{x.Y:0},{x.Z:0})");
                }
            }
            if (b != null && b.CloseUnawareSamples > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"### 4m 안 적을 모름 (표본 {b.CloseUnawareSamples})");
                foreach (var x in b.CloseUnaware ?? [])
                {
                    sb.AppendLine($"- {x.Role}↔{x.OtherRole} {x.Distance:0.0}m, 상대도 모름 {!x.OtherKnows}, 레이어 {x.Layer}, pos=({x.X:0},{x.Y:0},{x.Z:0}) t={x.RaidTime:0}s");
                }
            }
            if (b != null && b.CombatSamples > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"### 벽 보기 (SAIN 전투 레이어 표본 {b.CombatSamples} 중 1m 안 벽 {b.WallStareSamples}, 그중 적 방향 {b.WallStareTowardEnemy})");
                sb.AppendLine(string.Join(", ", (b.WallStareByDecision ?? new()).OrderByDescending(kv => kv.Value).Take(10).Select(kv => $"{kv.Key}={kv.Value}")));
            }
            sb.AppendLine();
            sb.AppendLine("### 감지된 문제");
            foreach (var f in Findings(m))
            {
                sb.AppendLine($"- [{f.Severity}] **{f.Title}** — {f.Detail}");
                if (!string.IsNullOrEmpty(f.Fix))
                {
                    sb.AppendLine($"  - 수정 방향(Claude): {f.Fix}");
                }
            }
            if (b != null)
            {
                if (b.Errors?.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("### 오류 (같은 위치끼리 묶음)");
                    foreach (var e in b.Errors.OrderByDescending(e => e.Count))
                    {
                        sb.AppendLine($"- ×{e.Count} [{e.Level}/{e.Source}] 첫 t={e.FirstRaidTime:0}s `{e.Key}`");
                        if (!string.IsNullOrEmpty(e.Sample))
                        {
                            sb.AppendLine("  ```");
                            foreach (string line in e.Sample.Split('\n').Take(8))
                            {
                                sb.AppendLine("  " + line.TrimEnd());
                            }
                            sb.AppendLine("  ```");
                        }
                    }
                }
                AppendRows(sb, "성격별 킬/사망 (sw=판단 전환 1.5초 안 사망)", b.ByPersonality);
                AppendRows(sb, "죽을 때 하던 판단 이유", b.ByReason, 2);
                var counters = OrderedCounters(m);
                if (counters.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine($"### 카운터 (★ = 이 맵에서 볼 것: {m.Watch})");
                    sb.AppendLine(string.Join(", ", counters.Select(kv => $"{(IsWatched(m, kv.Key) ? "★" : "")}{kv.Key}={kv.Value}")));
                }
                if (b.Notes?.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("### 메모");
                    foreach (string n in b.Notes)
                    {
                        sb.AppendLine("- " + n);
                    }
                }
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>For the Claude summary only: which code counts each counter group seen in this run.</summary>
    /// <summary>
    /// The to-do list at the top of the Claude summary (user 2026-10-09): every high/warn finding of every map with how to fix it.
    /// User's rule: [high] = fix right away, [warn] = ask the user before fixing.
    /// </summary>
    /// <summary>Leak check over the run: the menu's managed heap (after a full GC) before each map. Climbing map after map,
    /// whatever the map, means old raids are kept alive; up and down with the map size is just the map.</summary>
    public static SimFinding? MenuHeapFinding(SimRunRecord run)
    {
        var points = run.Maps.Where(m => m.Last != null && m.Last.MenuMonoMB > 0).Select(m => (m.MapName, Mb: m.Last!.MenuMonoMB)).ToList();
        if (points.Count < 3)
        {
            return null;
        }
        bool rising = true;
        for (int i = 1; i < points.Count; i++)
        {
            rising &= points[i].Mb > points[i - 1].Mb + 150;
        }
        long growth = points[^1].Mb - points[0].Mb;
        if (!rising || growth < 800)
        {
            return null;
        }
        string list = string.Join(" → ", points.Select(p => $"{p.MapName} {p.Mb}MB"));
        return new SimFinding(WARN, T("맵이 바뀔 때마다 메뉴 메모리가 계속 오름 (누수 의심)", "Menu memory keeps rising map after map (leak suspected)"),
            T($"레이드 시작 전 메뉴에서 GC 뒤 관리 메모리: {list} (+{growth}MB).", $"Managed heap in the menu after a full GC before each raid: {list} (+{growth} MB)."))
        {
            Fix = "레이드가 끝나도 남는 참조 찾기: SAIN/다른 모드의 정적 목록·이벤트 구독(Player/BotOwner/GameWorld), BotManagerComponent.OnDestroy의 Clear 누락. 같은 맵을 두 번 넣은 순회로 재확인.",
        };
    }

    private static void AppendTodo(StringBuilder sb, SimRunRecord run)
    {
        var items = run.Maps.SelectMany(m => Findings(m).Where(f => f.Severity is HIGH or WARN).Select(f => (m, f))).ToList();
        var leak = MenuHeapFinding(run);
        if (leak != null && run.Maps.Count > 0)
        {
            items.Add((run.Maps[^1], leak));
        }
        sb.AppendLine("## Claude 할 일 (자동 생성)");
        sb.AppendLine();
        if (items.Count == 0)
        {
            sb.AppendLine("- 심각·주의 없음.");
            sb.AppendLine();
            return;
        }
        sb.AppendLine("> 사용자 규칙: **[high]는 바로 수정**, **[warn]은 고치기 전에 사용자에게 먼저 묻는다**. 사용자 메모(Alt+F4 등)가 있으면 그걸 먼저 반영. 깊게 볼 때는 '요약 + 레이드 일지 합쳐 받기' md의 일지 원문.");
        sb.AppendLine();
        foreach (var (m, f) in items.OrderBy(x => x.f.Severity == HIGH ? 0 : 1))
        {
            sb.AppendLine($"- [{f.Severity}] {m.MapName}: **{f.Title}** → {f.Fix}");
        }
        sb.AppendLine();
    }

    /// <summary>Raw raid journals of the run's maps, appended to the summary for deep analysis (user 2026-10-09). Read from the
    /// journal path each map reported (same PC as the server); missing files are listed, not fatal.</summary>
    public static string JournalAppendix(SimRunRecord run)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("# 레이드 일지 원문 (병합)");
        sb.AppendLine();
        foreach (var m in run.Maps)
        {
            string? path = m.Last?.JournalFile;
            sb.AppendLine($"## 일지: {m.Index + 1}. {m.MapName} — `{path ?? "?"}`");
            sb.AppendLine();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                sb.AppendLine("- 파일 없음 (레이드 일지가 꺼져 있었거나 지워짐: F6 > General > 플레이어 스타일 기록 > 레이드 일지).");
                sb.AppendLine();
                continue;
            }
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                sb.AppendLine("```text");
                sb.AppendLine(reader.ReadToEnd().TrimEnd());
                sb.AppendLine("```");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"- 읽기 실패: {ex.Message}");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public static void AppendCodeGlossary(StringBuilder sb, SimRunRecord run)
    {
        var groups = new SortedDictionary<string, SimLabGlossary.Entry>(StringComparer.Ordinal);
        foreach (var m in run.Maps)
        {
            foreach (string key in m.Last?.Counters?.Keys ?? Enumerable.Empty<string>())
            {
                var e = SimLabGlossary.Find(SimLabGlossary.Counters, key);
                if (e != null)
                {
                    groups[e.Key] = e;
                }
            }
        }
        if (groups.Count == 0)
        {
            return;
        }
        sb.AppendLine("## 분석용 용어 (카운터 접두어 → 뜻 · 세는 코드)");
        sb.AppendLine();
        foreach (var e in groups.Values)
        {
            sb.AppendLine($"- `{e.Key}` {e.Ko} — `{e.Code}`");
        }
        sb.AppendLine("- 레이어: `SAIN : Combat Layer`=SAIN 전투, `OrbitBrainLayer`=ORBIT, 그 밖 `SAIN : *`=SAIN 보조, 나머지=바닐라·다른 모드. 벽 보기 = SAIN 전투 레이어에서 눈 앞 1m 레이캐스트(HighPolyWithTerrainMask) 적중.");
        sb.AppendLine("- 스폰 검사 = 새 봇을 처음 본 틱(2초 안)의 `BotsGroup.BotZone` 이름 vs 시나리오 구역, 거리 = 지정 구역 스폰 지점 중 가장 가까운 것.");
        sb.AppendLine();
    }

    private static void AppendRows(StringBuilder sb, string title, Dictionary<string, SimRow>? rows, int minEvents = 0)
    {
        if (rows == null || rows.Count == 0)
        {
            return;
        }
        sb.AppendLine();
        sb.AppendLine($"### {title}");
        sb.AppendLine(string.Join("; ", rows
            .Where(kv => kv.Value.Kills + kv.Value.Deaths >= minEvents)
            .OrderByDescending(kv => kv.Value.Kills + kv.Value.Deaths)
            .Select(kv => $"{kv.Key} {kv.Value.Kills}/{kv.Value.Deaths}{(kv.Value.DiedSwitching > 0 ? $" sw{kv.Value.DiedSwitching}" : "")}")));
    }

    public static string StatusText(string status)
    {
        return status switch
        {
            "running" => T("진행 중", "Running"),
            "done" => T("완료", "Done"),
            "interrupted" => T("중단됨", "Interrupted"),
            "ended" => T("다른 이유로 종료", "Ended otherwise"),
            _ => status,
        };
    }
}
