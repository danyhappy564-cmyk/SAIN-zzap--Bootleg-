namespace SAINServerMod.SimLab;

/// <summary>
/// zzap fork: built-in map tests. Zone names come from the SPT 4.1 location data (SpawnPointParams.BotZoneName, only zones
/// with boss/PMC spawn points). Each map gets a different test from its usual fight spots (user 2026-10-09: "maps have their
/// own character - use the usual fight spots and test something different on each").
/// </summary>
public static class SimLabDefaults
{
    public static readonly Dictionary<string, string> MapNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["factory4_day"] = "공장 (낮)",
        ["factory4_night"] = "공장 (밤)",
        ["bigmap"] = "세관",
        ["interchange"] = "인터체인지",
        ["rezervbase"] = "리저브",
        ["shoreline"] = "해안선",
        ["woods"] = "삼림",
        ["lighthouse"] = "등대",
        ["tarkovstreets"] = "타르코프 시내",
        ["sandbox"] = "그라운드 제로",
        ["sandbox_high"] = "그라운드 제로 (21+)",
        ["laboratory"] = "연구소",
        ["labyrinth"] = "미궁",
    };

    public static readonly Dictionary<string, string> MapNamesEn = new(StringComparer.OrdinalIgnoreCase)
    {
        ["factory4_day"] = "Factory (day)",
        ["factory4_night"] = "Factory (night)",
        ["bigmap"] = "Customs",
        ["interchange"] = "Interchange",
        ["rezervbase"] = "Reserve",
        ["shoreline"] = "Shoreline",
        ["woods"] = "Woods",
        ["lighthouse"] = "Lighthouse",
        ["tarkovstreets"] = "Streets of Tarkov",
        ["sandbox"] = "Ground Zero",
        ["sandbox_high"] = "Ground Zero (21+)",
        ["laboratory"] = "The Lab",
        ["labyrinth"] = "The Labyrinth",
    };

    /// <summary>Korean map name (stored in records and the summary file).</summary>
    public static string NameOf(string map)
    {
        return MapNames.TryGetValue(map, out string? name) ? name : map;
    }

    /// <summary>Map name in the web page's language.</summary>
    public static string DisplayName(string map)
    {
        if (!Web.Services.WebText.Korean && MapNamesEn.TryGetValue(map, out string? en))
        {
            return en;
        }
        return NameOf(map);
    }

    public static string ScenarioName(SimScenario scenario)
    {
        return !Web.Services.WebText.Korean && !string.IsNullOrEmpty(scenario.NameEn) ? scenario.NameEn : scenario.Name;
    }

    public static string ScenarioFocus(SimScenario scenario)
    {
        return !Web.Services.WebText.Korean && !string.IsNullOrEmpty(scenario.FocusEn) ? scenario.FocusEn : scenario.Focus;
    }

    /// <summary>Default order: small/indoor first, open maps later, heavy Streets near the end.</summary>
    public static List<SimMapEntry> Rotation()
    {
        return
        [
            Paced("factory4_day"),
            Paced("bigmap"),
            Paced("interchange"),
            Paced("rezervbase"),
            Paced("shoreline"),
            Paced("woods"),
            Paced("lighthouse"),
            Paced("tarkovstreets"),
            Paced("sandbox"),
            Paced("factory4_night", false),
            Paced("sandbox_high", false),
            Paced("laboratory", false),
            Paced("labyrinth", false),
        ];
    }

    /// <summary>
    /// Spawn pace per map (alive cap, refill seconds, squad size). Small indoor maps refill fast with small squads so many
    /// fights happen in 20 minutes; open maps refill slower (the sides need time to meet); Streets keeps fewer bots for frames.
    /// </summary>
    public static readonly Dictionary<string, (int Cap, int Respawn, int SquadMin, int SquadMax)> Pace = new(StringComparer.OrdinalIgnoreCase)
    {
        ["factory4_day"] = (10, 20, 2, 3),
        ["factory4_night"] = (10, 25, 2, 3),
        ["labyrinth"] = (8, 20, 2, 3),
        ["laboratory"] = (10, 30, 2, 3),
        ["bigmap"] = (12, 45, 3, 4),
        ["interchange"] = (12, 40, 3, 4),
        ["rezervbase"] = (12, 40, 3, 4),
        ["shoreline"] = (12, 50, 3, 4),
        ["sandbox"] = (12, 45, 3, 4),
        ["sandbox_high"] = (12, 40, 3, 4),
        ["woods"] = (12, 60, 3, 4),
        ["lighthouse"] = (12, 55, 3, 4),
        ["tarkovstreets"] = (10, 50, 3, 4),
    };

    /// <summary>Main personalities "even" spreads the sim PMCs over (Custom1-4 / None left out).</summary>
    public static readonly string[] MainPersonalities = { "GigaChad", "Chad", "Wreckless", "Normal", "SnappingTurtle", "Rat", "Timmy", "Coward" };

    /// <summary>
    /// Built-in sim presets (user 2026-10-10: "which preset = which map lineup / spawn mix / personalities, to check what").
    /// Each lists every default map - its own picks first and on, the rest off - so a saved rotation never gains maps back.
    /// </summary>
    public static List<SimPreset> Presets()
    {
        return
        [
            Preset("기본 순회 (전 맵 점검)", "Full rotation (every map)",
                "README 기능 전체가 맵마다 실제로 나오는지(기능 점검 표), 오류·성능·메모리. 성격은 SAIN 프리셋 그대로.",
                "", ("factory4_day", 20), ("bigmap", 20), ("interchange", 20), ("rezervbase", 20), ("shoreline", 20), ("woods", 20),
                ("lighthouse", 20), ("tarkovstreets", 20), ("sandbox", 20)),
            Preset("릴리즈 전 점검 (공장 30분 + 연구소 30분)", "Pre-release check (Factory 30 + Labs 30)",
                "켜 두고 자는 약 1시간: 실내 전투 데이터가 가장 확실한 두 맵. 공장(근접 난전·문·코너)과 연구소(복도·계단·층 사이 대치) 30분씩. 새 오류, 근접 판단·후퇴·점프, 판단 전환 직후 사망, 모드 아이템 금지. 성격은 SAIN 기본. 연구소는 Blackout 정전 끔(시뮬 판만), 출입 카드 없이 자동 시작(메뉴 검사를 안 거침, 미검증).",
                "", ("factory4_day", 30), ("laboratory", 30)),
            Preset("빠른 점검 (공장 10분)", "Quick check (Factory 10 min)",
                "빌드 직후: 오류가 새로 생겼는지, 분대가 제때 나오는지, 기본 전투가 도는지만 빠르게.",
                "", ("factory4_day", 10)),
            Preset("근접·실내 전술", "Close-range / indoor tactics",
                "문 전술·방 진입·다이아몬드 스텝·근접 장전·코너 추격·판단 전환 직후 사망. 실내 시나리오 맵만.",
                "", ("factory4_day", 15), ("bigmap", 15), ("shoreline", 15), ("rezervbase", 15)),
            // user 2026-10-13: "the door preset was meant at the sim stage, not in SAIN F6" - replaces the F6 preset
            // "zzap TEST 문 모드 [시뮬 전용]". Door-heavy arenas, attack-minded mix, door check switch on.
            DoorCheck(Preset("문 전술·박치기 점검", "Door tactics & ram check",
                "봇 문 박치기(방 진입·도망·돌격·수색·그 밖의 전투 이동)와 문 전술(피킹·방 진입·문 함정)이 실제로 나오고 문이 제대로 열리는지. "
                + "'문 점검' 스위치를 켬: 시뮬 판에서만 박치기 100%, 모든 PMC가 기가채드처럼 문 전술(시작 100%) — SAIN 프리셋은 그대로. "
                + "문이 많은 실내 맵 4개, 돌격형 성격 위주. 카운터 doorram.*·doormod.*·door.start.*, 일지 [DoorRam]·[DoorTactic]. 일반 순회 결과와 섞어 비교하지 말 것.",
                "GigaChad:2,Chad:3,Normal:2,Rat:1", ("factory4_day", 15), ("bigmap", 15), ("rezervbase", 15), ("laboratory", 15))),
            Preset("성격 비교 (고르게)", "Personality comparison (even)",
                "성격 8가지를 같은 비율로 섞어 성격별 킬/사망·정지·매복/후퇴/돌격 비율 비교. 근접(공장)과 다층 건물(세관) 두 맵.",
                "even", ("factory4_day", 20), ("bigmap", 20)),
            Preset("방어형 성격 (Rat·Coward·SnappingTurtle)", "Defensive personalities (Rat, Coward, SnappingTurtle)",
                "매복·후퇴·버티기 성격이 멍때리지 않고 자연스러운지(정지·매복 비율, 후퇴 정지, 치료). 문 옆 매복(Rat)까지 보려면 SAIN TEST 프리셋의 문 전술 '테스트 모드: 모든 PMC가 기가채드 전술 사용'을 끌 것.",
                "Rat:3,Coward:2,SnappingTurtle:3,Normal:1", ("factory4_day", 15), ("bigmap", 15), ("rezervbase", 15)),
            Preset("공격형 성격 (GigaChad·Chad·Wreckless)", "Aggressive personalities (GigaChad, Chad, Wreckless)",
                "돌격·코너 추격·일제 돌격·점프샷이 무리하지 않는지(돌격 중 사망, 판단 전환 사망).",
                "GigaChad:3,Chad:3,Wreckless:2,Normal:1", ("factory4_day", 15), ("bigmap", 15), ("interchange", 15)),
            Preset("실외·장거리", "Outdoor / long range",
                "재배치(실외 전용)·장거리 무기 전환·먼 적 상대 판단(정지·매복 거리 감점)·발소리 감지. 넓은 실외 맵.",
                "", ("woods", 20), ("lighthouse", 20), ("sandbox", 20), ("shoreline", 20)),
            // user 2026-10-23: a check-only preset for the modded bots SAIN now handles itself (Black Division without its fork).
            ModdedCheck(Preset("모드 봇 SAIN 점검 (블디·RUAF·UNTAR)", "Modded bot SAIN check (Black Division, RUAF, UNTAR)",
                "설치된 모드 봇(블랙 디비전·RUAF·UNTAR 등)이 SAIN으로 싸우는지. 보충 분대의 절반을 그 봇들로 채움(보스 제외, 설치 안 된 모드는 자동으로 빠짐). "
                + "LogOutput에서 [SAIN ModdedBots] 줄 — 레이드 시작 레이어 점검 수치, 모르는 적에게 맞고 알아챔, 성격 지정. 분석 페이지 팩션별 표의 BlackDivision 두뇌 칸이 SAIN인지. 일반 순회 결과와 섞어 비교하지 말 것.",
                "", ("factory4_day", 15), ("bigmap", 15))),
        ];
    }

    private static SimPreset DoorCheck(SimPreset preset)
    {
        preset.DoorTest = true;
        return preset;
    }

    private static SimPreset ModdedCheck(SimPreset preset)
    {
        preset.ModdedBotCheck = true;
        return preset;
    }

    private static readonly Dictionary<string, string> PurposeEn = new()
    {
        ["기본 순회 (전 맵 점검)"] = "Whether every README feature really shows up on each map (feature check table), errors, performance, memory. Personalities as in the SAIN preset.",
        ["릴리즈 전 점검 (공장 30분 + 연구소 30분)"] = "About an hour to leave running overnight: the two maps with the most reliable indoor fight data. Factory (close brawls, doors, corners) and Labs (corridors, stairs, floor-to-floor standoffs), 30 minutes each. New errors, close-range decisions, retreats and jumps, deaths right after a decision change, no mod items. SAIN default personalities. Labs runs without the Blackout power cut (sim raids only) and starts without a keycard (skips the menu check, unverified).",
        ["빠른 점검 (공장 10분)"] = "Right after a build: any new errors, do squads arrive on time, does basic combat run.",
        ["근접·실내 전술"] = "Door tactics, room clears, diamond steps, close-range reloads, corner chases, deaths right after a decision change. Indoor scenario maps only.",
        ["문 전술·박치기 점검"] = "Whether bot door rams (room clear, flee, rush, search, other combat moves) and door tactics (peek, room clear, door trap) really happen and doors open properly. Turns the 'door check' switch on: in sim raids only, rams 100% and every PMC plans door tactics like a GigaChad (start 100%) - the SAIN preset is untouched. 4 indoor maps with many doors, attack-minded personalities. Counters doorram.* / doormod.* / door.start.*, journal [DoorRam] / [DoorTactic]. Don't mix its results with normal rotations.",
        ["성격 비교 (고르게)"] = "All 8 personalities in equal shares to compare kills/deaths, freezes and ambush / retreat / rush rates per personality. Close range (Factory) and a multi-floor building (Customs).",
        ["방어형 성격 (Rat·Coward·SnappingTurtle)"] = "Whether ambush / retreat / holding personalities act naturally instead of zoning out (freeze and ambush rates, stalled retreats, healing). To see Rats ambushing next to doors, switch off the TEST preset's door tactic 'test mode: every PMC uses GigaChad tactics'.",
        ["공격형 성격 (GigaChad·Chad·Wreckless)"] = "Whether rushes, corner chases, squad storms and jump shots stay sensible (deaths while rushing, deaths right after a switch).",
        ["모드 봇 SAIN 점검 (블디·RUAF·UNTAR)"] = "Whether the installed modded bots (Black Division, RUAF, UNTAR...) fight with SAIN. Half of the refills are those bots (bosses left out, mods that aren't installed drop out by themselves). In LogOutput the [SAIN ModdedBots] lines - raid-start layer check counts, hit by an unknown enemy and noticing it, forced personality. In the analysis page's per-faction table, whether the BlackDivision brain column is SAIN. Don't mix its results with normal rotations.",
        ["실외·장거리"] ="Repositioning (outdoor only), switching to a long-range weapon, decisions against far enemies (freeze / ambush distance penalty), footstep detection. Large outdoor maps.",
    };

    private static SimPreset Preset(string name, string nameEn, string purpose, string personalities, params (string Map, float Minutes)[] picks)
    {
        var rotation = new List<SimMapEntry>();
        foreach (var (map, minutes) in picks)
        {
            var entry = Paced(map);
            entry.Minutes = minutes;
            rotation.Add(entry);
        }
        foreach (var entry in Rotation())
        {
            if (!rotation.Any(x => string.Equals(x.Map, entry.Map, StringComparison.OrdinalIgnoreCase)))
            {
                entry.Enabled = false;
                rotation.Add(entry);
            }
        }
        return new SimPreset
        {
            Name = name,
            NameEn = nameEn,
            Purpose = purpose,
            PurposeEn = PurposeEn.TryGetValue(name, out string? en) ? en : string.Empty,
            BuiltIn = true,
            Rotation = rotation,
            Personalities = personalities,
        };
    }

    private static SimMapEntry Paced(string map, bool enabled = true)
    {
        var entry = new SimMapEntry { Map = map, Minutes = 20f, Enabled = enabled };
        ApplyPace(entry);
        return entry;
    }

    public static void ApplyPace(SimMapEntry entry)
    {
        if (Pace.TryGetValue(entry.Map, out var pace))
        {
            entry.MaxAliveBots = pace.Cap;
            entry.RespawnSeconds = pace.Respawn;
            entry.SquadSizeMin = pace.SquadMin;
            entry.SquadSizeMax = pace.SquadMax;
        }
    }

    public static Dictionary<string, SimScenario> Scenarios()
    {
        return new Dictionary<string, SimScenario>(StringComparer.OrdinalIgnoreCase)
        {
            ["factory4_day"] = new()
            {
                NameEn = "Factory close-quarters brawl",
                FocusEn = "Tight indoor close combat: door tactics, diamond step, close-range reload decisions, corner chasing",
                Name = "공장 근접 난전",
                Focus = "좁은 실내 근접전: 문 전술, 다이아몬드 스텝, 근접 장전 판단, 코너 추격",
                ZonesA = "BotZone",
                ZonesB = "BotZone",
                Watch = "door.,diamond.,reload.,threat.,death.",
            },
            ["factory4_night"] = new()
            {
                NameEn = "Factory night close combat",
                FocusEn = "Dark interior: flashlight on/off, finding by sound, close standoffs",
                Name = "공장 야간 근접전",
                Focus = "어두운 실내: 손전등 켜기/끄기, 소리로 찾기, 근접 대치",
                ZonesA = "BotZone",
                ZonesB = "BotZone",
                Watch = "light.,door.,utility.,death.",
            },
            ["bigmap"] = new()
            {
                NameEn = "Customs dorms assault",
                FocusEn = "A holds the dorms, B comes in from the crossroads: entering a multi-floor building, room clearing, door stacking, stair fights",
                Name = "세관 기숙사 공방",
                Focus = "A는 기숙사(수비), B는 교차로에서 진입: 다층 건물 진입, 방 정리, 문 스택, 계단 교전",
                ZonesA = "ZoneDormitory",
                // ZoneGasStation dropped 2026-10-09 (first sim: "Customs bots spawned in odd places") - its spawn points are ~280m
                // from the dorms (zone centers from SPT 4.1 data: dorms (172,166), crossroad (202,52), gas station (433,56)).
                ZonesB = "ZoneCrossRoad",
                // The crossroad is ~118m of open ground from the dorms: an attractor halfway pulled both sides out there.
                ArenaOnA = true,
                Watch = "door.,squad.,nade.,repo.",
            },
            ["interchange"] = new()
            {
                NameEn = "Interchange mall interior",
                FocusEn = "Goshan vs IDEA side: long indoor sightlines, flanking, repositioning, squad crossfire",
                Name = "인터체인지 몰 내부",
                Focus = "고샨 vs 이케아(IDEA) 쪽: 긴 실내 시야, 측면 우회, 위치 바꾸기, 분대 교차 사격",
                ZonesA = "ZoneGoshan",
                ZonesB = "ZoneIDEA",
                Watch = "repo.,squad.,threat.,weapon.",
            },
            ["rezervbase"] = new()
            {
                NameEn = "Reserve pawn buildings",
                FocusEn = "PTOR1 vs PTOR2 (pawn buildings): stairs and floor-to-floor fights, window peeks, short open ground between buildings",
                Name = "리저브 폰 건물",
                Focus = "PTOR1 vs PTOR2(폰 건물): 계단·층간 교전, 창문 피킹, 건물 사이 짧은 개활지",
                ZonesA = "ZonePTOR1",
                ZonesB = "ZonePTOR2",
                Watch = "door.,lean.,repo.,retreat.",
            },
            ["shoreline"] = new()
            {
                NameEn = "Shoreline resort east vs west wing",
                FocusEn = "Both resort wings: hallway standoffs, grenades at doors, room clearing, long stalemates",
                Name = "해안선 리조트 동관 vs 서관",
                Focus = "리조트 양쪽 건물: 복도 대치, 문 앞 수류탄, 방 정리, 장기 교착",
                ZonesA = "ZoneSanatorium1",
                ZonesB = "ZoneSanatorium2",
                Watch = "door.,nade.,utility.,squad.",
            },
            ["woods"] = new()
            {
                NameEn = "Woods sawmill open ground",
                FocusEn = "Sawmill vs small house: long range, tree/rock cover, sniper swap, retreat zigzag",
                Name = "삼림 제재소 개활지",
                Focus = "제재소 vs 작은 집: 원거리 교전, 나무·바위 엄폐, 저격 무기 교체, 후퇴 지그재그",
                ZonesA = "ZoneWoodCutter",
                ZonesB = "ZoneMiniHouse",
                Watch = "weapon.,retreat.,repo.,suppress.",
            },
            ["lighthouse"] = new()
            {
                NameEn = "Lighthouse village vs chalet mid range",
                FocusEn = "Village vs chalet: mid range, mixed rock/building cover, flanking, relocation",
                Name = "등대 마을·샬레 중거리",
                Focus = "마을 vs 샬레: 중거리, 바위·건물 혼합 엄폐, 측면 이동, 재배치",
                ZonesA = "Zone_Village",
                ZonesB = "Zone_Chalet",
                Watch = "repo.,squad.,retreat.,weapon.",
            },
            ["tarkovstreets"] = new()
            {
                NameEn = "Streets Concordia urban fight",
                FocusEn = "Concordia vs construction site: entering city buildings, windows, stairs, crossing streets (heavy map for frame rate)",
                Name = "시내 콘코디아 도심전",
                Focus = "콘코디아 vs 공사장: 도심 건물 진입, 창문, 계단, 거리 횡단 (프레임 부담 큰 맵)",
                ZonesA = "ZoneConcordia_1",
                ZonesB = "ZoneConstruction",
                Watch = "door.,repo.,squad.,perf.",
            },
            ["sandbox"] = new()
            {
                NameEn = "Ground Zero city blocks",
                FocusEn = "Random spawns over the whole map: fights between city blocks, searching, after-fight behavior",
                Name = "그라운드 제로 도시 블록",
                Focus = "맵 전체 무작위 스폰: 도시 블록 사이 교전, 수색, 교전 후 행동",
                ZonesA = string.Empty,
                ZonesB = string.Empty,
                Watch = "postCombat.,repo.,squad.",
            },
            ["sandbox_high"] = new()
            {
                NameEn = "Ground Zero (21+) city blocks",
                FocusEn = "Both sides in the same area: close/mid range city fight",
                Name = "그라운드 제로 (21+) 도시 블록",
                Focus = "같은 지역 안 양측 스폰: 근중거리 도심전",
                ZonesA = "ZoneSandbox",
                ZonesB = "ZoneSandbox",
                Watch = "postCombat.,repo.,squad.",
            },
            ["laboratory"] = new()
            {
                NameEn = "Lab floor 1 vs floor 2",
                FocusEn = "Indoor halls and stairs: close standoffs, door tactics (auto start without a keycard not checked)",
                Name = "연구소 1층 vs 2층",
                Focus = "실내 복도와 계단: 근접 대치, 문 전술 (출입 카드 없이 자동 시작되는지 미확인)",
                ZonesA = "BotZoneFloor1",
                ZonesB = "BotZoneFloor2",
                Watch = "door.,diamond.,threat.",
            },
            ["labyrinth"] = new()
            {
                NameEn = "Labyrinth close combat",
                FocusEn = "Narrow corridors close combat (entry rules to be checked)",
                Name = "미궁 근접전",
                Focus = "좁은 통로 근접전 (입장 조건 확인 필요)",
                ZonesA = "BotZone",
                ZonesB = "BotZone",
                Watch = "door.,diamond.,stuck.",
            },
        };
    }
}
