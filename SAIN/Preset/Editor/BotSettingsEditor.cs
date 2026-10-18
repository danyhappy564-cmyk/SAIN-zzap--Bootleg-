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

    public static void ShowAllSettingsGUI(object settings, out bool wasEdited, string name, string savePath, float height, out bool Saved)
    {
        BeginHorizontal();

        Box(EditorText.UI(name), Height(height));

        Space(10);

        Label(EditorText.UI("Search"), Width(125f), Height(height));

        var container = SettingsContainers.GetContainer(settings.GetType(), name);
        container.SearchPattern = TextField(container.SearchPattern, null, Width(250), Height(height));

        if (Button(EditorText.UI("Clear"), EUISoundType.MenuContextMenu, Width(80), Height(height)))
        {
            container.SearchPattern = string.Empty;
        }

        Space(10);

        if (ConfigEditingTracker.UnsavedChanges)
        {
            BuilderClass.Alert(
                EditorText.UI("Click Save to export changes, and send changes to bots if in-game"),
                EditorText.UI("YOU HAVE UNSAVED CHANGES!"),
                height,
                ColorNames.DarkRed
            );
        }
        else
        {
            BuilderClass.Alert(null, null, height, null);
        }

        Saved = Button(EditorText.UI("Save and Export"), ConfigEditingTracker.GetUnsavedValuesString(), EUISoundType.InsuranceInsured, Height(height));

        EndHorizontal();

        // zzap fork (2026-10-18, mockup 5): without a search, a group list on the left and only the picked group on the right
        // (the old page unfolded every group into one long list). A search still lists every match, as before.
        if (string.IsNullOrEmpty(container.SearchPattern))
        {
            GroupedView(container, settings, out wasEdited);
            return;
        }

        container.Scroll = BeginScrollView(container.Scroll);

        CategoryOpenable(container.Categories, settings, out wasEdited, container.SearchPattern);

        EndScrollView();
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
        }

        BeginHorizontal();

        BeginVertical();
        Label(EditorText.UI("Groups"), Width(280f), Height(24f));
        foreach (var group in groups)
        {
            if (group.SubInfo != null && group.SubInfo.DoNotShowGUI)
            {
                continue;
            }
            string name = group.SubInfo != null ? "    " + group.SubInfo.Name : group.Category.CategoryInfo.Name;
            bool selected = group.Key == selectedKey;
            bool picked = Toggle(selected, new GUIContent($"{name}  ({group.Count})", group.SubInfo?.Description ?? group.Category.CategoryInfo.Description),
                EUISoundType.MenuDropdown, Width(280f), Height(26f));
            if (picked && !selected)
            {
                selectedKey = group.Key;
            }
        }
        EndVertical();

        Space(12f);

        BeginVertical();
        var current = groups.First(g => g.Key == selectedKey);
        string title = current.SubInfo != null ? $"{current.Category.CategoryInfo.Name} › {current.SubInfo.Name}" : current.Category.CategoryInfo.Name;
        Box(title, current.SubInfo?.Description ?? current.Category.CategoryInfo.Description, Height(30f));
        container.Scroll = BeginScrollView(container.Scroll);
        object categoryObject = current.Category.GetValue(settings);
        if (current.SubField != null)
        {
            object subObject = current.SubField.GetValue(categoryObject);
            if (subObject != null)
            {
                AttributesGUI.EditAllValuesInObj(subObject, out wasEdited);
            }
        }
        else
        {
            AttributesGUI.EditAllValuesInObj(current.Category, categoryObject, out wasEdited);
        }
        EndScrollView();
        EndVertical();

        EndHorizontal();

        if (_selectedGroup.TryGetValue(container, out string old) && old != selectedKey)
        {
            container.Scroll = UnityEngine.Vector2.zero;
        }
        _selectedGroup[container] = selectedKey;
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
                Box(attributes.Name, attributes.Description, Height(PresetHandler.EditorDefaults.ConfigEntryHeight));
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
