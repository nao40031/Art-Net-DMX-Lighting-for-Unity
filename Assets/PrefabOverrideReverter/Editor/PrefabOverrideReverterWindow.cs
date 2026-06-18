using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class PrefabOverrideReverterWindow : EditorWindow
{
    private const string WindowTitle = "Prefab Override Reverter";
    private const string UndoName = "Revert Selected Prefab Overrides";

    [SerializeField] private GameObject rootObject;
    [SerializeField] private GameObject prefabAsset;
    [SerializeField] private bool usePrefabStructureSelection = true;
    [SerializeField] private bool includeInactive = true;
    [SerializeField] private bool includeChildrenRecursively = true;
    [SerializeField] private bool groupByPrefabInstance = true;
    [SerializeField] private bool showFallbackRules;
    [SerializeField] private bool previewSelectedOnly = true;
    [SerializeField] private bool previewShowExcluded;
    [SerializeField] private string previewSearch = string.Empty;
    [SerializeField] private string includeGameObjects = "*";
    [SerializeField] private string excludeGameObjects = string.Empty;
    [SerializeField] private string includeComponentTypes = "*";
    [SerializeField] private string excludeComponentTypes = "DmxFixtureComponent";
    [SerializeField] private string includePropertyPaths = "*";
    [SerializeField] private string excludePropertyPaths = "universe\nstartAddress\nfixture\nmode";

    private readonly List<OverrideEntry> entries = new List<OverrideEntry>();
    private readonly List<PrefabTargetEntry> prefabTargets = new List<PrefabTargetEntry>();
    private readonly List<PrefabInstanceTarget> prefabInstanceTargets = new List<PrefabInstanceTarget>();
    private readonly HashSet<string> selectedPrefabTargetKeys = new HashSet<string>();
    private readonly Dictionary<string, bool> foldouts = new Dictionary<string, bool>();
    private Vector2 windowScroll;
    private Vector2 scroll;
    private Vector2 prefabScroll;
    private Vector2 instanceScroll;
    private string scanMessage;
    private GameObject lastPrefabAsset;
    private GameObject lastRootObject;

    [MenuItem("Tools/Prefab Override Reverter")]
    public static void ShowWindow()
    {
        GetWindow<PrefabOverrideReverterWindow>(WindowTitle);
    }

    private void OnGUI()
    {
        windowScroll = EditorGUILayout.BeginScrollView(windowScroll);

        EditorGUILayout.LabelField("Scan Target", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        rootObject = (GameObject)EditorGUILayout.ObjectField("Root Object", rootObject, typeof(GameObject), true);
        prefabAsset = (GameObject)EditorGUILayout.ObjectField("Prefab Asset", prefabAsset, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck())
        {
            RebuildPrefabTargets();
            RebuildPrefabInstanceTargets();
        }

        usePrefabStructureSelection = EditorGUILayout.Toggle("Use Prefab Selection", usePrefabStructureSelection);
        includeChildrenRecursively = EditorGUILayout.Toggle("Include Children", includeChildrenRecursively);
        includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);
        groupByPrefabInstance = EditorGUILayout.Toggle("Group By Prefab", groupByPrefabInstance);

        EditorGUILayout.Space(10f);
        DrawPresetSection();

        EditorGUILayout.Space(10f);
        DrawPrefabTargets();

        EditorGUILayout.Space(10f);
        DrawTargetPrefabInstances();

        EditorGUILayout.Space(10f);
        DrawRules();

        EditorGUILayout.Space(10f);
        DrawScanSection();

        EditorGUILayout.Space(10f);
        DrawPreview();

        EditorGUILayout.Space(10f);
        DrawRevertButton();

        EditorGUILayout.EndScrollView();
    }

    private void DrawPresetSection()
    {
        EditorGUILayout.LabelField("Preset", EditorStyles.boldLabel);
        if (GUILayout.Button("Moving Light Safe Preset", GUILayout.Height(28f)))
            ApplyMovingLightSafePreset();
    }

    private void DrawScanSection()
    {
        EditorGUILayout.LabelField("Scan", EditorStyles.boldLabel);
        if (GUILayout.Button("Scan Overrides", GUILayout.Height(30f)))
            ScanOverrides();

        if (!string.IsNullOrEmpty(scanMessage))
            EditorGUILayout.HelpBox(scanMessage, MessageType.Info);
    }

    private void DrawRules()
    {
        showFallbackRules = EditorGUILayout.Foldout(showFallbackRules, "Fallback Rules", true, EditorStyles.foldoutHeader);
        if (!showFallbackRules)
            return;

        EditorGUILayout.HelpBox(
            "Used when Prefab Asset is empty or Use Prefab Selection is off. One item per line. Use * to match all. GameObject filters use object names or hierarchy path endings. Component filters use type names such as Transform, Light, DmxFixtureComponent. Property filters use Unity serialized property paths or their last segment.",
            MessageType.None);

        includeGameObjects = DrawMultilineText("Include GameObjects", includeGameObjects);
        excludeGameObjects = DrawMultilineText("Exclude GameObjects", excludeGameObjects);
        includeComponentTypes = DrawMultilineText("Include Components", includeComponentTypes);
        excludeComponentTypes = DrawMultilineText("Exclude Components", excludeComponentTypes);
        includePropertyPaths = DrawMultilineText("Include Properties", includePropertyPaths);
        excludePropertyPaths = DrawMultilineText("Exclude Properties", excludePropertyPaths);
    }

    private void DrawPrefabTargets()
    {
        EditorGUILayout.LabelField("Prefab Structure Selection", EditorStyles.boldLabel);

        if (prefabAsset == null)
        {
            EditorGUILayout.HelpBox("Optional: assign a Prefab Asset to select exact Components from the prefab structure. This is safer than name-based rules.", MessageType.None);
            return;
        }

        if (!EditorUtility.IsPersistent(prefabAsset) || PrefabUtility.GetPrefabAssetType(prefabAsset) == PrefabAssetType.NotAPrefab)
        {
            EditorGUILayout.HelpBox("Prefab Asset must be a prefab selected from the Project window.", MessageType.Warning);
            return;
        }

        if (lastPrefabAsset != prefabAsset || prefabTargets.Count == 0)
            RebuildPrefabTargets();

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(string.Format("Selected components: {0}", selectedPrefabTargetKeys.Count), EditorStyles.miniLabel);

            if (GUILayout.Button("Select None", GUILayout.Width(90f)))
                selectedPrefabTargetKeys.Clear();

            if (GUILayout.Button("Refresh", GUILayout.Width(70f)))
                RebuildPrefabTargets();
        }

        prefabScroll = EditorGUILayout.BeginScrollView(prefabScroll, GUILayout.MinHeight(220f), GUILayout.MaxHeight(360f));
        foreach (IGrouping<string, PrefabTargetEntry> group in prefabTargets.GroupBy(t => t.GameObjectPath))
            DrawPrefabTargetGroup(group);
        EditorGUILayout.EndScrollView();
    }

    private void DrawPrefabTargetGroup(IGrouping<string, PrefabTargetEntry> group)
    {
        PrefabTargetEntry first = group.FirstOrDefault();
        if (first == null)
            return;

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(first.Depth * 14f);

            bool selected = group.Any(t => selectedPrefabTargetKeys.Contains(t.Key));
            EditorGUI.BeginChangeCheck();
            selected = EditorGUILayout.Toggle(selected, GUILayout.Width(18f));
            if (EditorGUI.EndChangeCheck())
            {
                foreach (PrefabTargetEntry target in group)
                {
                    if (selected)
                        selectedPrefabTargetKeys.Add(target.Key);
                    else
                        selectedPrefabTargetKeys.Remove(target.Key);
                }
            }

            EditorGUILayout.LabelField(first.GameObjectPath, EditorStyles.boldLabel);
        }

        foreach (PrefabTargetEntry target in group)
            DrawPrefabTarget(target);
    }

    private void DrawPrefabTarget(PrefabTargetEntry target)
    {
        if (target.Component == null)
            return;

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space((target.Depth + 1) * 14f);

            bool selected = selectedPrefabTargetKeys.Contains(target.Key);
            EditorGUI.BeginChangeCheck();
            selected = EditorGUILayout.Toggle(selected, GUILayout.Width(18f));
            if (EditorGUI.EndChangeCheck())
            {
                if (selected)
                    selectedPrefabTargetKeys.Add(target.Key);
                else
                    selectedPrefabTargetKeys.Remove(target.Key);
            }

            EditorGUILayout.LabelField(target.ComponentTypeName);
        }
    }

    private void DrawTargetPrefabInstances()
    {
        EditorGUILayout.LabelField("Target Prefab Instances", EditorStyles.boldLabel);

        if (rootObject == null || prefabAsset == null)
        {
            EditorGUILayout.HelpBox("Assign Root Object and Prefab Asset to collect target prefab instances.", MessageType.None);
            return;
        }

        if (lastRootObject != rootObject || lastPrefabAsset != prefabAsset || prefabInstanceTargets.Count == 0)
            RebuildPrefabInstanceTargets();

        int selectedCount = prefabInstanceTargets.Count(t => t.Selected && t.InstanceRoot != null);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(string.Format("Selected instances: {0} / {1}", selectedCount, prefabInstanceTargets.Count), EditorStyles.miniLabel);

            if (GUILayout.Button("Select All", GUILayout.Width(80f)))
                SetAllPrefabInstances(true);

            if (GUILayout.Button("Select None", GUILayout.Width(90f)))
                SetAllPrefabInstances(false);

            if (GUILayout.Button("Refresh", GUILayout.Width(70f)))
                RebuildPrefabInstanceTargets();
        }

        if (prefabInstanceTargets.Count == 0)
        {
            EditorGUILayout.HelpBox("No prefab instances matching Prefab Asset were found under Root Object.", MessageType.Warning);
            return;
        }

        instanceScroll = EditorGUILayout.BeginScrollView(instanceScroll, GUILayout.MinHeight(160f), GUILayout.MaxHeight(280f));
        foreach (PrefabInstanceTarget target in prefabInstanceTargets)
            DrawPrefabInstanceTarget(target);
        EditorGUILayout.EndScrollView();
    }

    private void DrawPrefabInstanceTarget(PrefabInstanceTarget target)
    {
        if (target.InstanceRoot == null)
            return;

        using (new EditorGUILayout.HorizontalScope())
        {
            target.Selected = EditorGUILayout.Toggle(target.Selected, GUILayout.Width(18f));
            EditorGUILayout.ObjectField(target.InstanceRoot, typeof(GameObject), true);

            if (GUILayout.Button("Select", GUILayout.Width(58f)))
            {
                Selection.activeGameObject = target.InstanceRoot;
                EditorGUIUtility.PingObject(target.InstanceRoot);
            }
        }
    }

    private void SetAllPrefabInstances(bool selected)
    {
        foreach (PrefabInstanceTarget target in prefabInstanceTargets)
            target.Selected = selected;
    }

    private static string DrawMultilineText(string label, string value)
    {
        EditorGUILayout.LabelField(label);
        return EditorGUILayout.TextArea(value, GUILayout.MinHeight(42f));
    }

    private void DrawPreview()
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        List<OverrideEntry> visibleEntries = GetVisiblePreviewEntries();

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(GetSummary(), EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(visibleEntries.Count == 0))
            {
                if (GUILayout.Button("Select All Visible", GUILayout.Width(110f)))
                    SetPreviewSelection(visibleEntries, true);

                if (GUILayout.Button("Clear", GUILayout.Width(60f)))
                    SetPreviewSelection(visibleEntries, false);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            previewSelectedOnly = EditorGUILayout.ToggleLeft("Selected only", previewSelectedOnly, GUILayout.Width(110f));
            previewShowExcluded = EditorGUILayout.ToggleLeft("Show excluded", previewShowExcluded, GUILayout.Width(110f));
        }

        previewSearch = EditorGUILayout.TextField("Search", previewSearch);

        if (entries.Count > 0)
            DrawSelectedBreakdown();

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(280f), GUILayout.MaxHeight(520f));
        if (entries.Count == 0)
        {
            EditorGUILayout.HelpBox("No scanned overrides. Assign a root object and click Scan Overrides.", MessageType.None);
        }
        else if (visibleEntries.Count == 0)
        {
            EditorGUILayout.HelpBox("No overrides match the current preview filters.", MessageType.None);
        }
        else
        {
            DrawEntries(visibleEntries);
        }
        EditorGUILayout.EndScrollView();

    }

    private List<OverrideEntry> GetVisiblePreviewEntries()
    {
        IEnumerable<OverrideEntry> query = entries;

        if (previewSelectedOnly)
            query = query.Where(e => e.Selected && !e.ExcludedByRule);

        if (!previewShowExcluded)
            query = query.Where(e => !e.ExcludedByRule);

        if (!string.IsNullOrWhiteSpace(previewSearch))
        {
            string search = previewSearch.Trim();
            query = query.Where(e =>
                (!string.IsNullOrEmpty(e.DisplayName) && e.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (!string.IsNullOrEmpty(e.GameObjectPath) && e.GameObjectPath.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (!string.IsNullOrEmpty(e.ComponentTypeName) && e.ComponentTypeName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (!string.IsNullOrEmpty(e.PropertyPath) && e.PropertyPath.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        return query.ToList();
    }

    private void DrawSelectedBreakdown()
    {
        List<OverrideEntry> selected = entries.Where(e => e.Selected && !e.ExcludedByRule).ToList();
        if (selected.Count == 0)
            return;

        string componentSummary = string.Join(", ",
            selected
                .GroupBy(e => e.ComponentTypeName)
                .OrderByDescending(g => g.Count())
                .Take(4)
                .Select(g => string.Format("{0}: {1}", g.Key, g.Count()))
                .ToArray());

        string propertySummary = string.Join(", ",
            selected
                .GroupBy(e => GetPropertyDisplayName(e.PropertyPath))
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => string.Format("{0}: {1}", g.Key, g.Count()))
                .ToArray());

        EditorGUILayout.HelpBox(
            string.Format("Will revert {0} selected overrides.\nComponents: {1}\nTop properties: {2}", selected.Count, componentSummary, propertySummary),
            MessageType.None);
    }

    private void DrawRevertButton()
    {
        EditorGUILayout.Space(4f);
        using (new EditorGUI.DisabledScope(!HasRevertableSelection()))
        {
            if (GUILayout.Button("Revert Selected", GUILayout.Height(30f)))
                RevertSelected();
        }
    }

    private bool HasRevertableSelection()
    {
        return entries.Any(e => e.Selected && !e.ExcludedByRule && e.CanRevert);
    }

    private string GetSummary()
    {
        int selected = entries.Count(e => e.Selected && !e.ExcludedByRule);
        int excluded = entries.Count(e => e.ExcludedByRule);
        int prefabs = entries.Select(e => e.PrefabRoot).Where(o => o != null).Distinct().Count();
        return string.Format("Overrides: {0}   Selected: {1}   Excluded: {2}   Prefab Instances: {3}", entries.Count, selected, excluded, prefabs);
    }

    private void DrawEntries(IEnumerable<OverrideEntry> previewEntries)
    {
        IEnumerable<IGrouping<string, OverrideEntry>> groups = groupByPrefabInstance
            ? previewEntries.GroupBy(e => e.PrefabRoot != null ? GetTransformPath(e.PrefabRoot.transform) : "(Missing Prefab Root)")
            : previewEntries.GroupBy(e => e.GameObject != null ? GetTransformPath(e.GameObject.transform) : "(Missing GameObject)");

        foreach (IGrouping<string, OverrideEntry> group in groups)
        {
            bool expanded = GetFoldout(group.Key);
            using (new EditorGUILayout.HorizontalScope())
            {
                bool groupSelected = group.Any(e => e.Selected && !e.ExcludedByRule);
                EditorGUI.BeginChangeCheck();
                bool nextGroupSelected = EditorGUILayout.Toggle(groupSelected, GUILayout.Width(18f));
                if (EditorGUI.EndChangeCheck())
                    SetGroupSelection(group, nextGroupSelected);

                bool nextExpanded = EditorGUILayout.Foldout(expanded, group.Key, true);
                SetFoldout(group.Key, nextExpanded);
            }

            if (!GetFoldout(group.Key))
                continue;

            EditorGUI.indentLevel++;
            foreach (OverrideEntry entry in group.OrderBy(e => e.GameObjectPath).ThenBy(e => e.ComponentTypeName).ThenBy(e => e.PropertyPath))
                DrawEntry(entry);
            EditorGUI.indentLevel--;
        }
    }

    private void DrawEntry(OverrideEntry entry)
    {
        using (new EditorGUI.DisabledScope(entry.ExcludedByRule || !entry.CanRevert))
        using (new EditorGUILayout.HorizontalScope())
        {
            entry.Selected = EditorGUILayout.Toggle(entry.Selected && !entry.ExcludedByRule, GUILayout.Width(18f));

            GUIContent label = new GUIContent(entry.DisplayName, entry.TargetObject != null ? entry.TargetObject.name : string.Empty);
            EditorGUILayout.LabelField(label);
        }

        if (entry.ExcludedByRule)
        {
            Rect rect = GUILayoutUtility.GetLastRect();
            rect.xMin = rect.xMax - 68f;
            GUI.Label(rect, "excluded", EditorStyles.miniLabel);
        }
    }

    private bool GetFoldout(string key)
    {
        bool value;
        if (!foldouts.TryGetValue(key, out value))
        {
            value = true;
            foldouts[key] = value;
        }

        return value;
    }

    private void SetFoldout(string key, bool value)
    {
        foldouts[key] = value;
    }

    private static void SetPreviewSelection(IEnumerable<OverrideEntry> previewEntries, bool selected)
    {
        foreach (OverrideEntry entry in previewEntries)
        {
            if (!entry.ExcludedByRule && entry.CanRevert)
                entry.Selected = selected;
        }
    }

    private static void SetGroupSelection(IEnumerable<OverrideEntry> group, bool selected)
    {
        foreach (OverrideEntry entry in group)
        {
            if (!entry.ExcludedByRule && entry.CanRevert)
                entry.Selected = selected;
        }
    }

    private void ApplyMovingLightSafePreset()
    {
        SelectPrefabTargetsByNames(
            new[] { "PanBody", "TiltBody", "TiltBody.001", "Spot Light" },
            new[] { "Transform", "Light" });

        includeGameObjects = "PanBody\nTiltBody\nTiltBody.001\nSpot Light";
        excludeGameObjects = string.Empty;
        includeComponentTypes = "Transform\nLight";
        excludeComponentTypes = "DmxFixtureComponent";
        includePropertyPaths = "*";
        excludePropertyPaths = "universe\nstartAddress\nfixture\nmode\ntargetLight\ntargetLights\npanTransform\ntiltTransform";
    }

    private void ScanOverrides()
    {
        entries.Clear();
        foldouts.Clear();

        if (rootObject == null)
        {
            scanMessage = "Root Object is required.";
            return;
        }

        RuleSet rules = new RuleSet(
            ParseRules(includeGameObjects),
            ParseRules(excludeGameObjects),
            ParseRules(includeComponentTypes),
            ParseRules(excludeComponentTypes),
            ParseRules(includePropertyPaths),
            ParseRules(excludePropertyPaths));
        PrefabSelection prefabSelection = CreatePrefabSelection();

        HashSet<GameObject> prefabRoots = CollectPrefabInstanceRoots(rootObject);
        foreach (GameObject prefabRoot in prefabRoots)
            AddPropertyOverrides(prefabRoot, rootObject.transform, rules, prefabSelection);

        int selected = entries.Count(e => e.Selected && !e.ExcludedByRule);
        scanMessage = string.Format("Scan complete. Found {0} property overrides. {1} selected by current rules.", entries.Count, selected);
    }

    private HashSet<GameObject> CollectPrefabInstanceRoots(GameObject root)
    {
        if (prefabAsset != null && prefabInstanceTargets.Count > 0)
        {
            return new HashSet<GameObject>(
                prefabInstanceTargets
                    .Where(t => t.Selected && t.InstanceRoot != null)
                    .Select(t => t.InstanceRoot));
        }

        HashSet<GameObject> prefabRoots = new HashSet<GameObject>();
        Transform[] transforms = includeChildrenRecursively
            ? root.GetComponentsInChildren<Transform>(includeInactive)
            : new[] { root.transform };

        foreach (Transform item in transforms)
        {
            if (item == null || !PrefabUtility.IsPartOfPrefabInstance(item.gameObject))
                continue;

            GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(item.gameObject);
            if (prefabRoot == null)
                continue;

            if (!IsUnderRoot(prefabRoot.transform, root.transform) && !IsUnderRoot(root.transform, prefabRoot.transform))
                continue;

            prefabRoots.Add(prefabRoot);
        }

        return prefabRoots;
    }

    private void AddPropertyOverrides(GameObject prefabRoot, Transform requestedRoot, RuleSet rules, PrefabSelection prefabSelection)
    {
        Transform[] transforms = prefabRoot.GetComponentsInChildren<Transform>(includeInactive);
        foreach (Transform transform in transforms)
        {
            if (transform == null || !IsUnderRoot(transform, requestedRoot))
                continue;

            Component[] components = transform.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component == null)
                    continue;

                AddSerializedPropertyOverrides(prefabRoot, component, rules, prefabSelection);
            }
        }
    }

    private void AddSerializedPropertyOverrides(GameObject prefabRoot, UnityEngine.Object targetObject, RuleSet rules, PrefabSelection prefabSelection)
    {
        SerializedObject serializedObject;
        try
        {
            serializedObject = new SerializedObject(targetObject);
        }
        catch (Exception ex)
        {
            Debug.LogWarning(string.Format("{0}: failed to inspect {1}. {2}", WindowTitle, targetObject.name, ex.Message), targetObject);
            return;
        }

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = true;
            if (!iterator.prefabOverride || iterator.propertyPath == "m_Script")
                continue;

            OverrideEntry entry = CreateEntry(prefabRoot, targetObject, iterator.propertyPath, rules, prefabSelection);
            if (entry != null)
                entries.Add(entry);
        }
    }

    private OverrideEntry CreateEntry(GameObject prefabRoot, UnityEngine.Object targetObject, string propertyPath, RuleSet rules, PrefabSelection prefabSelection)
    {
        if (targetObject == null || string.IsNullOrEmpty(propertyPath))
            return null;

        GameObject gameObject = ResolveGameObject(targetObject);
        if (gameObject == null)
            return null;

        string componentType = GetComponentTypeName(targetObject);
        string gameObjectPath = GetTransformPath(gameObject.transform);
        bool included;
        bool excluded;

        if (prefabSelection.Enabled)
        {
            included = MatchesPrefabSelection(targetObject, prefabSelection);
            excluded = !included || MatchesAny(rules.ExcludeProperties, propertyPath);
        }
        else
        {
            included =
                MatchesAny(rules.IncludeGameObjects, gameObject.name, gameObjectPath) &&
                MatchesAny(rules.IncludeComponents, componentType) &&
                MatchesAny(rules.IncludeProperties, propertyPath);
            excluded =
                MatchesAny(rules.ExcludeGameObjects, gameObject.name, gameObjectPath) ||
                MatchesAny(rules.ExcludeComponents, componentType) ||
                MatchesAny(rules.ExcludeProperties, propertyPath);
        }

        OverrideEntry entry = new OverrideEntry
        {
            PrefabRoot = prefabRoot,
            GameObject = gameObject,
            TargetObject = targetObject,
            ComponentTypeName = componentType,
            PropertyPath = propertyPath,
            GameObjectPath = gameObjectPath,
            ExcludedByRule = !included || excluded,
            Selected = included && !excluded
        };

        entry.DisplayName = string.Format("{0} / {1} / {2}", entry.GameObjectPath, entry.ComponentTypeName, entry.PropertyPath);
        entry.CanRevert = CanFindSerializedProperty(entry);
        if (!entry.CanRevert)
            entry.ExcludedByRule = true;

        return entry;
    }

    private void RebuildPrefabInstanceTargets()
    {
        HashSet<string> previousSelectedKeys = new HashSet<string>(
            prefabInstanceTargets
                .Where(t => t.Selected && !string.IsNullOrEmpty(t.Key))
                .Select(t => t.Key));

        prefabInstanceTargets.Clear();
        lastRootObject = rootObject;

        if (rootObject == null || prefabAsset == null || !EditorUtility.IsPersistent(prefabAsset))
            return;

        List<GameObject> roots = FindMatchingPrefabInstanceRoots(rootObject, prefabAsset);
        foreach (GameObject instanceRoot in roots)
        {
            string key = GetGlobalKey(instanceRoot);
            prefabInstanceTargets.Add(new PrefabInstanceTarget
            {
                InstanceRoot = instanceRoot,
                Key = key,
                Selected = previousSelectedKeys.Count == 0 || previousSelectedKeys.Contains(key)
            });
        }
    }

    private List<GameObject> FindMatchingPrefabInstanceRoots(GameObject root, GameObject prefab)
    {
        List<GameObject> results = new List<GameObject>();
        HashSet<GameObject> seen = new HashSet<GameObject>();
        if (root == null || prefab == null)
            return results;

        AddMatchingPrefabRootsInHierarchyOrder(root.transform, root.transform, prefab, includeInactive, results, seen);

        return results;
    }

    private static void AddMatchingPrefabRootsInHierarchyOrder(
        Transform current,
        Transform requestedRoot,
        GameObject prefab,
        bool includeInactiveChildren,
        List<GameObject> results,
        HashSet<GameObject> seen)
    {
        if (current == null)
            return;

        if ((includeInactiveChildren || current.gameObject.activeInHierarchy) && PrefabUtility.IsPartOfPrefabInstance(current.gameObject))
        {
            AddMatchingPrefabRoot(results, seen, PrefabUtility.GetNearestPrefabInstanceRoot(current.gameObject), requestedRoot, prefab);
            AddMatchingPrefabRoot(results, seen, PrefabUtility.GetOutermostPrefabInstanceRoot(current.gameObject), requestedRoot, prefab);
        }

        if (!includeInactiveChildren && !current.gameObject.activeInHierarchy)
            return;

        for (int i = 0; i < current.childCount; i++)
            AddMatchingPrefabRootsInHierarchyOrder(current.GetChild(i), requestedRoot, prefab, includeInactiveChildren, results, seen);
    }

    private static void AddMatchingPrefabRoot(List<GameObject> results, HashSet<GameObject> seen, GameObject instanceRoot, Transform requestedRoot, GameObject prefab)
    {
        if (instanceRoot == null || requestedRoot == null || prefab == null)
            return;

        if (seen.Contains(instanceRoot))
            return;

        if (!IsUnderRoot(instanceRoot.transform, requestedRoot) && !IsUnderRoot(requestedRoot, instanceRoot.transform))
            return;

        if (PrefabInstanceMatchesAsset(instanceRoot, prefab))
        {
            results.Add(instanceRoot);
            seen.Add(instanceRoot);
        }
    }

    private static bool PrefabInstanceMatchesAsset(GameObject instanceRoot, GameObject prefab)
    {
        string prefabPath = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(prefabPath))
            return false;

        string instancePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot);
        if (string.Equals(prefabPath, instancePath, StringComparison.OrdinalIgnoreCase))
            return true;

        UnityEngine.Object source = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
        if (source != null && string.Equals(prefabPath, AssetDatabase.GetAssetPath(source), StringComparison.OrdinalIgnoreCase))
            return true;

        UnityEngine.Object originalSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(instanceRoot);
        if (originalSource != null && string.Equals(prefabPath, AssetDatabase.GetAssetPath(originalSource), StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private void RebuildPrefabTargets()
    {
        prefabTargets.Clear();
        selectedPrefabTargetKeys.RemoveWhere(string.IsNullOrEmpty);
        lastPrefabAsset = prefabAsset;

        if (prefabAsset == null || !EditorUtility.IsPersistent(prefabAsset))
            return;

        Transform[] transforms = prefabAsset.GetComponentsInChildren<Transform>(true);
        foreach (Transform transform in transforms)
        {
            Component[] components = transform.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component == null)
                    continue;

                string key = GetGlobalKey(component);
                if (string.IsNullOrEmpty(key))
                    continue;

                prefabTargets.Add(new PrefabTargetEntry
                {
                    Component = component,
                    Key = key,
                    Depth = GetDepthFromPrefabRoot(transform, prefabAsset.transform),
                    GameObjectPath = GetPrefabRelativePath(transform, prefabAsset.transform),
                    GameObjectName = transform.name,
                    ComponentTypeName = component.GetType().Name,
                    DisplayName = string.Format("{0} / {1}", GetPrefabRelativePath(transform, prefabAsset.transform), component.GetType().Name)
                });
            }
        }
    }

    private void SelectPrefabTargetsByNames(IEnumerable<string> gameObjectNames, IEnumerable<string> componentTypeNames)
    {
        if (prefabAsset != null && (lastPrefabAsset != prefabAsset || prefabTargets.Count == 0))
            RebuildPrefabTargets();

        HashSet<string> objectNames = new HashSet<string>(gameObjectNames ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        HashSet<string> componentNames = new HashSet<string>(componentTypeNames ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        foreach (PrefabTargetEntry target in prefabTargets)
        {
            if (objectNames.Contains(target.GameObjectName) && componentNames.Contains(target.ComponentTypeName))
                selectedPrefabTargetKeys.Add(target.Key);
        }
    }

    private PrefabSelection CreatePrefabSelection()
    {
        bool enabled =
            usePrefabStructureSelection &&
            prefabAsset != null &&
            EditorUtility.IsPersistent(prefabAsset);

        return new PrefabSelection(enabled, new HashSet<string>(selectedPrefabTargetKeys));
    }

    private static bool MatchesPrefabSelection(UnityEngine.Object instanceObject, PrefabSelection prefabSelection)
    {
        foreach (string sourceKey in GetPrefabSourceKeys(instanceObject))
        {
            if (prefabSelection.SelectedKeys.Contains(sourceKey))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> GetPrefabSourceKeys(UnityEngine.Object instanceObject)
    {
        UnityEngine.Object source = PrefabUtility.GetCorrespondingObjectFromSource(instanceObject);
        string sourceKey = GetGlobalKey(source);
        if (!string.IsNullOrEmpty(sourceKey))
            yield return sourceKey;

        UnityEngine.Object originalSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(instanceObject);
        string originalSourceKey = GetGlobalKey(originalSource);
        if (!string.IsNullOrEmpty(originalSourceKey) && originalSourceKey != sourceKey)
            yield return originalSourceKey;
    }

    private static string GetGlobalKey(UnityEngine.Object obj)
    {
        if (obj == null)
            return string.Empty;

        try
        {
            return GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static int GetDepthFromPrefabRoot(Transform transform, Transform root)
    {
        int depth = 0;
        Transform current = transform;
        while (current != null && current != root)
        {
            depth++;
            current = current.parent;
        }

        return depth;
    }

    private static string GetPrefabRelativePath(Transform transform, Transform root)
    {
        if (transform == null)
            return "(Missing)";

        if (root == null || transform == root)
            return transform.name;

        Stack<string> names = new Stack<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Push(current.name);
            if (current == root)
                break;

            current = current.parent;
        }

        return string.Join("/", names.ToArray());
    }

    private static bool CanFindSerializedProperty(OverrideEntry entry)
    {
        try
        {
            SerializedObject serializedObject = new SerializedObject(entry.TargetObject);
            SerializedProperty property = serializedObject.FindProperty(entry.PropertyPath);
            return property != null;
        }
        catch
        {
            return false;
        }
    }

    private void RevertSelected()
    {
        List<OverrideEntry> selected = entries.Where(e => e.Selected && !e.ExcludedByRule && e.CanRevert).ToList();
        if (selected.Count == 0)
            return;

        if (!EditorUtility.DisplayDialog(
                WindowTitle,
                string.Format("Revert {0} selected prefab property overrides?", selected.Count),
                "Revert",
                "Cancel"))
        {
            return;
        }

        int reverted = 0;
        foreach (OverrideEntry entry in selected)
        {
            if (RevertEntry(entry))
                reverted++;
        }

        Debug.Log(string.Format("{0}: reverted {1} prefab property overrides.", WindowTitle, reverted));
        ScanOverrides();
    }

    private static bool RevertEntry(OverrideEntry entry)
    {
        try
        {
            SerializedObject serializedObject = new SerializedObject(entry.TargetObject);
            SerializedProperty property = serializedObject.FindProperty(entry.PropertyPath);
            if (property == null)
                return false;

            Undo.RecordObject(entry.TargetObject, UndoName);
            PrefabUtility.RevertPropertyOverride(property, InteractionMode.UserAction);
            EditorUtility.SetDirty(entry.TargetObject);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning(string.Format("{0}: failed to revert {1}. {2}", WindowTitle, entry.DisplayName, ex.Message), entry.TargetObject);
            return false;
        }
    }

    private static GameObject ResolveGameObject(UnityEngine.Object targetObject)
    {
        GameObject gameObject = targetObject as GameObject;
        if (gameObject != null)
            return gameObject;

        Component component = targetObject as Component;
        return component != null ? component.gameObject : null;
    }

    private static string GetComponentTypeName(UnityEngine.Object targetObject)
    {
        GameObject gameObject = targetObject as GameObject;
        if (gameObject != null)
            return "GameObject";

        Component component = targetObject as Component;
        if (component == null)
            return targetObject.GetType().Name;

        return component.GetType().Name;
    }

    private static bool IsUnderRoot(Transform child, Transform root)
    {
        if (child == null || root == null)
            return false;

        Transform current = child;
        while (current != null)
        {
            if (current == root)
                return true;

            current = current.parent;
        }

        return false;
    }

    private static string GetTransformPath(Transform transform)
    {
        if (transform == null)
            return "(Missing)";

        Stack<string> names = new Stack<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names.ToArray());
    }

    private static string GetPropertyDisplayName(string propertyPath)
    {
        if (string.IsNullOrEmpty(propertyPath))
            return "(Unknown)";

        string name = propertyPath;
        int dotIndex = name.LastIndexOf('.');
        if (dotIndex >= 0 && dotIndex < name.Length - 1)
            name = name.Substring(dotIndex + 1);

        if (name.StartsWith("m_", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(2);

        return name;
    }

    private static List<string> ParseRules(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new List<string>();

        return text
            .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrEmpty(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool MatchesAny(List<string> rules, string value)
    {
        if (rules == null || rules.Count == 0)
            return false;

        foreach (string rule in rules)
        {
            if (RuleMatches(rule, value))
                return true;
        }

        return false;
    }

    private static bool MatchesAny(List<string> rules, string value, string path)
    {
        if (rules == null || rules.Count == 0)
            return false;

        foreach (string rule in rules)
        {
            if (RuleMatches(rule, value) || RuleMatchesPath(rule, path))
                return true;
        }

        return false;
    }

    private static bool RuleMatches(string rule, string value)
    {
        if (string.IsNullOrEmpty(rule))
            return false;

        if (rule == "*")
            return true;

        if (string.IsNullOrEmpty(value))
            return false;

        if (string.Equals(rule, value, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(NormalizeRuleText(rule), NormalizeRuleText(value), StringComparison.OrdinalIgnoreCase))
            return true;

        string lastSegment = value;
        int dotIndex = lastSegment.LastIndexOf('.');
        if (dotIndex >= 0 && dotIndex < lastSegment.Length - 1)
            lastSegment = lastSegment.Substring(dotIndex + 1);

        if (string.Equals(rule, lastSegment, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(NormalizeRuleText(rule), NormalizeRuleText(lastSegment), StringComparison.OrdinalIgnoreCase))
            return true;

        string[] segments = value.Split('.');
        foreach (string segment in segments)
        {
            if (string.Equals(NormalizeRuleText(rule), NormalizeRuleText(segment), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool RuleMatchesPath(string rule, string path)
    {
        if (string.IsNullOrEmpty(rule))
            return false;

        if (rule == "*")
            return true;

        if (string.IsNullOrEmpty(path))
            return false;

        if (string.Equals(rule, path, StringComparison.OrdinalIgnoreCase))
            return true;

        return path.EndsWith("/" + rule, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRuleText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        string normalized = value.Trim();
        if (normalized.StartsWith("m_", StringComparison.OrdinalIgnoreCase))
            normalized = normalized.Substring(2);

        return normalized.Replace("_", string.Empty);
    }

    private class RuleSet
    {
        public readonly List<string> IncludeGameObjects;
        public readonly List<string> ExcludeGameObjects;
        public readonly List<string> IncludeComponents;
        public readonly List<string> ExcludeComponents;
        public readonly List<string> IncludeProperties;
        public readonly List<string> ExcludeProperties;

        public RuleSet(
            List<string> includeGameObjects,
            List<string> excludeGameObjects,
            List<string> includeComponents,
            List<string> excludeComponents,
            List<string> includeProperties,
            List<string> excludeProperties)
        {
            IncludeGameObjects = includeGameObjects;
            ExcludeGameObjects = excludeGameObjects;
            IncludeComponents = includeComponents;
            ExcludeComponents = excludeComponents;
            IncludeProperties = includeProperties;
            ExcludeProperties = excludeProperties;
        }
    }

    private class PrefabSelection
    {
        public readonly bool Enabled;
        public readonly HashSet<string> SelectedKeys;

        public PrefabSelection(bool enabled, HashSet<string> selectedKeys)
        {
            Enabled = enabled;
            SelectedKeys = selectedKeys ?? new HashSet<string>();
        }
    }

    private class PrefabTargetEntry
    {
        public Component Component;
        public string Key;
        public int Depth;
        public string GameObjectPath;
        public string GameObjectName;
        public string ComponentTypeName;
        public string DisplayName;
    }

    private class PrefabInstanceTarget
    {
        public GameObject InstanceRoot;
        public string Key;
        public bool Selected;
    }

    private class OverrideEntry
    {
        public GameObject PrefabRoot;
        public GameObject GameObject;
        public UnityEngine.Object TargetObject;
        public string ComponentTypeName;
        public string PropertyPath;
        public string GameObjectPath;
        public string DisplayName;
        public bool Selected;
        public bool ExcludedByRule;
        public bool CanRevert;
    }
}
