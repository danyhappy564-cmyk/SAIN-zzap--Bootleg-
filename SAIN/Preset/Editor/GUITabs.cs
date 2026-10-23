using EFT.UI;
using SAIN.Attributes;
using SAIN.Components;
using SAIN.Editor.GUISections;
using SAIN.Helpers;
using SAIN.Plugin;
using SAIN.Preset;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.GearStealthValues;
using static SAIN.Editor.SAINLayout;

namespace SAIN.Editor;

public static class GUITabs
{
    public static void CreateTabs(EEditorTab selectedTab)
    {
        EditTabsClass.BeginScrollView();
        BeginHorizontal(12f);
        BeginVertical();
        switch (selectedTab)
        {
            case EEditorTab.Quick:
                QuickSettingsGUI.Draw();
                break;

            case EEditorTab.Home:
                Home();
                break;

            case EEditorTab.BotSettings:
                BotSettings();
                break;

            case EEditorTab.Personalities:
                Personality();
                break;

            case EEditorTab.EquipmentStealth:
                Stealth();
                break;

            case EEditorTab.Advanced:
                Advanced();
                break;

            default:
                break;
        }
        EndVertical();
        EndHorizontal(12f);
        EditTabsClass.EndScrollView();
    }

    private static bool _presetManagerOpen;

    public static void Home()
    {
        // zzap fork (2026-10-18): presets are picked on the Quick Settings tab; the old manager (rename, description, delete,
        // save as new) stays here folded.
        _presetManagerOpen = BuilderClass.ExpandableMenu(
            EditorText.UI("Preset management (rename, description, delete)"),
            _presetManagerOpen,
            EditorText.UI("Presets are picked on the Quick Settings tab. Here: rename, change the description, save as new, delete."),
            28f
        );
        if (_presetManagerOpen)
        {
            PresetSelection.PresetSelectionMenu();
        }
        Space(8f);

        BotSettingsEditor.ShowAllSettingsGUI(
            SAINPlugin.LoadedPreset.GlobalSettings,
            out bool newEdit,
            "Global Settings",
            string.Format(EditorText.UI("Server preset '{0}'"), SAINPlugin.LoadedPreset.Info.Name),
            35f,
            out bool saved
        );

        if (saved)
        {
            SAINPresetClass.ExportAll(SAINPlugin.LoadedPreset);
            ConfigEditingTracker.Clear();
        }
    }

    public static void BotSettings()
    {
        BotSelectionClass.Menu();
    }

    public static void Personality()
    {
        BotPersonalityEditor.PersonalityMenu();
    }

    private static EEquipmentType? _stealthType;
    private static UnityEngine.Vector2 _stealthScroll;

    // zzap fork (2026-10-18, mockup 5): equipment types on the left, that type's items on the right (was two fold-out rows).
    private static void Stealth()
    {
        ZzapUI.EnsureStyles();
        var stealth = SAINPlugin.LoadedPreset.GearStealthValuesClass;
        var types = new System.Collections.Generic.List<EEquipmentType>();
        foreach (var type in EnumValues.GetEnum<EEquipmentType>())
        {
            if (stealth.ItemStealthValues.ContainsKey(type))
            {
                types.Add(type);
            }
        }
        if (types.Count == 0)
        {
            Label(EditorText.UI("No equipment stealth values in this preset."), ZzapUI.Small);
            return;
        }
        if (!_stealthType.HasValue || !types.Contains(_stealthType.Value))
        {
            _stealthType = types[0];
        }
        EEquipmentType current = _stealthType.Value;

        BeginHorizontal();
        UnityEngine.GUILayout.BeginVertical(ZzapUI.Panel1, Width(ZzapUI.SIDE_WIDTH), Height(ZzapUI.PANEL_HEIGHT));
        Label(EditorText.UI("EQUIPMENT"), ZzapUI.Section);
        Label(EditorText.UI("Modify the stealth value that certain pieces of equipment provide."), ZzapUI.Small);
        Space(6f);
        foreach (var type in types)
        {
            var captured = type;
            if (ZzapUI.ListItem(EditorText.Value(type), stealth.ItemStealthValues[type].Count.ToString(), type == current))
            {
                ZzapUI.Defer(() =>
                {
                    _stealthType = captured;
                    _stealthScroll = UnityEngine.Vector2.zero;
                });
            }
        }
        FlexibleSpace();
        UnityEngine.GUILayout.EndVertical();

        Space(12f);

        UnityEngine.GUILayout.BeginVertical(ZzapUI.Panel1, Height(ZzapUI.PANEL_HEIGHT), UnityEngine.GUILayout.ExpandWidth(true));
        ZzapUI.PanelHeading(EditorText.Value(current), EditorText.UI("Modify the stealth value that certain pieces of equipment provide."));
        _stealthScroll = BeginScrollView(_stealthScroll, UnityEngine.GUILayout.ExpandHeight(true));
        GearQuickGUI.Draw(current);
        EndScrollView();
        UnityEngine.GUILayout.EndVertical();
        EndHorizontal();
    }

    private static void ForceDecisions(int spacing)
    {
        Space(spacing);

        _forceDecisionMenuOpen = BuilderClass.ExpandableMenu(EditorText.UI("Force SAIN Bot Decisions"), _forceDecisionMenuOpen);
        if (_forceDecisionMenuOpen)
        {
            Space(spacing);

            ForceSoloOpen = BuilderClass.ExpandableMenu(EditorText.UI("Force Solo Decision"), ForceSoloOpen);
            if (ForceSoloOpen)
            {
                Space(spacing / 2f);

                if (Button(EditorText.UI("Reset")))
                {
                    SAINPlugin.ForceSoloDecision = ECombatDecision.None;
                }

                Space(spacing / 2f);

                SAINPlugin.ForceSoloDecision = BuilderClass.SelectionGrid(
                    SAINPlugin.ForceSoloDecision,
                    EnumValues.GetEnum<ECombatDecision>()
                );
            }

            Space(spacing);

            ForceSquadOpen = BuilderClass.ExpandableMenu(EditorText.UI("Force Squad Decision"), ForceSquadOpen);
            if (ForceSquadOpen)
            {
                Space(spacing / 2f);

                if (Button(EditorText.UI("Reset")))
                {
                    SAINPlugin.ForceSquadDecision = ESquadDecision.None;
                }

                Space(spacing / 2f);

                SAINPlugin.ForceSquadDecision = BuilderClass.SelectionGrid(
                    SAINPlugin.ForceSquadDecision,
                    EnumValues.GetEnum<ESquadDecision>()
                );
            }

            Space(spacing);

            ForceSelfOpen = BuilderClass.ExpandableMenu(EditorText.UI("Force Self Decision"), ForceSelfOpen);
            if (ForceSelfOpen)
            {
                Space(spacing / 2f);

                if (Button(EditorText.UI("Reset")))
                {
                    SAINPlugin.ForceSelfDecision = ESelfActionType.None;
                }

                Space(spacing / 2f);

                SAINPlugin.ForceSelfDecision = BuilderClass.SelectionGrid(
                    SAINPlugin.ForceSelfDecision,
                    EnumValues.GetEnum<ESelfActionType>()
                );
            }
        }
    }

    public static void Advanced()
    {
        ZzapUI.EnsureStyles();
        UnityEngine.GUILayout.BeginVertical(ZzapUI.Panel1);
        ZzapUI.PanelHeading(EditorText.UI("F6 editor settings"), EditorText.UI("How this editor looks and behaves (saved on this PC, not in the preset)."));
        AttributesGUI.EditAllValuesInObj(PresetHandler.EditorDefaults, out bool newEdit);
        UnityEngine.GUILayout.EndVertical();
        if (newEdit)
        {
            PresetHandler.ExportEditorDefaults();
        }

        if (!SAINPlugin.DebugMode)
        {
            return;
        }

        const int spacing = 4;
        ForceDecisions(spacing);
        ForceTalk(spacing);
    }

    private static void ForceTalk(int spacing)
    {
        Space(spacing);
        _forceTalkMenuOpen = BuilderClass.ExpandableMenu(EditorText.UI("Force Bots to Say Phrase"), _forceTalkMenuOpen);
        if (_forceTalkMenuOpen)
        {
            Space(5);
            _forceTagStatusToggle = Toggle(_forceTagStatusToggle, EditorText.UI("Force ETagStatus for Phrase"));
            if (_forceTagStatusToggle)
            {
                ETagStatus[] statuses = EnumValues.GetEnum<ETagStatus>();
                for (int i = 0; i < statuses.Length; i++)
                {
                    if (Toggle(_forcedTagStatus == statuses[i], statuses[i].ToString()))
                    {
                        if (_forcedTagStatus != statuses[i])
                        {
                            _forcedTagStatus = statuses[i];
                        }
                    }
                }
            }
            Space(5);
            _withGroupDelay = Toggle(_withGroupDelay, EditorText.UI("With Group Delay?"));
            Space(5);
            Label(EditorText.UI("Say Phrase"));
            EPhraseTrigger[] triggers = EnumValues.GetEnum<EPhraseTrigger>();
            for (int i = 0; i < triggers.Length; i++)
            {
                if (Button(triggers[i].ToString()))
                {
                    if (BotManagerComponent.Instance?.Bots != null)
                    {
                        foreach (var bot in BotManagerComponent.Instance.Bots.Values)
                        {
                            if (bot != null)
                            {
                                if (_forceTagStatusToggle)
                                {
                                    bot.Talk.Say(triggers[i], _forcedTagStatus, _withGroupDelay);
                                }
                                else
                                {
                                    bot.Talk.Say(triggers[i], null, _withGroupDelay);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private static bool _withGroupDelay;
    private static bool _forceTagStatusToggle;
    private static ETagStatus _forcedTagStatus;
    private static bool _forceTalkMenuOpen;
    private static bool _forceDecisionMenuOpen;
    private static bool ForceSoloOpen;
    private static bool ForceSquadOpen;
    private static bool ForceSelfOpen;
}
