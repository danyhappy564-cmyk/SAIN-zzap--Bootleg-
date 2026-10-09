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
        int doorStart = Sum(b, "door.start.");
        int doorAbort = Sum(b, "door.abort") + Sum(b, "door.reject.");
        if (doorStart >= 5 && doorAbort > doorStart)
        {
            list.Add(new(WARN, T("문 전술 중단이 시작보다 많음", "More door-tactic aborts than starts"), T($"시작 {doorStart} / 중단·거절 {doorAbort}.", $"Starts {doorStart} / aborts+rejects {doorAbort}.")));
        }
        int thrown = Sum(b, "nade.thrown");
        int nadeFail = Get(b, "nade.noArc") + Get(b, "nade.notReady");
        if (nadeFail >= 10 && nadeFail > thrown * 3)
        {
            list.Add(new(INFO, T("수류탄 궤적 실패가 많음", "Many grenade arc failures"), T($"던짐 {thrown} / 궤적 없음·준비 안 됨 {nadeFail}.", $"Thrown {thrown} / no arc + not ready {nadeFail}.")));
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
