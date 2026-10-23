using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using EFT.UI;
using SAIN.Attributes;
using SAIN.Plugin;
using SAIN.Preset.Shared.GlobalSettings;
using UnityEngine;
using static SAIN.Editor.SAINLayout;

namespace SAIN.Editor.GUISections;

public static class BotSettingsEditor
{
    private static readonly StringBuilder _stringBuilder = new();

    // zzap fork (2026-10-18, mockup 5): left panel = search box + group list, right panel = the picked group (or the search hits).
    // Save lives in the header now, so the old top row (name box, search label, red "unsaved" bar, second Save button) is gone.
    public static void ShowAllSettingsGUI(object settings, out bool wasEdited, string name, string savePath, float height, out bool Saved)
    {
        Saved = false;
        ZzapUI.EnsureStyles();
        var container = SettingsContainers.GetContainer(settings.GetType(), name);
        GroupedView(container, settings, out wasEdited);
    }

    private sealed class GroupEntry
    {
        public string Key;
        public Category Category;
        public FieldInfo SubField;
        public ConfigInfoClass SubInfo;
        public int Count;
        public int Depth;
    }

    private static readonly Dictionary<SettingsContainer, List<GroupEntry>> _groups = new();
    private static readonly Dictionary<SettingsContainer, string> _selectedGroup = new();

    // Top-level groups, plus the zzap sections inside "General" (door tactics, close combat, freeze ambush...) as their own rows.
    private static List<GroupEntry> Groups(SettingsContainer container)
    {
        if (_groups.TryGetValue(container, out var list))
        {
            return list;
        }
        list = new List<GroupEntry>();
        foreach (var category in container.Categories)
        {
            if (category.OptionCount(out _) == 0)
            {
                continue;
            }
            list.Add(new GroupEntry { Key = category.CategoryInfo.EnglishName, Category = category, Count = category.OptionCount(out _) });
            foreach (FieldInfo field in category.CategoryInfo.ValueType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!typeof(ISAINSettings).IsAssignableFrom(field.FieldType))
                {
                    continue;
                }
                var info = AttributesGUI.GetAttributeInfo(field);
                if (info == null || info.Hidden)
                {
                    continue;
                }
                var sub = new Category(info);
                list.Add(new GroupEntry
                {
                    Key = category.CategoryInfo.EnglishName + "/" + field.Name,
                    Category = category,
                    SubField = field,
                    SubInfo = info,
                    Count = sub.OptionCount(out _),
                    Depth = 1,
                });
            }
        }
        _groups[container] = list;
        return list;
    }

    private static readonly Dictionary<SettingsContainer, UnityEngine.Vector2> _listScroll = new();

    private static void GroupedView(SettingsContainer container, object settings, out bool wasEdited)
    {
        wasEdited = false;
        var groups = Groups(container);
        if (groups.Count == 0)
        {
            return;
        }
        if (!_selectedGroup.TryGetValue(container, out string selectedKey) || groups.All(g => g.Key != selectedKey))
        {
            selectedKey = groups[0].Key;
            _selectedGroup[container] = selectedKey;
        }
        // What the right panel shows is decided before this frame's clicks/typing (they apply next frame, see ZzapUI.Defer).
        string search = container.SearchPattern ?? string.Empty;
        bool searching = search.Length > 0;

        BeginHorizontal();

        GUILayout.BeginVertical(ZzapUI.Panel1, Width(ZzapUI.SIDE_WIDTH), Height(ZzapUI.PANEL_HEIGHT));
        container.SearchPattern = ZzapUI.Search(container.SearchPattern, EditorText.UI("Search settings (Korean or English)"));
        Space(8f);
        _listScroll.TryGetValue(container, out var listScroll);
        _listScroll[container] = BeginScrollView(listScroll, GUILayout.ExpandHeight(true));
        foreach (var group in groups)
        {
            if (group.SubInfo != null && group.SubInfo.DoNotShowGUI)
            {
                continue;
            }
            string label = group.SubInfo != null ? group.SubInfo.Name : group.Category.CategoryInfo.Name;
            string tip = group.SubInfo?.Description ?? group.Category.CategoryInfo.Description;
            bool selected = !searching && group.Key == selectedKey;
            if (ZzapUI.ListItem(label, group.Count.ToString(), selected, tip, group.Depth * 16f, 30f))
            {
                string key = group.Key;
                ZzapUI.Defer(() =>
                {
                    _selectedGroup[container] = key;
                    container.SearchPattern = string.Empty;
                    container.Scroll = UnityEngine.Vector2.zero;
                });
            }
        }
        EndScrollView();
        GUILayout.EndVertical();

        Space(12f);

        GUILayout.BeginVertical(ZzapUI.Panel1, Height(ZzapUI.PANEL_HEIGHT), GUILayout.ExpandWidth(true));
        if (searching)
        {
            ZzapUI.PanelHeading(string.Format(EditorText.UI("Search: '{0}'"), search), EditorText.UI("Every setting whose name or description matches. Clear the box to go back to the groups."));
            container.Scroll = BeginScrollView(container.Scroll, GUILayout.ExpandHeight(true));
            CategoryOpenable(container.Categories, settings, out wasEdited, search.ToLowerInvariant());
            EndScrollView();
        }
        else
        {
            var current = groups.First(g => g.Key == selectedKey);
            string title = current.SubInfo != null ? $"{current.Category.CategoryInfo.Name} › {current.SubInfo.Name}" : current.Category.CategoryInfo.Name;
            ZzapUI.PanelHeading(title, current.SubInfo?.Description ?? current.Category.CategoryInfo.Description);
            container.Scroll = BeginScrollView(container.Scroll, GUILayout.ExpandHeight(true));
            object categoryObject = current.Category.GetValue(settings);
            var baseline = QuickObjectGUI.Saved()?.GlobalSettings;
            object savedObject = baseline == null ? null : current.Category.GetValue(baseline);
            object quickObject = categoryObject;
            if (current.SubField != null)
            {
                quickObject = current.SubField.GetValue(categoryObject);
                savedObject = savedObject == null ? null : current.SubField.GetValue(savedObject);
            }
            QuickObjectGUI.Draw(quickObject, savedObject, current.Key);
            if (quickObject is SAIN.Preset.Shared.GlobalSettings.Categories.General.GeneralSettings general)
            {
                GUILayout.Label(EditorText.Korean ? "바닐라 묶음이 봇별 설정보다 우선합니다. SAIN 값은 보관되며 바닐라를 끄면 복원됩니다. 자리키·저격수는 바닐라 고정입니다. AI 전환은 게임 재시작 후 적용됩니다." : "Global vanilla overrides bot settings, preserving stored SAIN values. Zryachiy and snipers stay vanilla. Restart the game after AI changes.", ZzapUI.Small);
                QuickObjectGUI.Draw(general.VanillaBots, (savedObject as SAIN.Preset.Shared.GlobalSettings.Categories.General.GeneralSettings)?.VanillaBots, current.Key+".VanillaBots");
            }
            if (QuickObjectGUI.Advanced(current.Key))
            {
                if (quickObject != null) AttributesGUI.EditAllValuesInObj(quickObject, out wasEdited);
            }
            EndScrollView();
        }
        GUILayout.EndVertical();

        EndHorizontal();
    }

    public static bool CheckIfOpen(SettingsContainer container, float height = 30f)
    {
        BeginHorizontal();
        container.Open = BuilderClass.ExpandableMenu(EditorText.UI(container.Name), container.Open, null, height);
        if (Button(EditorText.UI("Clear"), EditorText.UI("Clear Selected Options in this Menu"), EFT.UI.EUISoundType.MenuDropdownSelect, Width(100), Height(height)))
        {
            container.SelectedCategories.Clear();
            foreach (var category in container.Categories)
            {
                category.SelectedList.Clear();
            }
        }
        EndHorizontal();
        return container.Open;
    }

    public static bool WasEdited;

    private static void CategoryOpenable(List<Category> categories, object settingsObject, out bool wasEdited, string search = null)
    {
        wasEdited = false;
        foreach (var categoryClass in categories)
        {
            if (categoryClass.OptionCount(out int notUsed) == 0)
            {
                continue;
            }
            // zzap fork: while searching, skip groups with no hit (they used to show as empty headings).
            if (!string.IsNullOrEmpty(search) && categoryClass.FieldAttributesList.All(f => f.DoNotShowGUI || AttributesGUI.SkipForSearch(f, search)))
            {
                continue;
            }

            var attributes = categoryClass.CategoryInfo;
            object categoryObject = categoryClass.GetValue(settingsObject);

            BeginHorizontal(30);

            bool open = true;
            if (string.IsNullOrEmpty(search))
            {
                categoryClass.Open = BuilderClass.ExpandableMenu(
                    attributes.Name,
                    categoryClass.Open,
                    attributes.Description,
                    EntryConfig.EntryHeight
                );
                open = categoryClass.Open;
            }
            else
            {
                Label(new GUIContent(attributes.Name, attributes.Description), ZzapUI.Section, Height(24f));
            }

            EndHorizontal(30);

            if (open)
            {
                AttributesGUI.EditAllValuesInObj(categoryClass, categoryObject, out bool newEdit, search);
                if (newEdit)
                {
                    wasEdited = true;
                }
            }
        }
    }

    private static readonly GUIEntryConfig EntryConfig = new();
}
