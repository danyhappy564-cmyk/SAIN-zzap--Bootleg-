using System.Text;
using SAIN.Preset.Shared.SimLab;
using static SAINServerMod.Web.Services.WebText;

namespace SAINServerMod.SimLab;

public sealed record SimFinding(string Severity, string Title, string Detail);

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
                T($"예비 분대 {b.SimWavesActivated}개를 불렀지만 봇이 하나도 안 나왔습니다. 구역 이름({m.Raid?.ZonesA} / {m.Raid?.ZonesB})이나 스폰 모드 충돌 의심.",
                  $"{b.SimWavesActivated} reserve squads were called but no bot appeared. Suspect the zone names ({m.Raid?.ZonesA} / {m.Raid?.ZonesB}) or a spawn mod conflict.")));
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
                T($"사망 {deaths} 중 {switching}이 판단 전환 1.5초 안 (30% 이상). 일지 [Death] why=·decisionAge= 확인.", $"{switching} of {deaths} deaths within 1.5 s of a decision change (30%+). Check why= / decisionAge= in the journal's [Death] lines.")));
        }

        int freezeDeaths = Get(b, "death.Freeze");
        if (freezeDeaths > 0)
        {
            list.Add(new(WARN, T("얼음 매복 중 사망", "Died while freeze-ambushing"), T($"death.Freeze={freezeDeaths} (0이어야 정상).", $"death.Freeze={freezeDeaths} (should be 0).")));
        }
        int stuck = Sum(b, "stuck.");
        if (stuck > 0)
        {
            list.Add(new(INFO, T("막힘 기록", "Stuck records"), T($"stuck.* 합계 {stuck}. 같은 자리 반복이면 맵 지형 문제.", $"stuck.* total {stuck}. Repeats at one spot point to map geometry.")));
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
        if (nadeFail >= 10 && nadeFail > thrown * 3)
        {
            list.Add(new(INFO, T("수류탄 궤적 실패가 많음", "Many grenade arc failures"), T($"던짐 {thrown} / 궤적 없음·준비 안 됨 {nadeFail}.", $"Thrown {thrown} / no arc + not ready {nadeFail}.")));
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
        AppendCodeGlossary(sb, run);
        foreach (var m in run.Maps)
        {
            var b = m.Last;
            sb.AppendLine($"## {m.Index + 1}. {m.MapName} (`{m.Map}`) — {m.Scenario}");
            sb.AppendLine();
            sb.AppendLine($"- 상태 **{StatusText(m.Status)}**, 진행 {PlayedMinutes(m):0.0}/{m.PlannedMinutes:0}분, 시작 {m.Started:HH:mm:ss}");
            sb.AppendLine($"- 목적: {m.Focus}");
            if (m.Raid != null)
            {
                sb.AppendLine($"- 스폰: A {m.Raid.SideA}@[{m.Raid.ZonesA}] vs B {m.Raid.SideB}@[{m.Raid.ZonesB}], 동시 최대 {m.Raid.MaxAliveBots}, 분대 {m.Raid.SquadSizeMin}~{m.Raid.SquadSizeMax}, 지운 웨이브 {m.Raid.RemovedBossWaves}+{m.Raid.RemovedWaves}");
            }
            if (b != null)
            {
                sb.AppendLine($"- 봇: 본 봇 {b.BotsSeen}, 사망 {b.BotDeaths} (봇에게 {b.BotKillsByBots}, 기타 {b.OtherKills}, 아군 {b.TeamKills}), 분대 호출 {b.SimWavesActivated}, 마지막 생존 {b.AliveBots}");
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
                    sb.AppendLine($"- 싸움 구역 원: 중심 ({m.Raid.ArenaX:0},{m.Raid.ArenaZ:0}) 반지름 {m.Raid.ArenaRadius:0}m");
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
