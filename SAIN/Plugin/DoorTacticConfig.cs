using BepInEx.Configuration;

namespace SAIN.Plugin;

/// <summary>
/// F12 toggles for the door tactics (DoorTacticClass). Kept in the BepInEx config rather than the
/// SAIN preset so presets (and preset mods built on them) don't need a schema change.
/// </summary>
internal static class DoorTacticConfig
{
    public static ConfigEntry<bool> Enabled { get; private set; }
    public static ConfigEntry<bool> JumpPeek { get; private set; }
    public static ConfigEntry<bool> FakeGrenade { get; private set; }
    public static ConfigEntry<bool> FakeHeal { get; private set; }
    public static ConfigEntry<bool> RoomTrap { get; private set; }
    public static ConfigEntry<bool> DoorGrenade { get; private set; }
    public static ConfigEntry<float> ChanceMultiplier { get; private set; }
    public static ConfigEntry<bool> DiagnosticLogs { get; private set; }
    public static ConfigEntry<bool> VerboseLogs { get; private set; }

    public static void Bind(ConfigFile config)
    {
        const string category = "Door Tactics (zzap)";
        Enabled = config.Bind(category, "Enabled", true, "Master switch for all door tactics below.");
        JumpPeek = config.Bind(
            category,
            "Jump Peek",
            true,
            "GigaChad/Chad: stack beside an open doorway, jump across it to look into the room, then step back."
        );
        FakeGrenade = config.Bind(
            category,
            "Fake Grenade",
            true,
            "GigaChad/Chad: after a peek, draw a grenade (the draw is audible) and put it away again, then hold the doorway."
        );
        FakeHeal = config.Bind(
            category,
            "Fake Heal",
            true,
            "GigaChad/Chad, only when actually hurt: start a heal (audible) next to the door and cancel it after ~1.5s to bait a push."
        );
        RoomTrap = config.Bind(
            category,
            "Room Trap",
            true,
            "GigaChad/SnappingTurtle close the door on an enemy in a room and hold it; Rat holds beside the door silently."
        );
        DoorGrenade = config.Bind(
            category,
            "Door Grenade",
            true,
            "GigaChad: after closing the door, back off and throw a grenade at the door so it goes off when the enemy comes out."
        );
        ChanceMultiplier = config.Bind(
            category,
            "Chance Multiplier",
            1f,
            new ConfigDescription(
                "Multiplies every per-personality chance to start a door tactic.",
                new AcceptableValueRange<float>(0f, 3f)
            )
        );
        DiagnosticLogs = config.Bind(
            category,
            "Diagnostic Logs",
            true,
            "Writes [DoorTactic] lines to LogOutput.log: every tactic start, step change and result."
        );
        VerboseLogs = config.Bind(
            category,
            "Verbose Logs",
            false,
            "Also logs why a nearby door was NOT used (rate limited). Noisy, for debugging only."
        );
    }
}
