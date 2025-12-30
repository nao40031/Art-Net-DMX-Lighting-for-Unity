// Assets/Editor/SpotLightReplicatorWindow.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class SpotLightReplicatorWindow : EditorWindow
{
    [Header("Search / Targets")]
    [SerializeField] private Transform root;                 // ここ配下から MovingLight_** を探す（任意）
    [SerializeField] private GameObject templateMovingLight; // Spot Light を持っている個体（テンプレ）

    [Header("Naming")]
    [SerializeField] private string movingLightPrefix = "MovingLight_";
    [SerializeField] private int startIndex = 1;
    [SerializeField] private int endIndex = 36;

    [Header("Hierarchy Path")]
    [Tooltip("MovingLight から TiltBody.001 までの相対パス（/ 区切り）")]
    [SerializeField] private string mountPath = "PanBody/TiltBody/TiltBody.001";

    [Tooltip("TiltBody.001 の子にある Light GameObject 名（例: 'Spot Light'）")]
    [SerializeField] private string templateLightObjectName = "Spot Light";

    [Header("Behavior")]
    [SerializeField] private bool replaceIfExists = true;
    [SerializeField] private bool logDetails = true;

    [MenuItem("Tools/MovingLight/Replicate SpotLight...")]
    public static void Open()
    {
        var w = GetWindow<SpotLightReplicatorWindow>("SpotLight Replicator");
        w.minSize = new Vector2(420, 320);
        w.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Replicate SpotLight to MovingLight_1..N", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            root = (Transform)EditorGUILayout.ObjectField("Root (optional)", root, typeof(Transform), true);
            templateMovingLight = (GameObject)EditorGUILayout.ObjectField("Template MovingLight", templateMovingLight, typeof(GameObject), true);
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            movingLightPrefix = EditorGUILayout.TextField("Prefix", movingLightPrefix);
            startIndex = EditorGUILayout.IntField("Start Index", startIndex);
            endIndex = EditorGUILayout.IntField("End Index", endIndex);
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            mountPath = EditorGUILayout.TextField("Mount Path", mountPath);
            templateLightObjectName = EditorGUILayout.TextField("Light Object Name", templateLightObjectName);
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            replaceIfExists = EditorGUILayout.Toggle("Replace If Exists", replaceIfExists);
            logDetails = EditorGUILayout.Toggle("Log Details", logDetails);
        }

        EditorGUILayout.Space(8);

        using (new EditorGUI.DisabledScope(!CanRun()))
        {
            if (GUILayout.Button("Replicate", GUILayout.Height(36)))
            {
                Replicate();
            }
        }

        if (!CanRun())
        {
            EditorGUILayout.HelpBox("Template MovingLight を指定してください。", MessageType.Info);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "テンプレの 'Spot Light' GameObject を丸ごと複製します（HDRPの追加コンポーネントも含まれるので安全）。\n" +
            "Mount Path が違う場合は、あなたの階層に合わせて変更してください。",
            MessageType.None);
    }

    private bool CanRun()
    {
        return templateMovingLight != null && startIndex > 0 && endIndex >= startIndex;
    }

    private void Replicate()
    {
        // 1) テンプレSpotLight取得
        var templateMount = FindChildByPath(templateMovingLight.transform, mountPath);
        if (templateMount == null)
        {
            Debug.LogError($"[SpotLightReplicator] Template mount not found. mountPath='{mountPath}' template='{templateMovingLight.name}'");
            return;
        }

        Transform templateLightTr = templateMount.Find(templateLightObjectName);
        if (templateLightTr == null)
        {
            // 名前で見つからない場合は、子孫から Light(Type=Spot) を探す（保険）
            templateLightTr = FindFirstSpotLightTransform(templateMount);
        }

        if (templateLightTr == null)
        {
            Debug.LogError($"[SpotLightReplicator] Template light not found under '{templateMount.name}'. " +
                           $"LightObjectName='{templateLightObjectName}' (or any Spot Light component).");
            return;
        }

        var templateLightGo = templateLightTr.gameObject;

        // 2) 対象MovingLightを処理
        int created = 0, replaced = 0, skipped = 0, missing = 0, mountMissing = 0;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        for (int i = startIndex; i <= endIndex; i++)
        {
            string targetName = $"{movingLightPrefix}{i}";
            var target = FindGameObjectByName(targetName, root);

            if (target == null)
            {
                missing++;
                if (logDetails) Debug.LogWarning($"[SpotLightReplicator] Target not found: {targetName}");
                continue;
            }

            var targetMount = FindChildByPath(target.transform, mountPath);
            if (targetMount == null)
            {
                mountMissing++;
                if (logDetails) Debug.LogWarning($"[SpotLightReplicator] Mount not found on '{targetName}'. mountPath='{mountPath}'");
                continue;
            }

            var existing = targetMount.Find(templateLightGo.name); // まず同名で探す
            if (existing == null && !string.IsNullOrEmpty(templateLightObjectName))
                existing = targetMount.Find(templateLightObjectName); // 次に指定名で探す

            if (existing != null)
            {
                if (!replaceIfExists)
                {
                    skipped++;
                    if (logDetails) Debug.Log($"[SpotLightReplicator] Skip (already exists): {targetName}/{existing.name}");
                    continue;
                }

                Undo.DestroyObjectImmediate(existing.gameObject);
                replaced++;
            }

            // 複製（GameObject丸ごと）
            var clone = (GameObject)Instantiate(templateLightGo, targetMount, false);
            clone.name = templateLightGo.name; // 名前も揃える
            Undo.RegisterCreatedObjectUndo(clone, "Replicate SpotLight");
            created++;

            if (logDetails) Debug.Log($"[SpotLightReplicator] Created: {targetName}/{mountPath}/{clone.name}");
        }

        Undo.CollapseUndoOperations(undoGroup);

        // シーンをdirtyにして保存対象にする
        EditorSceneManager.MarkAllScenesDirty();

        Debug.Log($"[SpotLightReplicator] Done. created={created}, replaced={replaced}, skipped={skipped}, " +
                  $"missingTarget={missing}, missingMount={mountMissing}");
    }

    private static GameObject FindGameObjectByName(string name, Transform root)
    {
        if (root != null)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t.gameObject;
            }
            return null;
        }

        // Root未指定ならシーン全体から
        return GameObject.Find(name);
    }

    private static Transform FindChildByPath(Transform start, string path)
    {
        if (start == null) return null;
        if (string.IsNullOrWhiteSpace(path)) return start;

        var current = start;
        var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            current = current.Find(p);
            if (current == null) return null;
        }
        return current;
    }

    private static Transform FindFirstSpotLightTransform(Transform parent)
    {
        if (parent == null) return null;
        var lights = parent.GetComponentsInChildren<Light>(true);
        foreach (var l in lights)
        {
            if (l != null && l.type == LightType.Spot)
                return l.transform;
        }
        return null;
    }
}
#endif
