// Assets/Editor/MovingLightPrefabReplacerWindow.cs
#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class MovingLightPrefabReplacerWindow : EditorWindow
{
    [Header("Search / Prefab")]
    [SerializeField] private Transform root;          // MovingLightGroup など（任意）
    [SerializeField] private GameObject prefabAsset;  // Project上のPrefabアセット（MovingLight.prefab）

    [Header("Target Names")]
    [SerializeField] private string prefix = "MovingLight_";
    [SerializeField] private int startIndex = 1;
    [SerializeField] private int endIndex = 36;

    [Header("Options")]
    [Tooltip("置換後のGameObject名をターゲット名（MovingLight_1 など）に揃える")]
    [SerializeField] private bool forceNameToTarget = true;

    [Tooltip("元のオブジェクトのActive/Layer/Tag/Staticもコピーする")]
    [SerializeField] private bool copyCommonFlags = true;

    [Tooltip("ログを出す")]
    [SerializeField] private bool logDetails = true;

    [MenuItem("Tools/MovingLight/Replace With Prefab...")]
    public static void Open()
    {
        var w = GetWindow<MovingLightPrefabReplacerWindow>("MovingLight Prefab Replacer");
        w.minSize = new Vector2(440, 280);
        w.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Replace MovingLight_1..N with Prefab Instances", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            root = (Transform)EditorGUILayout.ObjectField("Root (optional)", root, typeof(Transform), true);
            prefabAsset = (GameObject)EditorGUILayout.ObjectField("Prefab Asset", prefabAsset, typeof(GameObject), false);
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            prefix = EditorGUILayout.TextField("Prefix", prefix);
            startIndex = EditorGUILayout.IntField("Start Index", startIndex);
            endIndex = EditorGUILayout.IntField("End Index", endIndex);
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            forceNameToTarget = EditorGUILayout.Toggle("Force Name To Target", forceNameToTarget);
            copyCommonFlags = EditorGUILayout.Toggle("Copy Active/Layer/Tag/Static", copyCommonFlags);
            logDetails = EditorGUILayout.Toggle("Log Details", logDetails);
        }

        EditorGUILayout.Space(8);

        using (new EditorGUI.DisabledScope(!CanRun()))
        {
            if (GUILayout.Button("Replace", GUILayout.Height(36)))
            {
                Replace();
            }
        }

        if (!CanRun())
        {
            EditorGUILayout.HelpBox("Prefab Asset（Project上の.prefab）を指定してください。", MessageType.Info);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "注意：置換は既存GameObjectを削除して新規Prefabインスタンスを生成します。\n" +
            "他スクリプトからの参照がある場合は参照が切れます（必要なら別方式にします）。",
            MessageType.Warning);
    }

    private bool CanRun()
    {
        if (prefabAsset == null) return false;
        if (startIndex <= 0 || endIndex < startIndex) return false;

        // Prefabアセットかどうか軽くチェック
        var assetType = PrefabUtility.GetPrefabAssetType(prefabAsset);
        return assetType != PrefabAssetType.NotAPrefab;
    }

    private void Replace()
    {
        int replaced = 0, missing = 0;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        for (int i = startIndex; i <= endIndex; i++)
        {
            string targetName = $"{prefix}{i}";
            var targetGO = FindByName(targetName, root);

            if (targetGO == null)
            {
                missing++;
                if (logDetails) Debug.LogWarning($"[MovingLightPrefabReplacer] Target not found: {targetName}");
                continue;
            }

            // 元情報を保存
            Transform oldT = targetGO.transform;
            Transform parent = oldT.parent;
            int sibling = oldT.GetSiblingIndex();

            Vector3 localPos = oldT.localPosition;
            Quaternion localRot = oldT.localRotation;
            Vector3 localScale = oldT.localScale;

            bool wasActive = targetGO.activeSelf;
            int layer = targetGO.layer;
            string tag = targetGO.tag;
            bool isStatic = targetGO.isStatic;

            // Prefabインスタンス生成（シーンに生成）
            GameObject newGO = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, parent);
            if (newGO == null)
            {
                Debug.LogError($"[MovingLightPrefabReplacer] Failed to instantiate prefab: {prefabAsset.name}");
                continue;
            }

            Undo.RegisterCreatedObjectUndo(newGO, "Replace MovingLight With Prefab");

            // 位置合わせ（親は同じなのでローカルをコピー）
            Transform newT = newGO.transform;
            newT.SetSiblingIndex(sibling);
            newT.localPosition = localPos;
            newT.localRotation = localRot;
            newT.localScale = localScale;

            if (forceNameToTarget) newGO.name = targetName;

            if (copyCommonFlags)
            {
                newGO.layer = layer;
                // tag は未定義だと例外になるので try
                try { newGO.tag = tag; } catch { /* ignore */ }
                newGO.isStatic = isStatic;
                newGO.SetActive(wasActive);
            }

            // 元を削除
            Undo.DestroyObjectImmediate(targetGO);

            replaced++;
            if (logDetails) Debug.Log($"[MovingLightPrefabReplacer] Replaced: {targetName}");
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkAllScenesDirty();

        Debug.Log($"[MovingLightPrefabReplacer] Done. replaced={replaced}, missing={missing}");
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
}
#endif
