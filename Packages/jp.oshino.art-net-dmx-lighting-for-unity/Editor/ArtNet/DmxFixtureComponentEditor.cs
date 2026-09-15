/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

using ArtNet.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArtNet.Editor
{
    [CustomEditor(typeof(DmxFixtureComponent))]
    [CanEditMultipleObjects]
    public sealed class DmxFixtureComponentEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                if (property.propertyPath == "lensMaterialBindings")
                {
                    enterChildren = false;
                    continue;
                }

                using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
                {
                    if (property.propertyPath == "prismGoboScaleVlb")
                        EditorGUILayout.Slider(property, 0.1f, 3f, new GUIContent(
                            "Prism Gobo Scale (VLB)",
                            "VLBのプリズム時に、各ゴボ投影の外形サイズをSpot Angleで調整します。1で通常のSpot Lightに合わせた基準サイズです。"));
                    else
                        EditorGUILayout.PropertyField(property, true);
                }

                if (property.propertyPath == "vlbHdrpExposureWeight")
                    DrawVlbDefaultsButton();

                if (property.propertyPath == "goboLensHotspotStrength")
                    DrawGoboLensMaterialSetup();

                enterChildren = false;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGoboLensMaterialSetup()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Gobo Lens Material Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Setup Gobo Lens Material は、現在のRender Pipeline（HDRP / URP）を一度だけ判定し、登録済みの各Rendererスロットへ対応する共有マテリアルを割り当てます。再生中の差し替えは行いません。",
                MessageType.Info);

            var bindings = serializedObject.FindProperty("lensMaterialBindings");
            DrawLensMaterialBindings(bindings);

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("Setup Gobo Lens Material"))
                {
                    serializedObject.ApplyModifiedProperties();
                    SetupGoboLensMaterialForTargets();
                    serializedObject.Update();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate Gobo Lens Setup"))
                    ValidateGoboLensSetupForTargets();

                using (new EditorGUI.DisabledScope(Application.isPlaying))
                {
                    if (GUILayout.Button("Restore Original Materials"))
                    {
                        serializedObject.ApplyModifiedProperties();
                        RestoreOriginalMaterialsForTargets();
                        serializedObject.Update();
                    }
                }
            }
        }

        private static void DrawLensMaterialBindings(SerializedProperty bindings)
        {
            bindings.isExpanded = EditorGUILayout.Foldout(bindings.isExpanded, "Lens Material Bindings", true);
            if (!bindings.isExpanded)
                return;

            EditorGUI.indentLevel++;
            for (int i = 0; i < bindings.arraySize; i++)
            {
                var binding = bindings.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"Binding {i}", EditorStyles.miniBoldLabel);
                    EditorGUILayout.PropertyField(binding.FindPropertyRelative("renderer"));
                    EditorGUILayout.PropertyField(binding.FindPropertyRelative("materialSlot"));
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(binding.FindPropertyRelative("originalMaterial"), new GUIContent("Original Material"));
                        EditorGUILayout.PropertyField(binding.FindPropertyRelative("goboLensMaterial"), new GUIContent("Assigned Gobo Lens Material"));
                    }
                    if (GUILayout.Button("Remove Binding"))
                    {
                        bindings.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
            }

            if (GUILayout.Button("Add Lens Material Binding"))
            {
                int index = bindings.arraySize;
                bindings.InsertArrayElementAtIndex(index);
                var binding = bindings.GetArrayElementAtIndex(index);
                binding.FindPropertyRelative("renderer").objectReferenceValue = null;
                binding.FindPropertyRelative("materialSlot").intValue = 0;
                binding.FindPropertyRelative("originalMaterial").objectReferenceValue = null;
                binding.FindPropertyRelative("goboLensMaterial").objectReferenceValue = null;
            }
            EditorGUI.indentLevel--;
        }

        private static Material FindGoboLensMaterialForCurrentPipeline(out string expectedShaderName)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline
                : GraphicsSettings.defaultRenderPipeline;
            string pipelineName = pipeline != null ? pipeline.GetType().FullName : string.Empty;
            bool isHdrp = pipelineName != null && pipelineName.Contains("HighDefinition");
            bool isUrp = pipelineName != null && pipelineName.Contains("Universal");

            if (!isHdrp && !isUrp)
            {
                expectedShaderName = null;
                return null;
            }

            string materialName = isHdrp ? "GoboLensSurface_HDRP" : "GoboLensSurface_URP";
            expectedShaderName = isHdrp ? "ArtNet/HDRP/Gobo Lens Surface" : "ArtNet/URP/Gobo Lens Surface";
            string[] guids = AssetDatabase.FindAssets($"{materialName} t:Material");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null && material.shader != null && material.shader.name == expectedShaderName)
                    return material;
            }

            return null;
        }

        private void SetupGoboLensMaterialForTargets()
        {
            var material = FindGoboLensMaterialForCurrentPipeline(out string expectedShaderName);
            if (material == null)
            {
                Debug.LogError("Gobo Lens Material Setup: 現在のRender Pipelineに対応する共有マテリアルが見つかりません。HDRPまたはURPを有効にし、UPMパッケージを再importしてください。");
                return;
            }

            int configured = 0;
            for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
            {
                var targetObject = new SerializedObject(targets[targetIndex]);
                var bindings = targetObject.FindProperty("lensMaterialBindings");
                if (bindings == null) continue;
                MigrateLegacyLensRenderersToBindings(targetObject, bindings);

                for (int i = 0; i < bindings.arraySize; i++)
                {
                    var binding = bindings.GetArrayElementAtIndex(i);
                    var renderer = binding.FindPropertyRelative("renderer").objectReferenceValue as Renderer;
                    int slot = binding.FindPropertyRelative("materialSlot").intValue;
                    if (renderer == null || slot < 0 || renderer.sharedMaterials == null || slot >= renderer.sharedMaterials.Length)
                    {
                        Debug.LogWarning($"Gobo Lens Material Setup: {targets[targetIndex].name} のBinding {i} はRendererまたはMaterial Slotが無効です。", targets[targetIndex]);
                        continue;
                    }

                    Undo.RecordObject(renderer, "Setup Gobo Lens Material");
                    var materials = renderer.sharedMaterials;
                    var original = binding.FindPropertyRelative("originalMaterial");
                    if (original.objectReferenceValue == null) original.objectReferenceValue = materials[slot];
                    materials[slot] = material;
                    renderer.sharedMaterials = materials;
                    binding.FindPropertyRelative("goboLensMaterial").objectReferenceValue = material;
                    EditorUtility.SetDirty(renderer);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    configured++;
                }

                bindings.serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(targets[targetIndex]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(targets[targetIndex]);
            }
            Debug.Log($"Gobo Lens Material Setup: {configured} slot(s) に {expectedShaderName} を割り当てました。");
        }

        private static void MigrateLegacyLensRenderersToBindings(SerializedObject targetObject, SerializedProperty bindings)
        {
            if (bindings.arraySize > 0) return;
            AddRendererBinding(targetObject.FindProperty("lensRenderer"), bindings);
            var extraRenderers = targetObject.FindProperty("extraLensRenderers");
            if (extraRenderers == null || !extraRenderers.isArray) return;
            for (int i = 0; i < extraRenderers.arraySize; i++) AddRendererBinding(extraRenderers.GetArrayElementAtIndex(i), bindings);
        }

        private static void AddRendererBinding(SerializedProperty rendererProperty, SerializedProperty bindings)
        {
            var renderer = rendererProperty != null ? rendererProperty.objectReferenceValue as Renderer : null;
            if (renderer == null) return;
            int index = bindings.arraySize;
            bindings.InsertArrayElementAtIndex(index);
            var binding = bindings.GetArrayElementAtIndex(index);
            binding.FindPropertyRelative("renderer").objectReferenceValue = renderer;
            binding.FindPropertyRelative("materialSlot").intValue = 0;
        }

        private void RestoreOriginalMaterialsForTargets()
        {
            int restored = 0;
            for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
            {
                var bindings = new SerializedObject(targets[targetIndex]).FindProperty("lensMaterialBindings");
                if (bindings == null) continue;
                for (int i = 0; i < bindings.arraySize; i++)
                {
                    var binding = bindings.GetArrayElementAtIndex(i);
                    var renderer = binding.FindPropertyRelative("renderer").objectReferenceValue as Renderer;
                    var original = binding.FindPropertyRelative("originalMaterial").objectReferenceValue as Material;
                    int slot = binding.FindPropertyRelative("materialSlot").intValue;
                    if (renderer == null || original == null || slot < 0 || renderer.sharedMaterials == null || slot >= renderer.sharedMaterials.Length) continue;
                    Undo.RecordObject(renderer, "Restore Gobo Lens Material");
                    var materials = renderer.sharedMaterials;
                    materials[slot] = original;
                    renderer.sharedMaterials = materials;
                    binding.FindPropertyRelative("goboLensMaterial").objectReferenceValue = null;
                    EditorUtility.SetDirty(renderer);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    restored++;
                }
                bindings.serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(targets[targetIndex]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(targets[targetIndex]);
            }
            Debug.Log($"Gobo Lens Material Setup: {restored} slot(s) を元のマテリアルへ戻しました。");
        }

        private void ValidateGoboLensSetupForTargets()
        {
            var expectedMaterial = FindGoboLensMaterialForCurrentPipeline(out string expectedShaderName);
            if (expectedMaterial == null)
            {
                Debug.LogError("Gobo Lens Material Setup: 現在のRender Pipelineに対応する共有マテリアルが見つかりません。");
                return;
            }

            int valid = 0;
            int invalid = 0;
            for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
            {
                var bindings = new SerializedObject(targets[targetIndex]).FindProperty("lensMaterialBindings");
                if (bindings == null || bindings.arraySize == 0)
                {
                    invalid++;
                    Debug.LogWarning($"Gobo Lens Material Setup: {targets[targetIndex].name} にLens Material Bindingがありません。", targets[targetIndex]);
                    continue;
                }
                for (int i = 0; i < bindings.arraySize; i++)
                {
                    var binding = bindings.GetArrayElementAtIndex(i);
                    var renderer = binding.FindPropertyRelative("renderer").objectReferenceValue as Renderer;
                    int slot = binding.FindPropertyRelative("materialSlot").intValue;
                    bool isValid = renderer != null && slot >= 0 && renderer.sharedMaterials != null && slot < renderer.sharedMaterials.Length && renderer.sharedMaterials[slot] == expectedMaterial;
                    if (isValid) valid++;
                    else
                    {
                        invalid++;
                        Debug.LogWarning($"Gobo Lens Material Setup: {targets[targetIndex].name} のBinding {i} は {expectedShaderName} を参照していません。", targets[targetIndex]);
                    }
                }
            }
            Debug.Log($"Gobo Lens Material Setup: 検証完了。Valid: {valid}, Invalid: {invalid}。");
        }

        private void DrawVlbDefaultsButton()
        {
            EditorGUILayout.Space(2f);
            if (GUILayout.Button("Apply VLB Pipeline Defaults"))
            {
                serializedObject.ApplyModifiedProperties();
                ApplyVlbPipelineDefaultsToTargets();
                serializedObject.Update();
            }
        }

        private void ApplyVlbPipelineDefaultsToTargets()
        {
            for (int i = 0; i < targets.Length; i++)
            {
                var fixture = targets[i] as DmxFixtureComponent;
                if (fixture == null)
                    continue;

                Undo.RecordObject(fixture, "Apply VLB Pipeline Defaults");
                fixture.ApplyVlbPipelineDefaults();
                EditorUtility.SetDirty(fixture);
                PrefabUtility.RecordPrefabInstancePropertyModifications(fixture);
            }
        }
    }
}
