using System.Collections.Generic;

namespace SAIN.Editor;

/// <summary>
/// Korean labels for option values shown in F6 (enum type name + "." + value → Korean). Keyed by type so the same
/// word (None, Default, Far...) can read differently per list. Personalities, bot types and decisions are left
/// out on purpose: they stay English to match the logs and README.
/// </summary>
public static class KoreanValueText
{
    public static readonly Dictionary<string, string> Map = new()
    {
        // Personality > Search > Heard From Peace Behavior
        { "EHeardFromPeaceBehavior.None", "기본 반응" },
        { "EHeardFromPeaceBehavior.Freeze", "얼음(매복)" },
        { "EHeardFromPeaceBehavior.SearchNow", "바로 수색" },
        { "EHeardFromPeaceBehavior.Charge", "돌격" },

        // Weapon classes
        { "EWeaponClass.Default", "기본" },
        { "EWeaponClass.assaultRifle", "돌격소총" },
        { "EWeaponClass.assaultCarbine", "돌격 카빈" },
        { "EWeaponClass.machinegun", "기관총" },
        { "EWeaponClass.smg", "SMG(기관단총)" },
        { "EWeaponClass.pistol", "권총" },
        { "EWeaponClass.marksmanRifle", "지정사수소총(DMR)" },
        { "EWeaponClass.sniperRifle", "저격소총" },
        { "EWeaponClass.shotgun", "샷건" },
        { "EWeaponClass.grenadeLauncher", "유탄발사기" },
        { "EWeaponClass.specialWeapon", "특수 무기" },

        // Sound types
        { "SAINSoundType.None", "없음" },
        { "SAINSoundType.Generic", "일반 소리" },
        { "SAINSoundType.FootStep", "발소리" },
        { "SAINSoundType.Sprint", "전력 질주" },
        { "SAINSoundType.Prone", "엎드리기" },
        { "SAINSoundType.Looting", "루팅" },
        { "SAINSoundType.Reload", "재장전" },
        { "SAINSoundType.GearSound", "장비 소리" },
        { "SAINSoundType.GrenadePin", "수류탄 핀 뽑기" },
        { "SAINSoundType.GrenadeExplosion", "수류탄 폭발" },
        { "SAINSoundType.GrenadeDraw", "수류탄 꺼내기" },
        { "SAINSoundType.Jump", "점프" },
        { "SAINSoundType.Door", "문 여닫기" },
        { "SAINSoundType.DoorBreach", "문 부수기/차기" },
        { "SAINSoundType.Shot", "총소리" },
        { "SAINSoundType.SuppressedShot", "소음기 총소리" },
        { "SAINSoundType.Heal", "치료" },
        { "SAINSoundType.Food", "먹고 마시기" },
        { "SAINSoundType.Conversation", "대화" },
        { "SAINSoundType.Surgery", "수술" },
        { "SAINSoundType.DryFire", "빈 총 격발" },
        { "SAINSoundType.TurnSound", "방향 전환" },
        { "SAINSoundType.Breathing", "숨소리" },
        { "SAINSoundType.Pain", "신음" },
        { "SAINSoundType.Bush", "수풀" },
        { "SAINSoundType.BulletImpact", "탄 착탄" },
        { "SAINSoundType.Land", "착지" },

        // Sound dispersion types (hearing position error)
        { "ESoundDispersionType.None", "없음" },
        { "ESoundDispersionType.Footstep", "발소리" },
        { "ESoundDispersionType.HeardShot", "들린 총소리" },
        { "ESoundDispersionType.UnheardShot", "안 들린 총격(탄 스침)" },
        { "ESoundDispersionType.HeardSuppressedShot", "들린 소음기 총소리" },
        { "ESoundDispersionType.UnheardSuppressedShot", "안 들린 소음기 총격(탄 스침)" },

        // AI limit tiers (distance from the nearest human player)
        { "AILimitSetting.None", "제한 없음" },
        { "AILimitSetting.Far", "멂" },
        { "AILimitSetting.VeryFar", "아주 멂" },
        { "AILimitSetting.Narnia", "까마득히 멂" },

        // Suppression states
        { "ESuppressionState.None", "없음" },
        { "ESuppressionState.Light", "약함" },
        { "ESuppressionState.Medium", "보통" },
        { "ESuppressionState.Heavy", "강함" },
        { "ESuppressionState.Extreme", "극심" },

        // Equipment stealth
        { "EEquipmentType.Headwear", "머리 장비" },
        { "EEquipmentType.FaceCover", "얼굴 가리개" },
        { "EEquipmentType.BackPack", "배낭" },
        { "EEquipmentType.EyeWear", "안경" },
        { "EEquipmentType.ArmorVest", "방탄복" },
        { "EEquipmentType.Rig", "리그(조끼)" },

        // Maps
        { "ELocation.None", "없음" },
        { "ELocation.Factory", "팩토리" },
        { "ELocation.FactoryNight", "팩토리(야간)" },
        { "ELocation.Customs", "세관" },
        { "ELocation.GroundZero", "그라운드 제로" },
        { "ELocation.Reserve", "리저브" },
        { "ELocation.Streets", "스트리트 오브 타르코프" },
        { "ELocation.Lighthouse", "라이트하우스" },
        { "ELocation.Shoreline", "쇼어라인" },
        { "ELocation.Labs", "랩" },
        { "ELocation.Woods", "우즈" },
        { "ELocation.Interchange", "인터체인지" },
        { "ELocation.Terminal", "터미널" },
        { "ELocation.Town", "타운" },
        { "ELocation.Labyrinth", "미궁(Labyrinth)" },

        // Bot look modes
        { "EBotLookMode.Peace", "평화" },
        { "EBotLookMode.Combat", "교전" },
        { "EBotLookMode.CombatSprint", "교전 중 질주" },
        { "EBotLookMode.CombatVisibleEnemy", "교전 - 적 보임" },
        { "EBotLookMode.Aiming", "조준 중" },
        { "EBotLookMode.RandomLook", "두리번" },

        // Bot difficulties (Bot Settings tab)
        { "BotDifficulty.easy", "쉬움" },
        { "BotDifficulty.normal", "보통" },
        { "BotDifficulty.hard", "어려움" },
        { "BotDifficulty.impossible", "불가능" },
        { "ESainBotDifficulty.easy", "쉬움" },
        { "ESainBotDifficulty.normal", "보통" },
        { "ESainBotDifficulty.hard", "어려움" },
        { "ESainBotDifficulty.impossible", "불가능" },
    };
}
