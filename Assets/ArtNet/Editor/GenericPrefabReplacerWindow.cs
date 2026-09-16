// Assets/Editor/GenericPrefabReplacerWindow.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class GenericPrefabReplacerWindow : EditorWindow
{
    public enum TargetMode { PrefixNumberRange, ChildrenOfRoot }
    public enum TemplateMode { PrefabAsset, SceneObject }

    // Child Transform matching mode used when transferring local TRS values.
    public enum TransformMatchMode
    {
        PathOnly,              // "A/B/C"
        NamePlusSiblingIndex   // "A[0]/B[1]/C[2]" (distinguishes duplicate sibling names)
    }

    [Header("Target")]
    [SerializeField] private TargetMode targetMode = TargetMode.PrefixNumberRange;
    [SerializeField] private Transform root;

    [Header("Prefix + Range")]
    [SerializeField] private string targetPrefix = "MovingLight_";
    [SerializeField] private int startIndex = 1;
    [SerializeField] private int endIndex = 36;

    [Header("Children Filter")]
    [SerializeField] private bool onlyDirectChildren = false;
    [SerializeField] private string nameContains = "";
    [SerializeField] private string tagEquals = "";
    [SerializeField] private string requiredComponentTypeName = "";

    [Header("Template")]
    [SerializeField] private TemplateMode templateMode = TemplateMode.PrefabAsset;
    [SerializeField] private GameObject prefabAsset;
    [SerializeField] private GameObject templateSceneGO;

    [Header("Replace Options")]
    [SerializeField] private bool forceNameToTarget = true;
    [SerializeField] private bool copyCommonFlags = true;
    [SerializeField] private bool keepWorldTransform = false;
    [SerializeField] private bool logDetails = true;

    [Header("Transform Transfer")]
    [SerializeField] private bool transferAllChildLocalTransforms = true;

    [SerializeField] private TransformMatchMode transformMatchMode = TransformMatchMode.NamePlusSiblingIndex;

    [SerializeField] private bool warnOnMissingTransform = true;

    [MenuItem("Tools/Generic/Replace Targets With Template...")]
    public static void Open()
    {
        var w = GetWindow<GenericPrefabReplacerWindow>("Generic Prefab Replacer");
        w.minSize = new Vector2(580, 500);
        w.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Replace targets with template (optional: transfer child transforms)", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            targetMode = (TargetMode)EditorGUILayout.EnumPopup("Target Mode", targetMode);
            root = (Transform)EditorGUILayout.ObjectField("Root (optional*)", root, typeof(Transform), true);

            if (targetMode == TargetMode.PrefixNumberRange)
            {
                targetPrefix = EditorGUILayout.TextField("Target Prefix", targetPrefix);
                startIndex = EditorGUILayout.IntField("Start Index", startIndex);
                endIndex = EditorGUILayout.IntField("End Index", endIndex);
            }
            else
            {
                onlyDirectChildren = EditorGUILayout.Toggle("Only Direct Children", onlyDirectChildren);
                nameContains = EditorGUILayout.TextField("Name Contains", nameContains);
                tagEquals = EditorGUILayout.TextField("Tag Equals", tagEquals);
                requiredComponentTypeName = EditorGUILayout.TextField("Required Component (TypeName)", requiredComponentTypeName);
            }
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            templateMode = (TemplateMode)EditorGUILayout.EnumPopup("Template Mode", templateMode);
            if (templateMode == TemplateMode.PrefabAsset)
            {
                prefabAsset = (GameObject)EditorGUILayout.ObjectField("Prefab Asset", prefabAsset, typeof(GameObject), false);
            }
            else
            {
                templateSceneGO = (GameObject)EditorGUILayout.ObjectField("Template Scene GO", templateSceneGO, typeof(GameObject), true);
            }
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            forceNameToTarget = EditorGUILayout.Toggle("Force Name To Target", forceNameToTarget);
            copyCommonFlags = EditorGUILayout.Toggle("Copy Active/Layer/Tag/Static", copyCommonFlags);
            keepWorldTransform = EditorGUILayout.Toggle("Keep World Transform", keepWorldTransform);
            logDetails = EditorGUILayout.Toggle("Log Details", logDetails);
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            transferAllChildLocalTransforms = EditorGUILayout.Toggle("Transfer Child Local Transforms", transferAllChildLocalTransforms);

            using (new EditorGUI.DisabledScope(!transferAllChildLocalTransforms))
            {
                transformMatchMode = (TransformMatchMode)EditorGUILayout.EnumPopup("Transform Match Mode", transformMatchMode);
                warnOnMissingTransform = EditorGUILayout.Toggle("Warn If Missing", warnOnMissingTransform);

                EditorGUILayout.HelpBox(
                    "子Transformのlocal値を、置換前→置換後へ復元します。\n" +
                    "同名があるなら NamePlusSiblingIndex が安定（親の下の同名兄弟n番目で識別）。\n" +
                    "※構造や並び順を変えると一致しない箇所が出ることがあります。\n\n" +
                    "Restores child Transform local values from before the replacement to the new instance.\n" +
                    "When duplicate names exist, NamePlusSiblingIndex is more reliable because it identifies the nth same-named sibling under its parent.\n" +
                    "Some entries may not match if the hierarchy structure or sibling order has changed.",
                    MessageType.None);
            }
        }

        EditorGUILayout.Space(8);

        using (new EditorGUI.DisabledScope(!CanRun()))
        {
            if (GUILayout.Button("Replace", GUILayout.Height(38)))
                Replace();
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "注意：置換は「元を削除→テンプレ生成」です。参照が切れる可能性があります。\n" +
            "Warning: Replacement deletes the original object and creates the template. Existing references may be lost.",
            MessageType.Warning);
    }

    private bool CanRun()
    {
        if (targetMode == TargetMode.ChildrenOfRoot && root == null) return false;
        if (targetMode == TargetMode.PrefixNumberRange && (startIndex <= 0 || endIndex < startIndex)) return false;

        if (templateMode == TemplateMode.PrefabAsset)
        {
            if (prefabAsset == null) return false;
            return PrefabUtility.GetPrefabAssetType(prefabAsset) != PrefabAssetType.NotAPrefab;
        }
        else
        {
            return templateSceneGO != null;
        }
    }

    private void Replace()
    {
        var targets = CollectTargets();
        if (targets.Count == 0)
        {
            Debug.LogWarning("[GenericPrefabReplacer] No targets found.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        int replaced = 0;

        foreach (var target in targets)
        {
            if (target == null) continue;

            // Capture child Transform values before replacement.
            Dictionary<string, LocalTRS> trsMap = null;
            if (transferAllChildLocalTransforms)
                trsMap = CaptureLocalTransforms(target.transform, transformMatchMode);

            var oldT = target.transform;

            Transform parent = oldT.parent;
            int sibling = oldT.GetSiblingIndex();

            // Preserve the selected world/local root Transform values.
            Vector3 worldPos = oldT.position;
            Quaternion worldRot = oldT.rotation;
            Vector3 worldScale = oldT.lossyScale;

            Vector3 localPos = oldT.localPosition;
            Quaternion localRot = oldT.localRotation;
            Vector3 localScale = oldT.localScale;

            bool wasActive = target.activeSelf;
            int layer = target.layer;
            string tag = target.tag;
            bool isStatic = target.isStatic;

            // Instantiate the replacement.
            GameObject newGO = InstantiateTemplate(parent);
            if (newGO == null)
            {
                Debug.LogError("[GenericPrefabReplacer] Failed to instantiate template.");
                continue;
            }

            Undo.RegisterCreatedObjectUndo(newGO, "Replace With Template");

            newGO.transform.SetSiblingIndex(sibling);

            // Restore the replacement root Transform.
            if (keepWorldTransform)
            {
                newGO.transform.position = worldPos;
                newGO.transform.rotation = worldRot;

                if (newGO.transform.parent != null)
                {
                    var p = newGO.transform.parent;
                    var ps = p.lossyScale;
                    Vector3 safePs = new Vector3(ps.x == 0 ? 1 : ps.x, ps.y == 0 ? 1 : ps.y, ps.z == 0 ? 1 : ps.z);
                    newGO.transform.localScale = new Vector3(worldScale.x / safePs.x, worldScale.y / safePs.y, worldScale.z / safePs.z);
                }
                else
                {
                    newGO.transform.localScale = worldScale;
                }
            }
            else
            {
                newGO.transform.localPosition = localPos;
                newGO.transform.localRotation = localRot;
                newGO.transform.localScale = localScale;
            }

            if (forceNameToTarget) newGO.name = target.name;

            if (copyCommonFlags)
            {
                newGO.layer = layer;
                try { newGO.tag = tag; } catch { }
                newGO.isStatic = isStatic;
                newGO.SetActive(wasActive);
            }

            // Restore child Transform values without overriding the root Transform above.
            if (transferAllChildLocalTransforms && trsMap != null)
                RestoreLocalTransforms(newGO.transform, trsMap, transformMatchMode, warnOnMissingTransform);

            Undo.DestroyObjectImmediate(target);

            replaced++;
            if (logDetails) Debug.Log($"[GenericPrefabReplacer] Replaced: {newGO.name}");
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"[GenericPrefabReplacer] Done. targets={targets.Count}, replaced={replaced}");
    }

    // -------------------------
    // Target collection
    // -------------------------
    private List<GameObject> CollectTargets()
    {
        var results = new List<GameObject>();

        if (targetMode == TargetMode.PrefixNumberRange)
        {
            for (int i = startIndex; i <= endIndex; i++)
            {
                string name = $"{targetPrefix}{i}";
                var go = FindByName(name, root);
                if (go != null) results.Add(go);
            }
            return results;
        }

        if (root == null) return results;

        IEnumerable<Transform> trs;
        if (onlyDirectChildren)
        {
            var list = new List<Transform>();
            for (int i = 0; i < root.childCount; i++) list.Add(root.GetChild(i));
            trs = list;
        }
        else
        {
            trs = root.GetComponentsInChildren<Transform>(true);
        }

        Type requiredType = null;
        if (!string.IsNullOrWhiteSpace(requiredComponentTypeName))
        {
            requiredType = FindTypeByName(requiredComponentTypeName.Trim());
            if (requiredType == null)
                Debug.LogWarning($"[GenericPrefabReplacer] Required component type not found: {requiredComponentTypeName}");
        }

        foreach (var t in trs)
        {
            if (t == null) continue;
            if (t == root) continue;

            var go = t.gameObject;

            if (!string.IsNullOrEmpty(nameContains) && !go.name.Contains(nameContains))
                continue;

            if (!string.IsNullOrEmpty(tagEquals) && tagEquals != "Untagged")
            {
                try { if (!go.CompareTag(tagEquals)) continue; }
                catch { continue; }
            }

            if (requiredType != null && go.GetComponent(requiredType) == null)
                continue;

            results.Add(go);
        }

        return results;
    }

    // -------------------------
    // Template instantiate
    // -------------------------
    private GameObject InstantiateTemplate(Transform parent)
    {
        if (templateMode == TemplateMode.PrefabAsset)
            return (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, parent);

        return (GameObject)Instantiate(templateSceneGO, parent, false);
    }

    private static GameObject FindByName(string name, Transform root)
    {
        if (root != null)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
            return null;
        }
        return GameObject.Find(name);
    }

    private static Type FindTypeByName(string typeName)
    {
        var candidates = new[] { $"UnityEngine.{typeName}, UnityEngine", $"UnityEngine.{typeName}" };
        foreach (var c in candidates)
        {
            var t = Type.GetType(c);
            if (t != null) return t;
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var t in asm.GetTypes())
                {
                    if (t.Name == typeName) return t;
                    if (t.FullName == typeName) return t;
                }
            }
            catch { }
        }
        return null;
    }

    // -------------------------
    // Transform transfer (local TRS)
    // -------------------------
    [Serializable]
    private struct LocalTRS
    {
        public Vector3 pos;
        public Quaternion rot;
        public Vector3 scale;

        public LocalTRS(Vector3 p, Quaternion r, Vector3 s) { pos = p; rot = r; scale = s; }
    }

    private static Dictionary<string, LocalTRS> CaptureLocalTransforms(Transform root, TransformMatchMode mode)
    {
        var map = new Dictionary<string, LocalTRS>(512);
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            // The root is restored separately according to keepWorldTransform.
            // Capturing it here would overwrite that option during child restoration.
            if (t == root) continue;

            string key = mode == TransformMatchMode.PathOnly
                ? GetRelativePath(root, t)
                : GetRelativePathWithSameNameIndex(root, t);

            map[key] = new LocalTRS(t.localPosition, t.localRotation, t.localScale);
        }
        return map;
    }

    private static void RestoreLocalTransforms(Transform newRoot, Dictionary<string, LocalTRS> oldMap, TransformMatchMode mode, bool warnMissing)
    {
        foreach (var kv in oldMap)
        {
            Transform target = mode == TransformMatchMode.PathOnly
                ? FindByRelativePath(newRoot, kv.Key)
                : FindByRelativePathWithSameNameIndex(newRoot, kv.Key);

            if (target == null)
            {
                if (warnMissing) Debug.LogWarning($"[GenericPrefabReplacer] Missing transform in new instance: '{kv.Key}'");
                continue;
            }

            var trs = kv.Value;
            target.localPosition = trs.pos;
            target.localRotation = trs.rot;
            target.localScale = trs.scale;
        }
    }

    // --- PathOnly ---
    private static string GetRelativePath(Transform root, Transform t)
    {
        if (t == root) return "";
        var stack = new Stack<string>();
        var cur = t;
        while (cur != null && cur != root)
        {
            stack.Push(cur.name);
            cur = cur.parent;
        }
        return string.Join("/", stack);
    }

    private static Transform FindByRelativePath(Transform root, string path)
    {
        if (string.IsNullOrEmpty(path)) return root;
        var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var cur = root;
        foreach (var p in parts)
        {
            cur = cur.Find(p);
            if (cur == null) return null;
        }
        return cur;
    }

    // --- NamePlusSiblingIndex (same-name sibling order) ---
    // Key format: "PanBody[0]/TiltBody[1]/TiltBody.001[2]/Spot Light[0]"
    private static string GetRelativePathWithSameNameIndex(Transform root, Transform t)
    {
        if (t == root) return "";

        var stack = new Stack<string>();
        var cur = t;

        while (cur != null && cur != root)
        {
            int sameNameIndex = GetSameNameSiblingIndex(cur);
            stack.Push($"{cur.name}[{sameNameIndex}]");
            cur = cur.parent;
        }

        return string.Join("/", stack);
    }

    private static Transform FindByRelativePathWithSameNameIndex(Transform root, string key)
    {
        if (string.IsNullOrEmpty(key)) return root;

        var parts = key.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var cur = root;

        foreach (var part in parts)
        {
            // parse "Name[3]"
            if (!TryParseNameIndex(part, out string name, out int index))
                return null;

            cur = FindChildBySameNameIndex(cur, name, index);
            if (cur == null) return null;
        }

        return cur;
    }

    private static bool TryParseNameIndex(string token, out string name, out int index)
    {
        name = token;
        index = 0;

        int lb = token.LastIndexOf('[');
        int rb = token.LastIndexOf(']');

        if (lb < 0 || rb < 0 || rb <= lb) return false;

        name = token.Substring(0, lb);
        var num = token.Substring(lb + 1, rb - lb - 1);

        return int.TryParse(num, out index);
    }

    // Zero-based occurrence among siblings with the same name.
    private static int GetSameNameSiblingIndex(Transform t)
    {
        if (t.parent == null) return 0;

        int idx = 0;
        var p = t.parent;
        for (int i = 0; i < p.childCount; i++)
        {
            var c = p.GetChild(i);
            if (c.name == t.name)
            {
                if (c == t) return idx;
                idx++;
            }
        }
        return 0; // fallback
    }

    // Find the indexed occurrence among siblings with the same name.
    private static Transform FindChildBySameNameIndex(Transform parent, string name, int index)
    {
        int idx = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c.name != name) continue;

            if (idx == index) return c;
            idx++;
        }
        return null;
    }
}
#endif
