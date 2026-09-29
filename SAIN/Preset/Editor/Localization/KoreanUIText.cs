using System.Collections.Generic;

namespace SAIN.Editor;

/// <summary>Korean text for the F6 editor's own buttons, tabs and labels (English → Korean).</summary>
internal static class KoreanUIText
{
    public static readonly Dictionary<string, string> Map = new()
    {
        // Window / top bar
        { "SAIN AI Settings Editor", "SAIN AI 설정 에디터" },
        { "GUI Editor", "설정 에디터" },
        { "Preset", "프리셋" },
        { "Save All Changes", "모든 변경 저장" },
        { "Save all changes to the SAIN server mod for preset '{0}'", "프리셋 '{0}'의 모든 변경을 SAIN 서버 모드에 저장" },
        { "Advanced Settings: [{0}]", "고급 설정: [{0}]" },
        { "ON", "켬" },
        { "OFF", "끔" },
        { "Language: Korean / English", "언어: 한국어 / 영어" },

        // Tabs
        { "Home", "홈" },
        { "Select preset and modify global SAIN settings.", "프리셋을 고르고 SAIN 전역 설정을 수정합니다." },
        { "Bot Settings", "봇 설정" },
        {
            "Modify Settings that are unique to particular bot types for individual difficulties. Difficulty is determined on spawn by EFT, and is changed by selecting the Difficulty value when starting a raid. As Online is a mix of all difficulties.",
            "봇 종류별·난이도별 고유 설정을 수정합니다. 난이도는 봇이 생성될 때 EFT가 정하고, 레이드 시작 시 난이도 선택으로 바뀝니다. '온라인'은 모든 난이도가 섞여 있습니다."
        },
        { "Personalities", "성격" },
        {
            "Modify Individual Personality settings for how they are assigned to bots, and what each personality does for a bot's behavior.",
            "성격이 봇에게 배정되는 조건과, 각 성격이 봇 행동에 주는 영향을 수정합니다."
        },
        { "Equipment Stealth", "장비 은신" },
        { "Modify the stealth value that certain pieces of equipment provide.", "장비별 은신 값을 수정합니다." },
        { "Advanced Options", "고급 옵션" },
        { "Edit at your own risk. Enable additional advanced config options here", "수정은 본인 책임입니다. 추가 고급 설정을 여기서 켭니다" },

        // Save / unsaved
        { "Save and Export", "저장 및 내보내기" },
        { "Click Save to export changes, and send changes to bots if in-game", "저장을 누르면 변경을 내보내고, 레이드 중이면 봇에게도 바로 적용합니다" },
        { "YOU HAVE UNSAVED CHANGES!", "저장 안 된 변경이 있습니다!" },
        { "YOU HAVE UNSAVED CHANGES", "저장 안 된 변경이 있습니다" },

        // Search / common entry controls
        { "Search", "검색" },
        { "Clear", "지우기" },
        { "Clear Selected Options in this Menu", "이 메뉴에서 선택한 항목 지우기" },
        { "Reset", "초기화" },
        { "Reset To Default Value", "기본값으로 되돌리기" },
        { "No Default Value is assigned to this option.", "이 항목에는 기본값이 없습니다." },
        { "On", "켬" },
        { "Off", "끔" },
        { "Advanced", "고급" },
        { "Developer", "개발자" },
        { "Minimum", "최소값" },
        { "Maximum", "최대값" },
        { "The Rounding this option is set to", "이 항목의 반올림 단위" },
        { "Collapse", "접기" },
        { "Expand", "펼치기" },
        { "None", "없음" },
        { "Category", "분류" },
        { "Suppression State", "제압 상태" },
        { "Boss Personality", "보스 성격" },
        { "The Stealth Value for {0}", "{0}의 은신 값" },
        { "Select Options to Edit", "수정할 항목 선택" },

        // Hearing dispersion editor
        { "DistanceModifier", "거리 오차 배율" },
        { "MinAngle", "최소 각도" },
        { "MaxAngle", "최대 각도" },
        { "VerticalModifier", "수직 오차 배율" },
        { "How much to randomize the distance that a bot thinks a sound originated from.", "봇이 생각하는 소리 발생 위치의 거리를 얼마나 무작위로 흩뜨릴지." },

        // Home tab: server preset / force decisions / talk
        { "Global Settings", "전역 설정" },
        { "Server preset '{0}'", "서버 프리셋 '{0}'" },
        { "Force SAIN Bot Decisions", "SAIN 봇 판단 강제" },
        { "Force Solo Decision", "개인 판단 강제" },
        { "Force Squad Decision", "분대 판단 강제" },
        { "Force Self Decision", "자기 행동 판단 강제" },
        { "Force Bots to Say Phrase", "봇에게 대사 강제" },
        { "Force ETagStatus for Phrase", "대사에 ETagStatus 강제" },
        { "With Group Delay?", "분대 지연 적용?" },
        { "Say Phrase", "대사 말하기" },

        // Bot / personality selection
        { "Apply Values set below to selected Bot Type. Saves edited values to the SAIN server for preset '{0}'", "아래 값을 선택한 봇 종류에 적용합니다. 수정한 값을 프리셋 '{0}'로 SAIN 서버에 저장합니다" },
        { "Apply Values set below to Personalities. Saves edited values to the SPT server for preset '{0}'", "아래 값을 성격에 적용합니다. 수정한 값을 프리셋 '{0}'로 SPT 서버에 저장합니다" },
        { "Clear Bot Types", "봇 종류 선택 해제" },
        { "Clear all selected bot types", "선택한 봇 종류 전부 해제" },
        { "Difficulties", "난이도" },
        { "Select which difficulties you wish to modify.", "수정할 난이도를 고르세요." },
        { "Clear Difficulties", "난이도 선택 해제" },
        { "Clear all selected difficulties", "선택한 난이도 전부 해제" },
        { "No Bot Types Selected, please select at least one above.", "선택한 봇 종류가 없습니다. 위에서 하나 이상 고르세요." },
        { "No Bot Difficulties Selected, please select at least one above.", "선택한 난이도가 없습니다. 위에서 하나 이상 고르세요." },

        // Preset selection
        { "The server has forced the preset '{0}'.", "서버가 프리셋 '{0}'을(를) 강제했습니다." },
        { "SAIN presets are disabled by the server.", "서버에서 SAIN 프리셋이 꺼져 있습니다." },
        { "Preset selection is locked.", "프리셋 선택이 잠겨 있습니다." },
        {
            "Warning: The selected preset version is: [{0}], but current SAIN preset version is: [{1}] (SAIN version [{2}]), default bot config values may be set incorrectly due to updates to SAIN. THIS DOESN'T MEAN YOUR GAME IS BROKEN, just be aware bots might not act as intended.",
            "경고: 선택한 프리셋 버전은 [{0}]인데 현재 SAIN 프리셋 버전은 [{1}](SAIN 버전 [{2}])입니다. SAIN 업데이트 때문에 봇 기본 설정값이 맞지 않을 수 있습니다. 게임이 고장 났다는 뜻은 아니고, 봇이 의도대로 안 움직일 수 있다는 정도로만 알아두세요."
        },
        { "Presets", "프리셋" },
        { "Select an Installed preset for SAIN Settings", "SAIN 설정에 쓸 설치된 프리셋을 고르세요" },
        { "Refresh", "새로고침" },
        { "Refresh installed Presets", "설치된 프리셋 새로고침" },
        { "Create New Preset", "새 프리셋 만들기" },
        { "Default Presets", "기본 프리셋" },
        { "Custom Presets", "사용자 프리셋" },
        { "Save Info", "정보 저장" },
        { "Update the selected presets name, description, and creator.", "선택한 프리셋의 이름·설명·제작자를 갱신합니다." },
        { "Save A New Preset", "새 프리셋 저장" },
        { "Name", "이름" },
        { "Description", "설명" },
        { "Creator", "제작자" },
        { "Delete Selected Preset", "선택한 프리셋 삭제" },
        { "Are you Sure?", "정말 삭제할까요?" },
        { "CONFIRM DELETE OF {0} ?", "{0} 삭제 확정?" },
    };
}
