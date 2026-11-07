using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using EFT.UI;
using SAIN.Attributes;
using SAIN.Plugin;
using SAIN.Preset.Shared.GlobalSettings;
using SAIN.Preset.Shared.Preset;
using UnityEngine;
using static SAIN.Editor.SAINLayout;

namespace SAIN.Editor.GUISections;

public static class BotSettingsEditor
{
    private static readonly StringBuilder _stringBuilder = new();
    private static string T(string ko,string en)=>EditorText.Korean?ko:en;

    // zzap fork (2026-10-18, mockup 5): left panel = search box + group list, right panel = the picked group (or the search hits).
    // Save lives in the header now, so the old top row (name box, search label, red "unsaved" bar, second Save button) is gone.
    public static void ShowAllSettingsGUI(object settings, out bool wasEdited, string name, string savePath, float height, out bool Saved, GlobalScope scope = GlobalScope.All)
    {
        Saved = false;
        ZzapUI.EnsureStyles();
        var container = SettingsContainers.GetContainer(settings.GetType(), name);
        GroupedView(container, settings, out wasEdited, scope);
    }

    /// <summary>zzap (user 2026-11-04): the global settings split over three tabs - everything else / ZZAP tactics / simulation
    /// (sections listed in ZzapTactics). The settings object is the same; only which groups a tab lists differs.</summary>
    public enum GlobalScope
    {
        All,
        Zzap,
        Sim,
    }

    private const string OVERVIEW = "__zzap_overview";

    private static string TabName(GlobalScope scope) => scope switch
    {
        GlobalScope.Zzap => ZzapTactics.TabZzap,
        GlobalScope.Sim => ZzapTactics.TabSim,
        _ => ZzapTactics.TabAll,
    };

    private static bool InScope(GroupEntry group, GlobalScope scope)
    {
        string field = group.SubField?.Name;
        bool zzap = field != null && ZzapTactics.IsZzapSection(field);
        bool sim = field != null && ZzapTactics.IsSimSection(field);
        return scope switch
        {
            GlobalScope.Zzap => zzap,
            GlobalScope.Sim => sim,
            _ => !zzap && !sim,
        };
    }

    private static readonly Dictionary<GlobalScope, List<GroupEntry>> _scoped = new();

    private static List<GroupEntry> ScopedGroups(SettingsContainer container, GlobalScope scope)
    {
        if (_scoped.TryGetValue(scope, out var list))
        {
            return list;
        }
        list = new List<GroupEntry>();
        if (scope == GlobalScope.Zzap)
        {
            list.Add(new GroupEntry { Key = OVERVIEW, Count = ZzapTactics.All.Length });
        }
        foreach (var group in Groups(container))
        {
            if (!InScope(group, scope))
            {
                continue;
            }
            // On their own tab the sections are top-level rows (they sit under "General" in the full list).
            list.Add(scope == GlobalScope.All ? group : new GroupEntry
            {
                Key = group.Key, Category = group.Category, SubField = group.SubField, SubInfo = group.SubInfo, Count = group.Count, Depth = 0,
            });
        }
        _scoped[scope] = list;
        return list;
    }

    private static readonly Dictionary<GlobalScope, string> _scopeSearch = new();
    private static readonly Dictionary<GlobalScope, UnityEngine.Vector2> _scopeScroll = new();
    private static readonly Dictionary<GlobalScope, string> _selectedByScope = new();
    private static readonly Dictionary<GlobalScope, UnityEngine.Vector2> _listScrollByScope = new();

    // The ZZAP tab's first page: each main zzap switch as one line (name, plain help, switch, a dot when it differs from the
    // reference preset picked on the "Compare with" bar).
    private static void ZzapOverview(SAIN.Preset.Shared.GlobalSettings.GlobalSettingsClass settings)
    {
        var reference = QuickObjectGUI.Reference()?.GlobalSettings;
        string lastGroup = null;
        foreach (var tactic in ZzapTactics.All)
        {
            if (tactic.Group != lastGroup)
            {
                if (lastGroup != null) GUILayout.Space(10f);
                lastGroup = tactic.Group;
                GUILayout.Label(ZzapTactics.GroupName(tactic.Group, EditorText.Korean), ZzapUI.Section);
                GUILayout.Space(2f);
            }
            bool on = tactic.Get(settings);
            GUILayout.BeginHorizontal(ZzapUI.Panel1, GUILayout.Height(52f));
            GUILayout.BeginVertical();
            string dot = reference != null && tactic.Get(reference) != on ? " <color=#facc15>●</color>" : string.Empty;
            GUILayout.Label("<b>" + tactic.Name(EditorText.Korean) + "</b>" + dot, ZzapUI.Name);
            GUILayout.Label(tactic.Help(EditorText.Korean), ZzapUI.Small);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            bool next = ZzapUI.Switch(on, 30f, T("켬", "On"), T("끔", "Off"));
            GUILayout.EndHorizontal();
            if (next != on)
            {
                var captured = tactic;
                ZzapUI.Defer(() => { captured.Set(settings, next); ConfigEditingTracker.Add(captured.NameEn, next); });
            }
            GUILayout.Space(4f);
        }
        if (reference != null)
        {
            GUILayout.Label(string.Format(T("노란 점 = 비교 기준({0})과 다름", "Yellow dot = differs from the reference ({0})"), QuickObjectGUI.ReferenceLabel), ZzapUI.Small);
        }
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
    private static readonly HashSet<string> _expandedGroups = new();

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

    private static void GroupedView(SettingsContainer container, object settings, out bool wasEdited, GlobalScope scope)
    {
        wasEdited = false;
        var groups = ScopedGroups(container, scope);
        if (groups.Count == 0)
        {
            return;
        }
        if (!_selectedByScope.TryGetValue(scope, out string selectedKey) || groups.All(g => g.Key != selectedKey))
        {
            selectedKey = groups[0].Key;
            _selectedByScope[scope] = selectedKey;
        }
        // What the right panel shows is decided before this frame's clicks/typing (they apply next frame, see ZzapUI.Defer).
        _scopeSearch.TryGetValue(scope, out string search);
        search ??= string.Empty;
        bool searching = search.Length > 0;

        BeginHorizontal();

        GUILayout.BeginVertical(ZzapUI.Panel1, Width(ZzapUI.SIDE_WIDTH), Height(ZzapUI.PANEL_HEIGHT));
        _scopeSearch[scope] = ZzapUI.Search(search, EditorText.UI("Search settings (Korean or English)"));
        Space(8f);
        _listScrollByScope.TryGetValue(scope, out var listScroll);
        _listScrollByScope[scope] = BeginScrollView(listScroll, GUILayout.ExpandHeight(true));
        foreach (var group in groups)
        {
            if (group.Key == OVERVIEW)
            {
                if (ZzapUI.ListItem(T("★ 한눈에 켜고 끄기", "★ At a glance"), group.Count.ToString(), !searching && selectedKey == OVERVIEW, T("zzap 전술을 쉬운 말로 켜고 끄기", "Turn the zzap tactics on or off in plain words"), 0f, 30f))
                {
                    ZzapUI.Defer(() => { _selectedByScope[scope] = OVERVIEW; _scopeSearch[scope] = string.Empty; });
                }
                continue;
            }
            if (group.SubInfo != null && group.SubInfo.DoNotShowGUI)
            {
                continue;
            }
            string parentKey = group.Category.CategoryInfo.EnglishName;
            bool expanded = _expandedGroups.Contains(parentKey);
            if (group.Depth > 0 && !searching && !expanded) continue;
            bool hasChildren = group.Depth == 0 && group.SubField == null && groups.Any(child => child.Depth > 0 && child.Category == group.Category);
            if (hasChildren)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(expanded ? "▾" : "▸", ZzapUI.Button, GUILayout.Width(26f), GUILayout.Height(30f)))
                {
                    string captured = parentKey;
                    ZzapUI.Defer(() => { if (!_expandedGroups.Remove(captured)) _expandedGroups.Add(captured); });
                }
            }
            string label = group.SubInfo != null ? group.SubInfo.Name : group.Category.CategoryInfo.Name;
            string tip = group.SubInfo?.Description ?? group.Category.CategoryInfo.Description;
            bool selected = !searching && group.Key == selectedKey;
            if (ZzapUI.ListItem(label, group.Count.ToString(), selected, tip, group.Depth * 16f, 30f))
            {
                string key = group.Key;
                ZzapUI.Defer(() =>
                {
                    _selectedByScope[scope] = key;
                    _scopeSearch[scope] = string.Empty;
                    _scopeScroll[scope] = UnityEngine.Vector2.zero;
                });
            }
            if (hasChildren) GUILayout.EndHorizontal();
        }
        EndScrollView();
        GUILayout.EndVertical();

        Space(12f);

        GUILayout.BeginVertical(ZzapUI.Panel1, Height(ZzapUI.PANEL_HEIGHT), GUILayout.ExpandWidth(true));
        _scopeScroll.TryGetValue(scope, out var scroll);
        if (!searching && selectedKey == OVERVIEW && !SettingsFilterGUI.Active)
        {
            ZzapUI.PanelHeading(T("ZZAP 전술 — 한눈에 켜고 끄기", "ZZAP tactics — at a glance"),
                T("모든 봇에 적용됩니다. 봇 종류마다 다르게 하려면 '봇 종류별' 탭의 ZZAP 칸에서 바꾸세요. 세부 숫자는 왼쪽 각 묶음에 있습니다.", "Applies to all bots. To differ by bot type use the ZZAP part of the Bot Settings tab. Exact numbers are in each group on the left."));
            _scopeScroll[scope] = BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            ZzapOverview((SAIN.Preset.Shared.GlobalSettings.GlobalSettingsClass)settings);
            EndScrollView();
        }
        else if (searching || SettingsFilterGUI.Active)
        {
            ZzapUI.PanelHeading(T("전역 설정","Global settings"),T("모든 봇에 공통으로 적용합니다. 노란 점은 원본과 다른 값입니다.","Applies to all bots. A yellow dot marks values that differ from zzap original."));
            SettingsFilterGUI.Filters();
            _scopeScroll[scope] = BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            // Each tab searches its own sections only.
            string tab = TabName(scope);
            SettingsFilterGUI.Draw(settings,QuickObjectGUI.Defaults()?.GlobalSettings,search,path => ZzapTactics.TabOf(path) == tab);
            EndScrollView();
        }
        else
        {
            var current = groups.First(g => g.Key == selectedKey);
            string title = current.SubInfo != null ? $"{current.Category.CategoryInfo.Name} › {current.SubInfo.Name}" : current.Category.CategoryInfo.Name;
            ZzapUI.PanelHeading(title, current.SubInfo?.Description ?? current.Category.CategoryInfo.Description);
            SettingsFilterGUI.Filters();
            _scopeScroll[scope] = BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            object categoryObject = current.Category.GetValue(settings);
            var baseline = QuickObjectGUI.Saved()?.GlobalSettings;
            object savedObject = baseline == null ? null : current.Category.GetValue(baseline);
            object quickObject = categoryObject;
            if (current.SubField != null)
            {
                quickObject = current.SubField.GetValue(categoryObject);
                savedObject = savedObject == null ? null : current.SubField.GetValue(savedObject);
            }
            object Select(SAIN.Preset.Shared.Preset.SAINPresetBundle bundle)
            {
                object selected = bundle?.GlobalSettings == null ? null : current.Category.GetValue(bundle.GlobalSettings);
                return current.SubField == null || selected == null ? selected : current.SubField.GetValue(selected);
            }
            QuickObjectGUI.Draw(quickObject, savedObject, current.Key, Select);
            if (quickObject is SAIN.Preset.Shared.GlobalSettings.Categories.General.GeneralSettings general)
            {
                GUILayout.Label(EditorText.Korean ? "바닐라 묶음이 봇별 설정보다 우선합니다. SAIN 값은 보관되며 바닐라를 끄면 복원됩니다. 자리키·저격수는 바닐라 고정입니다. AI 전환은 게임 재시작 후 적용됩니다." : "Global vanilla overrides bot settings, preserving stored SAIN values. Zryachiy and snipers stay vanilla. Restart the game after AI changes.", ZzapUI.Small);
                QuickObjectGUI.Draw(general.VanillaBots, (savedObject as SAIN.Preset.Shared.GlobalSettings.Categories.General.GeneralSettings)?.VanillaBots, current.Key+".VanillaBots", bundle => (Select(bundle) as SAIN.Preset.Shared.GlobalSettings.Categories.General.GeneralSettings)?.VanillaBots, showReference: false);
            }
            if (QuickObjectGUI.Advanced(current.Key))
            {
                // "General" itself lists the zzap / simulation sections inside it - they are on their own tabs now.
                System.Func<string, bool> keep = current.SubField == null && current.Category.CategoryInfo.MemberInfo.Name == "General"
                    ? path => ZzapTactics.TabOf("General." + path) == ZzapTactics.TabAll
                    : null;
                if (quickObject != null) SettingsFilterGUI.Draw(quickObject,Select(QuickObjectGUI.Defaults()),null,keep);
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
