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

    public static string NameOf(string map)
    {
        return MapNames.TryGetValue(map, out string? name) ? name : map;
    }

    /// <summary>Default order: small/indoor first, open maps later, heavy Streets near the end.</summary>
    public static List<SimMapEntry> Rotation()
    {
        return
        [
            new SimMapEntry { Map = "factory4_day", Minutes = 20f },
            new SimMapEntry { Map = "bigmap", Minutes = 20f },
            new SimMapEntry { Map = "interchange", Minutes = 20f },
            new SimMapEntry { Map = "rezervbase", Minutes = 20f },
            new SimMapEntry { Map = "shoreline", Minutes = 20f },
            new SimMapEntry { Map = "woods", Minutes = 20f },
            new SimMapEntry { Map = "lighthouse", Minutes = 20f },
            new SimMapEntry { Map = "tarkovstreets", Minutes = 20f },
            new SimMapEntry { Map = "sandbox", Minutes = 20f },
            new SimMapEntry { Map = "factory4_night", Minutes = 20f, Enabled = false },
            new SimMapEntry { Map = "sandbox_high", Minutes = 20f, Enabled = false },
            new SimMapEntry { Map = "laboratory", Minutes = 20f, Enabled = false },
            new SimMapEntry { Map = "labyrinth", Minutes = 20f, Enabled = false },
        ];
    }

    public static Dictionary<string, SimScenario> Scenarios()
    {
        return new Dictionary<string, SimScenario>(StringComparer.OrdinalIgnoreCase)
        {
            ["factory4_day"] = new()
            {
                Name = "공장 근접 난전",
                Focus = "좁은 실내 근접전: 문 전술, 다이아몬드 스텝, 근접 장전 판단, 코너 추격",
                ZonesA = "BotZone",
                ZonesB = "BotZone",
                Watch = "door.,diamond.,reload.,threat.,death.",
            },
            ["factory4_night"] = new()
            {
                Name = "공장 야간 근접전",
                Focus = "어두운 실내: 손전등 켜기/끄기, 소리로 찾기, 근접 대치",
                ZonesA = "BotZone",
                ZonesB = "BotZone",
                Watch = "light.,door.,utility.,death.",
            },
            ["bigmap"] = new()
            {
                Name = "세관 기숙사 공방",
                Focus = "A는 기숙사(수비), B는 주유소·교차로에서 진입: 다층 건물 진입, 방 정리, 문 스택, 계단 교전",
                ZonesA = "ZoneDormitory",
                ZonesB = "ZoneGasStation,ZoneCrossRoad",
                Watch = "door.,squad.,nade.,repo.",
            },
            ["interchange"] = new()
            {
                Name = "인터체인지 몰 내부",
                Focus = "고샨 vs 이케아(IDEA) 쪽: 긴 실내 시야, 측면 우회, 위치 바꾸기, 분대 교차 사격",
                ZonesA = "ZoneGoshan",
                ZonesB = "ZoneIDEA",
                Watch = "repo.,squad.,threat.,weapon.",
            },
            ["rezervbase"] = new()
            {
                Name = "리저브 폰 건물",
                Focus = "PTOR1 vs PTOR2(폰 건물): 계단·층간 교전, 창문 피킹, 건물 사이 짧은 개활지",
                ZonesA = "ZonePTOR1",
                ZonesB = "ZonePTOR2",
                Watch = "door.,lean.,repo.,retreat.",
            },
            ["shoreline"] = new()
            {
                Name = "해안선 리조트 동관 vs 서관",
                Focus = "리조트 양쪽 건물: 복도 대치, 문 앞 수류탄, 방 정리, 장기 교착",
                ZonesA = "ZoneSanatorium1",
                ZonesB = "ZoneSanatorium2",
                Watch = "door.,nade.,utility.,squad.",
            },
            ["woods"] = new()
            {
                Name = "삼림 제재소 개활지",
                Focus = "제재소 vs 작은 집: 원거리 교전, 나무·바위 엄폐, 저격 무기 교체, 후퇴 지그재그",
                ZonesA = "ZoneWoodCutter",
                ZonesB = "ZoneMiniHouse",
                Watch = "weapon.,retreat.,repo.,suppress.",
            },
            ["lighthouse"] = new()
            {
                Name = "등대 마을·샬레 중거리",
                Focus = "마을 vs 샬레: 중거리, 바위·건물 혼합 엄폐, 측면 이동, 재배치",
                ZonesA = "Zone_Village",
                ZonesB = "Zone_Chalet",
                Watch = "repo.,squad.,retreat.,weapon.",
            },
            ["tarkovstreets"] = new()
            {
                Name = "시내 콘코디아 도심전",
                Focus = "콘코디아 vs 공사장: 도심 건물 진입, 창문, 계단, 거리 횡단 (프레임 부담 큰 맵)",
                ZonesA = "ZoneConcordia_1",
                ZonesB = "ZoneConstruction",
                Watch = "door.,repo.,squad.,perf.",
            },
            ["sandbox"] = new()
            {
                Name = "그라운드 제로 도시 블록",
                Focus = "맵 전체 무작위 스폰: 도시 블록 사이 교전, 수색, 교전 후 행동",
                ZonesA = string.Empty,
                ZonesB = string.Empty,
                Watch = "postCombat.,repo.,squad.",
            },
            ["sandbox_high"] = new()
            {
                Name = "그라운드 제로 (21+) 도시 블록",
                Focus = "같은 지역 안 양측 스폰: 근중거리 도심전",
                ZonesA = "ZoneSandbox",
                ZonesB = "ZoneSandbox",
                Watch = "postCombat.,repo.,squad.",
            },
            ["laboratory"] = new()
            {
                Name = "연구소 1층 vs 2층",
                Focus = "실내 복도와 계단: 근접 대치, 문 전술 (출입 카드 없이 자동 시작되는지 미확인)",
                ZonesA = "BotZoneFloor1",
                ZonesB = "BotZoneFloor2",
                Watch = "door.,diamond.,threat.",
            },
            ["labyrinth"] = new()
            {
                Name = "미궁 근접전",
                Focus = "좁은 통로 근접전 (입장 조건 확인 필요)",
                ZonesA = "BotZone",
                ZonesB = "BotZone",
                Watch = "door.,diamond.,stuck.",
            },
        };
    }
}
